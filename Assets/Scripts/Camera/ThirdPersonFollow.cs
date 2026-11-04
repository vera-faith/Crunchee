using UnityEngine;

namespace Crunchee.CameraRig
{
    /// <summary>
    /// Third-person follow (Faith, Nov 3, replacing the spiral orbit cam):
    /// you play AS the bunny. The camera rides directly BEHIND her and turns
    /// with her facing, so you always see her back and where she's headed -
    /// never a side or front view from some other angle. The camera yaw
    /// chases her facing smoothly (she only turns while moving), and the
    /// position eases after it, so swings feel soft, not snapped.
    /// </summary>
    public class ThirdPersonFollow : MonoBehaviour
    {
        public Transform target;
        public float followDistance = 4.6f; // behind her
        public float followHeight = 2.6f;   // above her - over-the-shoulder height
        public float lookAhead = 1.8f;      // aim this far in front of her
        public float aimHeight = 0.9f;      // aim at her head height
        public float yawDamp = 2.8f;
        public float posDamp = 5.0f;

        float yaw;
        bool yawInit;

        void LateUpdate()
        {
            if (!target) return;
            if (!yawInit) { yaw = target.eulerAngles.y; yawInit = true; }
            yaw = Mathf.LerpAngle(yaw, target.eulerAngles.y, 1f - Mathf.Exp(-yawDamp * Time.deltaTime));
            var fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 desired = target.position - fwd * followDistance + Vector3.up * followHeight;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-posDamp * Time.deltaTime));
            transform.LookAt(target.position + fwd * lookAhead + Vector3.up * aimHeight);
        }
    }
}
