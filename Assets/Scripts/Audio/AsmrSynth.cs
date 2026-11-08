using UnityEngine;
using Crunchee.Core;

namespace Crunchee.Audio
{
    /// <summary>
    /// Original procedural ASMR synthesis (redesign, Nov 6): fewer, more
    /// coherent sounds. A crack is ONE fracture event - a sharp band-limited
    /// transient plus a short modal ring, then a clustered cascade of
    /// secondary fractures and a tiny settle - not scattered ticks. Every
    /// base material has its own character; wax layers 1..10 morph
    /// continuously from thin glassy tick-snaps to deep chunky crunches.
    /// All filtered noise (one-pole LP/HP bands) - never raw first-difference.
    /// </summary>
    public static class AsmrSynth
    {
        const int SR = 44100;

        static float[] NewBuf(float seconds) => new float[Mathf.CeilToInt(seconds * SR)];

        /// <summary>Peak-normalize to a category target, not always 0.9.</summary>
        static AudioClip ToClip(string name, float[] data, float target = 0.9f)
        {
            float peak = 0.0001f;
            foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            float g = target / peak;
            for (int i = 0; i < data.Length; i++) data[i] *= g;
            var clip = AudioClip.Create(name, data.Length, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        /// <summary>One-pole band-limited noise burst written into buf.
        /// lo/hi in Hz; env decays with rate k.</summary>
        static void BandNoise(float[] buf, int start, int len, float lo, float hi,
                              float amp, float k, System.Random r)
        {
            float lpS = 0f, hpS = 0f;
            float aL = Mathf.Exp(-2f * Mathf.PI * hi / SR);
            float aH = Mathf.Exp(-2f * Mathf.PI * lo / SR);
            for (int i = 0; i < len && start + i < buf.Length; i++)
            {
                float n = Noise(r);
                lpS = aL * lpS + (1f - aL) * n;          // low-pass
                hpS = aH * hpS + (1f - aH) * lpS;        // then high-pass -> band
                float env = Mathf.Exp(-i * k / SR);
                buf[start + i] += (lpS - hpS) * env * amp;
            }
        }

        /// <summary>Decaying inharmonic sine (a modal "ring" of the shell).</summary>
        static void AddRing(float[] buf, int start, float freq, float amp, float decay, float dur)
        {
            int n = (int)(dur * SR);
            double ph = 0;
            for (int i = 0; i < n && start + i < buf.Length; i++)
            {
                ph += 2 * Mathf.PI * freq / SR;
                buf[start + i] += Mathf.Sin((float)ph) * Mathf.Exp(-i * decay / SR) * amp;
            }
        }

        /// <summary>Low body thump under a crack or landing.</summary>
        static void AddThump(float[] buf, int start, float f0, float amp, float dur)
        {
            int n = (int)(dur * SR);
            double ph = 0;
            for (int i = 0; i < n && start + i < buf.Length; i++)
            {
                float x = i / (float)n;
                double f = Mathf.Lerp(f0, f0 * 0.55f, x);
                ph += 2 * Mathf.PI * f / SR;
                buf[start + i] += Mathf.Sin((float)ph) * Mathf.Exp(-i * 9f / SR) * amp * (1f - x * 0.4f);
            }
        }

        // ============================== AU2: coherent wax crack ============

        /// <summary>
        /// ONE fracture event for layer n: sharp band-limited transient +
        /// modal ring (thin bright ~3-6 kHz at n=1, heavy ~0.9-2.5 kHz plus a
        /// 90-150 Hz body thump at n=10), then 3-7 clustered secondary
        /// fractures with falling amplitude, then a short settle. Deterministic.
        /// </summary>
        public static AudioClip WaxCrack(int layers, int seed)
        {
            layers = Mathf.Clamp(layers, 1, 10);
            var r = new System.Random(seed * 104729 + layers * 131);
            float f = (layers - 1) / 9f;                       // 0 thin .. 1 thick
            float dur = Mathf.Lerp(0.14f, 0.42f, f) + 0.08f;
            var d = NewBuf(dur);
            // ring frequencies fall as the shell gets heavier
            float fA = Mathf.Lerp(5200f, 2200f, f) * (0.96f + (float)r.NextDouble() * 0.08f);
            float fB = fA * (1.31f + (float)r.NextDouble() * 0.2f);
            float ringDecay = Mathf.Lerp(120f, 45f, f);        // thin rings die fast
            // the initial transient: one sharp click in the ring band
            BandNoise(d, 0, (int)Mathf.Lerp(900f, 2400f, f), Mathf.Lerp(2600f, 900f, f), Mathf.Lerp(8000f, 4000f, f), 1f, Mathf.Lerp(60f, 18f, f) * 40f, r);
            AddRing(d, 0, fA, 0.55f, ringDecay, Mathf.Lerp(0.05f, 0.16f, f));
            AddRing(d, 0, fB, 0.3f, ringDecay * 1.4f, Mathf.Lerp(0.04f, 0.13f, f));
            if (f > 0.4f) AddThump(d, 0, Mathf.Lerp(150f, 95f, f), Mathf.Lerp(0f, 0.5f, (f - 0.4f) / 0.6f), 0.12f);
            // secondary fractures: clustered, falling off
            int n2 = Mathf.RoundToInt(Mathf.Lerp(3f, 7f, f));
            float at = 0.008f;
            for (int s = 0; s < n2; s++)
            {
                int st = (int)(at * SR);
                float a = Mathf.Lerp(0.5f, 0.16f, s / (float)n2) * (0.8f + (float)r.NextDouble() * 0.3f);
                BandNoise(d, st, (int)Mathf.Lerp(600f, 1800f, f), Mathf.Lerp(2000f, 700f, f), Mathf.Lerp(7000f, 3000f, f), a, Mathf.Lerp(55f, 14f, f) * 40f, r);
                AddRing(d, st, fA * (0.85f + (float)r.NextDouble() * 0.3f), a * 0.4f, ringDecay * 1.6f, 0.05f);
                at += Mathf.Lerp(0.006f, 0.02f, f) * (0.5f + (float)r.NextDouble());
            }
            // tiny settle: a short soft grain wash
            BandNoise(d, (int)(at * SR), (int)(0.05f * SR), 1500f, 5000f, 0.08f, 30f, r);
            return ToClip($"Synth_Wax_{layers}", d, 0.92f);
        }

        /// <summary>Small single crackle for walking on cracked wax.</summary>
        public static AudioClip WaxStepCrack(int layers, int seed)
        {
            layers = Mathf.Clamp(layers, 1, 10);
            var r = new System.Random(seed * 31337 + layers * 71);
            float f = (layers - 1) / 9f;
            var d = NewBuf(0.16f);
            BandNoise(d, 0, (int)Mathf.Lerp(700f, 1600f, f), Mathf.Lerp(2400f, 900f, f), Mathf.Lerp(7500f, 3500f, f), 0.8f, Mathf.Lerp(55f, 20f, f) * 40f, r);
            AddRing(d, 0, Mathf.Lerp(4200f, 1600f, f), 0.3f, Mathf.Lerp(110f, 50f, f), 0.05f);
            int st = (int)((0.01f + r.NextDouble() * 0.02f) * SR);
            BandNoise(d, st, 700, 1800f, 6500f, 0.3f, 2200f, r); // one trailing snap
            return ToClip("Synth_WaxStep", d, 0.55f);
        }

        // ============================== AU3: per-material bases ============

        static float[] SoftSquishBuf(System.Random r, float dur, float f0, float foam,
                                     float squelchAmt, float thump, float hiss = 0f)
        {
            var d = NewBuf(dur);
            float lp = 0, lp2 = 0;
            double ph = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR, x = t / dur;
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(x, 0.7f));
                env *= env;
                lp += foam * (Noise(r) - lp);
                lp2 += 0.35f * (Noise(r) - lp2);
                ph += 2 * Mathf.PI * Mathf.Lerp(f0, f0 * 0.4f, x) / SR;
                float hissEnv = hiss * Mathf.Exp(-Mathf.Pow((x - 0.62f) * 5f, 2f)); // air release near the end
                d[i] = (lp * 1.15f + (lp2 - lp) * squelchAmt + Mathf.Sin((float)ph) * thump
                        + lp2 * hissEnv * 0.5f) * env;
            }
            return d;
        }

