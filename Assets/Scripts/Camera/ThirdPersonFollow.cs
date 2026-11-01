using UnityEngine;

namespace Crunchee.CameraRig
{
    /// <summary>Bird's-eye-ish follow (Faith, Oct 31): high angle (~55 degrees down) so the tops of the
    /// squishies on the path ahead read clearly, smooth follow behind the character.</summary>
    public class ThirdPersonFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 7.5f, -4.4f);
        public float followLerp = 6f;
        public float lookUpBias = 1.2f; // aim a little above the player so the climb ahead is visible

        void LateUpdate()
        {
            if (!target) return;
            Vector3 desired = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followLerp * Time.deltaTime));
            transform.LookAt(target.position + Vector3.up * lookUpBias);
        }
    }
}
