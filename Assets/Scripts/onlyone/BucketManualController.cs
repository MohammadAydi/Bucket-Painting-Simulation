using UnityEngine;

namespace onlyone
{
    /// <summary>
    /// User-facing input layer for the Manual Manipulation Mode.
    ///
    /// This class owns NO physics state whatsoever. It only reads input
    /// (a toggle key, plus either mouse picking or keyboard movement) and
    /// forwards a desired world-space target to <see cref="PbdRope"/>,
    /// which remains the sole authority over the rope's physical state
    /// (rest length, current pose, torsion frame, etc.).
    ///
    /// Under the hood, PbdRope no longer freezes anything while this mode
    /// is active: the bucket becomes a kinematic anchor (invMass = 0) and
    /// every other node stays fully dynamic, so the XPBD/DER solver keeps
    /// producing real sag/waves/bending as the target below changes. This
    /// controller never needs to know that — it just calls
    /// SetManualBucketPosition once per frame with the desired world point,
    /// and PbdRope takes care of clamping it to the valid
    /// [minimumManualDistance, ropeLength] shell and forcing it onto the
    /// solver every substep.
    ///
    /// WHY A NEW CLASS:
    /// Mouse picking does not exist anywhere in the current project, and
    /// input handling is a different concern from solving physics.
    /// PbdRope currently contains zero Input.* calls — it is purely a
    /// solver (XPBD/DER + coupling), exactly as SphericalPendulum is
    /// purely an RK4 integrator and RopeConfigLoader is purely a JSON
    /// loader. Adding Input.* calls directly into PbdRope would blur that
    /// existing separation of concerns. Keeping the input layer external
    /// also means it can be disabled, replaced, or driven by a different
    /// input system (e.g. the XR/VR controller already used elsewhere in
    /// the project) without touching the solver at all.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BucketManualController : MonoBehaviour
    {
        private enum ManualInputMode { Mouse, Keyboard }

        [Header("Target")]
        [SerializeField] private PbdRope rope;
        [Tooltip("الكاميرا المستخدمة للـ Mouse Picking. تُترك فارغة لاستخدام Camera.main تلقائياً.")]
        [SerializeField] private Camera pickCamera;

        [Header("Toggle")]
        [Tooltip("مفتاح التجميد/الاستئناف لوضع التحكم اليدوي.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F;

        [Header("Input Mode")]
        [SerializeField] private ManualInputMode inputMode = ManualInputMode.Mouse;

        [Header("Mouse Picking")]
        [Tooltip("طبقة اختيارية لتقييد الـ Raycast على كائن الدلو فقط. الدلو يحتاج Collider ليُلتقط.")]
        [SerializeField] private LayerMask pickMask = ~0;
        [Tooltip("سلاسة تتبّع الفأرة أثناء السحب (كلما زادت قلّ التخلف).")]
        [SerializeField, Range(1f, 60f)] private float mouseFollowSharpness = 20f;
        [SerializeField, Min(1f)] private float maxPickDistance = 100f;

        [Header("Keyboard Movement (بديل عن الفأرة)")]
        [Tooltip("WASD/Arrows للأفقي والعمق، Q/E للارتفاع.")]
        [SerializeField, Min(0.05f)] private float keyboardSpeed = 1.5f;

        private bool    dragging;
        private Vector3 currentTarget;
        private float   pickedDistanceFromCamera;

        private void Reset()
        {
            rope = GetComponent<PbdRope>();
            pickCamera = Camera.main;
        }

        private void Update()
        {
            if (!rope) return;
            if (!pickCamera) pickCamera = Camera.main;

            if (Input.GetKeyDown(toggleKey))
                ToggleManualMode();

            if (!rope.IsManualMode) return;

            switch (inputMode)
            {
                case ManualInputMode.Mouse:
                    UpdateMouseDrag();
                    break;
                case ManualInputMode.Keyboard:
                    UpdateKeyboardMove();
                    break;
            }
        }

        private void ToggleManualMode()
        {
            if (rope.IsManualMode)
            {
                rope.ExitManualMode();
                dragging = false;
            }
            else
            {
                rope.EnterManualMode();
                if (!rope.IsManualMode) return; // rope rejected the request (see its own guards/logs).

                // Start from the bucket's CURRENT position so the very
                // first control input never causes a jump.
                currentTarget = rope.CurrentBucketPosition;
                if (pickCamera)
                    pickedDistanceFromCamera = Vector3.Distance(pickCamera.transform.position, currentTarget);
            }
        }

        private void UpdateMouseDrag()
        {
            if (!pickCamera) return;

            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = pickCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, maxPickDistance, pickMask))
                {
                    dragging = true;
                    pickedDistanceFromCamera = Vector3.Distance(pickCamera.transform.position, hit.point);
                    currentTarget = hit.point;
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                dragging = false;
            }

            if (dragging)
            {
                Ray ray = pickCamera.ScreenPointToRay(Input.mousePosition);
                Vector3 desired = ray.GetPoint(pickedDistanceFromCamera);

                // Exponential smoothing for a natural "held by hand" feel,
                // independent of frame rate.
                currentTarget = Vector3.Lerp(
                    currentTarget, desired,
                    1f - Mathf.Exp(-mouseFollowSharpness * Time.deltaTime));
            }

            rope.SetManualBucketPosition(currentTarget);
        }

        private void UpdateKeyboardMove()
        {
            Vector3 move = new Vector3(
                Input.GetAxisRaw("Horizontal"),
                (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f),
                Input.GetAxisRaw("Vertical"));

            if (move.sqrMagnitude > 1e-6f)
                currentTarget += move.normalized * (keyboardSpeed * Time.deltaTime);

            rope.SetManualBucketPosition(currentTarget);
        }
    }
}
