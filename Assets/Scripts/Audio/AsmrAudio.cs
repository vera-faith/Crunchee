using UnityEngine;
using Crunchee.Core;

namespace Crunchee.Audio
{
    /// <summary>
    /// Layered ASMR playback: every step plays a BASE clip (the material)
    /// plus a COATING clip on top (crackle / crunch / sticky pull / glossy tap),
    /// with pitch + volume micro-variation so repeats never sound identical.
    /// Clips are assigned in the SoundTable asset (produced audio only - no ripped sounds).
    /// </summary>
    public class AsmrAudio : MonoBehaviour
    {
        public static AsmrAudio Instance { get; private set; }

        [System.Serializable]
        public class ClipSet { public AudioClip[] variants; }

        [Header("Assign in Inspector - one row per enum value, 2-3 variants each")]
        public ClipSet[] baseClips;     // index = (int)BaseMaterial
        public ClipSet[] coatingClips;  // index = (int)Coating (Plain = empty/none)

        AudioSource baseSource, coatingSource;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            baseSource = gameObject.AddComponent<AudioSource>();
            coatingSource = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { baseSource, coatingSource })
            { s.playOnAwake = false; s.spatialBlend = 0.35f; }
        }

        public static void PlaySquish(BaseMaterial b, Coating c, Vector3 pos, int waxLayers = 1)
        {
            if (Instance == null) return;
            Instance.Play(Instance.baseSource, Instance.baseClips, (int)b);
            if (c == Coating.Plain) return;
            if (c == Coating.Wax && waxLayers > 1)
            {
                // Thicker wax = a bigger, longer crack: lower pitch, louder,
                // plus 1 extra staggered crack layer per ~3 wax layers (capped).
                Instance.coatingSource.pitch = Mathf.Lerp(1.05f, 0.8f, (waxLayers - 1) / 9f) * Random.Range(0.97f, 1.03f);
                Instance.coatingSource.volume = Mathf.Lerp(0.7f, 1f, (waxLayers - 1) / 9f);
                Instance.PlayClip(Instance.coatingSource, Instance.coatingClips, (int)c);
                int extra = Mathf.Min(3, (waxLayers - 1) / 3);
                for (int i = 0; i < extra; i++)
                    Instance.Invoke(nameof(PlayWaxEcho), 0.05f * (i + 1));
            }
            else Instance.Play(Instance.coatingSource, Instance.coatingClips, (int)c);
        }

        void PlayWaxEcho() => PlayClip(coatingSource, coatingClips, (int)Coating.Wax);

        void PlayClip(AudioSource src, ClipSet[] table, int idx)
        {
            if (table == null || idx < 0 || idx >= table.Length) return;
            var set = table[idx];
            if (set == null || set.variants == null || set.variants.Length == 0) return;
            src.PlayOneShot(set.variants[Random.Range(0, set.variants.Length)]);
        }

        void Play(AudioSource src, ClipSet[] table, int idx)
        {
            if (table == null || idx < 0 || idx >= table.Length) return;
            var set = table[idx];
            if (set == null || set.variants == null || set.variants.Length == 0) return;
            src.pitch = Random.Range(0.94f, 1.06f);
            src.volume = Random.Range(0.9f, 1f);
            src.PlayOneShot(set.variants[Random.Range(0, set.variants.Length)]);
        }
    }
}
