using UnityEngine;

namespace Crunchee.CameraRig
{
    /// <summary>
    /// Fixed-view follow (Faith, Nov 6: "the character can face any
    /// direction, but the surrounding and top view stays the same - match the
    /// video"). The camera hangs high behind the course and looks down the
    /// climb at a constant angle. It only slides to keep up with her; it never
    /// turns when she turns, so she can spin freely while the world stays put.
    /// </summary>
    public class ThirdPersonFollow : MonoBehaviour
    {
        public Transform target;
        [Tooltip("Course direction the view looks along (degrees around Y).")]
        public float fixedYaw = 0f;
        [Tooltip("Camera position relative to her feet, in course space.")]
        public Vector3 viewOffset = new Vector3(0f, 5.4f, -5.4f);
        [Tooltip("Point the camera aims at, relative to her feet, in course space.")]
        public Vector3 aimOffset = new Vector3(0f, 0.6f, 1.8f);
        public float posDamp = 6f;      // sideways / forward follow
        public float heightDamp = 3.5f; // softer vertical follow so jumps don't shake the view
        float punch;

        /// <summary>Impact nudge — camera hops back a beat on a crack (ASMR punch).</summary>
        public void Punch(float strength = 1f) => punch = Mathf.Max(punch, strength);

        public Quaternion ViewRotation
        {
            get
            {
                var yaw = Quaternion.Euler(0f, fixedYaw, 0f);
                return Quaternion.LookRotation(yaw * (aimOffset - viewOffset));
            }
        }

        public Vector3 DesiredPosition => target ? target.position + Quaternion.Euler(0f, fixedYaw, 0f) * viewOffset : transform.position;

        void LateUpdate()
        {
            if (!target) return;
            var want = DesiredPosition;
            var p = transform.position;
            float k = 1f - Mathf.Exp(-posDamp * Time.deltaTime);
            float ky = 1f - Mathf.Exp(-heightDamp * Time.deltaTime);
            transform.position = new Vector3(Mathf.Lerp(p.x, want.x, k), Mathf.Lerp(p.y, want.y, ky), Mathf.Lerp(p.z, want.z, k));
            if (punch > 0.001f)
            {
                transform.position += (-transform.forward * 0.28f - Vector3.up * 0.07f) * punch;
                punch = Mathf.MoveTowards(punch, 0f, Time.deltaTime * 5f);
            }
            transform.rotation = ViewRotation;
        }
    }
}