        /// <summary>Dense creamy soft squish (Butter, also Croissant/Donut/Toast variants).</summary>
        public static AudioClip ButterBase(int seed)
        {
            var r = new System.Random(seed * 7919 + 13);
            return ToClip("Synth_Butter", SoftSquishBuf(r, 0.22f, 120f, 0.10f, 0.45f, 0.7f), 0.6f);
        }

        /// <summary>Airy foam press with a soft air-hiss release (CakeSponge).</summary>
        static AudioClip SpongeBase(int seed)
        {
            var r = new System.Random(seed * 3919 + 17);
            return ToClip("Synth_Sponge", SoftSquishBuf(r, 0.3f, 170f, 0.16f, 0.65f, 0.35f, 1f), 0.55f);
        }

        /// <summary>Wet slick squelch with a bubble (Jam/Grape/Melon).</summary>
        static AudioClip JamBase(int seed)
        {
            var r = new System.Random(seed * 2377 + 11);
            var d = SoftSquishBuf(r, 0.34f, 140f, 0.07f, 1.1f, 0.5f);
            // one little bubble pop mid-squelch
            int st = (int)((0.1f + (float)r.NextDouble() * 0.12f) * SR);
            AddRing(d, st, 900 + (float)r.NextDouble() * 500f, 0.3f, 60f, 0.04f);
            return ToClip("Synth_Jam", d, 0.55f);
        }

