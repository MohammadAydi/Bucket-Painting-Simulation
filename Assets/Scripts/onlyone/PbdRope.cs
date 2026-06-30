using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class PbdRope : MonoBehaviour
{
    [Header("Endpoints")]
    [SerializeField] private Transform pivot;
    [SerializeField] private Transform bob;

    [Tooltip("نقطة اتصال الحبل السفلي (منتصف المقبض) بدل Bob العادي. اختياري.")]
    [SerializeField] private Transform bobOverride;

    [Header("Rope")]
    [SerializeField, Range(2, 80)]       private int   segments = 24;
    [SerializeField, Min(0.01f)]         private float ropeLength = 1.22f;
    [SerializeField, Min(0.0005f)]       private float ropeWidth = 0.008f;
    [SerializeField, Range(0.001f, 10f)] private float linearDensity = 0.08f;

    [Header("Solver")]
    [SerializeField, Range(1, 80)]    private int   constraintIterations = 45;
    [SerializeField, Min(0f)]         private float compliance = 0.00005f;
    [SerializeField, Range(0.8f, 1f)] private float damping = 0.9998f;
    [SerializeField, Range(1f, 1.4f)] private float stretchLimit = 1.2f;

    [Header("Forces")]
    [SerializeField] private float   gravity = 9.81f;
    [SerializeField] private Vector3 wind = Vector3.zero;

    [Header("Integration")]
    [SerializeField, Min(0.0001f)] private float fixedStep = 0.004f;

    [Header("Rigid Mode")]
    [Tooltip("للتشخيص فقط: يرسم خطاً مستقيماً محور↔دلو دون فيزياء.")]
    [SerializeField] private bool rigidMode = false;

    [Header("Two-Way Coupling (الاقتران ثنائي الاتجاه)")]
    [Tooltip("الدلو يصبح الجسيم الحرّ الأخير؛ حركته من الحبل، ووزنه يمدّه، " +
             "وحالته تُغذّى للنواس. إطفاؤه = الوضع المستقل (النواس يقود).")]
    [SerializeField] private bool dynamicBucket = true;

    [Tooltip("كتلة الدلو (kg). أثقل = يشدّ الحبل أكثر ويتدلّى أخفض.")]
    [SerializeField, Min(0.01f)] private float bucketMass = 3f;

    [Tooltip("النواس — يُطفأ تكامله ويتلقّى حالته من الحبل عند الاقتران.")]
    [SerializeField] private SphericalPendulum pendulum;

    [Tooltip("جسم الدلو الذي يُمال باتجاه نهاية الحبل. عطّل BucketSwing عند الاقتران.")]
    [SerializeField] private Transform bucketBody;

    [Header("Bucket Aerodynamics (مقاومة هواء الدلو)")]
    [SerializeField, Min(0f)]     private float airDensity = 1.225f;
    [SerializeField, Min(0f)]     private float bucketDragCoefficient = 1.0f;
    [SerializeField, Min(0.001f)] private float bucketRadius = 0.13f;
 
    private LineRenderer lr;
    private int          n;
    private float        segLen;
    private Vector3[]    pos, prev, renderPos;
    private float[]      invMass, lambda;
    private float        accumulator;
    private bool         ready;
    private float        bucketTension;   // اشتقاق من λ القيد الأخير / dt²

    private void Start()
    {
        if (!pivot || !bob)
        {
            Debug.LogError($"[{nameof(PbdRope)}] 'pivot' and 'bob' must be assigned.", this);
            enabled = false;
            return;
        }

        if (dynamicBucket && pendulum != null)
        {
            pendulum.driveBucket      = false;
            pendulum.externallyDriven = true;
        }

        lr     = GetComponent<LineRenderer>();
        n      = segments + 1;
        segLen = ropeLength / segments;

        pos       = new Vector3[n];
        prev      = new Vector3[n];
        renderPos = new Vector3[n];
        invMass   = new float[n];
        lambda    = new float[segments];

        BuildMasses();
        InitLineRenderer();
        LaunchFromInitialState();

        ready = true;
        BuildRenderPositions(1f);
        Render();
    }

    private void LateUpdate()
    {
        if (!ready) return;

        if (rigidMode)
        {
            Vector3 bobTarget = bobOverride ? bobOverride.position : bob.position;
            for (int i = 0; i < n; i++)
                renderPos[i] = Vector3.Lerp(pivot.position, bobTarget, (float)i / segments);
            Render();
            return;
        }

        accumulator += Time.deltaTime;
        if (accumulator > 0.25f) accumulator = 0.25f;

        while (accumulator >= fixedStep)
        {
            Step(fixedStep);
            if (dynamicBucket && pendulum) FeedPendulumState(fixedStep);
            accumulator -= fixedStep;
        }

        BuildRenderPositions(accumulator / fixedStep);
        if (dynamicBucket) DriveBucket();
        Render();
    }

    private void BuildMasses()
    {
        invMass[0] = 0f;                                   // pivot pinned
        float m = Mathf.Max(1e-9f, linearDensity * segLen);
        for (int i = 1; i < n - 1; i++) invMass[i] = 1f / m;
        invMass[n - 1] = dynamicBucket ? 1f / bucketMass : 0f;
    }

    private void InitLineRenderer()
    {
        lr.useWorldSpace   = true;
        lr.positionCount   = n;
        lr.widthCurve      = AnimationCurve.Constant(0f, 1f, 1f);
        lr.widthMultiplier = ropeWidth;
    }
 
    private void LaunchFromInitialState()
    { 
        if (!dynamicBucket || pendulum == null)
        {
            Vector3 a = pivot.position;
            Vector3 b = bobOverride ? bobOverride.position : bob.position;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / segments;
                pos[i]  = Vector3.Lerp(a, b, t);
                prev[i] = pos[i];
            }
            return;
        }
 
        float th  = pendulum.StartThetaRad;
        float ph  = pendulum.StartPhiRad;
        float thD = pendulum.StartThetaDotRad;
        float phD = pendulum.StartPhiDotRad;

        float s = Mathf.Sin(th), c = Mathf.Cos(th);
        float cp = Mathf.Cos(ph), sp = Mathf.Sin(ph);
        Vector3 dir = new Vector3(s * cp, -c, -s * sp);     // اتجاه الحبل عند البداية

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / segments;
            pos[i]  = pivot.position + dir * (ropeLength * t);
            prev[i] = pos[i];
        }
 
        Vector3 eTheta = new Vector3(c * cp, s, -c * sp);
        Vector3 ePhi   = new Vector3(-sp, 0f, -cp);
        Vector3 vBucket = eTheta * (ropeLength * thD) + ePhi * (ropeLength * s * phD);
        prev[n - 1] = pos[n - 1] - vBucket * fixedStep;
    }

    private void Step(float dt)
    {
        float alphaTilde = compliance / (dt * dt);
        Array.Clear(lambda, 0, segments);

        Vector3 accDt2 = (Vector3.down * gravity + wind) * (dt * dt);
 
        for (int i = 1; i < n; i++)
        {
            if (invMass[i] < 1e-12f) continue;

            Vector3 cur   = pos[i];
            Vector3 delta = (cur - prev[i]) * damping;

            // سحب هوائي تربيعي على جسيم الدلو فقط ( F = −½ρCdA|v|v )
            if (i == n - 1 && dynamicBucket)
            {
                float a  = Mathf.PI * bucketRadius * bucketRadius;
                float kd = 0.5f * airDensity * bucketDragCoefficient * a * invMass[i];
                float f  = Mathf.Min(kd * delta.magnitude, 1f);    
                delta   *= (1f - f);
            }

            pos[i]  = cur + delta + accDt2;
            prev[i] = cur;
        }

        PinEndpoints(trackVelocity: true);

        for (int k = 0; k < constraintIterations; k++)
        {
            if ((k & 1) == 0) for (int i = 0; i < segments; i++) SolveSegment(i, alphaTilde);
            else              for (int i = segments - 1; i >= 0; i--) SolveSegment(i, alphaTilde);
            PinEndpoints(trackVelocity: false);
        }

        ClampStretch();
        PinEndpoints(trackVelocity: false);

        // الشدّ الحقيقي عند الدلو = |λ| / dt² (XPBD)
        bucketTension = Mathf.Abs(lambda[segments - 1]) / (dt * dt);
    }

    private void SolveSegment(int idx, float alphaTilde)
    {
        int a = idx, b = idx + 1;
        float wSum = invMass[a] + invMass[b];
        if (wSum < 1e-9f) return;

        Vector3 d = pos[b] - pos[a];
        float dist = d.magnitude;
        if (dist < 1e-7f) return;

        Vector3 nHat = d / dist;
        float c = dist - segLen;
        float dLambda = (-c - alphaTilde * lambda[idx]) / (wSum + alphaTilde);
        lambda[idx] += dLambda;

        pos[a] += -nHat * (invMass[a] * dLambda);
        pos[b] +=  nHat * (invMass[b] * dLambda);
    }

    private void ClampStretch()
    {
        float maxLen = segLen * stretchLimit;
        for (int i = 0; i < n - 1; i++)
        {
            Vector3 d = pos[i + 1] - pos[i];
            float dist = d.magnitude;
            if (dist <= maxLen) continue;

            Vector3 dir = d / dist;
            float over = dist - maxLen;
            bool aPinned = invMass[i] < 1e-9f;
            bool bPinned = invMass[i + 1] < 1e-9f;

            if (aPinned && bPinned) { }
            else if (aPinned) pos[i + 1] -= dir * over;
            else if (bPinned) pos[i]     += dir * over;
            else { pos[i] += dir * (over * 0.5f); pos[i + 1] -= dir * (over * 0.5f); }
        }
    }

    private void PinEndpoints(bool trackVelocity)
    {
        if (trackVelocity)
        {
            prev[0] = pos[0];
            if (!dynamicBucket) prev[n - 1] = pos[n - 1];
        }

        pos[0] = pivot.position;
        if (!dynamicBucket)
            pos[n - 1] = bobOverride ? bobOverride.position : bob.position;
    }

    // ── Feedback: rope -> pendulum ──────────────────────────────────────────
    private void FeedPendulumState(float dt)
    {
        Vector3 r = pos[n - 1] - pos[0];
        double lEff = r.magnitude;
        if (lEff < 1e-5) return;

        double cT = Math.Max(-1.0, Math.Min(1.0, -r.y / lEff));
        double theta = Math.Acos(cT);
        double phi   = Math.Atan2(-r.z, r.x);

        double sT    = Math.Sin(theta);
        double sSafe = Math.Abs(sT) < 1e-3 ? 1e-3 : sT;
        double cp    = Math.Cos(phi), sp = Math.Sin(phi);

        Vector3 eTheta = new Vector3((float)(cT * cp), (float)sT, -(float)(cT * sp));
        Vector3 ePhi   = new Vector3(-(float)sp, 0f, -(float)cp);

        Vector3 v = (pos[n - 1] - prev[n - 1]) / dt;
        double thetaDot = Vector3.Dot(v, eTheta) / lEff;
        double phiDot   = Vector3.Dot(v, ePhi)   / (lEff * sSafe);

        pendulum.SetStateFromWorld(theta, phi, thetaDot, phiDot, lEff);
    }
 
    private void DriveBucket()
    {
        if (bucketBody && n >= 2)
        {
            Vector3 up = renderPos[n - 2] - renderPos[n - 1];
            if (up.sqrMagnitude > 1e-8f)
                bucketBody.rotation =
                    Quaternion.FromToRotation(bucketBody.up, up.normalized) * bucketBody.rotation;
        }
        Vector3 attach = bobOverride ? bobOverride.position : bob.position;
        bob.position += renderPos[n - 1] - attach;
    }

    private void BuildRenderPositions(float alpha)
    {
        for (int i = 0; i < n; i++)
            renderPos[i] = Vector3.Lerp(prev[i], pos[i], alpha);
    }

    private void Render() => lr.SetPositions(renderPos);

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!ready || pos == null) return;

        Gizmos.color = new Color(0f, 0.9f, 1f, 0.5f);
        for (int i = 1; i < n - 1; i++) Gizmos.DrawWireSphere(pos[i], ropeWidth * 0.5f);
 
        if (n >= 2)
        {
            Gizmos.color = Color.red;
            Vector3 dir = (pos[n - 2] - pos[n - 1]).normalized;
            Gizmos.DrawLine(pos[n - 1], pos[n - 1] + dir * Mathf.Min(bucketTension * 0.01f, 0.5f));
        }
    }
#endif
}
