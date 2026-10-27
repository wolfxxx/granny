using UnityEngine;
using UnityEngine.InputSystem;

namespace GrannyWorld
{
    /// <summary>
    /// Third-person platformer controller: camera-relative movement, walk/run/sprint,
    /// coyote time, jump buffering, variable jump height, double jump with a front flip,
    /// bounce pads and respawn when Granny falls into the sea.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class GrannyController : MonoBehaviour
    {
        [Header("References")]
        public Transform cameraTransform;
        public Animator animator;
        [Tooltip("Pivot at body centre that is rotated for the double-jump flip.")]
        public Transform flipPivot;

        [Header("Movement")]
        public float walkSpeed = 2.2f;
        public float runSpeed = 5.5f;
        public float sprintSpeed = 8.5f;
        public float groundAcceleration = 35f;
        public float airAcceleration = 14f;
        public float turnSharpness = 14f;

        [Header("Jumping")]
        public float jumpHeight = 2.1f;
        public float doubleJumpHeight = 1.8f;
        public float gravity = -28f;
        public float fallGravityMultiplier = 1.6f;
        public float shortHopGravityMultiplier = 2.2f;
        public float maxFallSpeed = 45f;
        public float coyoteTime = 0.12f;
        public float jumpBufferTime = 0.15f;
        public float flipDuration = 0.55f;

        [Header("World")]
        [Tooltip("Granny can't swim: below this height she is put back on dry land.")]
        public float drownHeight = 4.6f;

        CharacterController controller;
        Vector3 planarVelocity;
        float verticalVelocity;
        float lastGroundedTime = -10f;
        float lastJumpPressedTime = -10f;
        bool canDoubleJump;
        bool ignoreShortHop;
        bool wasGrounded = true;
        float airTime;
        float flipTimer = -1f;
        Vector3 lastSafePosition;
        float safeTimer;
        ParticleSystem runDust;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
        static readonly int JumpId = Animator.StringToHash("Jump");
        static readonly int DoubleJumpId = Animator.StringToHash("DoubleJump");

        public bool IsGrounded => controller != null && controller.isGrounded;
        public Vector3 Velocity => planarVelocity + Vector3.up * verticalVelocity;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        void Start()
        {
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            lastSafePosition = transform.position;
            runDust = Fx.CreateRunDust(transform);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool grounded = controller.isGrounded;
            if (grounded)
            {
                lastGroundedTime = Time.time;
                canDoubleJump = true;
            }

            bool jumpPressed = JumpPressedThisFrame();
            if (jumpPressed) lastJumpPressedTime = Time.time;

            // --- Horizontal movement relative to the camera
            Vector2 input = ReadMove();
            Vector3 forward = Vector3.forward;
            if (cameraTransform != null)
            {
                forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
                if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(cameraTransform.up, Vector3.up);
                forward.Normalize();
            }
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 wish = forward * input.y + right * input.x;
            float inputMagnitude = Mathf.Clamp01(wish.magnitude);

            float targetSpeed = WalkHeld() ? walkSpeed : SprintHeld() ? sprintSpeed : runSpeed;
            Vector3 targetVelocity = inputMagnitude > 0.01f ? wish.normalized * (targetSpeed * inputMagnitude) : Vector3.zero;
            float accel = grounded ? groundAcceleration : airAcceleration;
            planarVelocity = Vector3.MoveTowards(planarVelocity, targetVelocity, accel * dt);

            if (inputMagnitude > 0.05f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(wish.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-turnSharpness * dt));
            }

            // --- Jumping
            bool jumpBuffered = Time.time - lastJumpPressedTime <= jumpBufferTime;
            bool inCoyoteWindow = Time.time - lastGroundedTime <= coyoteTime;
            if (jumpBuffered && inCoyoteWindow)
            {
                Launch(jumpHeight);
                lastGroundedTime = -10f;
                lastJumpPressedTime = -10f;
                animator?.SetTrigger(JumpId);
                GameAudio.Play(ProceduralAudio.Jump, 0.55f, 0.08f);
                Fx.Dust(transform.position, 10);
            }
            else if (jumpPressed && !grounded && canDoubleJump)
            {
                canDoubleJump = false;
                Launch(doubleJumpHeight);
                lastJumpPressedTime = -10f;
                flipTimer = 0f;
                animator?.SetTrigger(DoubleJumpId);
                GameAudio.Play(ProceduralAudio.DoubleJump, 0.5f, 0.05f);
                Fx.Sparkles(transform.position + Vector3.up * 0.6f, new Color(1f, 0.85f, 0.4f), 18);
            }

            // --- Gravity
            if (grounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f; // keep the controller snapped to slopes
                ignoreShortHop = false;
            }
            else
            {
                float g = gravity;
                if (verticalVelocity < 0f) { g *= fallGravityMultiplier; ignoreShortHop = false; }
                else if (!JumpHeld() && !ignoreShortHop) g *= shortHopGravityMultiplier;
                verticalVelocity = Mathf.Max(verticalVelocity + g * dt, -maxFallSpeed);
            }

            // --- Move
            CollisionFlags flags = controller.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
            grounded = controller.isGrounded;

            // Lose horizontal speed when running into walls so the animation calms down.
            Vector3 actual = controller.velocity; actual.y = 0f;
            if (grounded && actual.sqrMagnitude < planarVelocity.sqrMagnitude * 0.25f && (flags & CollisionFlags.Sides) != 0)
                planarVelocity = Vector3.Lerp(planarVelocity, actual, 0.5f);

            // --- Landing
            if (grounded && !wasGrounded)
            {
                if (airTime > 0.35f)
                {
                    Fx.Dust(transform.position, Mathf.RoundToInt(Mathf.Lerp(8, 22, Mathf.InverseLerp(0.35f, 1.5f, airTime))));
                    GameAudio.Play(ProceduralAudio.Land, Mathf.Lerp(0.25f, 0.6f, airTime), 0.1f);
                }
                flipTimer = -1f;
            }
            airTime = grounded ? 0f : airTime + dt;
            wasGrounded = grounded;

            UpdateFlip(dt);
            UpdateSafety(grounded, dt);
            UpdateRunDust(grounded);

            if (animator != null)
            {
                animator.SetFloat(SpeedId, new Vector3(planarVelocity.x, 0, planarVelocity.z).magnitude, 0.08f, dt);
                animator.SetBool(GroundedId, grounded);
                animator.SetFloat(VerticalSpeedId, verticalVelocity);
            }
        }