        /// <summary>Soft rubbery press, muted squeak, low thud (Mochi/PawPad).</summary>
        static AudioClip MochiBase(int seed)
        {
            var r = new System.Random(seed * 1291 + 19);
            var d = SoftSquishBuf(r, 0.28f, 100f, 0.06f, 0.3f, 0.8f);
            // muted rubber squeak: short descending sine
            int st = (int)(0.04f * SR);
            double ph = 0;
            for (int i = 0; i < 0.09f * SR && st + i < d.Length; i++)
            {
                ph += 2 * Mathf.PI * Mathf.Lerp(620f, 380f, i / (0.09f * SR)) / SR;
                d[st + i] += Mathf.Sin((float)ph) * Mathf.Exp(-i * 6f / SR) * 0.22f;
            }
            return ToClip("Synth_Mochi", d, 0.55f);
        }

        /// <summary>Clean hard snap with a woody resonance (Chocolate/KeyboardKey).</summary>
        static AudioClip ChocoBase(int seed)
        {
            var r = new System.Random(seed * 2017 + 23);
            var d = NewBuf(0.22f);
            BandNoise(d, 0, 1400, 1500f, 7000f, 1f, 2600f, r);      // the snap
            AddRing(d, 0, 640f + (float)r.NextDouble() * 80f, 0.4f, 55f, 0.12f); // woody body
            AddRing(d, 0, 1620f, 0.2f, 90f, 0.08f);
            return ToClip("Synth_Choco", d, 0.7f);
        }

        /// <summary>Crisp shell crunch, then a soft chew (Macaron).</summary>
        static AudioClip MacaronBase(int seed)
        {
            var r = new System.Random(seed * 3049 + 29);
            var d = NewBuf(0.3f);
            BandNoise(d, 0, 2000, 2800f, 7500f, 0.9f, 3200f, r);     // shell crack
            for (int s = 0; s < 3; s++)                              // tiny shell bits
                BandNoise(d, (int)((0.012f + s * 0.014f) * SR), 500, 2400f, 6500f, 0.35f - s * 0.08f, 2400f, r);
            var chew = SoftSquishBuf(r, 0.2f, 160f, 0.14f, 0.7f, 0.2f);
            int st = (int)(0.07f * SR);                              // soft chew follows
            for (int i = 0; i < chew.Length && st + i < d.Length; i++) d[st + i] += chew[i] * 0.7f;
            return ToClip("Synth_Macaron", d, 0.6f);
        }

        static AudioClip FlourBase(int seed)
        {
            var r = new System.Random(seed * 4099 + 19);
            var d = SoftSquishBuf(r, 0.38f, 95f, 0.22f, 0.08f, 0.25f, 1.8f);
            BandNoise(d, 0, (int)(0.3f * SR), 350f, 2800f, 0.5f, 18f, r);
            return ToClip("Synth_Flour", d, 0.5f);
        }

        static AudioClip VanillaBase(int seed)
        {
            var r = new System.Random(seed * 3253 + 23);
            var d = SoftSquishBuf(r, 0.45f, 130f, 0.12f, 1.25f, 0.3f, 1.2f);
            for (int i = 0; i < 3; i++)
            {
                int at = (int)((0.045f + i * 0.06f + r.NextDouble() * 0.018f) * SR);
                AddRing(d, at, 650f + (float)r.NextDouble() * 800f, 0.2f, 75f, 0.06f);
            }
            return ToClip("Synth_Vanilla", d, 0.58f);
        }

