using System;
using UnityEngine;

namespace onlyone
{
    // الحلّال الوحيد للنظام المقترن. مسؤول عن:
    // القوى الخارجية على كل عقدة (جاذبية/رياح/سحب)، تكامل Verlet،
    // قيود XPBD (شد/انحناء/التواء DER)، تحديث المواضع والسرعات،
    // وحساب الحالة المشتقة (θ,φ,θ̇,φ̇,tension,energy) وإرسالها للنواس كـ RopeState.
    // المراجع: Bergou 2008 (DER)، Macklin 2016 (XPBD)، Macklin 2019 (substeps).
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

        private LineRenderer lr;
        private int n; private float segLen;
        private Vector3[] pos, prev, renderPos;
        private float[] invMass, lambda, lambdaBend;
        private float accumulator; private bool ready;
        private float bucketTension;          // شدّ الدلو (N) من λ آخر قطعة
        private float lastSubstep;            // آخر خطوة فرعية لحساب السرعات

        private float[]   twist, twistPrev, invInertia, lambdaTw, refTwist;
        private Vector3[] tangent, prevTangent, refDir;
        private float     lastAppliedTwist;

        // مخرجات القراءة (للتشخيص/الواجهة).
        public float  TensionN     => bucketTension;
        public double TotalEnergyJ => currentState.TotalEnergy;
        private RopeState currentState;

        private void Start()
        {
            if (!pivot || !bob)
            {
                Debug.LogError($"[{nameof(PbdRope)}] 'pivot' and 'bob' must be assigned.", this);
                enabled = false; return;
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
            torsionRadius         = c.torsionRadius;
            torsionIterations     = Mathf.Max(1, c.torsionIterations);
            clampPivotTwist       = c.clampPivotTwist;
            maxTwistRate          = c.maxTwistRate;

            if (pivot && bob) InitializeRope();
        }

        private void InitializeRope()
        {
            ready = false;

            // ضبط ملكية المحاكاة: الحبل يقود ⇒ النواس مُراقِب.
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

        // الإطلاق من الشروط الابتدائية للنواس (المصدر الوحيد لشروط البداية).
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

            // I_edge = ½ m r² (أسطوانة حول محورها).
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

            // لفّ ابتدائي: dθ/ds ثابت ⇒ توزيع خطي؛ twistPrev=twist ⇒ ω₀=0.
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

            // XPBD Alg.1 line 4: تصفير λ بداية كل خطوة.
            Array.Clear(lambda, 0, segments);
            Array.Clear(lambdaBend, 0, n);

            float retention = Mathf.Exp(-internalDampingRate * dt);
            Vector3 accDt2 = (Vector3.down * gravity + wind) * (dt * dt);

            for (int i = 1; i < n; i++)
            {
                if (invMass[i] < 1e-12f) continue;

                Vector3 cur   = pos[i];
                Vector3 delta = (cur - prev[i]) * retention;

                if (i == n - 1 && dynamicBucket)
                {
                    // سحب هواء تربيعي مطبّق كعامل على الإزاحة.
                    float a  = Mathf.PI * bucketRadius * bucketRadius;
                    float kd = 0.5f * airDensity * bucketDragCoefficient * a * invMass[i];
                    float f  = Mathf.Min(kd * delta.magnitude, 1f);
                    delta *= (1f - f);
                }

                pos[i]  = cur + delta + accDt2;
                prev[i] = cur;
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

            // شدّ الدلو: |λ| للقطعة الأخيرة مقسوماً على dt² (XPBD: القوة = λ·∇C/dt²).
            bucketTension = Mathf.Abs(lambda[segments - 1]) / (dt * dt);
        }

        // قيد مسافة XPBD مع حدّ تخميد β̃ (Eq.26) على تشوّه القطعة فقط.
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
                velTerm = Vector3.Dot(nHat, dxB - dxA);   // ∇C·Δx
            }

            float gamma = alphaTilde * betaTilde;
            float dL = (-c - gamma * velTerm - alphaTilde * lambda[idx])
                     / ((1f + gamma) * wSum + alphaTilde);
            lambda[idx] += dL;

            pos[a] -= nHat * (invMass[a] * dL);
            pos[b] += nHat * (invMass[b] * dL);
        }

        // قيد انحناء DER: C=φ (زاوية الانعطاف)، rest=0. ‖κb‖=2tan(φ/2)=φ+O(φ³).
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

            // نقل زمني للإطار المرجعي (Bergou §4.2.2).
            for (int i = 0; i < segments; i++)
            {
                Vector3 d = ParallelTransport(refDir[i], prevTangent[i], tangent[i]);
                d -= Vector3.Dot(d, tangent[i]) * tangent[i];
                refDir[i] = d.sqrMagnitude > 1e-10f ? d.normalized : OrthoSeed(tangent[i]);
                prevTangent[i] = tangent[i];
            }

            // Reference twist بين الحواف (تبادل Twist↔Writhe).
            for (int j = 1; j < segments; j++)
            {
                Vector3 spaceT = ParallelTransport(refDir[j - 1], tangent[j - 1], tangent[j]);
                refTwist[j] = SignedAngle(spaceT, refDir[j], tangent[j]);
            }

            // تكامل Verlet زاوي بتخميد رايلي موحّد ζ.
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

            // قيود XPBD توائية: C_j = θ_{j+1} − θ_j + refTwist_{j+1}.
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

        // يحسب الحالة المشتقة من حالة الحبل الحقيقية ويرسلها للمُراقِب كـ RopeState.
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

            Vector3 v = (pos[n - 1] - prev[n - 1]) / dt;   // dt = خطوة فرعية
            double thetaDot = Vector3.Dot(v, eTheta) / lEff;
            double phiDot   = Vector3.Dot(v, ePhi)   / (lEff * sSafe);

            ComputeEnergy(dt, out double ke, out double pe, out double elastic);

            currentState = new RopeState(theta, phi, thetaDot, phiDot,
                                         lEff, bucketTension, ke, pe, elastic);
            pendulum.PushState(currentState);
        }

        // طاقة النظام (الحبل يملك كل العقد والسرعات ⇒ هو الوحيد القادر على حسابها).
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
                pe += mi * gravity * (pos[i].y - pivotPos.y);   // مرجع الجهد: المحور
            }

            // طاقة مرنة: شد Σ½k_s·C² + انحناء Σ½k_b·φ² + التواء Σ½k_t·m² (Bergou Eq.1-2).
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
            Vector3 attach = bobOverride ? bobOverride.position : bob.position;
            bob.position += renderPos[n - 1] - attach;
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
        }
#endif
    }
}