using System;
using UnityEngine;

namespace onlyone
{
    [DisallowMultipleComponent]
    public class SphericalPendulum : MonoBehaviour
    {
        [Header("Scene References")] 
        [SerializeField] private Transform pivot;
 
        [SerializeField] private Transform bob;

        [Header("Suspension (التعليق)")]
        [Tooltip("Rope length l (meters).")]
        [SerializeField, Min(0.01f)] public float length = 1.2f;

        [Tooltip("يُطفئه PbdRope تلقائياً عند الاقتران، فيتوقّف النواس عن تحريك الدلو.")]
        public bool driveBucket = true;
 
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
        [SerializeField] private float gravity = 9.81f;
        [SerializeField, Min(0f)] private float airDensity = 1.225f;

        [Tooltip("Pivot/rope friction f (1/s). Small constant damping at the suspension point.")]
        [SerializeField, Min(0f)] private float pivotFriction = 0.02f;

        [Header("Bucket / Bob (الدلو)")]
        [SerializeField, Min(0.001f)] private float mass = 3.0f;

        [Tooltip("Bucket radius (m). Frontal area used for drag = pi * r^2.")]
        [SerializeField, Min(0f)] private float bucketRadius = 0.13f;

        [Tooltip("Drag coefficient Cd. ~0.47 sphere, ~1.0 open bucket, ~1.05 cube.")]
        [SerializeField, Min(0f)] private float dragCoefficient = 1.0f;

        [Header("Integration")]
        [SerializeField, Min(0.0001f)] private float fixedStep = 0.004f;

        private double th, ph, thDot, phDot;
        private double accumulator;
        private double effectiveLength;

        private const double MinSin = 1e-3;
        private float FrontalArea => Mathf.PI * bucketRadius * bucketRadius;

        public double Theta           => th;
        public double Phi             => ph;
        public double ThetaDot        => thDot;
        public double PhiDot          => phDot;
        public double EffectiveLength => effectiveLength;
        public float  Mass            => mass;

        public float StartThetaRad    => startTheta    * Mathf.Deg2Rad;
        public float StartPhiRad      => startPhi      * Mathf.Deg2Rad;
        public float StartThetaDotRad => startThetaDot * Mathf.Deg2Rad;
        public float StartPhiDotRad   => startPhiDot   * Mathf.Deg2Rad;

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
 
        public void ApplyConfig(RopeConfig c)
        {
            length        = c.length;
            startTheta    = c.startTheta;
            startPhi      = c.startPhi;
            startThetaDot = c.startThetaDot;
            startPhiDot   = c.startPhiDot;
            gravity       = c.gravity;
            airDensity    = c.airDensity;
            pivotFriction = c.pivotFriction;
            mass          = c.bucketMass;          
            bucketRadius  = c.bucketRadius;         
            dragCoefficient = c.bucketDragCoefficient;
            fixedStep     = c.fixedStep;
            Relaunch();
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
        }

        private void Update()
        {
            if (externallyDriven) return;

            accumulator += Time.deltaTime;
            if (accumulator > 0.25) accumulator = 0.25;
            while (accumulator >= fixedStep)
            {
                Integrate(fixedStep);
                accumulator -= fixedStep;
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
            Derivatives(th,              thDot,              phDot,
                out double k1Th, out double k1Thd, out double k1Phd);
            double k1Ph = phDot;

            Derivatives(th + 0.5*h*k1Th, thDot + 0.5*h*k1Thd, phDot + 0.5*h*k1Phd,
                out double k2Th, out double k2Thd, out double k2Phd);
            double k2Ph = phDot + 0.5*h*k1Phd;

            Derivatives(th + 0.5*h*k2Th, thDot + 0.5*h*k2Thd, phDot + 0.5*h*k2Phd,
                out double k3Th, out double k3Thd, out double k3Phd);
            double k3Ph = phDot + 0.5*h*k2Phd;

            Derivatives(th + h*k3Th,     thDot + h*k3Thd,     phDot + h*k3Phd,
                out double k4Th, out double k4Thd, out double k4Phd);
            double k4Ph = phDot + h*k3Phd;

            th    += h / 6.0 * (k1Th  + 2*k2Th  + 2*k3Th  + k4Th);
            thDot += h / 6.0 * (k1Thd + 2*k2Thd + 2*k3Thd + k4Thd);
            ph    += h / 6.0 * (k1Ph  + 2*k2Ph  + 2*k3Ph  + k4Ph);
            phDot += h / 6.0 * (k1Phd + 2*k2Phd + 2*k3Phd + k4Phd);
        }

        // ── Feedback: rope -> pendulum (called by PbdRope every physics step) ────
        public void SetStateFromWorld(double theta, double phi,
            double thetaDot, double phiDot, double effLen)
        {
            th = theta;       ph = phi;
            thDot = thetaDot; phDot = phiDot;
            effectiveLength = effLen;
        }

        private void RenderPendulum()
        {
            if (!driveBucket) return;
            bob.position = pivot.position + SphericalToCartesian(th, ph, length);
        }

        private static Vector3 SphericalToCartesian(double theta, double phi, double l)
        {
            float s  = (float)Math.Sin(theta);
            float c  = (float)Math.Cos(theta);
            float cp = (float)Math.Cos(phi);
            float sp = (float)Math.Sin(phi);
            return new Vector3((float)l * s * cp, -(float)l * c, -(float)l * s * sp);
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
}
