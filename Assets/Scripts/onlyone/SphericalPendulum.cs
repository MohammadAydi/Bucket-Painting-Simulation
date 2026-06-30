using System;
using UnityEngine;

 
[DisallowMultipleComponent]
public class SphericalPendulum : MonoBehaviour
{
    [Header("Scene References")]
    [Tooltip("Fixed suspension point. The pendulum hangs from this world position.")]
    [SerializeField] private Transform pivot;

    [Tooltip("The bob (ball / bucket). Its position is driven every frame.")]
    [SerializeField] private Transform bob;

    [Tooltip("Optional LineRenderer rope. Leave None if you use a 3-D cylinder rope.")]
    [SerializeField] private LineRenderer rope;

    [Header("Suspension (التعليق)")]
    [Tooltip("Rope length l (meters).")]
    [SerializeField, Min(0.01f)] public float length = 1.2f;

    [Tooltip("يُطفئه PbdRope تلقائياً عند الاقتران، فيتوقّف النواس عن تحريك الدلو.")]
    public bool driveBucket = true;
 
    [Tooltip("يُضبط تلقائياً من PbdRope عند الاقتران: عندها يتوقف النواس عن التكامل " +
             "ويتلقّى حالته من ديناميكا الحبل (الحبل هو مصدر الحقيقة الوحيد).")]
    [HideInInspector] public bool externallyDriven = false;

    [Header("Motion - initial conditions (الحركة)")]
    [Tooltip("Initial polar angle theta_0 from the downward vertical (deg).")]
    [SerializeField] private float startTheta = 60f;

    [Tooltip("Initial azimuthal angle phi_0 (deg).")]
    [SerializeField] private float startPhi = 0f;

    [Tooltip("Initial polar angular velocity theta'_0 (deg/s).")]
    [SerializeField] private float startThetaDot = 0f;

    [Tooltip("Initial azimuthal angular velocity phi'_0 (deg/s). " +
             "0 = flat swing; non-zero = conical / orbiting swing.")]
    [SerializeField] private float startPhiDot = 0f;

    [Header("Environment (البيئة)")]
    [Tooltip("Gravitational acceleration g (m/s^2).")]
    [SerializeField] private float gravity = 9.81f;

    [Tooltip("Air density rho (kg/m^3). ~1.225 at sea level.")]
    [SerializeField, Min(0f)] private float airDensity = 1.225f;

    [Tooltip("Pivot/rope friction f (1/s). Small constant damping at the suspension point.")]
    [SerializeField, Min(0f)] private float pivotFriction = 0.02f;

    [Header("Bucket / Bob (الدلو)")]
    [Tooltip("Bucket mass m (kg). Heavier = slower aerodynamic decay.")]
    [SerializeField, Min(0.001f)] private float mass = 3.0f;

    [Tooltip("Bucket radius (m). Frontal area used for drag = pi * r^2.")]
    [SerializeField, Min(0f)] private float bucketRadius = 0.13f;

    [Tooltip("Drag coefficient Cd. ~0.47 sphere, ~1.0 open bucket, ~1.05 cube.")]
    [SerializeField, Min(0f)] private float dragCoefficient = 1.0f;

    [Header("Integration")]
    [Tooltip("Fixed physics sub-step (s). Smaller = more accurate, more CPU.")]
    [SerializeField, Min(0.0001f)] private float fixedStep = 0.004f;
 
    private double th, ph, thDot, phDot;
    private double accumulator;
    private bool   running;
 
    private double effectiveLength;   
    public  float  BucketTensionNewtons { get; set; } 

    private const double MinSin = 1e-3;
    private float FrontalArea => Mathf.PI * bucketRadius * bucketRadius;
 
    public double Theta           => th;
    public double Phi             => ph;
    public double ThetaDot        => thDot;
    public double PhiDot          => phDot;
    public double EffectiveLength => effectiveLength;
    public float  Mass            => mass;
    public float StartThetaRad   => startTheta   * Mathf.Deg2Rad;
    public float StartPhiRad      => startPhi     * Mathf.Deg2Rad;
    public float StartThetaDotRad => startThetaDot * Mathf.Deg2Rad;
    public float StartPhiDotRad   => startPhiDot   * Mathf.Deg2Rad;
    public Transform Pivot        => pivot;

    private void Start()
    {
        if (pivot == null || bob == null)
        {
            Debug.LogError($"{nameof(SphericalPendulum)}: assign 'pivot' and 'bob'.", this);
            enabled = false;
            return;
        }
        Relaunch();
        RenderPendulum();
    }

    private void OnValidate()
    {
        if (pivot == null || bob == null) return;
        if (Application.isPlaying) return;
        th = startTheta * Mathf.Deg2Rad;
        ph = startPhi   * Mathf.Deg2Rad;
        bob.position = pivot.position + SphericalToCartesian(th, ph, length);
    }

