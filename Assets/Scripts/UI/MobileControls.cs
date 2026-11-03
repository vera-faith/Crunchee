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
    /// VirtualJoystick and JumpButton live in their own files (Unity only
    /// serializes the class that matches the file name reliably).
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
}
