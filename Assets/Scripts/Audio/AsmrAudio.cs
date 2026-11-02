using System.Collections.Generic;
using UnityEngine;
using Crunchee.Core;

namespace Crunchee.Audio
{
    /// <summary>
    /// Layered ASMR playback: every step plays a BASE clip (the material)
    /// plus a COATING clip on top (crackle / crunch / sticky pull / glossy tap),
    /// with pitch + volume micro-variation so repeats never sound identical.
    /// Recorded clips (SoundTable, produced audio only) win when assigned;
    /// until then AsmrSynth generates original sounds procedurally.
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
        readonly Dictionary<string, AudioClip> synthCache = new Dictionary<string, AudioClip>();

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
            var self = Instance;

            // Base layer: recorded clip, else synth butter thup.
            var baseClip = self.FromTable(self.baseClips, (int)b) ?? self.Synth("butter", () => AsmrSynth.ButterBase(Random.Range(1, 99999)));
            self.PlayOne(self.baseSource, baseClip, Random.Range(0.94f, 1.06f), Random.Range(0.9f, 1f));

            if (c == Coating.Plain) return;

            if (c == Coating.Wax)
            {
                int layers = Mathf.Clamp(waxLayers, 1, 10);
                var clip = self.FromTable(self.coatingClips, (int)c)
                           ?? self.Synth($"wax{layers}", () => AsmrSynth.WaxCrack(layers, Random.Range(1, 99999)));
                float f = (layers - 1) / 9f;
                self.PlayOne(self.coatingSource, clip,
                             Mathf.Lerp(1.03f, 0.88f, f) * Random.Range(0.98f, 1.02f),
                             Mathf.Lerp(0.75f, 1f, f));
            }
            else
            {
                var clip = self.FromTable(self.coatingClips, (int)c) ?? self.SynthCoating(c);
                self.PlayOne(self.coatingSource, clip, Random.Range(0.95f, 1.05f), 0.95f);
            }
        }

        AudioClip SynthCoating(Coating c)
        {
            int v = Random.Range(0, 3); // a few cached variants per coating
            switch (c)
            {
                case Coating.Sugar: return Synth($"sugar{v}", () => AsmrSynth.SugarCrunch(v + 1));
                case Coating.Honey: return Synth($"honey{v}", () => AsmrSynth.HoneyPull(v + 1));
                case Coating.NailPolish: return Synth($"nail{v}", () => AsmrSynth.NailPolishTap(v + 1));
                default: return null;
            }
        }

        AudioClip Synth(string key, System.Func<AudioClip> make)
        {
            if (!synthCache.TryGetValue(key, out var clip) || clip == null)
            { clip = make(); synthCache[key] = clip; }
            return clip;
        }

        AudioClip FromTable(ClipSet[] table, int idx)
        {
            if (table == null || idx < 0 || idx >= table.Length) return null;
            var set = table[idx];
            if (set == null || set.variants == null || set.variants.Length == 0) return null;
            return set.variants[Random.Range(0, set.variants.Length)];
        }

        void PlayOne(AudioSource src, AudioClip clip, float pitch, float volume)
        {
            if (clip == null) return;
            src.pitch = pitch; src.volume = volume;
            src.PlayOneShot(clip);
        }
    }
}
