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

            // Base layer (Faith's model): the SQUISH itself - a deep soft press,
            // with the butter body's low thump tucked underneath.
            int dv = Random.Range(0, 3);
            var deep = self.Synth($"squishdeep{dv}", () => AsmrSynth.SquishStep(Random.Range(1, 99999), true));
            self.PlayOne(self.baseSource, deep, Random.Range(0.95f, 1.03f), 0.95f);
            var thump = self.FromTable(self.baseClips, (int)b) ?? self.Synth("butter", () => AsmrSynth.ButterBase(Random.Range(1, 99999)));
            self.PlayOne(self.baseSource, thump, Random.Range(0.94f, 1.02f), 0.35f);

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

        /// <summary>Soft re-land on an already-cracked squishy: just a quiet squish, no crack.</summary>
        public static void PlaySoftBase(Vector3 pos)
        {
            if (Instance == null) return;
            int v = Random.Range(0, 3);
            var clip = Instance.Synth($"squishstep{v}", () => AsmrSynth.SquishStep(Random.Range(1, 99999)));
            Instance.PlayOne(Instance.baseSource, clip, Random.Range(0.92f, 1f), 0.3f);
        }

        /// <summary>
        /// One walking step on a squishy (Faith's sound model, Nov 3): the soft
        /// SQUISH plays every step; the coating layers its sound OVER it - for
        /// wax, a crackle whose volume grows with the layer count (a whisper on
        /// butter 1, loud and dense on butter 10).
        /// </summary>
        public static void PlayStep(Vector3 pos, BaseMaterial b, Coating c, int waxLayers)
        {
            if (Instance == null) return;
            var self = Instance;
            int v = Random.Range(0, 3);
            var squish = self.Synth($"squishstep{v}", () => AsmrSynth.SquishStep(Random.Range(1, 99999)));
            self.PlayOne(self.baseSource, squish, Random.Range(0.95f, 1.08f), 0.5f);

            if (c == Coating.Wax && waxLayers > 0)
            {
                int cv = Random.Range(0, 4);
                var crack = self.Synth($"waxstep{waxLayers}_{cv}", () => AsmrSynth.WaxStepCrack(waxLayers, Random.Range(1, 99999)));
                self.PlayOne(self.coatingSource, crack, Random.Range(0.94f, 1.06f),
                             0.16f + 0.05f * Mathf.Clamp(waxLayers, 1, 10));
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
