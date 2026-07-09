
using UnityEngine;

public class FluidCameraController : MonoBehaviour
{
    [Header("Fly")]
    public float flySpeed           = 5f;
    public float flyLookSensitivity = 2f;
    public float flyScrollSpeedMult = 1.2f;
    public float flyBoostMultiplier = 3f;
    public float flySmoothing       = 8f;

    float   _flyYaw;
    float   _flyPitch;
    Vector3 _flyVelocity;
    bool    _prevRMB;

    void Start()
    {
        _flyYaw   = transform.eulerAngles.y;
        float rawPitch = transform.eulerAngles.x;
        _flyPitch = rawPitch > 180f ? rawPitch - 360f : rawPitch;
    }

    void Update()
    {
        bool  rmb    = Input.GetMouseButton(1);
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (rmb && !_prevRMB) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        if (!rmb && _prevRMB) { Cursor.lockState = CursorLockMode.None;   Cursor.visible = true;  }
        _prevRMB = rmb;

        if (rmb)
        {
            // Look
            _flyYaw   += Input.GetAxis("Mouse X") * flyLookSensitivity;
            _flyPitch -= Input.GetAxis("Mouse Y") * flyLookSensitivity;
            _flyPitch  = Mathf.Clamp(_flyPitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(_flyPitch, _flyYaw, 0f);

            // Move — WASD + Q/E
            float speed = flySpeed * (Input.GetKey(KeyCode.LeftShift) ? flyBoostMultiplier : 1f);
            Vector3 input = new Vector3(
                (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f),
                (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f),
                (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f));

            Vector3 target = transform.TransformDirection(input.normalized) * speed;
            _flyVelocity = Vector3.Lerp(_flyVelocity, target, flySmoothing * Time.deltaTime);
            transform.position += _flyVelocity * Time.deltaTime;
        }
        else
        {
            _flyVelocity = Vector3.Lerp(_flyVelocity, Vector3.zero, flySmoothing * Time.deltaTime);
        }

        if (Mathf.Abs(scroll) > 0.001f)
            flySpeed = Mathf.Clamp(
                flySpeed * (scroll > 0 ? flyScrollSpeedMult : 1f / flyScrollSpeedMult),
                0.1f, 500f);
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }
}