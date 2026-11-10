using UnityEngine;

namespace Crunchee.Game
{
    /// <summary>
    /// Parkour spinner (Nov 9): the squishy slowly turntables in place, so the
    /// bunny has to land on it, walk against the rotation, then hop off.
    /// PlayerController reads SurfaceVelocityAt to carry her along.
    /// </summary>
    public class SpinPlatform : MonoBehaviour
    {
        [Tooltip("Turntable speed; sign picks the direction.")]
        public float degreesPerSecond = 30f;

        void Update()
        {
            transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);
        }

        /// <summary>Tangential velocity of the spinning surface at a world-space contact point.</summary>
        public Vector3 SurfaceVelocityAt(Vector3 worldPos)
        {
            var local = transform.InverseTransformPoint(worldPos);
            local.y = 0f;
            // Positive Unity yaw swings +z toward +x, so the surface velocity
            // at a point is (local.z, 0, -local.x) * omega.
            var tangent = new Vector3(local.z, 0f, -local.x);
            return transform.TransformDirection(tangent) * (degreesPerSecond * Mathf.Deg2Rad);
        }
    }
}
