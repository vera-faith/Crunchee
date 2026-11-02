using UnityEngine;
using Crunchee.Core;
using Crunchee.UI;

namespace Crunchee.Player
{
    /// <summary>
    /// Faith's traversal model (from the reference videos): the character WALKS
    /// from squishy to squishy. First touch on a squishy = its one landing
    /// (crack + sound), then the character rides it down as it slowly sinks -
    /// no bounce, no repeated jumping on items. The JUMP button still exists
    /// for gaps and fun hops, it just isn't how squishies are farmed.
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
        Squishy groundSquishy;
        bool wasGrounded;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            cc.stepOffset = 0.55f; // tall enough to walk up onto the next squishy in the climb
            squash = GetComponentInChildren<SquashAndStretch>();
        }

        void Update()
        {
            ReadInput();
            Vector3 camF = cameraRig ? Vector3.ProjectOnPlane(cameraRig.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 camR = Vector3.Cross(Vector3.up, camF);
            Vector3 move = (camF * dragInput.y + camR * dragInput.x) * moveSpeed;

            bool grounded = cc.isGrounded;
            if (grounded && velocity.y < 0f) velocity.y = -2f;
            bool jumping = false;
            if (grounded && JumpPressed())
            {
                velocity.y = jumpForce; squash?.OnJump(); jumping = true;
                ReleaseSquishy();
            }

            velocity.y += gravity * Time.deltaTime;
            cc.Move((move + velocity) * Time.deltaTime);

            if (!jumping) UpdateSquishyContact();
            if (move.sqrMagnitude > 0.01f) transform.forward = Vector3.Slerp(transform.forward, move.normalized, 12f * Time.deltaTime);
            wasGrounded = cc.isGrounded;
        }

        void ReleaseSquishy()
        {
            if (groundSquishy != null) { groundSquishy.SetOccupied(false); groundSquishy = null; }
        }

        void UpdateSquishyContact()
        {
            Squishy sq = null;
            if (cc.isGrounded && velocity.y <= 0.5f &&
                Physics.Raycast(transform.position + Vector3.up * 0.25f, Vector3.down, out var hit, 0.85f))
                sq = hit.collider.GetComponent<Squishy>();

            if (sq != groundSquishy)
            {
                ReleaseSquishy();
                groundSquishy = sq;
                if (sq != null)
                {
                    sq.SetOccupied(true);
                    sq.OnLanded(Mathf.Abs(velocity.y));
                    if (!wasGrounded) squash?.OnLand();
                }
            }

            // Ride the surface: as the squishy sinks (or recovers), stay glued to its top.
            if (groundSquishy != null && cc.isGrounded)
            {
                float delta = groundSquishy.TopY - transform.position.y;
                if (Mathf.Abs(delta) > 0.001f && Mathf.Abs(delta) < 0.6f)
                    cc.Move(new Vector3(0f, delta, 0f));
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
