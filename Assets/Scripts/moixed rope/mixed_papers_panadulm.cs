using System;
using UnityEngine;

public class MooringLinePBD_Spherical : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────
    //  INSPECTOR FIELDS
    // ─────────────────────────────────────────────────────────────────────

    [Header("Rope Topology / طبولوجيا الحبل")]
    [Tooltip("عدد الأجزاء. عدد العقد = numSegments + 1")]
    public int numSegments = 20;
    [Tooltip("يُستخدم فقط إذا كان pivot / ball غير مُعيَّنَين أو متطابقَين")]
    public float fallbackRopeLength = 5.0f;

    [Header("Mass Properties / خصائص الكتلة")]
    [Tooltip("كتلة كل عقدة وسطى من الحبل (kg)")]
    public float nodeMass = 0.05f;
    [Tooltip("كتلة الدلو / الكرة في الطرف الحر (kg)")]
    public float ballMass = 2.0f;

    [Header("Sub-Stepping / الخطوات الفرعية")]
    [Tooltip("عدد الخطوات الفرعية لكل FixedUpdate. كلما زاد زادت الدقة والثبات")]
    [Range(1, 20)] public int subSteps = 8;

    [Header("1 · Distance Constraint / قيد المسافة")]
    [Range(1, 20)] public int distanceIterations = 4;
    [Tooltip("صلابة الشد [0,1]. 1 = غير قابل للاستطالة تقريبًا")]
    [Range(0f, 1f)] public float stretchStiffness = 1.0f;
    [Tooltip("صلابة الانضغاط [0,1]. 0 = يسمح بالتراخي")]
    [Range(0f, 1f)] public float compressionStiffness = 0f;

    [Header("2 · Bending Constraint / قيد الانحناء")]
    public bool enableBending = true;
    [Range(1, 20)] public int bendingIterations = 2;
    [Tooltip("صلابة الانحناء [0,1]. القيمة الصغيرة تجعل الحبل مرنًا كسلسلة حقيقية")]
    [Range(0f, 1f)] public float bendingStiffness = 0.02f;

    [Header("3 · Long Range Attachment (LRA)")]
    [Tooltip("يُنصح بتعطيله أو خفض lraIterations إذا تعارض مع الحركة الكروية")]
    public bool enableLRA = false;
    [Range(1, 10)] public int lraIterations = 1;

    [Header("Rope Damping (intermediate nodes only) / تخميد الحبل الداخلي فقط")]
    [Tooltip("معامل التخميد الخطي لعقد الحبل الوسيطة (1/s). لا يُطبَّق على الدلو")]
    [Range(0f, 0.5f)] public float ropeDamping = 0.02f;

    [Header("Anchoring / التثبيت")]
    [Tooltip("نقطة التعليق الثابتة — العقدة 0 تُثبَّت هنا")]
    public Transform pivot;
    [Tooltip("كائن الدلو البصري — يتتبع العقدة الأخيرة")]
    public Transform ball;

    [Header("Spherical Pendulum — Initial Conditions / الشروط الابتدائية")]
    [Tooltip("تفعيل التهيئة الكروية الصريحة من الزوايا أدناه")]
    public bool useSphericalInit = true;
    [Tooltip("الزاوية القطبية الابتدائية θ₀ من العمود الرأسي السفلي (درجة)")]
    public float startTheta = 30f;
    [Tooltip("الزاوية السمتية الابتدائية φ₀ (درجة)")]
    public float startPhi = 0f;
    [Tooltip("السرعة الزاوية القطبية الابتدائية θ̇₀ (درجة/ثانية)")]
    public float startThetaDot = 0f;
    [Tooltip("السرعة الزاوية السمتية الابتدائية φ̇₀ (درجة/ثانية). قيمة غير صفرية → حركة كروية")]
    public float startPhiDot = 120f;

    [Header("Spherical Pendulum — Physical Parameters / معاملات البندول الكروي")]
    [Tooltip("تسارع الجاذبية المستخدم في معادلات لاغرانج")]
    public float gravity = 9.81f;
    [Tooltip("كثافة الهواء ρ (kg/m³). ~1.225 عند مستوى سطح البحر")]
    [Min(0f)] public float airDensity = 1.225f;
    [Tooltip("معامل السحب الهوائي Cd (بلا أبعاد). ~1.0 دلو مفتوح")]
    [Min(0f)] public float dragCoefficient = 1.0f;
    [Tooltip("نصف قطر الدلو (m) — يُحدِّد مساحة المقطع الأمامي = π r²")]
    [Min(0f)] public float bucketRadius = 0.12f;
    [Tooltip("احتكاك نقطة التعليق f (1/s) — تخميد ثابت صغير عند المحور")]
    [Min(0f)] public float pivotFriction = 0.02f;

    [Header("Visual Binding / الربط البصري")]
    [Tooltip("Option A: كائنات Transform جاهزة (عددها = numSegments)")]
    public Transform[] ropeSegments;
    [Tooltip("Option B: LineRenderer احتياطي (يُنشأ تلقائيًا إذا لزم)")]
    public LineRenderer lineRenderer;
    public float lineWidth = 0.03f;

    // ─────────────────────────────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────────────────────────────

    // PBD arrays — جميعها بحجم numSegments + 1
    private Vector3[] positions;    // x_i  : الموضع المُثبَّت في نهاية كل خطوة
    private Vector3[] predicted;    // p_i  : الموضع المؤقت أثناء حل القيود
    private Vector3[] velocities;   // v_i  : السرعة
    private float[]   invMass;      // w_i  : مقلوب الكتلة (0 = مثبَّت)
    private float[]   mass;         // m_i  : الكتلة
    private float[]   lraMaxDist;   // r_i  : نصف القطر الأقصى من pivot (LRA)
    private float[]   bendRestAngle;// φ₀_m : زاوية الانحناء عند الراحة

    // Spherical pendulum state — double precision لتطابق SphericalPendulum.cs
    private double th;     // θ : الزاوية القطبية
    private double ph;     // φ : الزاوية السمتية
    private double thDot;  // θ̇
    private double phDot;  // φ̇

    // Constants
    private const double MinSin = 1e-3; // حد أدنى لـ sin(θ) لتفادي التفرد عند القطبين

    // Derived
    private double pendulumLength;          // L = totalRopeLength (ثابت)
    private float  restLengthPerSegment;
    private float  totalRopeLength;
    private bool   usingLineRendererFallback;
    private float  FrontalArea => Mathf.PI * bucketRadius * bucketRadius;

    // ─────────────────────────────────────────────────────────────────────
    //  INITIALISATION
    // ─────────────────────────────────────────────────────────────────────

    void Awake()
    {
        int n = numSegments + 1;
        positions    = new Vector3[n];
        predicted    = new Vector3[n];
        velocities   = new Vector3[n];
        invMass      = new float[n];
        mass         = new float[n];
        lraMaxDist   = new float[n];
        bendRestAngle = new float[n];

        Vector3 pivotPos = (pivot != null) ? pivot.position : transform.position;
        Vector3 ballPos  = (ball  != null) ? ball.position  :
                           pivotPos + Vector3.down * fallbackRopeLength;

        totalRopeLength = Vector3.Distance(pivotPos, ballPos);
        if (totalRopeLength < 1e-4f)
        {
            ballPos         = pivotPos + Vector3.down * Mathf.Max(fallbackRopeLength, 0.1f);
            totalRopeLength = Vector3.Distance(pivotPos, ballPos);
        }

        pendulumLength       = totalRopeLength;
        restLengthPerSegment = totalRopeLength / numSegments;

        // ── الشروط الابتدائية الكروية ──────────────────────────────────
        if (useSphericalInit)
        {
            th    = startTheta    * Mathf.Deg2Rad;
            ph    = startPhi      * Mathf.Deg2Rad;
            thDot = startThetaDot * Mathf.Deg2Rad;
            phDot = startPhiDot   * Mathf.Deg2Rad;

            // نُعيد حساب موضع الكرة من الزوايا (يتجاوز موضع الـ Transform في Editor)
            ballPos = pivotPos + SphericalToCartesian(th, ph, pendulumLength);
        }
        else
        {
            // نستخلص الزوايا من موضع الكرة الحالي في الـ Editor
            CartesianToSpherical(ballPos - pivotPos, out th, out ph);
            thDot = 0.0;
            phDot = 0.0;
        }

        // ── توزيع العقد على الخط الواصل ────────────────────────────────
        for (int i = 0; i < n; i++)
        {
            float t      = (float)i / numSegments;
            positions[i] = Vector3.Lerp(pivotPos, ballPos, t);
            velocities[i] = Vector3.zero;

            if (i == 0)
            {
                // العقدة 0: مثبَّتة عند pivot → w = 0
                mass[i]    = float.PositiveInfinity;
                invMass[i] = 0f;
            }
            else if (i == numSegments)
            {
                // العقدة الأخيرة: الدلو — كتلة ثقيلة حقيقية داخل نظام القيود
                mass[i]    = Mathf.Max(ballMass, 1e-6f);
                invMass[i] = 1f / mass[i];
            }
            else
            {
                mass[i]    = Mathf.Max(nodeMass, 1e-6f);
                invMass[i] = 1f / mass[i];
            }

            lraMaxDist[i] = Vector3.Distance(positions[i], pivotPos);
        }

        // ── السرعة الابتدائية للدلو من الحالة الكروية ──────────────────
        velocities[numSegments] = CartesianVelocityFromSpherical(th, ph, thDot, phDot);

        // ── زوايا الانحناء عند الراحة ───────────────────────────────────
        for (int m = 1; m < numSegments; m++)
        {
            Vector3 vA  = positions[m - 1] - positions[m];
            Vector3 vB  = positions[m + 1] - positions[m];
            float   lA  = vA.magnitude;
            float   lB  = vB.magnitude;
            if (lA < 1e-8f || lB < 1e-8f) { bendRestAngle[m] = Mathf.PI; continue; }
            float   d   = Mathf.Clamp(Vector3.Dot(vA / lA, vB / lB), -0.9999f, 0.9999f);
            bendRestAngle[m] = Mathf.Acos(d);
        }

        SetupVisuals(n);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  MAIN LOOP
    // ─────────────────────────────────────────────────────────────────────

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) return;

        float h = dt / subSteps;

        for (int s = 0; s < subSteps; s++)
            SimulateSubstep(h);

        UpdateVisuals();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  SUB-STEP: القلب الكامل للمحاكاة المدمجة
    // ─────────────────────────────────────────────────────────────────────

    private void SimulateSubstep(float h)
    {
        int     n        = positions.Length;
        int     lastIdx  = numSegments;
        Vector3 pivotPos = (pivot != null) ? pivot.position : positions[0];

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // STEP 1A — مرحلة التنبؤ للعقد الوسيطة (PBD كلاسيكي)
        //   الجاذبية + تخميد خطي مستقل عن معدل الإطارات
        //   لا يُطبَّق على الدلو (lastIdx) — يُعالَج في 1B
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        for (int i = 0; i < n; i++)
        {
            if (invMass[i] == 0f)
            {
                // العقدة المثبَّتة: تتبع pivot مباشرة
                predicted[i] = pivotPos;
                continue;
            }
            if (i == lastIdx) continue; // الدلو يُعالَج في 1B

            // تطبيق الجاذبية والتخميد على عقد الحبل الوسيطة فقط
            velocities[i] += h * Physics.gravity;
            velocities[i] *= Mathf.Clamp01(1f - ropeDamping * h);
            predicted[i]   = positions[i] + h * velocities[i];
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // STEP 1B — تحديث الدلو بتكامل RK4 (لاغرانج / بندول كروي)
        //
        //   هذا هو الفارق الجوهري عن PBD البسيط:
        //   بدل تطبيق Physics.gravity مباشرة على الدلو، نُشغِّل معادلات
        //   حركة لاغرانج الكاملة للبندول الكروي عبر RK4:
        //
        //     θ̈ = φ̇²·sin(θ)·cos(θ) − (g/L)·sin(θ) − damp·θ̇
        //     φ̈ = −2·θ̇·φ̇·cot(θ) − damp·φ̇
        //
        //   هذه المعادلات تحفظ الزخم الزاوي حول المحور الرأسي تلقائيًا
        //   (في حدود التخميد)، مما يُنتج حركة كروية حقيقية وليست مستوية.
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        IntegrateRK4((double)h, ref th, ref ph, ref thDot, ref phDot);

        // تحويل الحالة الكروية الجديدة إلى إحداثيات ديكارتية
        predicted[lastIdx] = pivotPos + SphericalToCartesian(th, ph, pendulumLength);

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // STEP 2 — حل قيود PBD على جميع العقد (بما فيها الدلو)
        //          هذا هو "تأثير الحبل على الدلو":
        //          إذا كان الحبل متوتراً أو منحنياً، يُعدِّل القيدُ
        //          موضعَ الدلو المتوقَّع قليلاً
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        // قيود المسافة — تبديل الاتجاه كل تكرار لتقارع أسرع وأقل تحيزاً
        for (int iter = 0; iter < distanceIterations; iter++)
        {
            if (iter % 2 == 0)
                for (int c = 0; c < numSegments; c++)
                    SolveDistanceConstraint(c, c + 1, restLengthPerSegment);
            else
                for (int c = numSegments - 1; c >= 0; c--)
                    SolveDistanceConstraint(c, c + 1, restLengthPerSegment);
        }

        // قيود الانحناء — نفس تبديل الاتجاه
        if (enableBending)
        {
            for (int iter = 0; iter < bendingIterations; iter++)
            {
                if (iter % 2 == 0)
                    for (int m = 1; m < numSegments; m++)
                        SolveBendingConstraint(m - 1, m, m + 1);
                else
                    for (int m = numSegments - 1; m >= 1; m--)
                        SolveBendingConstraint(m - 1, m, m + 1);
            }
        }

        // قيد LRA (اختياري)
        if (enableLRA)
        {
            for (int iter = 0; iter < lraIterations; iter++)
                for (int i = 1; i < n; i++)
                    SolveLRAConstraint(i, pivotPos);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // STEP 3 — الإلتزام: تحديث السرعات والمواضع
        //          ثم استخلاص الحالة الكروية من الموضع/السرعة المُصحَّحَين
        //
        //          هذا هو "تأثير الحبل على RK4 في الخطوة القادمة":
        //          (θ, φ, θ̇, φ̇) المُستخلَصَة من الموضع المُصحَّح
        //          هي ما ستبدأ به خطوة RK4 التالية — والحلقة تغلق.
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        for (int i = 0; i < n; i++)
        {
            if (invMass[i] == 0f)
            {
                positions[i] = predicted[i];
                continue;
            }
            // السرعة = الإزاحة الكلية (بما فيها تصحيح القيود) / h
            velocities[i] = (predicted[i] - positions[i]) / h;
            positions[i]  = predicted[i];
        }

        // ── استخلاص الحالة الكروية من الدلو المُصحَّح ─────────────
        // هذا يُغلق حلقة التأثير المتبادل:
        //   موضع الدلو بعد القيود → (θ, φ) جديدتان
        //   سرعة الدلو بعد القيود → (θ̇, φ̇) جديدتان
        Vector3 rBall = positions[lastIdx] - pivotPos;
        if (rBall.sqrMagnitude > 1e-10f)
        {
            CartesianToSpherical(rBall, out th, out ph);
            ExtractSphericalVelocity(th, ph, velocities[lastIdx], out thDot, out phDot);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  LAGRANGIAN SPHERICAL PENDULUM EQUATIONS  (من SphericalPendulum.cs)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// معادلات الحركة الاشتقاقية للبندول الكروي (لاغرانج):
    ///   dθ/dt    = θ̇
    ///   dθ̇/dt   = φ̇²·sin(θ)·cos(θ) − (g/L)·sin(θ) − damp·θ̇
    ///   dφ̇/dt   = −2·θ̇·φ̇·cot(θ)  − damp·φ̇
    /// حيث damp = سحب هوائي + احتكاك التعليق
    /// </summary>
    private void SphericalDerivatives(
        double theta,    double thetaDot, double phiDot,
        out double dTheta, out double dThetaDot, out double dPhiDot)
    {
        double s     = Math.Sin(theta);
        double c     = Math.Cos(theta);
        double sSafe = Math.Abs(s) < MinSin ? (s < 0 ? -MinSin : MinSin) : s;
        double cot   = c / sSafe;

        double gOverL = gravity / pendulumLength;

        // السرعة الخطية للدلو (للسحب الهوائي)
        double speed = pendulumLength *
            Math.Sqrt(thetaDot * thetaDot + s * s * phiDot * phiDot);

        // معامل التخميد الكلي: سحب هوائي + احتكاك نقطة التعليق
        double aeroDrag = (0.5 * airDensity * dragCoefficient * FrontalArea / ballMass)
                          * speed;
        double damp = aeroDrag + pivotFriction;

        dTheta    = thetaDot;
        dThetaDot = phiDot * phiDot * s * c - gOverL * s - damp * thetaDot;
        dPhiDot   = -2.0 * thetaDot * phiDot * cot             - damp * phiDot;
    }

    /// <summary>
    /// تكامل RK4 من الرتبة الرابعة (من SphericalPendulum.Integrate)
    /// خطأ الاقتطاع O(h⁵) — مناسب جدًا لخطوات h ≤ 0.005 ثانية
    /// </summary>
    private void IntegrateRK4(double h,
        ref double theta, ref double phi, ref double thetaDot, ref double phiDot)
    {
        SphericalDerivatives(theta, thetaDot, phiDot,
            out var a1, out var c1, out var d1);
        SphericalDerivatives(theta + 0.5*h*a1, thetaDot + 0.5*h*c1, phiDot + 0.5*h*d1,
            out var a2, out var c2, out var d2);
        SphericalDerivatives(theta + 0.5*h*a2, thetaDot + 0.5*h*c2, phiDot + 0.5*h*d2,
            out var a3, out var c3, out var d3);
        SphericalDerivatives(theta + h*a3, thetaDot + h*c3, phiDot + h*d3,
            out var a4, out var c4, out var d4);

        // التدفق الزمني لـ φ (زاوية مستقلة)
        double b1 = phiDot;
        double b2 = phiDot + 0.5*h*d1;
        double b3 = phiDot + 0.5*h*d2;
        double b4 = phiDot +     h*d3;

        theta    += h / 6.0 * (a1 + 2*a2 + 2*a3 + a4);
        phi      += h / 6.0 * (b1 + 2*b2 + 2*b3 + b4);
        thetaDot += h / 6.0 * (c1 + 2*c2 + 2*c3 + c4);
        phiDot   += h / 6.0 * (d1 + 2*d2 + 2*d3 + d4);

        // تقييد θ بعيدًا عن القطبين لتفادي التفرد الرياضي
        theta = Math.Max(MinSin, Math.Min(Math.PI - MinSin, theta));
    }

    // ─────────────────────────────────────────────────────────────────────
    //  COORDINATE CONVERSION HELPERS
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// تحويل (θ, φ, L) → إزاحة ديكارتية عن pivot
    /// نفس اتفاقية SphericalPendulum.SphericalToCartesian:
    ///   x = L·sin(θ)·cos(φ)
    ///   y = −L·cos(θ)       ← θ=0 → أسفل
    ///   z = −L·sin(θ)·sin(φ)
    /// </summary>
    private static Vector3 SphericalToCartesian(double theta, double phi, double l)
    {
        float s  = (float)Math.Sin(theta);
        float c  = (float)Math.Cos(theta);
        float cp = (float)Math.Cos(phi);
        float sp = (float)Math.Sin(phi);
        return new Vector3((float)l * s * cp, -(float)l * c, -(float)l * s * sp);
    }

    /// <summary>
    /// تحويل إزاحة ديكارتية عن pivot → (θ, φ)
    /// عكس SphericalToCartesian بالضبط
    /// </summary>
    private void CartesianToSpherical(Vector3 r, out double theta, out double phi)
    {
        float dist = r.magnitude;
        if (dist < 1e-6f) dist = 1e-6f;
        Vector3 rn = r / dist;

        // cos(θ) = −rn.y  (لأن y = −L·cos(θ))
        theta = Math.Acos(Math.Max(-1.0, Math.Min(1.0, (double)(-rn.y))));
        // φ = atan2(−rn.z, rn.x)
        phi   = Math.Atan2((double)(-rn.z), (double)rn.x);
        theta = Math.Max(MinSin, Math.Min(Math.PI - MinSin, theta));
    }

    /// <summary>
    /// حساب السرعة الديكارتية من الحالة الكروية:
    ///   v = L·(θ̇·e_θ + φ̇·sin(θ)·e_φ)
    /// حيث:
    ///   e_θ = (cos(θ)cos(φ),  sin(θ), −cos(θ)sin(φ))
    ///   e_φ = (       −sin(φ),      0,        −cos(φ))
    /// كلاهما متعامد تمامًا مع e_r → السرعة مماسية بحتة (لا مركبة شعاعية)
    /// </summary>
    private Vector3 CartesianVelocityFromSpherical(
        double theta, double phi, double thetaDot, double phiDot)
    {
        float s  = (float)Math.Sin(theta);
        float c  = (float)Math.Cos(theta);
        float cp = (float)Math.Cos(phi);
        float sp = (float)Math.Sin(phi);

        Vector3 eTheta = new Vector3(c * cp,  s, -c * sp);
        Vector3 ePhi   = new Vector3(   -sp, 0f,     -cp);

        float L = (float)pendulumLength;
        return L * ((float)thetaDot * eTheta + (float)phiDot * s * ePhi);
    }

    /// <summary>
    /// استخلاص (θ̇, φ̇) من سرعة ديكارتية — عكس CartesianVelocityFromSpherical
    ///   θ̇ = (v · e_θ) / L
    ///   φ̇ = (v · e_φ) / (L·sin(θ))
    /// </summary>
    private void ExtractSphericalVelocity(
        double theta, double phi, Vector3 v,
        out double thetaDot, out double phiDot)
    {
        float s  = (float)Math.Sin(theta);
        float c  = (float)Math.Cos(theta);
        float cp = (float)Math.Cos(phi);
        float sp = (float)Math.Sin(phi);

        Vector3 eTheta = new Vector3(c * cp,  s, -c * sp);
        Vector3 ePhi   = new Vector3(   -sp, 0f,     -cp);

        float L     = (float)pendulumLength;
        float sSafe = Math.Abs(s) < (float)MinSin ? (float)MinSin : s;

        thetaDot = Vector3.Dot(v, eTheta) / L;
        phiDot   = Vector3.Dot(v, ePhi)   / (L * sSafe);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  PBD CONSTRAINT SOLVERS (من MooringLinePBD_Rope، بدون تعديل)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// قيد المسافة — C(p_i, p_j) = |p_i − p_j| − d
    /// صلابة غير متماثلة: stretchStiffness عند C>0، compressionStiffness عند C&lt;0
    /// </summary>
    private void SolveDistanceConstraint(int i, int j, float restLength)
    {
        float wi = invMass[i], wj = invMass[j], wSum = wi + wj;
        if (wSum <= 0f) return;

        Vector3 delta = predicted[i] - predicted[j];
        float   len   = delta.magnitude;
        if (len < 1e-8f) return;

        Vector3 n = delta / len;
        float   C = len - restLength;
        float   k = (C > 0f) ? stretchStiffness : compressionStiffness;

        Vector3 corr = -(C / wSum) * n * k;
        predicted[i] += wi * corr;
        predicted[j] -= wj * corr;
    }

    /// <summary>
    /// قيد الانحناء — يحافظ على الزاوية بين ثلاث عقد متتالية
    /// المشتقات مُشتقَّة من أول المبادئ (unit edge-vector formulation)
    /// </summary>
    private void SolveBendingConstraint(int prevIdx, int midIdx, int nextIdx)
    {
        float wM = invMass[midIdx], wP = invMass[prevIdx], wN = invMass[nextIdx];
        if (wM + wP + wN <= 0f) return;

        Vector3 vA  = predicted[prevIdx] - predicted[midIdx];
        Vector3 vB  = predicted[nextIdx] - predicted[midIdx];
        float   lA  = vA.magnitude, lB = vB.magnitude;
        if (lA < 1e-8f || lB < 1e-8f) return;

        Vector3 nA  = vA / lA, nB = vB / lB;
        float   d   = Mathf.Clamp(Vector3.Dot(nA, nB), -0.9999f, 0.9999f);
        float   inv = 1f / Mathf.Sqrt(1f - d * d);
        float   C   = Mathf.Acos(d) - bendRestAngle[midIdx];

        Vector3 gP = -inv * (1f / lA) * (nB - d * nA);
        Vector3 gN = -inv * (1f / lB) * (nA - d * nB);
        Vector3 gM = -(gP + gN);

        float den = wM * gM.sqrMagnitude + wP * gP.sqrMagnitude + wN * gN.sqrMagnitude;
        if (den < 1e-8f) return;

        float lam = -C / den;
        predicted[midIdx]  += bendingStiffness * wM * lam * gM;
        predicted[prevIdx] += bendingStiffness * wP * lam * gP;
        predicted[nextIdx] += bendingStiffness * wN * lam * gN;
    }

    /// <summary>
    /// قيد LRA: يمنع العقدة من الابتعاد عن pivot أكثر من مسافتها الابتدائية
    /// (إسقاط هندسي مباشر — غير محافظ على الزخم)
    /// </summary>
    private void SolveLRAConstraint(int i, Vector3 anchor)
    {
        if (invMass[i] == 0f) return;
        Vector3 v    = predicted[i] - anchor;
        float   dist = v.magnitude;
        float   maxD = lraMaxDist[i];
        if (dist > maxD && dist > 1e-8f)
            predicted[i] = anchor + v * (maxD / dist);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  VISUALS
    // ─────────────────────────────────────────────────────────────────────

    private void SetupVisuals(int nodeCount)
    {
        usingLineRendererFallback = (ropeSegments == null || ropeSegments.Length == 0);
        if (!usingLineRendererFallback)
        {
            if (ropeSegments.Length != numSegments)
                Debug.LogWarning($"[MooringLinePBD_Spherical] ropeSegments.Length " +
                    $"({ropeSegments.Length}) ≠ numSegments ({numSegments}).");
            return;
        }

        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
                lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        if (lineRenderer.sharedMaterial == null)
        {
            Shader sh  = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            var    mat = new Material(sh) { color = Color.white };
            lineRenderer.material = mat;
        }

        lineRenderer.positionCount  = nodeCount;
        lineRenderer.startWidth     = lineWidth;
        lineRenderer.endWidth       = lineWidth;
        lineRenderer.useWorldSpace  = true;
    }

    private void UpdateVisuals()
    {
        if (!usingLineRendererFallback)
        {
            int count = Mathf.Min(ropeSegments.Length, numSegments);
            for (int i = 0; i < count; i++)
            {
                if (ropeSegments[i] == null) continue;
                ropeSegments[i].position = positions[i];
                Vector3 dir = positions[i + 1] - positions[i];
                if (dir.sqrMagnitude > 1e-10f)
                    ropeSegments[i].rotation = Quaternion.LookRotation(dir);
            }
        }
        else if (lineRenderer != null)
        {
            for (int i = 0; i < positions.Length; i++)
                lineRenderer.SetPosition(i, positions[i]);
        }

        if (ball != null)
            ball.position = positions[positions.Length - 1];
    }

    void OnDrawGizmosSelected()
    {
        if (positions == null) return;
        Gizmos.color = new Color(0.3f, 0.8f, 1f);
        foreach (var p in positions) Gizmos.DrawSphere(p, 0.03f);
    }
}