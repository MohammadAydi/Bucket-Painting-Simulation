using UnityEngine;
 
[DisallowMultipleComponent]
public sealed class BucketSwing : MonoBehaviour
{
    [Header("Physics")]
    [SerializeField, Min(0.01f)] private float comDistance   = 0.14f;
    [SerializeField]             private float gravity       = 9.81f;
    [SerializeField, Range(0.8f, 1f)] private float damping = 0.985f;
    [SerializeField, Range(5f, 89f)]  private float maxTilt = 35f;

    [Header("Rotation Smoothing")]
    [Tooltip("كلما قل الرقم كلما كان الدوران أبطأ وأكثر واقعية")]
    [SerializeField, Range(1f, 20f)] private float rotationSpeed = 8f;

    [Header("Integration")]
    [SerializeField, Min(0.0001f)] private float fixedStep = 0.004f;

    private Vector3 comPos;
    private Vector3 comPrev;
    private Vector3 prevAnchor;
    private Quaternion smoothRotation;
    private float accumulator;
    private bool ready;

    private void Start()
    {
        comPos       = transform.position + Vector3.down * comDistance;
        comPrev      = comPos;
        prevAnchor   = transform.position;
        smoothRotation = transform.rotation;
        ready = true;
    }

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
        Vector3 anchor = transform.position;
  
        Vector3 velocity = (comPos - comPrev) * damping;
 
        float maxVel = comDistance * 2f;
        if (velocity.magnitude > maxVel)
            velocity = velocity.normalized * maxVel;

        Vector3 cur = comPos;
        comPos  = comPos + velocity + Vector3.down * (gravity * dt * dt);
        comPrev = cur;
 
        Vector3 dir  = comPos - anchor;
        float   dist = dir.magnitude;
        dir = dist < 1e-6f ? Vector3.down : dir / dist;
 
        float tilt = Vector3.Angle(Vector3.down, dir);
        if (tilt > maxTilt)
            dir = Vector3.RotateTowards(Vector3.down, dir, maxTilt * Mathf.Deg2Rad, 0f);

        Vector3 newComPos = anchor + dir * comDistance;
 
        comPrev += (newComPos - comPos);
        comPos   = newComPos; 

        prevAnchor = anchor;
    }

    private void ApplyRotation()
    { 
        Vector3 targetUp = (transform.position - comPos).normalized;
        if (targetUp.sqrMagnitude < 1e-8f) return;
 
        Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, targetUp);
 
        smoothRotation      = Quaternion.Slerp(smoothRotation, targetRot,
                                               Time.deltaTime * rotationSpeed); 
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(transform.position, 0.01f);
        if (!ready) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, comPos);
        Gizmos.DrawSphere(comPos, 0.015f);
    }
#endif
}