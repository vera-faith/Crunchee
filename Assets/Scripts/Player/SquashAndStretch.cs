using UnityEngine;

namespace Crunchee.Player
{
    /// <summary>Wobble-spring squash and stretch for the character model (child of the controller).</summary>
    public class SquashAndStretch : MonoBehaviour
    {
        public float landSquash = 0.35f;   // flatten amount on landing
        public float jumpStretch = 0.25f;  // elongation on jump
        Vector3 rest;
        float amount, velocity;

        void Awake() { rest = transform.localScale; }

        void Update()
        {
            float accel = -140f * amount - 10f * velocity;
            velocity += accel * Time.deltaTime;
            amount += velocity * Time.deltaTime;
            float y = 1f - amount;
            float xz = 1f + amount * 0.55f;
            transform.localScale = new Vector3(rest.x * xz, rest.y * y, rest.z * xz);
        }

        public void OnLand() { amount = landSquash; velocity = 0f; }
        public void OnJump() { amount = -jumpStretch; velocity = 0f; }
    }
}