    [ContextMenu("Relaunch (إعادة التجربة)")]
    public void Relaunch()
    {
        th    = startTheta    * Mathf.Deg2Rad;
        ph    = startPhi      * Mathf.Deg2Rad;
        thDot = startThetaDot * Mathf.Deg2Rad;
        phDot = startPhiDot   * Mathf.Deg2Rad;
        effectiveLength = length;
        accumulator = 0;
        running = true;
    }

    private void Update()
    { 
        if (running && !externallyDriven)
        {
            accumulator += Time.deltaTime;
            if (accumulator > 0.25) accumulator = 0.25;
            while (accumulator >= fixedStep)
            {
                Integrate(fixedStep);
                accumulator -= fixedStep;
            }
        }
        RenderPendulum();  
    }
 
    private void Derivatives(
        double thetaIn, double thetaDotIn, double phiDotIn,
        out double dTheta, out double dThetaDot, out double dPhiDot)
    {
        double s = Math.Sin(thetaIn);
        double c = Math.Cos(thetaIn);
        double sSafe = Math.Abs(s) < MinSin ? Math.Sign(s == 0 ? 1 : s) * MinSin : s;

        // |v| = l·√(θ̇² + sin²θ·φ̇²)
        double speed = length * Math.Sqrt(thetaDotIn * thetaDotIn + s * s * phiDotIn * phiDotIn);
        double damp  = (0.5 * airDensity * dragCoefficient * FrontalArea / mass) * speed
                     + pivotFriction;

        dTheta    = thetaDotIn;
        dThetaDot = phiDotIn * phiDotIn * s * c
                  - (gravity / length) * s
                  - damp * thetaDotIn;
        dPhiDot   = -2.0 * thetaDotIn * phiDotIn * (c / sSafe)
                  - damp * phiDotIn;
    }

    private void Integrate(double h)
    { 
        Derivatives(th,                thDot,                phDot,
                    out double k1th, out double k1thd, out double k1phd);
        double k1ph = phDot;

        Derivatives(th + 0.5*h*k1th,   thDot + 0.5*h*k1thd,  phDot + 0.5*h*k1phd,
                    out double k2th, out double k2thd, out double k2phd);
        double k2ph = phDot + 0.5*h*k1phd;

        Derivatives(th + 0.5*h*k2th,   thDot + 0.5*h*k2thd,  phDot + 0.5*h*k2phd,
                    out double k3th, out double k3thd, out double k3phd);
        double k3ph = phDot + 0.5*h*k2phd;

        Derivatives(th + h*k3th,       thDot + h*k3thd,      phDot + h*k3phd,
                    out double k4th, out double k4thd, out double k4phd);
        double k4ph = phDot + h*k3phd;

        th    += h / 6.0 * (k1th  + 2*k2th  + 2*k3th  + k4th);
        thDot += h / 6.0 * (k1thd + 2*k2thd + 2*k3thd + k4thd);
        ph    += h / 6.0 * (k1ph  + 2*k2ph  + 2*k3ph  + k4ph);
        phDot += h / 6.0 * (k1phd + 2*k2phd + 2*k3phd + k4phd);
    }

 
    public void SetStateFromWorld(double theta, double phi,
                                  double thetaDot, double phiDot,
                                  double effLen, float tensionNewtons)
    {
        th = theta;  ph = phi;
        thDot = thetaDot;  phDot = phiDot;
        effectiveLength = effLen;
        BucketTensionNewtons = tensionNewtons;
    }

    private void RenderPendulum()
    {
        if (!driveBucket) return;           
        bob.position = pivot.position + SphericalToCartesian(th, ph, length);
        
        /*Vector3 ropeDir = (pivot.position - bobPos).normalized;
        Quaternion targetRotation = Quaternion.FromToRotation(Vector3.up, ropeDir);

        bob.rotation = Quaternion.Slerp(
            bob.rotation,
            targetRotation,
            Time.deltaTime * 10f
        );*/
    }

    private static Vector3 SphericalToCartesian(double theta, double phi, double l)
    {
        float s  = (float)Math.Sin(theta);
        float c  = (float)Math.Cos(theta);
        float cp = (float)Math.Cos(phi);
        float sp = (float)Math.Sin(phi);
        return new Vector3((float)l * s * cp, -(float)l * c, -(float)l * s * sp);
    }
 
    public float SpeedMetersPerSecond
        => (float)(effectiveLength *
           Math.Sqrt(thDot * thDot + Math.Sin(th) * Math.Sin(th) * phDot * phDot));

    public float MechanicalEnergy
    {
        get
        {
            double l = effectiveLength;
            double s = Math.Sin(th);
            double T = 0.5 * mass * l * l * (thDot * thDot + s * s * phDot * phDot);
            double V = -mass * gravity * l * Math.Cos(th);
            return (float)(T + V);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (pivot == null) return;
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.12f);
        Gizmos.DrawWireSphere(pivot.position, length);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(pivot.position, 0.03f);
    }
}
