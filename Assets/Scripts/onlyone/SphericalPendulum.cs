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

    [Header("Environment (البيئة)")]
    [Tooltip("Gravitational acceleration g (m/s^2).")]
    [SerializeField] private float gravity = 9.81f;

    [Tooltip("Air density rho (kg/m^3). ~1.225 at sea level; humidity lowers it slightly.")]
    [SerializeField, Min(0f)] private float airDensity = 1.225f;

    [Tooltip("Pivot/rope friction f (1/s). Small constant damping at the suspension point.")]
    [SerializeField, Min(0f)] private float pivotFriction = 0.02f;

    [Header("Bucket / Bob (الدلو)")]
    [Tooltip("Bucket mass m (kg). Heavier = slower aerodynamic decay.")]
    [SerializeField, Min(0.001f)] private float mass = 3.0f;

    [Tooltip("Bucket radius (m). Frontal area used for drag = pi * r^2.")]
    [SerializeField, Min(0f)] private float bucketRadius = 0.12f;

    [Tooltip("Drag coefficient Cd (dimensionless). ~0.47 sphere, ~1.0 open bucket, ~1.05 cube.")]
    [SerializeField, Min(0f)] private float dragCoefficient = 1.0f;

    [Header("Integration")]
    [Tooltip("Fixed physics sub-step (s). Smaller = more accurate, more CPU.")]
    [SerializeField, Min(0.0001f)] private float fixedStep = 0.004f;
    
    private double th, ph, thDot, phDot;
    private double accumulator;  
    private bool   running;

    private const double MinSin = 1e-3;  

    private float FrontalArea => Mathf.PI * bucketRadius * bucketRadius;

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
                accumulator -= fixedStep;
            }
        }
        RenderPendulum(); 
    }

    // -------------------- Physics --------------------
 
    private void Derivatives(double theta, double thetaDot, double phiDot,
        out double dTheta, out double dThetaDot, out double dPhiDot)
    {
        double s = Math.Sin(theta);
        double c = Math.Cos(theta);
        double sSafe = Math.Abs(s) < MinSin ? (s < 0 ? -MinSin : MinSin) : s;
        double cot = c / sSafe;

        double gOverL = gravity / length;

        double speed = length * Math.Sqrt(thetaDot * thetaDot + s * s * phiDot * phiDot);
        double d     = (0.5 * airDensity * dragCoefficient * FrontalArea / mass) * speed;
        double damp  = d + pivotFriction;

        dTheta    = thetaDot;
        dThetaDot = phiDot * phiDot * s * c - gOverL * s - damp * thetaDot;
        dPhiDot   = -2.0 * thetaDot * phiDot * cot - damp * phiDot;
    }

    private void Integrate(double h)
    { 
        Derivatives(th,            thDot,            phDot,            out var a1, out var c1, out var d1);
        Derivatives(th + 0.5*h*a1, thDot + 0.5*h*c1, phDot + 0.5*h*d1, out var a2, out var c2, out var d2);
        Derivatives(th + 0.5*h*a2, thDot + 0.5*h*c2, phDot + 0.5*h*d2, out var a3, out var c3, out var d3);
        Derivatives(th + h*a3,     thDot + h*c3,      phDot + h*d3,      out var a4, out var c4, out var d4);
 
        double b1 = phDot;
        double b2 = phDot + 0.5*h*d1;
        double b3 = phDot + 0.5*h*d2;
        double b4 = phDot + h*d3;

        th    += h / 6.0 * (a1 + 2*a2 + 2*a3 + a4);
        ph    += h / 6.0 * (b1 + 2*b2 + 2*b3 + b4);
        thDot += h / 6.0 * (c1 + 2*c2 + 2*c3 + c4);
        phDot += h / 6.0 * (d1 + 2*d2 + 2*d3 + d4);
    }

    private void RenderPendulum()
    {
        bob.position= pivot.position + SphericalToCartesian(th, ph, length);
       
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
        float s = (float)Math.Sin(theta);
        float c = (float)Math.Cos(theta);
        float cp = (float)Math.Cos(phi);
        float sp = (float)Math.Sin(phi);
        return new Vector3((float)l * s * cp, -(float)l * c, -(float)l * s * sp);
    }
    
    public float SpeedMetersPerSecond
        => (float)(length * Math.Sqrt(thDot * thDot + Math.Sin(th) * Math.Sin(th) * phDot * phDot));
 
    public float MechanicalEnergy
    {
        get
        {
            double s = Math.Sin(th);
            double T = 0.5 * mass * length * length * (thDot * thDot + s * s * phDot * phDot);
            double v = -mass * gravity * length * Math.Cos(th);
            return (float)(T + v);
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
