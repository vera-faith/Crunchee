using UnityEngine;

namespace Crunchee.Player
{
    /// <summary>
    /// BunnyBody (Faith, Nov 3-5): the cute-movement layer for the Pudding
    /// Bunny. The builder hangs her ears, arms and legs off pivots; this
    /// script animates them:
    ///  - walking: the tiny legs swing in a step cycle, arms counter-swing,
    ///    the whole body bobs and sways, ears bounce a beat behind the steps
    ///  - jumping: arms fly UP, ears flop back on the rise and float on the
    ///    fall, then overshoot-flop when she lands
    ///  - idle: gentle breathing and micro ear sway
    /// Everything runs on springy targets so the motion stays soft and
    /// squishy, never robotic. It animates Model/BodyBob and the pivots
    /// only - SquashAndStretch owns Model's scale, PlayerController owns
    /// the movement itself.
    /// </summary>
    public class BunnyBody : MonoBehaviour
    {
        Transform bob, earL, earR, armL, armR, legL, legR;
        CharacterController cc;

        float walkPhase;
        float earPitch, earPitchVel;   // ear flop forward/back (spring, degrees)
        float earRoll, earRollVel;     // ear side-sway offset (spring, degrees)
        float armRaise, armRaiseVel;   // 0 = arms down, 1 = arms up (spring)
        bool wasGrounded = true;
        float lastAirVy;
        float breath;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            var model = transform.Find("Model");
            bob = model != null ? model.Find("BodyBob") : null;
            if (bob == null) { enabled = false; return; }
            earL = bob.Find("EarPivotL"); earR = bob.Find("EarPivotR");
            armL = bob.Find("ArmPivotL"); armR = bob.Find("ArmPivotR");
            legL = bob.Find("LegPivotL"); legR = bob.Find("LegPivotR");
        }

        static float Spring(float current, ref float vel, float target, float stiffness, float damping, float dt)
        {
            float accel = (target - current) * stiffness - vel * damping;
            vel += accel * dt;
            return current + vel * dt;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 v = cc.velocity;
            float planar = new Vector2(v.x, v.z).magnitude;
            bool grounded = cc.isGrounded;
            breath += dt;

            // --- Air transitions: the flop impulses ---
            if (wasGrounded && !grounded && v.y > 2.5f)
            {
                // Jump! Her head shoots up and the soft ears trail BACK
                earPitchVel += 260f;
                armRaiseVel += 6f;
            }
            if (!wasGrounded && grounded)
            {
                // Landing: she stops, the ears keep coming and flop FORWARD,
                // scaled by how hard she fell
                earPitchVel -= Mathf.Clamp(-lastAirVy * 22f, 60f, 320f);
                earRollVel += Random.Range(-40f, 40f);
            }
            if (!grounded) lastAirVy = v.y;
            wasGrounded = grounded;

            // --- Targets per state ---
            float earPitchTarget, armRaiseTarget;
            if (!grounded)
            {
                earPitchTarget = v.y > 0.5f ? 34f : -18f;    // rise: flop back; fall: float up-forward
                armRaiseTarget = v.y > -2f ? 1f : 0.75f;     // arms up!
            }
            else if (planar > 0.4f)
            {
                earPitchTarget = Mathf.Sin(walkPhase - 1.1f) * 9f;  // bounce a beat behind the steps
                armRaiseTarget = 0f;
            }
            else
            {
                earPitchTarget = Mathf.Sin(breath * 1.7f) * 2.5f;   // idle micro sway
                armRaiseTarget = 0f;
            }

            // --- Springs (soft + squishy, slightly underdamped) ---
            earPitch = Spring(earPitch, ref earPitchVel, earPitchTarget, 60f, 8f, dt);
            float earRollTarget = grounded && planar > 0.4f ? Mathf.Sin(walkPhase - 1.4f) * 5f : 0f;
            earRoll = Spring(earRoll, ref earRollVel, earRollTarget, 55f, 8f, dt);
            armRaise = Spring(armRaise, ref armRaiseVel, armRaiseTarget, 90f, 11f, dt);
            armRaise = Mathf.Clamp(armRaise, -0.15f, 1.2f);

            // --- Walk cycle ---
            float legSwing = 0f, armSwing = 0f, bobY = 0f;
            if (grounded && planar > 0.4f)
            {
                walkPhase += dt * planar * 2.6f;
                legSwing = Mathf.Sin(walkPhase) * 34f;
                armSwing = Mathf.Sin(walkPhase) * 14f;
                bobY = Mathf.Abs(Mathf.Cos(walkPhase)) * 0.05f;
            }
            else
            {
                walkPhase = Mathf.Lerp(walkPhase, Mathf.Round(walkPhase / Mathf.PI) * Mathf.PI, dt * 10f);
            }
            float legTargetL = grounded ? legSwing : 12f;   // airborne: legs trail slightly
            float legTargetR = grounded ? -legSwing : 12f;

            // --- Apply ---
            if (earL != null) earL.localRotation = Quaternion.Euler(earPitch, 0f, -14f + earRoll);
            if (earR != null) earR.localRotation = Quaternion.Euler(earPitch, 0f, 14f - earRoll);
            if (armL != null) armL.localRotation = Quaternion.Euler(-armSwing - armRaise * 22f, 0f, -10f - armRaise * 150f);
            if (armR != null) armR.localRotation = Quaternion.Euler(armSwing - armRaise * 22f, 0f, 10f + armRaise * 150f);
            if (legL != null) legL.localRotation = Quaternion.Euler(legTargetL, 0f, 0f);
            if (legR != null) legR.localRotation = Quaternion.Euler(legTargetR, 0f, 0f);
            if (bob != null)
            {
                float breatheY = grounded && planar <= 0.4f ? Mathf.Sin(breath * 1.8f) * 0.008f : 0f;
                bob.localPosition = new Vector3(0f, bobY + breatheY, 0f);
                float lean = grounded ? planar * 1.1f : 2f;          // eager little forward lean
                float sway = grounded && planar > 0.4f ? Mathf.Sin(walkPhase) * 2.2f : 0f;
                bob.localRotation = Quaternion.Euler(lean, 0f, sway);
                float br = grounded && planar <= 0.4f ? 1f + Mathf.Sin(breath * 1.8f) * 0.012f : 1f;
                bob.localScale = new Vector3(1f, br, 1f);
            }
        }
    }
}
