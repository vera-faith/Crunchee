using UnityEngine;
using UnityEngine.UI;
using Crunchee.Audio;

namespace Crunchee.UI
{
    /// <summary>
    /// Top-right mute toggle (halohalo-style corner button): speaker icon,
    /// flips to the crossed-out icon while muted. Persisted through
    /// AsmrAudio.Muted so it survives restarts.
    /// </summary>
    public class MuteButton : MonoBehaviour
    {
        [Header("Wired by the builder")]
        public Image icon;
        public Sprite soundOn;
        public Sprite soundOff;

        static readonly Color OnColor = new Color(0.55f, 0.4f, 0.28f);
        static readonly Color OffColor = new Color(0.78f, 0.45f, 0.4f);

        void Start()
        {
            var b = GetComponent<Button>();
            if (b != null) b.onClick.AddListener(Toggle);
            Refresh();
        }

        public void Toggle()
        {
            AsmrAudio.Muted = !AsmrAudio.Muted;
            Refresh();
        }

        void Refresh()
        {
            if (icon == null) return;
            bool muted = AsmrAudio.Muted;
            icon.sprite = muted ? soundOff : soundOn;
            icon.color = muted ? OffColor : OnColor;
        }
    }
}
