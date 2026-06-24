using System;
using UnityEngine;


[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class PbdRope : MonoBehaviour
{
    [Header("Endpoints")] [Tooltip("Pinned top – the suspension / pivot point.")] [SerializeField]
    private Transform pivot;

    [Tooltip("Pinned bottom – the bob, driven by SphericalPendulum.")] [SerializeField]
    private Transform bob;

    [Header("Rope")]
    [Tooltip("Number of rope segments. More → smoother; heavier on the solver.")]
    [SerializeField, Range(2, 80)]
    private int segments = 30;

    [Tooltip("Total rope rest length (m). Greater than pivot→bob chord → visible sag.")] [SerializeField, Min(0.01f)]
    private float ropeLength =  0.55f;

    [Tooltip("Rope visual diameter (m).")] [SerializeField, Min(0.0005f)]
    private float ropeWidth = 0.008f;

    [Tooltip("Linear mass density (kg / m). Heavier rope sags more and reacts differently.")]
    [SerializeField, Range(0.001f, 10f)]
    private float linearDensity = 0.1f;

    [Header("Solver")]
    [Tooltip("Gauss-Seidel iterations per sub-step. Higher → stiffer / less stretch.")]
    [SerializeField, Range(1, 80)]
    private int constraintIterations = 25;

    [Tooltip("XPBD compliance (m / N). 0 = perfectly rigid; larger = softer rope.")] [SerializeField, Min(0f)]
    private float compliance = 1e-5f;

    [Tooltip("Per-sub-step velocity retention. 1 = undamped; 0.98 = light air drag.")] [SerializeField, Range(0.8f, 1f)]
    private float damping = 0.96f;

    [Tooltip("Hard upper stretch cap per segment. 1.0 = inextensible; 1.05 = 5 % slack allowed.")]
    [SerializeField, Range(1f, 1.2f)]
    private float stretchLimit = 1.1f;

    [Header("Forces")] [Tooltip("Gravitational acceleration magnitude (m / s²).")] [SerializeField]
    private float gravity = 9.81f;

    [Tooltip("Constant external acceleration on all free particles (m / s²), e.g. wind.")] [SerializeField]
    private Vector3 wind = Vector3.zero;

    [Header("Integration")]
    [Tooltip("Physics sub-step duration (s). Smaller → more accurate, more costly.")]
    [SerializeField, Min(0.0001f)]
    private float fixedStep = 0.004f;

    [Header("Rigid Mode")] [Tooltip("خط مستقيم تماماً بدون فيزياء — لا التواء ولا رجة")] [SerializeField]
    private bool rigidMode;
 
    [Header("Rope Attachment Override")]
    [Tooltip("إذا عيّنت هنا Transform، سيتصل طرف الحبل السفلي بهذه النقطة بدلاً من Bob العادي")]
    [SerializeField] private Transform bobOverride;
    
    private LineRenderer lr;
    private int n;
    private float segLen;

    private Vector3[] pos;
    private Vector3[] prev;
    private Vector3[] renderPos;
    private float[] invMass;
    private float[] lambda;

    private float accumulator;
    private bool ready;

    public Vector3 Wind
    {
        get => wind;
        set => wind = value;
    }

    private void Reset() => lr = GetComponent<LineRenderer>();

    private void Start()
    {
        if (!pivot || !bob)
        {
            Debug.LogError($"[{nameof(PbdRope)}] 'pivot' and 'bob' must be assigned.", this);
            enabled = false;
            return;
        }

        lr = GetComponent<LineRenderer>();
        n = segments + 1;
        segLen = ropeLength / segments;

        pos = new Vector3[n];
        prev = new Vector3[n];
        renderPos = new Vector3[n];
        invMass = new float[n];
        lambda = new float[segments];

        BuildMasses();
        InitCatenary();
        InitLineRenderer();

        ready = true;
        BuildRenderPositions(1f);
        Render();
    }
 
    private void LateUpdate()
    {
        if (!ready) return;
        if (rigidMode)
        { 
            Vector3 bobTarget = (bobOverride) ? bobOverride.position : bob.position;
        
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / segments;
                renderPos[i] = Vector3.Lerp(pivot.position, bobTarget, t);
            }

            Render();
            return;
        }

        accumulator += Time.deltaTime;
        if (accumulator > 0.25f) accumulator = 0.25f;

        while (accumulator >= fixedStep)
        {
            Step(fixedStep);
            accumulator -= fixedStep;
        }

        BuildRenderPositions(accumulator / fixedStep);
        Render();
    }


    private void BuildMasses()
    {
        invMass[0] = 0f; // pivot pin  → infinite mass
        invMass[n - 1] = 0f; // bob   pin  → infinite mass

        float m = Mathf.Max(1e-9f, linearDensity * segLen);
        for (int i = 1; i < n - 1; i++)
            invMass[i] = 1f / m;
    }

    private void InitCatenary()
    {
        Vector3 a = pivot.position;
        Vector3 b = bob.position;
        float slack = Mathf.Max(0f, ropeLength - Vector3.Distance(a, b));

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / segments;
            float sag = 4f * slack * t * (1f - t); // parabola: sag=0 at both ends
            pos[i] = Vector3.Lerp(a, b, t) + Vector3.down * sag;
            prev[i] = pos[i];
        }
    }

    private void InitLineRenderer()
    {
        lr.useWorldSpace = true;
        lr.positionCount = n;
        lr.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
        lr.widthMultiplier = ropeWidth;
    }

    private void Step(float dt)
    {
        float alphaTilde = compliance / (dt * dt);

        Array.Clear(lambda, 0, segments);

        Vector3 accDt2 = (Vector3.down * gravity + wind) * (dt * dt);
        for (int i = 1; i < n - 1; i++)
        {
            Vector3 cur = pos[i];
            pos[i] = cur + (cur - prev[i]) * damping + accDt2;
            prev[i] = cur;
        }

        PinEndpoints(trackVelocity: true);

        for (int k = 0; k < constraintIterations; k++)
        {
            if ((k & 1) == 0)
                for (int i = 0; i < segments; i++)
                    SolveSegment(i, alphaTilde);
            else
                for (int i = segments - 1; i >= 0; i--)
                    SolveSegment(i, alphaTilde);

            PinEndpoints(trackVelocity: false);
        }
 
        ClampStretch();
        PinEndpoints(trackVelocity: false);
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
        pos[b] += nHat * (invMass[b] * dLambda);
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

            if (aPinned && bPinned)
            {
            }
            else if (aPinned) pos[i + 1] -= dir * over;
            else if (bPinned) pos[i] += dir * over;
            else
            {
                pos[i] += dir * (over * 0.5f);
                pos[i + 1] -= dir * (over * 0.5f);
            }
        }
    }
 

    private void PinEndpoints(bool trackVelocity)
    {
        Vector3 bobTarget = (bobOverride) ? bobOverride.position : bob.position;

        if (trackVelocity)
        {
            prev[0]     = pos[0];
            prev[n - 1] = pos[n - 1];
        }

        pos[0]     = pivot.position;
        pos[n - 1] = bobTarget;
    }


    private void BuildRenderPositions(float alpha)
    {
        for (int i = 0; i < n; i++)
            renderPos[i] = Vector3.Lerp(prev[i], pos[i], alpha);
    }

    // Single batch call – avoids per-element overhead of SetPosition.
    private void Render() => lr.SetPositions(renderPos);
 
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!ready || pos == null) return;
        Gizmos.color = new Color(0f, 0.9f, 1f, 0.5f);
        float r = ropeWidth * 0.5f;
        for (int i = 1; i < n - 1; i++)
            Gizmos.DrawWireSphere(pos[i], r);
    }
#endif
}