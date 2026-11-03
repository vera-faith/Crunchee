using UnityEngine;
using UnityEngine.EventSystems;

namespace Crunchee.UI
{
    /// <summary>Jump button: pointer down queues one jump.</summary>
    public class JumpButton : MonoBehaviour, IPointerDownHandler
    {
        public void OnPointerDown(PointerEventData e) => MobileControls.Instance?.QueueJump();
    }
}
