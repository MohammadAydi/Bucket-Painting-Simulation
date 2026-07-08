using System;
using UnityEngine;

namespace onlyone
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class PbdRope : MonoBehaviour
    {
        [Header("Endpoints")]
        [SerializeField] private Transform pivot;
        [SerializeField] private Transform bob;
        [Tooltip("نقطة اتصال الحبل السفلي (منتصف المقبض). اختياري.")]
        [SerializeField] private Transform bobOverride;

        [Header("Rope")]
        [SerializeField, Range(2, 80)]       private int   segments = 26;
        [SerializeField, Min(0.01f)]         private float ropeLength = 1.22f;
        [SerializeField, Min(0.0005f)]       private float ropeWidth = 0.008f;
        [SerializeField, Range(0.001f, 10f)] private float linearDensity = 0.1f;

        [Header("Solver (XPBD + Substeps)")]
        [Tooltip("خطوات فرعية لكل fixedStep. Macklin 2019: خطأ XPBD الزمني ∝ dt²، " +
                 "فالتقسيم يتفوق على رفع التكرارات في حفظ الطاقة.")]
        [SerializeField, Range(1, 8)] private int substeps = 4;
        [SerializeField, Range(1, 80)] private int constraintIterations = 8;
        [Tooltip("امتثال قيد المسافة α (m/N). XPBD Eq.24: α̃ = α/dt².")]
        [SerializeField, Min(0f)] private float compliance = 0.00004f;
        [Tooltip("تخميد داخلي σ (1/s): v ← v·exp(−σ·dt). مستقل عن حجم الخطوة. " +
                 "(نموذج لزج قياسي — ليس من DER/XPBD.)")]
        [SerializeField, Min(0f)] private float internalDampingRate = 0.05f;
        [Tooltip("حاجز أمان ضد الانفجار العددي فقط.")]
        [SerializeField, Range(1f, 1.4f)] private float stretchLimit = 1.2f;
        [Tooltip("β̃ لقيد المسافة (XPBD Eq.26): يخمد تشوّه القطعة دون تخميد الحركة المكانية.")]
        [SerializeField, Min(0f)] private float stretchDampingBeta;

        [Header("Bending (انحناء DER)")]
        [SerializeField] private bool enableBending = true;
        [Tooltip("k_b = EI/l̄ [N·m/rad]. طاقة DER (Bergou Eq.1): E=(k_b/2)·φ²، rest curvature=0.")]
        [SerializeField, Min(0f)] private float bendingStiffness = 0.02f;

        [Header("Forces")]
        [SerializeField] private float   gravity = 9.81f;
        [SerializeField] private Vector3 wind = Vector3.zero;

        [Header("Integration")]
        [SerializeField, Min(0.0001f)] private float fixedStep = 0.004f;

        [Header("Two-Way Coupling")]
        [SerializeField] private bool dynamicBucket = true;
        [SerializeField, Min(0.01f)] private float bucketMass = 3f;
        [Tooltip("النواس — مُراقِب قراءة فقط: الحبل مصدر الحقيقة الوحيد.")]
        [SerializeField] private SphericalPendulum pendulum;
        [SerializeField] private Transform bucketBody;

        [Header("Bucket Aerodynamics")]
        [SerializeField, Min(0f)]     private float airDensity = 1.225f;
        [SerializeField, Min(0f)]     private float bucketDragCoefficient = 1.0f;
        [SerializeField, Min(0.001f)] private float bucketRadius = 0.13f;

        [Header("Torsion (DER §4.2)")]
        [SerializeField] private bool enableTorsion = true;
        [Tooltip("لفّات ابتدائية موزّعة خطياً (dθ/ds ثابت) ⇒ طاقة كامنة، سرعة صفر.")]
        [SerializeField] private float initialTurns = 8f;
        [Tooltip("k_t = GJ/l̄ [N·m/rad]، J=(π/2)r⁴ للمقطع الدائري.")]
        [SerializeField, Min(0f)] private float torsionalStiffness = 25f;
        [Tooltip("نسبة تخميد رايلي ζ: retention=exp(−2ζω_n·dt), ω_n=√(k_t/I). موحّدة عبر العقد.")]
        [SerializeField, Range(0f, 1f)] private float dampingRatio = 0.10f;
        [Tooltip("نصف قطر الحبل لقصور الحواف I=½mr².")]
        [SerializeField, Min(0.0005f)] private float torsionRadius = 0.015f;
        [SerializeField, Range(1, 40)] private int torsionIterations = 8;
        [Tooltip("المحور مثبّت دورانياً (شرط حدّي: θ₀=0, w₀=0).")]
        [SerializeField] private bool clampPivotTwist = true;
        [Tooltip("حاجز أمان (rad/s).")]
        [SerializeField, Min(1f)] private float maxTwistRate = 600f;

        // =====================================================================
        // MANUAL MANIPULATION MODE - المتغيرات المضافة
        // =====================================================================
        [Header("Manual Manipulation Mode")]
        [Tooltip("أدنى مسافة مسموحة بين الدلو والمحور أثناء التحكم اليدوي، لتفادي الانفراد العددي قرب المحور. " +
                 "لا تُغيّر طول الحبل الاسمي (ropeLength) بأي حال — هي فقط تحدّ حركة اليد.")]
        [SerializeField, Min(0.01f)] private float minimumManualDistance = 0.25f;
        [Tooltip("عدد العُقد الأخيرة (بما فيها الدلو نفسه) التي تتجمّد معاً لتشكيل ‘مقبض’ صلب أثناء " +
                 "التحكم اليدوي. يمنع اهتزاز/جنون الدلو عند السحب السريع بمنح الحلّال قاعدة صلبة ذات " +
                 "ذراع حقيقي بدل نقطة واحدة عديمة القصور الدوراني. يُقصّ تلقائياً إلى مدى آمن عند الدخول.")]
        [SerializeField, Min(1)] private int manualFrozenNodesCount = 3;

        // ── حالة الوضع اليدوي الحالية ──
        public bool IsManualMode { get; private set; }
        public float RopeLength => ropeLength;
        public float MinimumManualDistance => minimumManualDistance;
        public Vector3 PivotPosition => pivot ? pivot.position : transform.position;
        public Vector3 CurrentBucketPosition =>
            (ready && pos != null && n > 0) ? pos[n - 1]
            : (bucketBody ? bucketBody.position : transform.position);

        // ── حالة المقبض الصلب (Kinematic Handle) ──
        private int handleSize;
        private int handleFirstIdx;
        private float[] cachedHandleInvMass;
        private Vector3[] handleOffset;
        private Vector3 manualTarget;

        // ── متغيرات تتبع السرعة في الوضع اليدوي ──
        private Vector3 previousManualTarget;
        private Vector3 manualVelocity;
        private bool hasPreviousTarget = false;

        private LineRenderer lr;
        private int n; private float segLen;
        private Vector3[] pos, prev, renderPos;
        private float[] invMass, lambda, lambdaBend;
        private float accumulator; private bool ready;
        private float bucketTension;
        private float lastSubstep;

        private float[]   twist, twistPrev, invInertia, lambdaTw, refTwist;
        private Vector3[] tangent, prevTangent, refDir;
        private float     lastAppliedTwist;

        public float  TensionN     => bucketTension;
        public double TotalEnergyJ => currentState.totalEnergy;
        private RopeState currentState;

        private void Start()
        {
            if (!pivot || !bob)
            {
                Debug.LogError($"[{nameof(PbdRope)}] 'pivot' and 'bob' must be assigned.", this);
                enabled = false; return;
            }

            // التحقق من وجود bobOverride عند استخدام dynamicBucket
            if (dynamicBucket && !bobOverride)
            {
                Debug.LogWarning(
                    $"[{nameof(PbdRope)}] dynamicBucket is true but bobOverride is not assigned. " +
                    "The rope will connect to the bucket's position instead of the handle midpoint.", this);
            }

            InitializeRope();
        }

        public void ApplyConfig(RopeConfig c)
        {
            segments              = Mathf.Clamp(c.segments, 2, 80);
            ropeLength            = c.ropeLength;
            ropeWidth             = c.ropeWidth;
            linearDensity         = c.linearDensity;
            substeps              = Mathf.Clamp(c.substeps, 1, 8);
            constraintIterations  = Mathf.Max(1, c.constraintIterations);
            compliance            = c.compliance;
            internalDampingRate   = c.internalDampingRate;
            stretchLimit          = c.stretchLimit;
            stretchDampingBeta    = c.stretchDampingRatio;
            enableBending         = c.enableBending;
            bendingStiffness      = c.bendingStiffness;
            gravity               = c.gravity;
            wind                  = c.wind;
            fixedStep             = c.fixedStep;
            dynamicBucket         = c.dynamicBucket;
            bucketMass            = c.bucketMass;
            airDensity            = c.airDensity;
            bucketDragCoefficient = c.bucketDragCoefficient;
            bucketRadius          = c.bucketRadius;
            enableTorsion         = c.enableTorsion;
            initialTurns          = c.initialTurns;
            torsionalStiffness    = c.torsionalStiffness;
            dampingRatio          = c.dampingRatio;
            // torsionRadius         = c.torsionRadius;
            torsionIterations     = Mathf.Max(1, c.torsionIterations);
            clampPivotTwist       = c.clampPivotTwist;
            maxTwistRate          = c.maxTwistRate;

            if (pivot && bob) InitializeRope();
        }

        private void InitializeRope()
        {
            ready = false;

            // إعادة تعيين حالة الوضع اليدوي عند إعادة التهيئة
            IsManualMode = false;
            handleSize = 0;
            handleFirstIdx = 0;
            cachedHandleInvMass = null;
            handleOffset = null;
            manualTarget = Vector3.zero;
            previousManualTarget = Vector3.zero;
            manualVelocity = Vector3.zero;
            hasPreviousTarget = false;

            if (pendulum)
            {
                bool coupled = dynamicBucket;
                pendulum.driveBucket      = !coupled;
                pendulum.externallyDriven = coupled;
            }

            lr = GetComponent<LineRenderer>();
            n = segments + 1;
            segLen = ropeLength / segments;
            lastSubstep = fixedStep / substeps;

            pos        = new Vector3[n];
            prev       = new Vector3[n];
            renderPos  = new Vector3[n];
            invMass    = new float[n];
            lambda     = new float[segments];
            lambdaBend = new float[n];
            accumulator = 0f;

            BuildMasses();
            InitLineRenderer();
            LaunchFromInitialState();
            InitTorsion();

            ready = true;
            BuildRenderPositions(1f);
            Render();
        }

        private void LateUpdate()
        {
            if (!ready) return;

            accumulator += Time.deltaTime;
            if (accumulator > 0.25f) accumulator = 0.25f;

            float h = fixedStep / substeps;
            lastSubstep = h;
            while (accumulator >= fixedStep)
            {
                for (int s = 0; s < substeps; s++)
                {
                    Step(h);
                    if (enableTorsion) SolveTorsion(h);
                }
                if (dynamicBucket && pendulum) PublishState(h);
                accumulator -= fixedStep;
            }

            BuildRenderPositions(accumulator / fixedStep);
            if (dynamicBucket) DriveBucket();
            Render();
        }

        private void BuildMasses()
        {
            invMass[0] = 0f;
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
            if (!dynamicBucket || !pendulum)
            {
                Vector3 a = pivot.position;
                Vector3 b = bobOverride ? bobOverride.position : bob.position;
                for (int i = 0; i < n; i++)
                {
                    pos[i] = Vector3.Lerp(a, b, (float)i / segments);
                    prev[i] = pos[i];
                }
                return;
            }

            float th = pendulum.StartThetaRad, ph = pendulum.StartPhiRad;
            float thD = pendulum.StartThetaDotRad, phD = pendulum.StartPhiDotRad;

            float s = Mathf.Sin(th), c = Mathf.Cos(th);
            float cp = Mathf.Cos(ph), sp = Mathf.Sin(ph);
            Vector3 dir = new Vector3(s * cp, -c, -s * sp);

            for (int i = 0; i < n; i++)
            {
                pos[i] = pivot.position + dir * (ropeLength * i / segments);
                prev[i] = pos[i];
            }

            Vector3 eTheta = new Vector3(c * cp, s, -c * sp);
            Vector3 ePhi   = new Vector3(-sp, 0f, -cp);
            Vector3 vB = eTheta * (ropeLength * thD) + ePhi * (ropeLength * s * phD);
            prev[n - 1] = pos[n - 1] - vB * (fixedStep / substeps);
        }

        private void InitTorsion()
        {
            twist       = new float[segments];
            twistPrev   = new float[segments];
            invInertia  = new float[segments];
            lambdaTw    = new float[segments];
            refTwist    = new float[segments];
            tangent     = new Vector3[segments];
            prevTangent = new Vector3[segments];
            refDir      = new Vector3[segments];

            float mEdge = Mathf.Max(1e-9f, linearDensity * segLen);
            float iedge = 0.5f * mEdge * torsionRadius * torsionRadius;
            for (int i = 0; i < segments; i++) invInertia[i] = 1f / Mathf.Max(1e-12f, iedge);

            if (dynamicBucket)
            {
                float ibucket = 0.5f * bucketMass * bucketRadius * bucketRadius;
                invInertia[segments - 1] = 1f / Mathf.Max(1e-12f, ibucket);
            }
            if (clampPivotTwist) invInertia[0] = 0f;

            for (int i = 0; i < segments; i++)
            {
                Vector3 e = pos[i + 1] - pos[i];
                tangent[i] = e.sqrMagnitude > 1e-14f ? e.normalized : Vector3.down;
                prevTangent[i] = tangent[i];
            }
            refDir[0] = OrthoSeed(tangent[0]);
            for (int i = 1; i < segments; i++)
                refDir[i] = ParallelTransport(refDir[i - 1], tangent[i - 1], tangent[i]);
            Array.Clear(refTwist, 0, segments);

            if (initialTurns != 0f)
            {
                float total = initialTurns * 2f * Mathf.PI;
                int denom = Mathf.Max(1, segments - 1);
                for (int i = 0; i < segments; i++)
                {
                    twist[i] = total * i / denom;
                    twistPrev[i] = twist[i];
                }
                if (clampPivotTwist) { twist[0] = 0f; twistPrev[0] = 0f; }
            }
            lastAppliedTwist = twist[segments - 1];
        }

        private void Step(float dt)
        {
            float alphaStretch = compliance / (dt * dt);
            float alphaBend = (bendingStiffness > 1e-9f)
                            ? (1f / bendingStiffness) / (dt * dt) : 0f;

            Array.Clear(lambda, 0, segments);
            Array.Clear(lambdaBend, 0, n);

            float retention = Mathf.Exp(-internalDampingRate * dt);
            Vector3 accDt2 = (Vector3.down * gravity + wind) * (dt * dt);

            Vector3 bucketPredicted = pos[n - 1];

            for (int i = 1; i < n; i++)
            {
                if (invMass[i] < 1e-12f) continue;

                Vector3 cur   = pos[i];
                Vector3 delta = (cur - prev[i]) * retention;

                if (i == n - 1 && dynamicBucket)
                {
                    float a  = Mathf.PI * bucketRadius * bucketRadius;
                    float kd = 0.5f * airDensity * bucketDragCoefficient * a * invMass[i];
                    float f  = Mathf.Min(kd * delta.magnitude, 1f);
                    delta *= (1f - f);
                }

                pos[i]  = cur + delta + accDt2;
                prev[i] = cur;
                if (i == n - 1) bucketPredicted = pos[i];
            }

            PinEndpoints(trackVelocity: true);

            for (int k = 0; k < constraintIterations; k++)
            {
                bool fwd = (k & 1) == 0;
                if (fwd) for (int i = 0; i < segments; i++)      SolveSegment(i, alphaStretch, stretchDampingBeta);
                else     for (int i = segments - 1; i >= 0; i--) SolveSegment(i, alphaStretch, stretchDampingBeta);

                if (enableBending)
                {
                    if (fwd) for (int i = 1; i < n - 1; i++)  SolveBend(i, alphaBend);
                    else     for (int i = n - 2; i >= 1; i--) SolveBend(i, alphaBend);
                }
                PinEndpoints(trackVelocity: false);
            }

            ClampStretch();
            PinEndpoints(trackVelocity: false);

            // حساب التوتر - يدعم الوضع اليدوي
            if (IsManualMode)
            {
                int anchorSeg = Mathf.Max(0, handleFirstIdx - 1);
                bucketTension = Mathf.Abs(lambda[anchorSeg]) / (dt * dt);
            }
            else
            {
                float bucketMassEff = dynamicBucket ? bucketMass : Mathf.Max(1e-9f, linearDensity * segLen);
                Vector3 bucketCorrection = pos[n - 1] - bucketPredicted;
                Vector3 ropeForceOnBucket = bucketCorrection * (bucketMassEff / (dt * dt));
                bucketTension = ropeForceOnBucket.magnitude;
            }
        }

        private void SolveSegment(int idx, float alphaTilde, float betaTilde)
        {
            int a = idx, b = idx + 1;
            float wSum = invMass[a] + invMass[b];
            if (wSum < 1e-9f) return;

            Vector3 d = pos[b] - pos[a];
            float dist = d.magnitude;
            if (dist < 1e-7f) return;

            Vector3 nHat = d / dist;
            float c = dist - segLen;

            float velTerm = 0f;
            if (betaTilde > 0f)
            {
                Vector3 dxA = pos[a] - prev[a];
                Vector3 dxB = pos[b] - prev[b];
                velTerm = Vector3.Dot(nHat, dxB - dxA);
            }

            float gamma = alphaTilde * betaTilde;
            float dL = (-c - gamma * velTerm - alphaTilde * lambda[idx])
                     / ((1f + gamma) * wSum + alphaTilde);
            lambda[idx] += dL;

            pos[a] -= nHat * (invMass[a] * dL);
            pos[b] += nHat * (invMass[b] * dL);
        }

        private void SolveBend(int i, float alphaTilde)
        {
            Vector3 ea = pos[i] - pos[i - 1];
            Vector3 eb = pos[i + 1] - pos[i];
            float la = ea.magnitude, lb = eb.magnitude;
            if (la < 1e-7f || lb < 1e-7f) return;

            Vector3 ta = ea / la, tb = eb / lb;
            float cos = Mathf.Clamp(Vector3.Dot(ta, tb), -1f, 1f);
            float phi = Mathf.Acos(cos);
            float sin = Mathf.Sqrt(Mathf.Max(1e-12f, 1f - cos * cos));
            if (phi < 1e-5f) return;

            Vector3 da = (tb - cos * ta) / sin;
            Vector3 db = (ta - cos * tb) / sin;

            Vector3 g0 = da / la;
            Vector3 g2 = -db / lb;
            Vector3 g1 = -(g0 + g2);

            float w = invMass[i - 1] * g0.sqrMagnitude
                    + invMass[i]     * g1.sqrMagnitude
                    + invMass[i + 1] * g2.sqrMagnitude;
            if (w < 1e-12f) return;

            float dL = (-phi - alphaTilde * lambdaBend[i]) / (w + alphaTilde);
            lambdaBend[i] += dL;

            pos[i - 1] += g0 * (invMass[i - 1] * dL);
            pos[i]     += g1 * (invMass[i]     * dL);
            pos[i + 1] += g2 * (invMass[i + 1] * dL);
        }

        private void SolveTorsion(float dt)
        {
            for (int i = 0; i < segments; i++)
            {
                Vector3 e = pos[i + 1] - pos[i];
                tangent[i] = e.sqrMagnitude > 1e-14f ? e.normalized : tangent[i];
            }

            for (int i = 0; i < segments; i++)
            {
                Vector3 d = ParallelTransport(refDir[i], prevTangent[i], tangent[i]);
                d -= Vector3.Dot(d, tangent[i]) * tangent[i];
                refDir[i] = d.sqrMagnitude > 1e-10f ? d.normalized : OrthoSeed(tangent[i]);
                prevTangent[i] = tangent[i];
            }

            for (int j = 1; j < segments; j++)
            {
                Vector3 spaceT = ParallelTransport(refDir[j - 1], tangent[j - 1], tangent[j]);
                refTwist[j] = SignedAngle(spaceT, refDir[j], tangent[j]);
            }

            for (int i = 0; i < segments; i++)
            {
                if (invInertia[i] < 1e-12f) { twistPrev[i] = twist[i]; continue; }

                float omegaN = Mathf.Sqrt(torsionalStiffness * invInertia[i]);
                float ret = Mathf.Exp(-2f * dampingRatio * omegaN * dt);

                float cur = twist[i];
                float vel = (twist[i] - twistPrev[i]) * ret;
                vel = Mathf.Clamp(vel, -maxTwistRate * dt, maxTwistRate * dt);
                twist[i] = cur + vel;
                twistPrev[i] = cur;
            }

            if (clampPivotTwist) twist[0] = 0f;

            float alphaTw = (torsionalStiffness > 1e-9f)
                          ? (1f / torsionalStiffness) / (dt * dt) : 0f;
            Array.Clear(lambdaTw, 0, lambdaTw.Length);

            for (int k = 0; k < torsionIterations; k++)
            {
                bool fwd = (k & 1) == 0;
                int start = fwd ? 0 : segments - 2;
                int end   = fwd ? segments - 1 : -1;
                int stepd = fwd ? 1 : -1;

                for (int j = start; j != end; j += stepd)
                {
                    int a = j, b = j + 1;
                    float wSum = invInertia[a] + invInertia[b];
                    if (wSum < 1e-12f) continue;

                    float c = twist[b] - twist[a] + refTwist[b];
                    float dL = (-c - alphaTw * lambdaTw[j]) / (wSum + alphaTw);
                    lambdaTw[j] += dL;

                    twist[a] -= invInertia[a] * dL;
                    twist[b] += invInertia[b] * dL;
                }
            }
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
                bool aPin = invMass[i] < 1e-9f, bPin = invMass[i + 1] < 1e-9f;

                if (aPin && bPin) { }
                else if (aPin) pos[i + 1] -= dir * over;
                else if (bPin) pos[i]     += dir * over;
                else { pos[i] += dir * (over * 0.5f); pos[i + 1] -= dir * (over * 0.5f); }
            }
        }

        // ================================================================
        // الدالة PinEndpoints المعدلة - مع دعم الوضع اليدوي
        // ================================================================
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

            // ================================================================
            // الوضع اليدوي - المقبض الصلب مع الحفاظ على السرعة
            // ================================================================
            if (IsManualMode)
            {
                for (int k = 0; k < handleSize; k++)
                {
                    int idx = handleFirstIdx + k;
                    Vector3 p = manualTarget + handleOffset[k];

                    // نطبق السرعة المحسوبة من حركة الهدف
                    if (trackVelocity && hasPreviousTarget)
                    {
                        prev[idx] = p - manualVelocity * lastSubstep;
                    }

                    pos[idx] = p;

                    // إذا لم تكن لدينا سرعة محسوبة بعد، نضع prev = pos
                    if (!hasPreviousTarget)
                    {
                        prev[idx] = p;
                    }
                }
            }
        }

        private void PublishState(float dt)
        {
            Vector3 r = pos[n - 1] - pos[0];
            double lEff = r.magnitude;
            if (lEff < 1e-5) return;

            double cT = Math.Max(-1.0, Math.Min(1.0, -r.y / lEff));
            double theta = Math.Acos(cT);
            double phi   = Math.Atan2(-r.z, r.x);

            double sT = Math.Sin(theta);
            double sSafe = Math.Abs(sT) < 1e-3 ? 1e-3 : sT;
            double cp = Math.Cos(phi), sp = Math.Sin(phi);

            Vector3 eTheta = new Vector3((float)(cT * cp), (float)sT, -(float)(cT * sp));
            Vector3 ePhi   = new Vector3(-(float)sp, 0f, -(float)cp);

            Vector3 v = (pos[n - 1] - prev[n - 1]) / dt;
            double thetaDot = Vector3.Dot(v, eTheta) / lEff;
            double phiDot   = Vector3.Dot(v, ePhi)   / (lEff * sSafe);

            ComputeEnergy(dt, out double ke, out double pe, out double elastic);

            currentState = new RopeState(theta, phi, thetaDot, phiDot,
                                         lEff, bucketTension, ke, pe, elastic);
            pendulum.PushState(currentState);
        }

        private void ComputeEnergy(float dt, out double ke, out double pe, out double elastic)
        {
            ke = 0; pe = 0;
            float mEdge = Mathf.Max(1e-9f, linearDensity * segLen);
            Vector3 pivotPos = pos[0];

            for (int i = 1; i < n; i++)
            {
                float mi = (i == n - 1 && dynamicBucket) ? bucketMass : mEdge;
                Vector3 vel = (pos[i] - prev[i]) / dt;
                ke += 0.5 * mi * vel.sqrMagnitude;
                pe += mi * gravity * (pos[i].y - pivotPos.y);
            }

            double eStretch = 0, eBend = 0, eTwist = 0;
            float ks = compliance > 1e-12f ? 1f / compliance : 0f;
            for (int i = 0; i < segments; i++)
            {
                float c = (pos[i + 1] - pos[i]).magnitude - segLen;
                eStretch += 0.5 * ks * c * c;
            }
            if (enableBending)
                for (int i = 1; i < n - 1; i++)
                {
                    Vector3 ta = (pos[i] - pos[i - 1]).normalized;
                    Vector3 tb = (pos[i + 1] - pos[i]).normalized;
                    float phi = Mathf.Acos(Mathf.Clamp(Vector3.Dot(ta, tb), -1f, 1f));
                    eBend += 0.5 * bendingStiffness * phi * phi;
                }
            if (enableTorsion)
                for (int j = 1; j < segments; j++)
                {
                    float m = twist[j] - twist[j - 1] + refTwist[j];
                    eTwist += 0.5 * torsionalStiffness * m * m;
                }

            elastic = eStretch + eBend + eTwist;
        }

        private void DriveBucket()
        {
            Vector3 targetPos = renderPos[n - 1];

            Vector3 attach = bobOverride ? bobOverride.position : bob.position;
            bob.position += targetPos - attach;

            if (bucketBody && n >= 2)
            {
                Vector3 up = renderPos[n - 2] - renderPos[n - 1];
                if (up.sqrMagnitude > 1e-8f)
                {
                    Vector3 upN = up.normalized;
                    bucketBody.rotation =
                        Quaternion.FromToRotation(bucketBody.up, upN) * bucketBody.rotation;

                    if (enableTorsion)
                    {
                        float t = twist[segments - 1];
                        bucketBody.Rotate(upN, (t - lastAppliedTwist) * Mathf.Rad2Deg, Space.World);
                        lastAppliedTwist = t;
                    }
                }
            }

            // تحديث bobOverride إذا كان موجوداً
            if (bobOverride)
            {
                bobOverride.position = targetPos;
            }
        }

        private void BuildRenderPositions(float alpha)
        {
            for (int i = 0; i < n; i++)
                renderPos[i] = Vector3.Lerp(prev[i], pos[i], alpha);
        }

        private void Render() => lr.SetPositions(renderPos);

        private static Vector3 ParallelTransport(Vector3 v, Vector3 t0, Vector3 t1)
        {
            Vector3 axis = Vector3.Cross(t0, t1);
            float sn = axis.magnitude;
            if (sn < 1e-6f) return v;
            axis /= sn;
            float ang = Mathf.Atan2(sn, Vector3.Dot(t0, t1)) * Mathf.Rad2Deg;
            return Quaternion.AngleAxis(ang, axis) * v;
        }

        private static float SignedAngle(Vector3 a, Vector3 b, Vector3 axis)
            => Mathf.Atan2(Vector3.Dot(Vector3.Cross(a, b), axis), Vector3.Dot(a, b));

        private static Vector3 OrthoSeed(Vector3 t)
        {
            Vector3 seed = Mathf.Abs(t.y) < 0.9f ? Vector3.up : Vector3.right;
            return Vector3.ProjectOnPlane(seed, t).normalized;
        }

        // ================================================================
        // MANUAL MANIPULATION MODE - الدوال العامة
        // ================================================================

        public void EnterManualMode()
        {
            if (!ready || IsManualMode) return;

            if (!dynamicBucket)
            {
                Debug.LogWarning(
                    $"[{nameof(PbdRope)}] Manual Manipulation Mode requires dynamicBucket = true " +
                    "(the bucket must be the rope's own simulated endpoint, not an externally " +
                    "driven transform).", this);
                return;
            }

            handleSize = Mathf.Clamp(manualFrozenNodesCount, 1, Mathf.Max(1, n - 2));
            handleFirstIdx = n - handleSize;

            cachedHandleInvMass = new float[handleSize];
            handleOffset = new Vector3[handleSize];

            Vector3 bucketPos = pos[n - 1];
            for (int k = 0; k < handleSize; k++)
            {
                int idx = handleFirstIdx + k;
                cachedHandleInvMass[k] = invMass[idx];
                invMass[idx] = 0f;
                handleOffset[k] = pos[idx] - bucketPos;
            }

            // إعادة تعيين متغيرات السرعة
            previousManualTarget = bucketPos;
            manualVelocity = Vector3.zero;
            hasPreviousTarget = false;

            manualTarget = bucketPos;
            IsManualMode = true;

            if (pendulum) pendulum.SetManualFreeze(true);
        }

        public void ExitManualMode()
        {
            if (!IsManualMode) return;

            for (int k = 0; k < handleSize; k++)
            {
                int idx = handleFirstIdx + k;
                invMass[idx] = cachedHandleInvMass[k];
                // نحافظ على السرعة - لا نضع prev = pos
            }

            IsManualMode = false;

            if (dynamicBucket && pendulum)
                PublishState(fixedStep);

            if (pendulum) pendulum.SetManualFreeze(false);
        }

        public void SetManualBucketPosition(Vector3 desiredWorldPosition)
        {
            if (!ready || !IsManualMode) return;

            Vector3 clamped = ClampToRopeConstraint(desiredWorldPosition);

            // حساب السرعة من حركة الهدف
            if (hasPreviousTarget)
            {
                manualVelocity = (clamped - previousManualTarget) / Time.deltaTime;
                float maxSpeed = 20f;
                if (manualVelocity.magnitude > maxSpeed)
                    manualVelocity = manualVelocity.normalized * maxSpeed;
            }
            else
            {
                manualVelocity = Vector3.zero;
                hasPreviousTarget = true;
            }

            previousManualTarget = clamped;
            manualTarget = clamped;
        }

        public Vector3 ClampToRopeConstraint(Vector3 desiredWorldPosition)
        {
            Vector3 pivotPos = PivotPosition;
            Vector3 offset = desiredWorldPosition - pivotPos;
            float dist = offset.magnitude;

            Vector3 dir = dist > 1e-6f ? offset / dist : Vector3.down;

            float minD = Mathf.Min(minimumManualDistance, ropeLength);
            float clampedDist = Mathf.Clamp(dist, minD, ropeLength);

            return pivotPos + dir * clampedDist;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!ready || pos == null) return;

            Gizmos.color = new Color(0f, 0.9f, 1f, 0.5f);
            for (int i = 1; i < n - 1; i++) Gizmos.DrawWireSphere(pos[i], ropeWidth * 0.5f);

            if (enableTorsion && twist != null)
            {
                Gizmos.color = Color.green;
                for (int i = 0; i < segments; i++)
                {
                    Vector3 mid = (pos[i] + pos[i + 1]) * 0.5f;
                    Vector3 mat = Quaternion.AngleAxis(twist[i] * Mathf.Rad2Deg, tangent[i]) * refDir[i];
                    Gizmos.DrawLine(mid, mid + mat * (ropeWidth * 3f));
                }
            }

            if (pivot)
            {
                Gizmos.color = new Color(1f, 0.65f, 0f, 0.85f);
                Gizmos.DrawWireSphere(pivot.position, ropeLength);
                Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.85f);
                Gizmos.DrawWireSphere(pivot.position, minimumManualDistance);
            }

            if (Application.isPlaying && IsManualMode)
            {
                Gizmos.color = new Color(1f, 0f, 1f, 0.9f);
                for (int k = 0; k < handleSize; k++)
                    Gizmos.DrawWireSphere(pos[handleFirstIdx + k], ropeWidth * 1.8f);
            }
        }
#endif
    }
}
