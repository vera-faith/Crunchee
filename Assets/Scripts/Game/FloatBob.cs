using UnityEngine;

namespace Crunchee.Game
{
    /// <summary>Slow dreamy bob + spin for the floating candy props.</summary>
    public class FloatBob : MonoBehaviour
    {
        public float amplitude = 0.25f;
        public float speed = 0.6f;
        public float spin = 12f;
        public float phase;

        Vector3 home;

        void Start() { home = transform.localPosition; }

        void Update()
        {
            float t = Time.time * speed + phase;
            transform.localPosition = home + new Vector3(0f, Mathf.Sin(t) * amplitude, 0f);
            transform.Rotate(0f, spin * Time.deltaTime, 0f, Space.World);
        }
    }
}
