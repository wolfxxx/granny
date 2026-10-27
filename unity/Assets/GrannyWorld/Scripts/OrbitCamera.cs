using UnityEngine;
using UnityEngine.InputSystem;

namespace GrannyWorld
{
    /// <summary>Third-person orbit camera with mouse/gamepad look, scroll zoom and wall collision.</summary>
    public class OrbitCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 targetOffset = new Vector3(0f, 1.35f, 0f);
        public float distance = 6.5f;
        public float minDistance = 2.5f;
        public float maxDistance = 14f;
        public float mouseSensitivity = 0.12f;
        public float stickSensitivity = 170f;
        public float minPitch = -35f;
        public float maxPitch = 75f;
        public float collisionRadius = 0.3f;
        public LayerMask collisionMask = ~(1 << 2);

        float yaw;
        float pitch = 14f;
        float currentDistance;
        Vector3 focus;
        float focusVelocityY;

        void Start()
        {
            if (target != null)
            {
                yaw = target.eulerAngles.y;
                focus = target.position + targetOffset;
            }
            currentDistance = distance;
            SetCursorLocked(true);
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;

            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse != null)
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    yaw += delta.x * mouseSensitivity;
                    pitch -= delta.y * mouseSensitivity;
                }
                else if (mouse.leftButton.wasPressedThisFrame)
                {
                    SetCursorLocked(true);
                }
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) distance = Mathf.Clamp(distance - Mathf.Sign(scroll) * 0.8f, minDistance, maxDistance);
            }
            if (kb != null && kb.escapeKey.wasPressedThisFrame) SetCursorLocked(false);

            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.rightStick.ReadValue();
                yaw += stick.x * stickSensitivity * dt;
                pitch -= stick.y * stickSensitivity * 0.7f * dt;
            }
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            // Follow tightly horizontally, softly vertically so jumps don't jerk the view.
            Vector3 desired = target.position + targetOffset;
            float k = 1f - Mathf.Exp(-18f * dt);
            focus.x = Mathf.Lerp(focus.x, desired.x, k);
            focus.z = Mathf.Lerp(focus.z, desired.z, k);
            focus.y = Mathf.SmoothDamp(focus.y, desired.y, ref focusVelocityY, 0.14f);

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 back = rotation * Vector3.back;
            float wanted = distance;
            if (Physics.SphereCast(focus, collisionRadius, back, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
                wanted = Mathf.Max(hit.distance - 0.05f, 0.6f);
            currentDistance = wanted < currentDistance ? wanted : Mathf.Lerp(currentDistance, wanted, 1f - Mathf.Exp(-5f * dt));

            transform.SetPositionAndRotation(focus + back * currentDistance, rotation);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