        static AudioClip CroissantBase(int seed)
        {
            var r = new System.Random(seed * 6151 + 29);
            var d = SoftSquishBuf(r, 0.32f, 135f, 0.15f, 0.5f, 0.3f);
            BandNoise(d, 0, (int)(0.1f * SR), 1100f, 6200f, 0.85f, 2400f, r);
            for (int i = 0; i < 4; i++)
                BandNoise(d, (int)((0.025f + i * 0.018f) * SR), 700, 1800f, 5800f, 0.32f - i * 0.05f, 2300f, r);
            return ToClip("Synth_Croissant", d, 0.64f);
        }

        /// <summary>Route a base material to its character sound.</summary>
        public static AudioClip BaseFor(BaseMaterial b, int seed)
        {
            switch (b)
            {
                case BaseMaterial.Flour: return FlourBase(seed);
                case BaseMaterial.Vanilla: return VanillaBase(seed);
                case BaseMaterial.Croissant: return CroissantBase(seed);
                case BaseMaterial.CakeSponge: return SpongeBase(seed);
                case BaseMaterial.Jam:
                case BaseMaterial.Grape:
                case BaseMaterial.Melon: return JamBase(seed);
                case BaseMaterial.Mochi:
                case BaseMaterial.PawPad: return MochiBase(seed);
                case BaseMaterial.Chocolate:
                case BaseMaterial.KeyboardKey: return ChocoBase(seed);
                case BaseMaterial.Macaron: return MacaronBase(seed);
                default: return ButterBase(seed); // Butter/Croissant/Donut/Toast
            }
        }

        // ============================== coatings ==========================