        void Launch(float height)
        {
            verticalVelocity = Mathf.Sqrt(2f * -gravity * height);
            ignoreShortHop = false;
        }

        /// <summary>Called by bounce pads. Launches Granny regardless of the jump button.</summary>
        public void Bounce(float height)
        {
            Launch(height);
            ignoreShortHop = true;
            canDoubleJump = true;
            lastGroundedTime = -10f;
            flipTimer = -1f;
            animator?.SetTrigger(JumpId);
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y < 0.5f || verticalVelocity > 0.5f) return;
            var pad = hit.collider.GetComponentInParent<BouncePad>();
            if (pad != null) pad.Launch(this);
        }

        void UpdateFlip(float dt)
        {
            if (flipPivot == null) return;
            if (flipTimer < 0f)
            {
                flipPivot.localRotation = Quaternion.Slerp(flipPivot.localRotation, Quaternion.identity, 1f - Mathf.Exp(-20f * dt));
                return;
            }
            flipTimer += dt;
            float t = Mathf.Clamp01(flipTimer / flipDuration);
            float eased = t * t * (3f - 2f * t);
            flipPivot.localRotation = Quaternion.AngleAxis(360f * eased, Vector3.right);
            if (t >= 1f) flipTimer = -1f;
        }

        void UpdateSafety(bool grounded, float dt)
        {
            safeTimer += dt;
            if (grounded && safeTimer > 0.4f && transform.position.y > drownHeight + 1.2f && !IsOnBouncePad())
            {
                lastSafePosition = transform.position;
                safeTimer = 0f;
            }
            if (transform.position.y < drownHeight)
            {
                Fx.Splash(new Vector3(transform.position.x, drownHeight + 1.4f, transform.position.z));
                GameAudio.Play(ProceduralAudio.Splash, 0.6f, 0.05f);
                Teleport(lastSafePosition + Vector3.up * 0.5f);
            }
        }

        bool IsOnBouncePad()
        {
            return Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 0.6f, ~0, QueryTriggerInteraction.Ignore)
                   && hit.collider.GetComponentInParent<BouncePad>() != null;
        }

        public void Teleport(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            flipTimer = -1f;
        }

        void UpdateRunDust(bool grounded)
        {
            if (runDust == null) return;
            var emission = runDust.emission;
            float speed = planarVelocity.magnitude;
            emission.rateOverTime = grounded && speed > runSpeed + 0.5f ? 18f : grounded && speed > runSpeed - 0.5f ? 5f : 0f;
        }

        // --- Input (new Input System, keyboard + gamepad)
        static Vector2 ReadMove()
        {
            Vector2 v = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > v.sqrMagnitude) v = stick;
            }
            return Vector2.ClampMagnitude(v, 1f);
        }

        static bool JumpPressedThisFrame()
        {
            var kb = Keyboard.current; var pad = Gamepad.current;
            return (kb != null && kb.spaceKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
        }

        static bool JumpHeld()
        {
            var kb = Keyboard.current; var pad = Gamepad.current;
            return (kb != null && kb.spaceKey.isPressed) || (pad != null && pad.buttonSouth.isPressed);
        }

        static bool SprintHeld()
        {
            var kb = Keyboard.current; var pad = Gamepad.current;
            return (kb != null && kb.leftShiftKey.isPressed) || (pad != null && (pad.leftStickButton.isPressed || pad.rightTrigger.isPressed));
        }

        static bool WalkHeld()
        {
            var kb = Keyboard.current;
            return kb != null && (kb.leftCtrlKey.isPressed || kb.leftAltKey.isPressed);
        }
    }
}
