using UnityEngine;
using UnityEngine.EventSystems;

namespace Crunchee.UI
{
    /// <summary>
    /// On-screen controls exactly as mocked up (Faith, Oct 31):
    /// virtual joystick bottom-left with a "MOVE" label pill below it,
    /// big JUMP button bottom-right with the arrow centered in the circle
    /// and the word "JUMP" in a label pill BELOW the button.
    /// The ButterPathBuilder creates the Canvas objects and wires these.
    /// </summary>
    public class MobileControls : MonoBehaviour
    {
        public static MobileControls Instance;

        public Vector2 Move { get; private set; }
        bool jumpQueued;
        public bool ConsumeJump() { bool j = jumpQueued; jumpQueued = false; return j; }
        public void QueueJump() => jumpQueued = true;
        public void SetMove(Vector2 v) => Move = v;

        void Awake() => Instance = this;
    }

    /// <summary>Drag joystick: knob follows the finger within the base radius.</summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform baseRect, knobRect;
        float radius;

        void Start() => radius = baseRect.sizeDelta.x * 0.5f;

        public void OnPointerDown(PointerEventData e) => OnDrag(e);
        public void OnDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(baseRect, e.position, e.pressEventCamera, out var local);
            var clamped = Vector2.ClampMagnitude(local, radius);
            knobRect.anchoredPosition = clamped;
            MobileControls.Instance?.SetMove(clamped / radius);
        }
        public void OnPointerUp(PointerEventData e)
        {
            knobRect.anchoredPosition = Vector2.zero;
            MobileControls.Instance?.SetMove(Vector2.zero);
        }
    }

    /// <summary>Jump button: pointer down queues one jump.</summary>
    public class JumpButton : MonoBehaviour, IPointerDownHandler
    {
        public void OnPointerDown(PointerEventData e) => MobileControls.Instance?.QueueJump();
    }
}