        /// <summary>Sugar coating: ONE gritty granular crunch - dense grains
        /// under a single envelope, not a sprinkle of ticks.</summary>
        public static AudioClip SugarCrunch(int seed)
        {
            var r = new System.Random(seed * 31 + 7);
            var d = NewBuf(0.26f);
            // one dense grain bed under one decay envelope
            float lp = 0, hp = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR, x = t / (float)d.Length;
                float env = Mathf.Exp(-x * 5.5f) * Mathf.Min(1f, x * 60f); // fast attack, single decay
                lp += 0.3f * (Noise(r) - lp);
                float grain = (Noise(r) > 0.45f ? 1f : Noise(r) < -0.45f ? -1f : 0f) * (lp - hp + 0.4f);
                hp += 0.05f * (lp - hp);
                d[i] = (lp - hp) * 0.6f * env + grain * 0.4f * env;
            }
            AddThump(d, 0, 110f, 0.15f, 0.08f);
            return ToClip("Synth_Sugar", d, 0.65f);
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
                float swell = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.42f));
                float release = t > 0.34f ? Mathf.Exp(-(t - 0.34f) * 9f) * 0.5f : 0f;
                lp += 0.06f * (Noise(r) - lp);
                phase += 2 * Mathf.PI * Mathf.Lerp(280f, 85f, t / 0.5f) / SR;
                d[i] = lp * 1.4f * (swell * 0.7f + release) + Mathf.Sin((float)phase) * 0.12f * swell;
            }
            for (int p = 0; p < 6; p++)
            {
                int start = (int)((0.05f + r.NextDouble() * 0.36f) * SR);
                AddRing(d, start, 300 + (float)r.NextDouble() * 500f, 0.2f, 45f, 0.04f);
            }
            return ToClip("Synth_Honey", d, 0.6f);
        }

        /// <summary>Glossy hard tap: click transient + short resonant ping.</summary>
        public static AudioClip NailPolishTap(int seed)
        {
            var r = new System.Random(seed * 13 + 5);
            var d = NewBuf(0.13f);
            float f = 1450f * (0.95f + (float)r.NextDouble() * 0.1f);
            BandNoise(d, 0, 400, 3000f, 9000f, 0.9f, 5200f, r);
            AddRing(d, 0, f, 0.5f, 55f, 0.1f);
            AddRing(d, 0, f * 2.76f, 0.22f, 80f, 0.07f);
            return ToClip("Synth_NailPolish", d, 0.6f);
        }

        /// <summary>The soft material squish for footsteps / soft re-lands.</summary>
        public static AudioClip SquishStep(int seed, bool deep = false)
        {
            var r = new System.Random(seed * 6151 + 29);
            return ToClip(deep ? "Synth_SquishDeep" : "Synth_SquishStep",
                SoftSquishBuf(r, deep ? 0.3f : 0.17f, deep ? 118f : 148f, 0.10f, 0.5f, deep ? 0.7f : 0.5f),
                deep ? 0.6f : 0.35f);
        }

        // ============================== AU6: dreamy ambient bed =============

        static float MidiHz(float midi) => 440f * Mathf.Pow(2f, (midi - 69f) / 12f);

        /// <summary>Warm low-passed pad voice: a few detuned sines through a
        /// soft low-pass with a slow swell/release envelope.</summary>
        static void AddPadVoice(float[] buf, float midi, double start, double len, float lpCut)
        {
            int n = buf.Length;
            int count = (int)(len * SR);
            double f = MidiHz(midi);
            float lp = 0;
            float a = Mathf.Exp(-2f * Mathf.PI * lpCut / SR);
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)SR, x = t / len;
                double env = System.Math.Min(1.0, t / 1.4) * (x > 0.6 ? System.Math.Pow(1.0 - (x - 0.6) / 0.4, 1.3) : 1.0);
                double v = System.Math.Sin(2 * System.Math.PI * f * t) * 0.05
                         + System.Math.Sin(2 * System.Math.PI * f * 1.003 * t) * 0.025
                         + System.Math.Sin(2 * System.Math.PI * f * 0.5 * t) * 0.02; // sub octave warmth
                lp = a * lp + (1f - a) * (float)v;
                buf[((int)(start * SR) + i) % n] += lp * (float)env;
            }
        }

        /// <summary>Soft kalimba/celesta note: low register, mellow partials.</summary>
        static void AddPluck(float[] buf, float midi, double start, float amp)
        {
            int n = buf.Length, count = (int)(2.2 * SR);
            double f = MidiHz(midi);
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)SR;
                double env = System.Math.Exp(-t * 2.6) * System.Math.Min(1.0, t / 0.006);
                double v = (System.Math.Sin(2 * System.Math.PI * f * t)
                          + 0.3 * System.Math.Sin(2 * System.Math.PI * f * 2.01 * t)) * env * amp;
                buf[((int)(start * SR) + i) % n] += (float)v;
            }
        }

        static void AddBass(float[] buf, float midi, double start, double len)
        {
            int n = buf.Length, count = (int)(len * SR);
            double f = MidiHz(midi);
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)SR;
                double env = System.Math.Min(1.0, t / 0.5) * (t > len - 0.8 ? (len - t) / 0.8 : 1.0);
                buf[((int)(start * SR) + i) % n] += (float)(System.Math.Sin(2 * System.Math.PI * f * t) * 0.045 * env);
            }
        }

        /// <summary>
        /// Soft dreamy ambient bed (replaces the bright music box): ~62 BPM
        /// feel, warm low-passed pads breathing over a gentle bass, sparse
        /// low-register kalimba notes - no twinkles. 44 s, seamless loop,
        /// ORIGINAL composition. Cmaj7-Am7-Fmaj7-Gsus, calm and far away.
        /// </summary>
        public static AudioClip DreamyAmbient()
        {
            const double bar = 3.85;                  // ~62 bpm, 4 beats of 0.96 s
            const int bars = 11;
            var buf = new float[(int)(bar * bars * SR)];   // ~42 s loop
            float[][] chords =
            {
                new float[] { 48, 55, 59, 64 }, // Cmaj7
                new float[] { 45, 52, 57, 60 }, // Am7
                new float[] { 41, 48, 52, 57 }, // Fmaj7
                new float[] { 43, 50, 55, 59 }, // G add9-ish
            };
            float[] roots = { 36, 33, 29, 31 };
            for (int b = 0; b < bars; b++)
            {
                var ch = chords[b % 4];
                double at = b * bar;
                foreach (var tone in ch) AddPadVoice(buf, tone, at, bar + 1.2, 900f);
                AddBass(buf, roots[b % 4], at + 0.2, bar - 0.4);
            }
            // sparse kalimba: a few notes every other bar, mid-low register
            var notes = new (int bar, double beat, float midi)[]
            {
                (0, 2.0, 64), (1, 1.0, 60), (1, 3.2, 67), (2, 0.5, 59), (3, 2.4, 64),
                (4, 1.2, 57), (5, 3.0, 60), (6, 0.8, 64), (7, 2.0, 55), (8, 1.5, 60),
                (9, 3.0, 64), (10, 1.0, 57),
            };
            foreach (var (b, bt, m) in notes) AddPluck(buf, m, b * bar + bt * 0.96, 0.12f);
            return ToClip("Synth_DreamyAmbient", buf, 0.5f); // sits under the ASMR
        }
    }
}
