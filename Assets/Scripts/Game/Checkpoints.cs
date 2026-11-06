using UnityEngine;

namespace Crunchee.Game
{
    /// <summary>Section checkpoint (Bakery: Counter / Pastry Case / Bread Shelves / Cake Room / Top Shelf).
    /// Falling below the kill height respawns the player at the highest checkpoint touched - never to zero.
    /// NOTE: FinishTrigger used to live in this file too - it was split into FinishTrigger.cs on Nov 3
    /// because Unity cannot load a serialized component whose class name does not match its file name.</summary>
    public class Checkpoint : MonoBehaviour
    {
        public static Checkpoint Current;
        public float killHeight = -6f;
        Transform player;

        void Start() { if (Current == null) Current = this; player = GameObject.FindGameObjectWithTag("Player")?.transform; }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player")) Current = this;
        }

        void Update()
        {
            if (player != null && Current != null && player.position.y < Current.transform.position.y + killHeight)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc) cc.enabled = false;
                player.position = Current.transform.position + Vector3.up * 1.2f;
                if (cc) cc.enabled = true;
            }
        }
    }
}
