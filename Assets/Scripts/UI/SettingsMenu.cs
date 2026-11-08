using UnityEngine;
using UnityEngine.UI;
using Crunchee.Audio;
using Crunchee.Game;

namespace Crunchee.UI
{
    /// <summary>
    /// The settings panel: music/ASMR volume sliders (persisted through
    /// AsmrAudio's PlayerPrefs API) and the map cards. While it's open,
    /// IsOpen is true and PlayerController ignores all input - timeScale
    /// stays 1 so the ASMR keeps breathing.
    /// </summary>
    public class SettingsMenu : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        [Header("Wired by the builder")]
        public GameObject panel;
        public Slider musicSlider;
        public Slider asmrSlider;

        void Awake()
        {
            if (panel != null) panel.SetActive(false);
            IsOpen = false;
        }

        void Start()
        {
            if (musicSlider != null)
            {
                musicSlider.SetValueWithoutNotify(AsmrAudio.MusicVolume);
                musicSlider.onValueChanged.AddListener(v => AsmrAudio.MusicVolume = v);
            }
            if (asmrSlider != null)
            {
                asmrSlider.SetValueWithoutNotify(AsmrAudio.AsmrVolume);
                asmrSlider.onValueChanged.AddListener(v => AsmrAudio.AsmrVolume = v);
            }
        }

        public void Open() { if (panel) panel.SetActive(true); IsOpen = true; }
        public void Close() { if (panel) panel.SetActive(false); IsOpen = false; }
        public void Toggle() { if (IsOpen) Close(); else Open(); }

        /// <summary>Map card click (wired by the builder for available maps).</summary>
        public void SelectMap(string id) { MapCatalog.Select(id); Close(); }
    }
}
