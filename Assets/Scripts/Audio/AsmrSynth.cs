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
    }
}
