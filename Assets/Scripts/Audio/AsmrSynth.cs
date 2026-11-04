using UnityEngine;

namespace Crunchee.Audio
{
    /// <summary>
    /// Original procedural ASMR synthesis. Faith's rule: produced sounds only,
    /// nothing ripped. Until recorded clips are produced, every squish sound is
    /// synthesized sample-by-sample here - each coating family has its own
    /// recipe, and wax cracks scale with the layer count (1..10) so every
    /// Butter Path step genuinely sounds different.
    /// </summary>
    public static class AsmrSynth
    {
        const int SR = 44100;

        static float[] NewBuf(float seconds) => new float[Mathf.CeilToInt(seconds * SR)];

        static AudioClip ToClip(string name, float[] data)
        {
            // normalize to a safe peak
            float peak = 0.0001f;
            foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            float g = 0.9f / peak;
            for (int i = 0; i < data.Length; i++) data[i] *= g;
            var clip = AudioClip.Create(name, data.Length, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        /// <summary>Soft low "thup" of the butter body itself.</summary>
        public static AudioClip ButterBase(int seed)
        {
            var r = new System.Random(seed * 7919 + 13);
            var d = NewBuf(0.16f);
            double phase = 0;
            float lp = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Exp(-t * 28f);
                double freq = Mathf.Lerp(135f, 68f, t / 0.16f);
                phase += 2 * Mathf.PI * freq / SR;
                lp += 0.12f * (Noise(r) - lp); // soft puff
                d[i] = (Mathf.Sin((float)phase) * 0.8f + lp * 0.5f) * env;
            }
            return ToClip("Synth_Butter", d);
        }

        /// <summary>
        /// Wax crack: a cluster of sharp micro-snaps over a low knock.
        /// More layers = more snaps, spread longer, denser and heavier.
        /// </summary>
        public static AudioClip WaxCrack(int layers, int seed)
        {
            layers = Mathf.Clamp(layers, 1, 10);
            var r = new System.Random(seed * 104729 + layers * 131);
            float dur = 0.16f + layers * 0.022f;
            var d = NewBuf(dur + 0.05f);
            // low body knock
            double phase = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                double freq = Mathf.Lerp(110f, 55f, Mathf.Min(1f, t / 0.1f));
                phase += 2 * Mathf.PI * freq / SR;
                d[i] += Mathf.Sin((float)phase) * Mathf.Exp(-t * 30f) * 0.45f;
            }
            int snaps = 5 + layers * 3;
            for (int s = 0; s < snaps; s++)
            {
                int start = (int)(r.NextDouble() * dur * 0.85f * SR);
                int len = 50 + r.Next(160);
                float amp = (0.35f + (float)r.NextDouble() * 0.6f) * (0.65f + layers * 0.045f);
                float prev = 0;
                for (int i = 0; i < len && start + i < d.Length; i++)
                {
                    float n = Noise(r);
                    float hp = n - prev; prev = n;              // bright, crackly
                    float env = Mathf.Exp(-i / (len * 0.45f));
                    d[start + i] += hp * env * amp;
                }
            }
            return ToClip($"Synth_Wax_{layers}", d);
        }

        /// <summary>Gritty bright crunch: dozens of tiny granule bursts.</summary>
        public static AudioClip SugarCrunch(int seed)
        {
            var r = new System.Random(seed * 31 + 7);
            var d = NewBuf(0.34f);
            for (int s = 0; s < 110; s++)
            {
                float at = (float)r.NextDouble() * 0.26f;
                int start = (int)(at * SR);
                int len = 18 + r.Next(50);
                float amp = (0.2f + (float)r.NextDouble() * 0.55f) * Mathf.Exp(-at * 4.5f);
                float prev = 0;
                for (int i = 0; i < len && start + i < d.Length; i++)
                {
                    float n = Noise(r);
                    float hp = n - prev; prev = n;
                    d[start + i] += hp * amp * (1f - i / (float)len);
                }
            }
            double phase = 0; // faint body
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                phase += 2 * Mathf.PI * 95f / SR;
                d[i] += Mathf.Sin((float)phase) * Mathf.Exp(-t * 22f) * 0.18f;
            }
            return ToClip("Synth_Sugar", d);
        }

