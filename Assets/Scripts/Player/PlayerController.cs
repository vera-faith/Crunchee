using UnityEngine;
using Crunchee.Core;
using Crunchee.UI;

namespace Crunchee.Player
{
    /// <summary>
    /// M1 greybox controller: drag = move relative to camera, tap (quick touch / Space in editor) = jump.
    /// Landing on a Squishy triggers its pipeline and applies its bounce.
    /// Character model itself is a child object so SquashAndStretch can deform it freely.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public float moveSpeed = 5f;
        public float jumpForce = 9f;
        public float gravity = -22f;
        public Transform cameraRig;

        CharacterController cc;
        Vector3 velocity;
        SquashAndStretch squash;
        Vector2 dragInput;

        void Awake() { cc = GetComponent<CharacterController>(); squash = GetComponentInChildren<SquashAndStretch>(); }

        void Update()
        {
            ReadInput();
            Vector3 camF = cameraRig ? Vector3.ProjectOnPlane(cameraRig.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 camR = Vector3.Cross(Vector3.up, camF);
            Vector3 move = (camF * dragInput.y + camR * dragInput.x) * moveSpeed;

            bool grounded = cc.isGrounded;
            if (grounded && velocity.y < 0f) velocity.y = -2f;
            if (grounded && JumpPressed()) { velocity.y = jumpForce; squash?.OnJump(); }

            velocity.y += gravity * Time.deltaTime;
            Vector3 delta = (move + velocity) * Time.deltaTime;
            var flags = cc.Move(delta);

            if ((flags & CollisionFlags.Below) != 0 && velocity.y <= 0f) CheckSquishLanding();
            if (move.sqrMagnitude > 0.01f) transform.forward = Vector3.Slerp(transform.forward, move.normalized, 12f * Time.deltaTime);
        }

        void CheckSquishLanding()
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out var hit, 0.6f))
            {
                var s = hit.collider.GetComponent<Squishy>();
                if (s != null)
                {
                    velocity.y = s.OnStepped(Mathf.Abs(velocity.y));
                    squash?.OnLand();
                }
            }
        }

        bool jumpQueued;
        bool JumpPressed()
        {
            bool uiJump = MobileControls.Instance != null && MobileControls.Instance.ConsumeJump();
            bool j = jumpQueued || uiJump || Input.GetKeyDown(KeyCode.Space);
            jumpQueued = false; return j;
        }

        void ReadInput()
        {
            dragInput = Vector2.zero;
            // On-screen joystick wins when present (real device build)
            if (MobileControls.Instance != null && MobileControls.Instance.Move.sqrMagnitude > 0.001f)
            {
                dragInput = MobileControls.Instance.Move;
                return;
            }
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Moved) dragInput = t.deltaPosition.normalized;
                if (t.phase == TouchPhase.Began) jumpQueued = false;
                if (t.phase == TouchPhase.Ended && t.deltaPosition.magnitude < 2f) jumpQueued = true; // tap = jump
            }
            dragInput.x += Input.GetAxis("Horizontal");
            dragInput.y += Input.GetAxis("Vertical");
            dragInput = Vector2.ClampMagnitude(dragInput, 1f);
        }
    }
}
