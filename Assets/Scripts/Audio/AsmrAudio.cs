using System.Collections.Generic;
using UnityEngine;
using Crunchee.Core;

namespace Crunchee.Audio
{
    /// <summary>
    /// ASMR playback engine (redesign, Nov 6): a small POOL of voices - every
    /// one-shot gets its own AudioSource so pitch/volume never bend a sound
    /// already playing. spatialBlend is low for intimacy. Landing = squish
    /// first, crack ~0.07 s later and LOUDER. Footsteps are quiet squishes
    /// with an occasional small crackle. Music ducks under cracks.
    /// Recorded clips (SoundTable) still win when assigned.
    /// </summary>
    public class AsmrAudio : MonoBehaviour
    {
        public static AsmrAudio Instance { get; private set; }

        [System.Serializable]
        public class ClipSet { public AudioClip[] variants; }

        [Header("Assign in Inspector - one row per enum value, 2-3 variants each")]
        public ClipSet[] baseClips;     // index = (int)BaseMaterial
        public ClipSet[] coatingClips;  // index = (int)Coating (Plain = empty/none)

        const int Voices = 10;
        AudioSource[] pool;
        int nextVoice;
        AudioSource musicSource;
        readonly Dictionary<string, AudioClip> synthCache = new Dictionary<string, AudioClip>();
        float duck;                                        // 0..1 ducked amount
        readonly Dictionary<string, float> lastPlay = new Dictionary<string, float>();

        // Persisted mix volumes for the settings menu.
        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat("crunchee.musicVolume", 0.6f);
            set { PlayerPrefs.SetFloat("crunchee.musicVolume", Mathf.Clamp01(value)); if (Instance) Instance.ApplyMusicVol(); }
        }
        public static float AsmrVolume
        {
            get => PlayerPrefs.GetFloat("crunchee.asmrVolume", 1f);
            set => PlayerPrefs.SetFloat("crunchee.asmrVolume", Mathf.Clamp01(value));
        }

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            pool = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false; s.spatialBlend = 0.1f; // intimate, mostly non-spatial
                pool[i] = s;
            }
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false; musicSource.loop = true;
            musicSource.spatialBlend = 0f;
            ApplyMusicVol();
        }

        void Start()
        {
            if (musicSource.clip == null)
            {
                musicSource.clip = Synth("lofi", () => AsmrSynth.LofiLoop());
                musicSource.Play();
            }
        }

        void Update()
        {
            // duck recovery: ~0.7 s release back to full music level
            duck = Mathf.MoveTowards(duck, 0f, Time.deltaTime / 0.7f);
            ApplyMusicVol();
        }

        void ApplyMusicVol()
        {
            // -4 dB while ducked (x0.63), smooth via the Update lerp
            if (musicSource) musicSource.volume = MusicVolume * Mathf.Lerp(1f, 0.63f, duck);
        }

        // ------------------------------------------------- public API

        public static void PlaySquish(BaseMaterial b, Coating c, Vector3 pos, int waxLayers = 1)
        {
            if (Instance == null) return;
            var self = Instance;

            // Landing: squish first, crack a beat later and louder.
            int dv = Random.Range(0, 3);
            var deep = self.Synth($"squishdeep{dv}", () => AsmrSynth.SquishStep(dv * 31 + 5, true));
            self.Voice("squish", deep, Random.Range(0.95f, 1.03f), 0.5f);
            var baseClip = self.FromTable(self.baseClips, (int)b)
                           ?? self.Synth($"base{(int)b}_{dv}", () => AsmrSynth.BaseFor(b, dv * 17 + 3));
            self.Voice("base", baseClip, Random.Range(0.94f, 1.02f), 0.45f);

            if (c == Coating.Plain) return;

            if (c == Coating.Wax)
            {
                int layers = Mathf.Clamp(waxLayers, 1, 10);
                int cv = Random.Range(0, 4); // 4 cached variants per layer
                var clip = self.FromTable(self.coatingClips, (int)c)
                           ?? self.Synth($"wax{layers}_{cv}", () => AsmrSynth.WaxCrack(layers, cv * 911 + layers));
                float f = (layers - 1) / 9f;
                self.StartCoroutine(self.DelayedVoice(clip,
                             Mathf.Lerp(1.03f, 0.88f, f) * Random.Range(0.98f, 1.02f),
                             Mathf.Lerp(0.85f, 1f, f), 0.07f, true));
            }
            else
            {
                var clip = self.FromTable(self.coatingClips, (int)c) ?? self.SynthCoating(c);
                self.Voice("coating", clip, Random.Range(0.95f, 1.05f), 0.8f);
            }
        }

        /// <summary>Soft re-land on an already-cracked squishy: quiet squish only.</summary>
        public static void PlaySoftBase(Vector3 pos)
        {
            if (Instance == null) return;
            int v = Random.Range(0, 3);
            var clip = Instance.Synth($"squishstep{v}", () => AsmrSynth.SquishStep(v * 7 + 1));
            Instance.Voice("step", clip, Random.Range(0.92f, 1f), 0.14f);
        }

        /// <summary>
        /// One footstep on a squishy: the soft gummy squish, and on wax a
        /// small crackle cluster EVERY step (the reference cracks under each
        /// footfall). The crackle settles a little the longer she walks the
        /// same shell but never goes silent. Rate-limited per type.
        /// </summary>
        public static void PlayStep(Vector3 pos, BaseMaterial b, Coating c, int waxLayers, int walkedSteps = 0)
        {
            if (Instance == null) return;
            var self = Instance;
            int v = Random.Range(0, 3);
            var squish = self.Synth($"squishstep{v}", () => AsmrSynth.SquishStep(v * 7 + 1));
            self.Voice("step", squish, Random.Range(0.95f, 1.08f), 0.14f);

            if (c == Coating.Wax && waxLayers > 0) // every step cracks
            {
                int cv = Random.Range(0, 4);
                var crack = self.Synth($"waxstep{waxLayers}_{cv}", () => AsmrSynth.WaxStepCrack(waxLayers, cv * 13 + waxLayers));
                float settle = Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(walkedSteps / 14f)); // eases, never silent
                self.Voice("stepcrack", crack, Random.Range(0.94f, 1.06f),
                           (0.28f + 0.05f * Mathf.Clamp(waxLayers, 1, 10)) * settle);
            }
        }

        // ------------------------------------------------- internals

        AudioClip SynthCoating(Coating c)
        {
            int v = Random.Range(0, 3);
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

        /// <summary>One shot on its OWN pooled voice (pitch set before play,
        /// so nothing already sounding is bent). Rate-limited per type.</summary>
        void Voice(string type, AudioClip clip, float pitch, float volume)
        {
            if (clip == null) return;
            if (lastPlay.TryGetValue(type, out float t) && Time.time - t < 0.09f) return;
            lastPlay[type] = Time.time;
            var s = pool[nextVoice]; nextVoice = (nextVoice + 1) % Voices;
            s.pitch = pitch;
            s.volume = volume * AsmrVolume;
            s.PlayOneShot(clip);
        }

        System.Collections.IEnumerator DelayedVoice(AudioClip clip, float pitch, float volume, float delay, bool duckMusic)
        {
            yield return new WaitForSeconds(delay);
            if (duckMusic) duck = 1f;   // music ducks under the crack
            Voice("crack", clip, pitch, volume);
        }
    }
}
