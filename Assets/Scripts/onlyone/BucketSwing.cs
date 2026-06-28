using UnityEngine;

/// <summary>
/// Secondary pendulum — the bucket sways from its handle with real inertia.
///
/// The bucket hangs from its handle, which sits at THIS transform's origin (the
/// suspension point moved each frame by the main SphericalPendulum). We model the
/// bucket's centre of mass as a point on a rigid rod of length 'comDistance' below
/// that point. The centre of mass lags behind when the handle accelerates (Verlet
/// inertia); gravity plus the rod constraint restore it toward "straight down".
/// We then rotate this transform so the bucket leans toward the lagging centre of
/// mass — producing a natural tilt/sway instead of an artificial "always face the
/// rope" orientation.
///
/// This is the reference study's idea (p.7): the tangential component of gravity
/// restores the body toward equilibrium (centre of mass directly below the pivot).
/// The same stable Verlet + constraint method as the rope is used for consistency.
///
/// SETUP: put this on a transform whose ORIGIN is the suspension point (the handle /
/// where the rope attaches), with the bucket mesh as a CHILD hanging below it.
/// </summary>
[DisallowMultipleComponent]
public sealed class BucketSwing : MonoBehaviour
{
    [Header("Physics")]
    [Tooltip("Distance from the handle (this origin) to the bucket's centre of mass (m).")]
    [SerializeField, Min(0.01f)] private float comDistance = 0.27f;

    [Tooltip("Gravity magnitude (m/s^2). Keep equal to the pendulum/rope gravity.")]
    [SerializeField] private float gravity = 9.81f;

    [Tooltip("Velocity retention per sub-step (0..1). Lower = settles faster; higher = sways longer.")]
    [SerializeField, Range(0.8f, 1f)] private float damping = 0.97f;

    [Tooltip("Maximum tilt from vertical (deg). Safety clamp so the bucket never flips.")]
    [SerializeField, Range(5f, 89f)] private float maxTilt = 60f;

    [Header("Integration")]
    [SerializeField, Min(0.0001f)] private float fixedStep = 0.004f;

    private Vector3 comPos;
    private Vector3 comPrev;
    private float accumulator;
    private bool ready;

    private void Start()
    {
        comPos = transform.position + Vector3.down * comDistance;
        comPrev = comPos;
        ready = true;
    }

    // Runs after the main SphericalPendulum has moved this transform (the handle) this frame.
    private void LateUpdate()
    {
        if (!ready) return;

        accumulator += Time.deltaTime;
        if (accumulator > 0.25f) accumulator = 0.25f;

        while (accumulator >= fixedStep)
        {
            Step(fixedStep);
            accumulator -= fixedStep;
        }

        ApplyRotation();
    }

    private void Step(float dt)
    {
        Vector3 anchor = transform.position; // suspension point (handle), moved by the main pendulum

        // Verlet integration of the centre of mass under gravity (inertia + lag).
        Vector3 cur = comPos;
        comPos = comPos + (comPos - comPrev) * damping + Vector3.down * (gravity * dt * dt);
        comPrev = cur;

        // Rigid-rod constraint: keep the com exactly 'comDistance' from the handle.
        Vector3 dir = comPos - anchor;
        float dist = dir.magnitude;
        dir = dist < 1e-6f ? Vector3.down : dir / dist;

        // Clamp the tilt so the bucket can never flip over.
        float tilt = Vector3.Angle(Vector3.down, dir);
        if (tilt > maxTilt)
            dir = Vector3.RotateTowards(Vector3.down, dir, maxTilt * Mathf.Deg2Rad, 0f);

        comPos = anchor + dir * comDistance;
    }

    private void ApplyRotation()
    {
        // The handle->com direction is the bucket's "down"; its up points the other way.
        Vector3 up = (transform.position - comPos).normalized;
        if (up.sqrMagnitude < 1e-8f) return;

        // Align this transform's up with the target up, preserving any yaw.
        Quaternion delta = Quaternion.FromToRotation(transform.up, up);
        transform.rotation = delta * transform.rotation;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(transform.position, 0.01f);                 // suspension point
        if (ready)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, comPos);             // the rod
            Gizmos.DrawSphere(comPos, 0.015f);                       // centre of mass
        }
    }
#endif
}
