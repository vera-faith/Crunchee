using UnityEngine;
using UnityEngine.EventSystems;

namespace Crunchee.UI
{
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
}
