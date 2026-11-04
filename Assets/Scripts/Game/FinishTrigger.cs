using UnityEngine;

namespace Crunchee.Game
{
    /// <summary>Top-of-tower finish (Bakery: the giant layer cake). Fires OnFinish when the player enters.
    /// Lives in its own file: Unity only loads a serialized MonoBehaviour reliably when the class name matches the file name.</summary>
    public class FinishTrigger : MonoBehaviour
    {
        public System.Action OnFinish;
        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player")) OnFinish?.Invoke();
        }
    }
}
