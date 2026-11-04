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
        public float moveSpeed = 3.6f;
        public float jumpForce = 9f;
        public float gravity = -22f;
        public float turnSpeed = 2.6f;   // rad/s cap on turning - a full about-face takes over a second, in a wide smooth arc
        public float inputSmooth = 4.5f; // the smoothed input chases the stick SLOWLY - long gentle acceleration + glide to a stop (Faith, Nov 3: "still too sensitive, has to be a lot smoother")
        public Transform cameraRig;

        CharacterController cc;
        Vector3 velocity;
        SquashAndStretch squash;
        Vector2 dragInput;
        Vector2 smoothInput; // dragInput eased over time, so starts/stops/direction changes ramp instead of jerking
        Squishy groundSquishy;
        bool wasGrounded;
        float distOnSquishy;
        Vector3 lastPos;
        Vector3 spawnPos;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            cc.stepOffset = 0.6f; // tall enough to walk up onto the next squishy in the climb
            squash = GetComponentInChildren<SquashAndStretch>();
            lastPos = transform.position;
            spawnPos = transform.position;
        }

        void Update()
        {
            ReadInput();
            Vector3 camF = cameraRig ? Vector3.ProjectOnPlane(cameraRig.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 camR = Vector3.Cross(Vector3.up, camF);
            // Ease the input: full stick no longer means instant full speed
            // or an instant direction flip (the "whip" Faith reported).
            smoothInput = Vector2.Lerp(smoothInput, dragInput, 1f - Mathf.Exp(-inputSmooth * Time.deltaTime));
            Vector3 move = (camF * smoothInput.y + camR * smoothInput.x) * moveSpeed;

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

            // Safety net: never fall forever. If she somehow leaves the
            // world, put her back at the start (Nov 3: the room floor had no
            // collider and she fell 41,720 units into the void).
            if (transform.position.y < -5f)
            {
                ReleaseSquishy();
                velocity = Vector3.zero;
                smoothInput = Vector2.zero;
                cc.enabled = false;
                transform.position = spawnPos;
                cc.enabled = true;
            }

            if (!jumping) UpdateSquishyContact();
            // Turn at a capped rate along the smoothed movement direction -
            // a flick of the stick becomes a smooth arc, and the camera
            // behind her swings gently instead of whipping around.
            if (move.sqrMagnitude > 0.04f)
                transform.forward = Vector3.RotateTowards(transform.forward, move.normalized, turnSpeed * Time.deltaTime, 0f);
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
                distOnSquishy = 0f;
                if (sq != null)
                {
                    sq.SetOccupied(true);
                    sq.OnLanded(Mathf.Abs(velocity.y), transform.position);
                    if (!wasGrounded) squash?.OnLand();
                }
            }

            // Footsteps on the squishy: every step cracks a little more wax -
            // visually (the trail connects) and audibly (squish + crackle).
            if (groundSquishy != null)
            {
                var p = transform.position;
                groundSquishy.TrackPlayer(p);
                distOnSquishy += new Vector2(p.x - lastPos.x, p.z - lastPos.z).magnitude;
                if (distOnSquishy > 0.42f)
                {
                    distOnSquishy = 0f;
                    groundSquishy.OnStep(p);
                }
            }
            lastPos = transform.position;

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
