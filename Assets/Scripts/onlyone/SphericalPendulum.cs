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
    [SerializeField, Min(0.01f)] private float length = 1.0f;

    [Header("Motion - initial conditions (الحركة)")]
    [Tooltip("Initial polar angle theta_0 from the downward vertical (deg).")]
    [SerializeField] private float startTheta = 30f;

    [Tooltip("Initial azimuthal angle phi_0 (deg).")]
    [SerializeField] private float startPhi = 0f;

    [Tooltip("Initial polar angular velocity theta'_0 (deg/s).")]
    [SerializeField] private float startThetaDot = 0f;

    [Tooltip("Initial azimuthal angular velocity phi'_0 (deg/s). " +
             "0 = flat swing; non-zero = conical / orbiting swing.")]
    [SerializeField] private float startPhiDot = 120f;

    [Tooltip("Stop after this many turning points (swing ends). 0 = run forever.")]
    [SerializeField, Min(0)] private int maxSwings = 0;

    [Header("Environment (البيئة)")]
    [Tooltip("Gravitational acceleration g (m/s^2).")]
    [SerializeField] private float gravity = 9.81f;

    [Tooltip("Air density rho (kg/m^3). ~1.225 at sea level; humidity lowers it slightly.")]
    [SerializeField, Min(0f)] private float airDensity = 1.225f;

    [Tooltip("Pivot/rope friction f (1/s). Small constant damping at the suspension point.")]
    [SerializeField, Min(0f)] private float pivotFriction = 0.02f;

    [Header("Bucket / Bob (الدلو)")]
    [Tooltip("Bucket mass m (kg). Heavier = slower aerodynamic decay.")]
    [SerializeField, Min(0.001f)] private float mass = 2.0f;

    [Tooltip("Bucket radius (m). Frontal area used for drag = pi * r^2.")]
    [SerializeField, Min(0f)] private float bucketRadius = 0.12f;

    [Tooltip("Drag coefficient Cd (dimensionless). ~0.47 sphere, ~1.0 open bucket, ~1.05 cube.")]
    [SerializeField, Min(0f)] private float dragCoefficient = 1.0f;

    [Header("Integration")]
    [Tooltip("Fixed physics sub-step (s). Smaller = more accurate, more CPU.")]
    [SerializeField, Min(0.0001f)] private float fixedStep = 0.004f;

    [Header("Live Readout (runtime, read-only)")]
    [SerializeField] private float outElapsedTime;
    [SerializeField] private float outSpeed;         
    [SerializeField] private int   outSwings;
    [SerializeField] private int   outRevolutions;
    [SerializeField] private float outThetaDeg;
 
    private double th, ph, thDot, phDot;
    private double accumulator;
    private double prevThDot, ph0;
    private float  elapsedTime;
    private int    swingCount;
    private bool   running;

    private const double MinSin = 1e-3;  

    private float FrontalArea => Mathf.PI * bucketRadius * bucketRadius;

    private void Reset() => rope = GetComponent<LineRenderer>();

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
 
    [ContextMenu("Relaunch (إعادة التجربة)")]
    public void Relaunch()
    {
        th    = startTheta    * Mathf.Deg2Rad;
        ph    = startPhi      * Mathf.Deg2Rad;
        thDot = startThetaDot * Mathf.Deg2Rad;
        phDot = startPhiDot   * Mathf.Deg2Rad;

        ph0 = ph;
        prevThDot = thDot;
        elapsedTime = 0f;
        swingCount = 0;
        accumulator = 0;
        running = true;
    }

    private void Update()
    {
        if (running)
        {
            accumulator += Time.deltaTime;
            if (accumulator > 0.25) accumulator = 0.25;  

            while (accumulator >= fixedStep)
            {
                Integrate(fixedStep);
                elapsedTime += (float)fixedStep;
                DetectSwing();
                accumulator -= fixedStep;

                if (maxSwings > 0 && swingCount >= maxSwings) { running = false; accumulator = 0; break; }
            }
        }
        RenderPendulum();
        UpdateReadout();
    }

    // -------------------- Physics --------------------

    private void Derivatives(double theta, double phi, double thetaDot, double phiDot,
                             out double dTheta, out double dPhi, out double dThetaDot, out double dPhiDot)
    {
        double s = Math.Sin(theta);
        double c = Math.Cos(theta);
        double sSafe = Math.Abs(s) < MinSin ? (s < 0 ? -MinSin : MinSin) : s;
        double cot = c / sSafe;

        double gOverL = gravity / length;
 
        double speed = length * Math.Sqrt(thetaDot * thetaDot + s * s * phiDot * phiDot);
        double D = (0.5 * airDensity * dragCoefficient * FrontalArea / mass) * speed;
        double damp = D + pivotFriction;

        dTheta    = thetaDot;
        dPhi      = phiDot;
        dThetaDot = phiDot * phiDot * s * c - gOverL * s - damp * thetaDot;
        dPhiDot   = -2.0 * thetaDot * phiDot * cot - damp * phiDot;
    }
 
    private void Integrate(double h)
    {
        double a1, b1, c1, d1, a2, b2, c2, d2, a3, b3, c3, d3, a4, b4, c4, d4;

        Derivatives(th, ph, thDot, phDot, out a1, out b1, out c1, out d1);
        Derivatives(th + 0.5 * h * a1, ph + 0.5 * h * b1, thDot + 0.5 * h * c1, phDot + 0.5 * h * d1,
                    out a2, out b2, out c2, out d2);
        Derivatives(th + 0.5 * h * a2, ph + 0.5 * h * b2, thDot + 0.5 * h * c2, phDot + 0.5 * h * d2,
                    out a3, out b3, out c3, out d3);
        Derivatives(th + h * a3, ph + h * b3, thDot + h * c3, phDot + h * d3,
                    out a4, out b4, out c4, out d4);

        th    += h / 6.0 * (a1 + 2 * a2 + 2 * a3 + a4);
        ph    += h / 6.0 * (b1 + 2 * b2 + 2 * b3 + b4);
        thDot += h / 6.0 * (c1 + 2 * c2 + 2 * c3 + c4);
        phDot += h / 6.0 * (d1 + 2 * d2 + 2 * d3 + d4);
    }
 
    private void DetectSwing()
    {
        if (prevThDot * thDot < 0 && Math.Abs(th) > 2.0 * Mathf.Deg2Rad)
            swingCount++;
        prevThDot = thDot;
    }
 

    private void RenderPendulum()
    {
        Vector3 bobPos = pivot.position + SphericalToCartesian(th, ph, length);
        bob.position = bobPos;

        if (rope != null)
        {
            rope.positionCount = 2;
            rope.SetPosition(0, pivot.position);
            rope.SetPosition(1, bobPos);
        }
    }
 
    private static Vector3 SphericalToCartesian(double theta, double phi, double l)
    {
        float s = (float)Math.Sin(theta);
        float c = (float)Math.Cos(theta);
        float cp = (float)Math.Cos(phi);
        float sp = (float)Math.Sin(phi);
        return new Vector3((float)l * s * cp, -(float)l * c, -(float)l * s * sp);
    }

    private void UpdateReadout()
    {
        outElapsedTime = elapsedTime;
        outSpeed       = SpeedMetersPerSecond;
        outSwings      = swingCount;
        outRevolutions = RevolutionCount;
        outThetaDeg    = CurrentThetaDegrees;
    } 

    public bool  IsRunning              => running;
    public float ElapsedTime            => elapsedTime;
    public int   SwingCount             => swingCount;
    public int   RevolutionCount        => (int)(Math.Abs(ph - ph0) / (2.0 * Math.PI));
    public float CurrentThetaDegrees    => (float)(th * Mathf.Rad2Deg);
    public float CurrentPhiDegrees      => (float)(ph * Mathf.Rad2Deg);
    public float SpeedMetersPerSecond
        => (float)(length * Math.Sqrt(thDot * thDot + Math.Sin(th) * Math.Sin(th) * phDot * phDot));
 
    public float MechanicalEnergy
    {
        get
        {
            double s = Math.Sin(th);
            double T = 0.5 * mass * length * length * (thDot * thDot + s * s * phDot * phDot);
            double V = -mass * gravity * length * Math.Cos(th);
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
