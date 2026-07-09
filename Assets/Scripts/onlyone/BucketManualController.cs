using UnityEngine;

namespace onlyone
{
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

        [Header("Keyboard Movement")]
        [Tooltip("Arrow keys للأفقي والعمق، PageUp/PageDown للارتفاع.")]
        [SerializeField, Min(0.05f)] private float keyboardSpeed = 1.5f;

        private bool    dragging;
        private Vector3 currentTarget;
        private float   pickedDistanceFromCamera;

        public bool IsManualModeActive => rope != null && rope.IsManualMode;
        public bool UsesKeyboardInput  { get => inputMode == ManualInputMode.Keyboard; set => inputMode = value ? ManualInputMode.Keyboard : ManualInputMode.Mouse; }

        public void ForceExitManualMode()
        {
            if (rope != null && rope.IsManualMode)
            {
                rope.ExitManualMode();
                dragging = false;
            }
        }

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
                case ManualInputMode.Mouse:    UpdateMouseDrag();    break;
                case ManualInputMode.Keyboard: UpdateKeyboardMove(); break;
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
                if (!rope.IsManualMode) return;

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
                currentTarget = Vector3.Lerp(
                    currentTarget, desired,
                    1f - Mathf.Exp(-mouseFollowSharpness * Time.deltaTime));
            }

            rope.SetManualBucketPosition(currentTarget);
        }

        private void UpdateKeyboardMove()
        {
           
            Vector3 move = new Vector3(
                (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow)  ? 1f : 0f),
                (Input.GetKey(KeyCode.PageUp)     ? 1f : 0f) - (Input.GetKey(KeyCode.PageDown)   ? 1f : 0f),
                (Input.GetKey(KeyCode.UpArrow)    ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow)  ? 1f : 0f));

            if (move.sqrMagnitude > 1e-6f)
                currentTarget += move.normalized * (keyboardSpeed * Time.deltaTime);

            rope.SetManualBucketPosition(currentTarget);
        }
    }
}