        /// <summary>Sticky pull: slow glossy swell, bubble pops, reluctant release.</summary>
        public static AudioClip HoneyPull(int seed)
        {
            var r = new System.Random(seed * 17 + 3);
            var d = NewBuf(0.55f);
            float lp = 0;
            double phase = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float swell = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.42f));       // in-out
                float release = t > 0.34f ? Mathf.Exp(-(t - 0.34f) * 9f) * 0.5f : 0f; // sticky tail
                lp += 0.06f * (Noise(r) - lp);
                double freq = Mathf.Lerp(280f, 85f, t / 0.5f);
                phase += 2 * Mathf.PI * freq / SR;
                d[i] = lp * 1.4f * (swell * 0.7f + release) + Mathf.Sin((float)phase) * 0.12f * swell;
            }
            for (int p = 0; p < 6; p++) // bubbles
            {
                int start = (int)((0.05f + r.NextDouble() * 0.36f) * SR);
                int len = 300 + r.Next(500);
                double f0 = 300 + r.NextDouble() * 500;
                double ph = 0;
                for (int i = 0; i < len && start + i < d.Length; i++)
                {
                    double f = f0 * (1.0 - i / (double)len * 0.6);
                    ph += 2 * Mathf.PI * f / SR;
                    d[start + i] += Mathf.Sin((float)ph) * Mathf.Exp(-i / (len * 0.4f)) * 0.22f;
                }
            }
            return ToClip("Synth_Honey", d);
        }

        /// <summary>Glossy hard tap: click transient + short resonant ping.</summary>
        public static AudioClip NailPolishTap(int seed)
        {
            var r = new System.Random(seed * 13 + 5);
            var d = NewBuf(0.13f);
            float f = 1450f * (0.95f + (float)r.NextDouble() * 0.1f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float ping = Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t * 55f)
                           + 0.5f * Mathf.Sin(2 * Mathf.PI * f * 2.76f * t) * Mathf.Exp(-t * 80f);
                float click = i < 40 ? Noise(r) * Mathf.Exp(-i / 9f) * 0.9f : 0f;
                d[i] = ping * 0.55f + click;
            }
            return ToClip("Synth_NailPolish", d);
        }

        /// <summary>
        /// THE squish (Faith's sound model, Nov 3): the default sound of moving
        /// on a squishy - a soft wet foamy press with a low bloop as the surface
        /// gives under her weight. Coatings layer their own sound OVER this.
        /// deep = the bigger landing version.
        /// </summary>
        public static AudioClip SquishStep(int seed, bool deep = false)
        {
            var r = new System.Random(seed * 6151 + 29);
            float dur = deep ? 0.3f : 0.17f;
            var d = NewBuf(dur);
            float lp = 0, lp2 = 0;
            double phase = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float x = t / dur;
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(x, 0.7f)); // swell in, release out
                env *= env;
                lp += 0.10f * (Noise(r) - lp);        // foamy body
                lp2 += 0.35f * (Noise(r) - lp2);      // brighter surface noise
                double freq = Mathf.Lerp(deep ? 118f : 148f, 58f, x);
                phase += 2 * Mathf.PI * freq / SR;
                float bloop = Mathf.Sin((float)phase) * 0.5f;
                float squelch = (lp2 - lp) * 0.8f;    // wet band movement
                d[i] = (lp * 1.15f + squelch * 0.5f + bloop * (deep ? 0.7f : 0.5f)) * env;
            }
            return ToClip(deep ? "Synth_SquishDeep" : "Synth_SquishStep", d);
        }

        /// <summary>
        /// Footstep crackle for walking around on already-cracked wax (Faith,
        /// Nov 2): a shorter, brighter cousin of the full crack - a soft sole
        /// press plus a few micro-snaps, density still grows with layer count.
        /// </summary>
        public static AudioClip WaxStepCrack(int layers, int seed)
        {
            layers = Mathf.Clamp(layers, 1, 10);
            var r = new System.Random(seed * 31337 + layers * 71);
            float dur = 0.1f + layers * 0.011f;
            var d = NewBuf(dur + 0.03f);
            int snaps = 6 + layers * 2; // multiple cracks per step (Faith, Nov 3)
            for (int s = 0; s < snaps; s++)
            {
                int start = (int)(r.NextDouble() * dur * 0.8f * SR);
                int len = 40 + r.Next(120);
                float amp = (0.25f + (float)r.NextDouble() * 0.45f) * (0.6f + layers * 0.04f);
                float prev = 0;
                for (int i = 0; i < len && start + i < d.Length; i++)
                {
                    float n = Noise(r);
                    float hp = n - prev; prev = n;
                    float env = Mathf.Exp(-i / (len * 0.4f));
                    d[start + i] += hp * env * amp;
                }
            }
            // soft sole press underneath - kept faint, the crackle is the point
            float lp = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                lp += 0.2f * (Noise(r) - lp);
                d[i] += lp * Mathf.Exp(-t * 40f) * 0.1f;
            }
            return ToClip("Synth_WaxStep", d);
        }

        // --- Background music (Faith, Nov 3): a dreamy, cute, twinkly loop in
        // the reference videos' spirit - but an ORIGINAL composition, written
        // note by note here like every other sound in the game. Music-box
        // melody over soft pad chords and a gentle bass, C major, I-IV-vi-V
        // twice, 16 seconds, seamless (note tails wrap around the loop point).
        static float MidiHz(float midi) => 440f * Mathf.Pow(2f, (midi - 69f) / 12f);

        static void AddPadTone(float[] buf, float midi, double start, double len)
        {
            int n = buf.Length;
            int s0 = (int)(start * SR);
            int count = (int)(len * SR);
            double f = MidiHz(midi);
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)SR;
                double x = t / len;
                // slow swell in, gentle release out - chords breathe per bar
                double env = System.Math.Min(1.0, t / 0.5) * (x > 0.62 ? System.Math.Pow(1.0 - (x - 0.62) / 0.38, 1.4) : 1.0);
                double ph = 2 * System.Math.PI * f * t;
                double v = (System.Math.Sin(ph) * 0.045 + System.Math.Sin(ph * 1.002) * 0.02) * env;
                buf[(s0 + i) % n] += (float)v;
            }
        }

        static void AddBoxNote(float[] buf, float midi, double start, float amp)
        {
            int n = buf.Length;
            int s0 = (int)(start * SR);
            int count = (int)(1.4 * SR);
            double f = MidiHz(midi);
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)SR;
                double env = System.Math.Exp(-t * 3.1) * System.Math.Min(1.0, t / 0.004);
                double v = (System.Math.Sin(2 * System.Math.PI * f * t)
                          + 0.42 * System.Math.Sin(2 * System.Math.PI * f * 2.0 * t)
                          + 0.16 * System.Math.Sin(2 * System.Math.PI * f * 3.01 * t)) * env * amp;
                buf[(s0 + i) % n] += (float)v;
            }
        }

        static void AddBassNote(float[] buf, float midi, double start)
        {
            int n = buf.Length;
            int s0 = (int)(start * SR);
            int count = (int)(1.8 * SR);
            double f = MidiHz(midi);
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)SR;
                double env = System.Math.Exp(-t * 2.2) * System.Math.Min(1.0, t / 0.01);
                buf[(s0 + i) % n] += (float)(System.Math.Sin(2 * System.Math.PI * f * t) * 0.055 * env);
            }
        }

        public static AudioClip DreamyMusicBox()
        {
            const double beat = 0.5, bar = 4 * beat; // 120bpm feel, 8 bars = 16 s
            var buf = new float[(int)(bar * 8 * SR)];
            // Chords as midi tones: dreamy open voicings with 7th color.
            float[][] chords =
            {
                new float[] { 48, 55, 64, 71 }, // C
                new float[] { 53, 57, 64, 69 }, // F
                new float[] { 45, 52, 60, 64 }, // Am
                new float[] { 47, 55, 62, 67 }, // G
                new float[] { 48, 55, 64, 71 }, // C
                new float[] { 53, 57, 64, 69 }, // F
                new float[] { 45, 52, 60, 64 }, // Am
                new float[] { 47, 55, 62, 67 }, // G
            };
            float[] bassRoots = { 36, 41, 33, 31, 36, 41, 33, 31 };
            for (int b = 0; b < 8; b++)
            {
                double at = b * bar;
                foreach (var tone in chords[b]) AddPadTone(buf, tone, at, bar + 0.4);
                AddBassNote(buf, bassRoots[b], at);
            }
            // The melody: an original music-box line (bar, beat-in-bar, midi).
            var melody = new (int bar, float beat, float midi)[]
            {
                (0, 0.0f, 76), (0, 0.5f, 79), (0, 1.0f, 81), (0, 1.5f, 79), (0, 2.0f, 76), (0, 3.0f, 74),
                (1, 0.0f, 77), (1, 0.5f, 81), (1, 1.0f, 84), (1, 1.5f, 81), (1, 2.0f, 77), (1, 3.0f, 76),
                (2, 0.0f, 76), (2, 0.5f, 81), (2, 1.0f, 84), (2, 1.5f, 81), (2, 2.0f, 79), (2, 3.0f, 76),
                (3, 0.0f, 74), (3, 0.5f, 79), (3, 1.0f, 83), (3, 1.5f, 79), (3, 2.5f, 74), (3, 3.0f, 71),
                (4, 0.0f, 72), (4, 0.5f, 76), (4, 1.0f, 79), (4, 2.0f, 81), (4, 3.0f, 79),
                (5, 0.0f, 81), (5, 0.5f, 84), (5, 1.0f, 81), (5, 1.5f, 77), (5, 2.0f, 76), (5, 3.0f, 74),
                (6, 0.0f, 76), (6, 1.0f, 79), (6, 1.5f, 81), (6, 2.0f, 84), (6, 3.0f, 81),
                (7, 0.0f, 79), (7, 0.5f, 74), (7, 1.0f, 72), (7, 2.0f, 76), (7, 3.0f, 72),
            };
            foreach (var (b, bt, m) in melody) AddBoxNote(buf, m, b * bar + bt * beat, 0.13f);
            // Twinkles: tiny high sparkles late in even bars.
            foreach (var (b, m) in new (int, float)[] { (1, 91), (3, 88), (5, 91), (7, 88) })
                AddBoxNote(buf, m, b * bar + 3.5 * beat, 0.035f);
            return ToClip("Synth_MusicBox", buf);
        }
    }
}
