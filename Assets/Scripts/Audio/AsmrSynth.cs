using UnityEngine;
using Crunchee.Core;

namespace Crunchee.Audio
{
    /// <summary>
    /// Original procedural ASMR synthesis. A wax crack is a dense Poisson
    /// swarm of distinct sub-millisecond clicks (two-pole resonators excited
    /// by tapered impulses) with power-law sizes over a crunchy low body -
    /// matched to the TikTok reference where every footstep on wax reads as
    /// a continuous crackle and a full landing as a broadband crunch. Every
    /// base material has its own character; wax layers 1..10 morph
    /// continuously from thin glassy crackle to deep chunky crunches.
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

        // ============================== AU2: wax crackle swarms ===========

        /// <summary>ONE wax micro-fracture: a two-pole resonator at fRes
        /// (tau in ms) excited by a short tapered noise impulse. The output
        /// mixes the resonator with a little of the raw impulse for
        /// broadband crispness, then normalizes so amp IS the click's peak
        /// (resonator gain varies wildly with fRes, which otherwise makes
        /// low clicks swamp the power-law sizes).</summary>
        static void AddClick(float[] d, int at, float amp, float fRes, float tauMs, System.Random r)
        {
            double rad = System.Math.Exp(-1.0 / (tauMs * 0.001 * SR));
            double c1 = 2 * rad * System.Math.Cos(2 * System.Math.PI * fRes / SR);
            double c2 = -rad * rad;
            int impLen = 2 + r.Next(5);                       // 2-6 samples
            int n = (int)(6 * tauMs * 0.001 * SR) + 8;
            var mix = new float[n];
            double y1 = 0, y2 = 0;
            double scale = (1 - rad) * 4.0;
            float pk = 0.000000001f;
            for (int i = 0; i < n; i++)
            {
                double x = i < impLen ? Noise(r) * (1f - i / (float)impLen) : 0;
                double y = x + c1 * y1 + c2 * y2;
                y2 = y1; y1 = y;
                mix[i] = (float)(y * scale + x * 0.12);
                pk = Mathf.Max(pk, Mathf.Abs(mix[i]));
            }
            for (int i = 0; i < n && at + i < d.Length; i++)
                if (at + i >= 0) d[at + i] += mix[i] * amp / pk;
        }

        /// <summary>A Poisson swarm of clicks: rate shaped by a rise/hold/
        /// taper envelope over [t0, t0+dur], sizes Pareto (alpha 1.15) with
        /// a mid-swell, frequencies log-uniform with big clicks lower, one
        /// quarter spawning micro-burst children, every click adding a low
        /// body grain, plus one hero snap at heroPos through the swarm.
        /// </summary>
        static void CrackleSwarm(float[] d, float t0, float dur, float rate,
                                 float fLo, float fHi, float tauLo, float tauHi,
                                 float bodyAmt, float amp, float heroPos, System.Random r)
        {
            float t = t0;
            int guard = 0;
            while (guard++ < 100000)
            {
                float x = Mathf.Clamp01((t - t0) / dur);
                float shape = x < 0.06f ? x / 0.06f
                            : x > 0.75f ? 1f - 0.6f * (x - 0.75f) / 0.25f
                            : 1f;
                if (shape < 0.05f) shape = 0.05f;
                t += (float)(-System.Math.Log(r.NextDouble() * 0.999999 + 0.000001) / (rate * shape));
                if (t >= t0 + dur) break;
                float xc = (t - t0) / dur;
                float rel = Mathf.Min(13f, Mathf.Pow((float)r.NextDouble(), -1f / 1.15f));
                float a = amp * 0.10f * Mathf.Pow(rel, 1.25f) * (0.7f + 0.3f * Mathf.Sin(Mathf.PI * xc));
                float fRes = Mathf.Max(900f, fLo * Mathf.Pow(fHi / fLo, (float)r.NextDouble())
                                           * Mathf.Pow(rel, -0.10f));
                float tau = tauLo * Mathf.Pow(tauHi / tauLo, (float)r.NextDouble());
                int at = (int)(t * SR);
                AddClick(d, at, a, fRes, tau, r);
                // every click drags a little 150-1200 Hz body grain
                BandNoise(d, at, (int)((0.003f + (float)r.NextDouble() * 0.005f) * SR),
                          150f, 1200f, a * bodyAmt, 500f, r);
                // micro-burst: 1-3 children within the next 0.3-1.5 ms
                if (r.NextDouble() < 0.25)
                {
                    int kids = 1 + r.Next(3);
                    for (int k = 0; k < kids; k++)
                    {
                        int cat = (int)((t + (0.0003f + (float)r.NextDouble() * 0.0012f)) * SR);
                        float cf = Mathf.Max(900f, fLo * Mathf.Pow(fHi / fLo, (float)r.NextDouble()));
                        AddClick(d, cat, a * (0.3f + (float)r.NextDouble() * 0.4f), cf,
                                 tauLo * Mathf.Pow(tauHi / tauLo, (float)r.NextDouble()), r);
                    }
                }
            }
            // hero snap: the one big fracture every crack needs, placed in
            // the 4-6 kHz detection band so it always reads as THE snap
            float hx = heroPos;
            int hat = (int)((t0 + hx * dur) * SR);
            float ha = amp * 0.08f * 42f * (0.7f + 0.3f * Mathf.Sin(Mathf.PI * hx));
            AddClick(d, hat, ha, Mathf.Lerp(4000f, 6000f, (float)r.NextDouble()), 2.5f, r);
            BandNoise(d, hat, (int)((0.003f + (float)r.NextDouble() * 0.005f) * SR),
                      150f, 1200f, ha * bodyAmt * 2f, 500f, r);
        }

        /// <summary>Peak-normalize, soft-saturate, a gentle one-pole
        /// low-pass to seat the clicks in the reference's darker band,
        /// then clip at target.</summary>
        static AudioClip CrackClip(string name, float[] d, float target, float lpHz = 6000f)
        {
            float pk = 0.0001f;
            foreach (var v in d) pk = Mathf.Max(pk, Mathf.Abs(v));
            float t = Mathf.Tanh(1.25f);
            float aL = Mathf.Exp(-2f * Mathf.PI * lpHz / SR);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                lp = aL * lp + (1f - aL) * Mathf.Tanh(1.25f * d[i] / pk) / t;
                d[i] = lp;
            }
            return ToClip(name, d, target);
        }

        /// <summary>
        /// ONE landing fracture for layer n, matched to the reference: a
        /// ~0.5-0.8 s dense swarm of distinct sub-ms clicks (210-280/s,
        /// power-law sized) over a crunchy low body, one hero snap early,
        /// chunk snaps and a body thump as wax thickens, then a sparse
        /// settle tail. Thin wax reads bright and glassy; thick wax drops
        /// the band and adds weight. Deterministic.
        /// </summary>
        public static AudioClip WaxCrack(int layers, int seed)
        {
            layers = Mathf.Clamp(layers, 1, 10);
            var r = new System.Random(seed * 104729 + layers * 131);
            float f = (layers - 1) / 9f;                       // 0 thin .. 1 thick
            float dur = Mathf.Lerp(0.55f, 0.85f, f);
            var d = NewBuf(dur + 0.15f);
            float fLo = Mathf.Lerp(2200f, 1700f, f), fHi = Mathf.Lerp(7500f, 5500f, f);
            float tauLo = Mathf.Lerp(0.8f, 1.1f, f), tauHi = Mathf.Lerp(2.0f, 3.0f, f);
            CrackleSwarm(d, 0.005f, dur, Mathf.Lerp(450f, 480f, f), fLo, fHi,
                         tauLo, tauHi, Mathf.Lerp(2.3f, 2.9f, f), 1f,
                         0.25f + (float)r.NextDouble() * 0.15f, r);
            if (f > 0.3f)
            {
                // chunk snaps: big plates letting go through the crunch
                int chunks = 1 + Mathf.RoundToInt(3f * f);
                for (int s = 0; s < chunks; s++)
                {
                    int at = (int)((0.1f + (float)r.NextDouble() * 0.6f) * dur * SR);
                    AddClick(d, at, 0.55f, Mathf.Lerp(1500f, 2500f, (float)r.NextDouble()), 2.5f, r);
                    AddRing(d, at, Mathf.Lerp(2200f, 900f, f) * (0.85f + (float)r.NextDouble() * 0.3f),
                            0.15f, 250f, 0.05f);
                }
                AddThump(d, 0, Mathf.Lerp(150f, 95f, f), 0.25f * f, 0.1f);
            }
            // sparse settle tail
            CrackleSwarm(d, 0.005f + dur, 0.1f, 40f, fLo, fHi, tauLo, tauHi,
                         Mathf.Lerp(2.3f, 2.9f, f), 0.35f, 0.5f, r);
            return CrackClip($"Synth_Wax_{layers}", d, 0.95f);
        }

        /// <summary>
        /// One footstep on wax: a ~0.4-0.52 s crackle swarm, sparser than a
        /// landing but the same anatomy - distinct clicks, power-law sizes,
        /// hero snap mid-step, crunchy low body. Fired every step so walking
        /// reads as continuous crackle like the reference.
        /// </summary>
        public static AudioClip WaxStepCrack(int layers, int seed)
        {
            layers = Mathf.Clamp(layers, 1, 10);
            var r = new System.Random(seed * 31337 + layers * 71);
            float f = (layers - 1) / 9f;
            float dur = Mathf.Lerp(0.4f, 0.52f, f);
            var d = NewBuf(dur + 0.15f);
            CrackleSwarm(d, 0.003f, dur, Mathf.Lerp(330f, 420f, f),
                         Mathf.Lerp(2400f, 2100f, f), Mathf.Lerp(5000f, 4300f, f),
                         Mathf.Lerp(0.6f, 0.8f, f), Mathf.Lerp(1.6f, 2.5f, f),
                         Mathf.Lerp(1.7f, 2.0f, f), 1f,
                         0.35f + (float)r.NextDouble() * 0.2f, r);
            return CrackClip("Synth_WaxStep", d, 0.85f, 5000f);
        }

        // ============================== AU3: per-material bases ============

        /// <summary>
        /// Soft squish voice. The gummy character comes from the reference's
        /// clear signature: a resonant BODY tone ringing ~480-700 Hz that
        /// pulses with a jelly WOBBLE at ~20-30 Hz decaying over ~0.3 s, plus
        /// mid-band squelch noise. Energy peaks in the 300-800 Hz band and
        /// rolls off fast above ~1.5 kHz so it stays chewy, not crispy.
        /// </summary>
        static float[] SoftSquishBuf(System.Random r, float dur, float f0, float foam,
                                     float squelchAmt, float thump, float hiss = 0f,
                                     float wobHz = 0f, float wobAmp = 0f,
                                     float midAmt = 0f, float midLo = 280f, float midHi = 1150f,
                                     float bodyHz = 0f, float bodyAmp = 0f, float lpGain = 0.6f)
        {
            var d = NewBuf(dur);
            float lp = 0, lp2 = 0, mlp = 0, mhp = 0;
            float aML = Mathf.Exp(-2f * Mathf.PI * midHi / SR);
            float aMH = Mathf.Exp(-2f * Mathf.PI * midLo / SR);
            double ph = 0, ph2 = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR, x = t / dur;
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(x, 0.7f));
                env *= env;
                lp += foam * (Noise(r) - lp);
                lp2 += 0.35f * (Noise(r) - lp2);
                float n3 = Noise(r);
                mlp = aML * mlp + (1f - aML) * n3;
                mhp = aMH * mhp + (1f - aMH) * mlp;
                // decaying jiggle: starts at a peak so the press lands hard,
                // then 5-7 pulses ride the squelch down
                float wob = wobHz > 0f
                    ? (1f - wobAmp + wobAmp * Mathf.Sin(2f * Mathf.PI * wobHz * t + Mathf.PI * 0.5f)) * Mathf.Exp(-x * 1.6f)
                    : 1f;
                ph += 2 * Mathf.PI * Mathf.Lerp(f0, f0 * 0.4f, x) / SR;
                // resonant gummy body: the "boing" of the jelly, pulsing with
                // the jiggle and decaying slower than the squelch so it reads
                ph2 += 2 * Mathf.PI * bodyHz / SR;
                float body = bodyHz > 0f
                    ? Mathf.Sin((float)ph2) * Mathf.Exp(-x * 4.5f) * (0.5f + 0.5f * wob) * bodyAmp
                    : 0f;
                float hissEnv = hiss * Mathf.Exp(-Mathf.Pow((x - 0.62f) * 5f, 2f)); // air release near the end
                d[i] = (lp * lpGain + (lp2 - lp) * squelchAmt + Mathf.Sin((float)ph) * thump
                        + (mlp - mhp) * midAmt * (0.45f + 0.55f * wob) + body
                        + lp2 * hissEnv * 0.5f) * env;
            }
            return d;
        }

        /// <summary>Dense creamy gummy squish - a 560 Hz jiggle "boing" over
        /// soft squelch (Butter, also Croissant/Donut/Toast variants).</summary>
        public static AudioClip ButterBase(int seed)
        {
            var r = new System.Random(seed * 7919 + 13);
            return ToClip("Synth_Butter",
                SoftSquishBuf(r, 0.26f, 130f, 0.06f, 0.45f, 0.05f, 0f, 23f, 0.45f, 0.7f,
                              280f, 1150f, 560f, 1.6f), 0.6f);
        }

        /// <summary>Airy foam press, brighter gummy ring, soft air-hiss release
        /// (CakeSponge).</summary>
        static AudioClip SpongeBase(int seed)
        {
            var r = new System.Random(seed * 3919 + 17);
            return ToClip("Synth_Sponge",
                SoftSquishBuf(r, 0.3f, 170f, 0.14f, 0.6f, 0.04f, 1f, 21f, 0.3f, 0.55f,
                              280f, 1150f, 700f, 1.2f), 0.55f);
        }

        /// <summary>Wet gummy squelch with the strongest jiggle and a bubble
        /// (Jam/Grape/Melon - the BlossomJelly squishies).</summary>
        static AudioClip JamBase(int seed)
        {
            var r = new System.Random(seed * 2377 + 11);
            var d = SoftSquishBuf(r, 0.36f, 150f, 0.06f, 0.9f, 0.05f, 0f, 26f, 0.55f, 0.9f,
                                  280f, 1150f, 640f, 1.7f);
            // one little bubble pop mid-squelch
            int st = (int)((0.1f + (float)r.NextDouble() * 0.12f) * SR);
            AddRing(d, st, 900 + (float)r.NextDouble() * 500f, 0.3f, 60f, 0.04f);
            return ToClip("Synth_Jam", d, 0.55f);
        }

        /// <summary>Soft rubbery press, darker 480 Hz jiggle, muted squeak,
        /// low thud (Mochi/PawPad - the Marshmallow squishies).</summary>
        static AudioClip MochiBase(int seed)
        {
            var r = new System.Random(seed * 1291 + 19);
            var d = SoftSquishBuf(r, 0.3f, 105f, 0.05f, 0.3f, 0.1f, 0f, 20f, 0.4f, 0.5f,
                                  280f, 1150f, 480f, 1.4f);
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

        /// <summary>The soft material squish for footsteps / soft re-lands.
        /// Gummy mid-focus with a light jiggle - on unwaxed squishies this is
        /// the whole sound, so it carries the chew.</summary>
        public static AudioClip SquishStep(int seed, bool deep = false)
        {
            var r = new System.Random(seed * 6151 + 29);
            return ToClip(deep ? "Synth_SquishDeep" : "Synth_SquishStep",
                SoftSquishBuf(r, deep ? 0.32f : 0.2f, deep ? 118f : 148f,
                              deep ? 0.08f : 0.07f, 0.5f,
                              deep ? 0.12f : 0.05f, 0f, deep ? 22f : 25f,
                              deep ? 0.5f : 0.35f, deep ? 0.7f : 0.55f, 280f, 1150f,
                              deep ? 560f : 620f, deep ? 1.3f : 1.1f),
                deep ? 0.6f : 0.35f);
        }

        // ============================== AU6: cozy tune ====================

        static float MidiHz(float midi) => 440f * Mathf.Pow(2f, (midi - 69f) / 12f);

        /// <summary>
        /// ORIGINAL cozy tune (C mixolydian-flavoured): a sustained
        /// near-pure-tone "ocarina/whistle" lead - one continuous
        /// phase-accumulating oscillator with slurred slides between notes,
        /// trills, slow-blooming vibrato and a breathy edge - over a held
        /// bass pedal and a soft breathing pad. NO drums and nothing that
        /// decays like a piano, so the wax cracks always stay clear on top.
        /// 112 BPM, 16 bars (~34.3 s), seamless loop (all wrap tails written
        /// modulo the buffer length).
        /// </summary>
        public static AudioClip CozyTune()
        {
            var r = new System.Random(7777);
            const double beat = 60.0 / 112.0;
            const double bar = beat * 4;
            const int bars = 16;
            int n = (int)(bar * bars * SR);
            var buf = new float[n];
            var lead = new float[n];

            int[][] pads =
            {
                new[] { 55, 62, 64, 67 }, new[] { 55, 62, 64, 67 },
                new[] { 58, 62, 65 }, new[] { 58, 62, 65 },
                new[] { 57, 60, 65 }, new[] { 57, 60, 65 },
                new[] { 57, 60, 62, 65 }, new[] { 58, 62, 65, 67 },
                new[] { 55, 60, 64, 67 }, new[] { 57, 60, 64, 65 },
                new[] { 57, 62, 65, 69 }, new[] { 55, 60, 64, 67 },
                new[] { 55, 60, 64, 67 }, new[] { 57, 60, 62, 65 },
                new[] { 57, 62, 65, 69 }, new[] { 58, 62, 65, 67 },
            };
            int[] bassRoots = { 36, 36, 36, 36, 36, 36, 36, 36,
                                33, 38, 34, 36, 33, 38, 34, 36 };

            var melody = new (int bar, double beat, int midi, double dur)[]
            {
                (0, 0.0, 76, 0.5), (0, 0.5, 74, 0.5), (0, 1.0, 72, 1.0),
                (0, 2.0, 67, 0.25), (0, 2.25, 69, 1.75),
                (1, 1.0, 72, 0.5), (1, 1.5, 74, 0.5), (1, 2.0, 76, 0.15),
                (1, 2.15, 77, 0.15), (1, 2.3, 76, 0.15), (1, 2.45, 77, 0.15),
                (1, 2.6, 76, 1.4),
                (2, 0.0, 77, 1.0), (2, 1.0, 74, 0.5), (2, 1.5, 70, 0.5),
                (2, 2.0, 74, 2.0),
                (3, 0.0, 72, 0.5), (3, 0.5, 74, 0.5), (3, 1.0, 76, 0.25),
                (3, 1.25, 77, 1.75),
                (4, 0.0, 81, 1.5), (4, 1.5, 79, 0.5), (4, 2.0, 77, 0.5),
                (4, 2.5, 72, 1.5),
                (5, 0.5, 77, 0.5), (5, 1.0, 79, 0.5), (5, 1.5, 81, 0.25),
                (5, 1.75, 82, 0.25), (5, 2.0, 81, 0.15), (5, 2.15, 82, 0.15),
                (5, 2.3, 81, 1.7),
                (6, 0.0, 77, 1.0), (6, 1.0, 74, 0.5), (6, 1.5, 72, 0.5),
                (6, 2.0, 69, 1.0), (6, 3.0, 72, 0.5), (6, 3.5, 74, 0.5),
                (7, 0.0, 74, 2.0), (7, 3.5, 67, 0.5),
                (8, 0.0, 72, 0.5), (8, 0.5, 76, 0.5), (8, 1.0, 79, 1.5),
                (8, 2.5, 76, 0.5), (8, 3.0, 74, 1.0),
                (9, 0.0, 76, 0.25), (9, 0.25, 77, 1.25), (9, 1.5, 76, 0.5),
                (9, 2.0, 74, 0.5), (9, 2.5, 72, 0.5), (9, 3.0, 69, 1.0),
                (10, 0.0, 74, 1.5), (10, 1.5, 77, 0.5), (10, 2.0, 81, 0.15),
                (10, 2.15, 82, 0.15), (10, 2.3, 81, 1.7),
                (11, 0.0, 79, 1.0), (11, 1.0, 76, 0.5), (11, 1.5, 72, 0.5),
                (11, 2.0, 67, 0.25), (11, 2.25, 69, 0.25), (11, 2.5, 72, 1.5),
                (12, 0.0, 76, 0.5), (12, 0.5, 79, 0.5), (12, 1.0, 81, 1.5),
                (12, 2.5, 79, 0.5), (12, 3.0, 76, 1.0),
                (13, 0.0, 77, 0.5), (13, 0.5, 76, 0.5), (13, 1.0, 74, 0.5),
                (13, 1.5, 72, 0.5), (13, 2.0, 74, 0.15), (13, 2.15, 76, 0.15),
                (13, 2.3, 74, 1.7),
                (14, 0.0, 77, 1.5), (14, 1.5, 74, 0.5), (14, 2.0, 70, 0.5),
                (14, 2.5, 72, 0.5), (14, 3.0, 74, 1.0),
                (15, 0.0, 72, 2.5),
            };

            // ---------- pad: soft breathing voicing per bar ----------
            for (int b = 0; b < bars; b++)
            {
                foreach (var m in pads[b])
                {
                    double f = MidiHz(m);
                    int st = (int)(b * bar * SR), len = (int)((bar + 0.6) * SR);
                    for (int i = 0; i < len; i++)
                    {
                        double t = i / (double)SR;
                        double env = System.Math.Min(1.0, t / 0.35)
                                   * System.Math.Min(1.0, (len - i) / (0.6 * SR));
                        double trem = 1.0 + 0.1 * System.Math.Sin(2 * System.Math.PI * 0.15 * (b * bar + t));
                        // absolute-time phase: a note held into the next bar
                        // overlaps in phase instead of cancelling
                        double ph = 2 * System.Math.PI * f * (st + i) / SR;
                        buf[(st + i) % n] += (float)((System.Math.Sin(ph)
                            + 0.04 * System.Math.Sin(2 * ph)) * env * trem * 0.045);
                    }
                }
            }

            // ---------- bass pedal: per-bar root + octave, half-bar bounce ----------
            for (int b = 0; b < bars; b++)
            {
                double f = MidiHz(bassRoots[b]);
                int st = (int)(b * bar * SR), len = (int)((bar + 0.08) * SR);
                for (int i = 0; i < len; i++)
                {
                    double t = i / (double)SR;
                    double env = System.Math.Min(1.0, t / 0.08)
                               * System.Math.Min(1.0, (len - i) / (0.08 * SR));
                    double half = t % (bar * 0.5);
                    env *= 1.0 - 0.1 * System.Math.Exp(-half / 0.12);
                    double ph = 2 * System.Math.PI * f * (st + i) / SR; // in phase across bars
                    buf[(st + i) % n] += (float)((System.Math.Sin(ph)
                        + 0.4 * System.Math.Sin(2 * ph)) * env * 0.16);
                }
            }

            // ---------- ocarina lead: one continuous oscillator ----------
            double aPitch = System.Math.Exp(-1.0 / (0.028 * SR)); // 28 ms slur
            double aAtk = System.Math.Exp(-1.0 / (0.070 * SR));
            double aRel = System.Math.Exp(-1.0 / (0.090 * SR));
            double lgF = System.Math.Log(MidiHz(melody[0].midi)), lgT = lgF;
            double ph2 = 0, ampEnv = 0;
            float bLp = 0, bHp = 0;
            float aBL = Mathf.Exp(-2f * Mathf.PI * 3500f / SR);
            float aBH = Mathf.Exp(-2f * Mathf.PI * 1200f / SR);
            int ni = 0;
            double curEnd = -1, lastOnset = -1, prevEnd = -1;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SR;
                if (ni < melody.Length && t >= (melody[ni].bar * bar + melody[ni].beat * beat))
                {
                    double st2 = melody[ni].bar * bar + melody[ni].beat * beat;
                    bool legato = prevEnd >= 0 && st2 - prevEnd <= 0.02;
                    if (!legato) lastOnset = t;
                    prevEnd = st2 + melody[ni].dur * beat;
                    curEnd = prevEnd;
                    lgT = System.Math.Log(MidiHz(melody[ni].midi));
                    ni++;
                }
                lgF = aPitch * lgF + (1 - aPitch) * lgT;
                bool gate = t < curEnd;
                ampEnv += ((gate ? 1.0 : 0.0) - ampEnv) * (1 - (gate ? aAtk : aRel));
                double vibDepth = lastOnset >= 0 ? System.Math.Min(1.0, (t - lastOnset) / 0.25) : 0;
                double vib = System.Math.Sin(2 * System.Math.PI * 5.0 * t) * vibDepth * 14.0; // cents
                ph2 += 2 * System.Math.PI * System.Math.Exp(lgF) * System.Math.Pow(2, vib / 1200.0) / SR;
                float breath = Noise(r);
                bLp = aBL * bLp + (1f - aBL) * breath;
                bHp = aBH * bHp + (1f - aBH) * bLp;
                double breathGain = 0.02 * ampEnv;
                lead[i] = (float)(((System.Math.Sin(ph2) + 0.05 * System.Math.Sin(2 * ph2)
                    + 0.02 * System.Math.Sin(3 * ph2)) * ampEnv * 0.16)
                    + (bLp - bHp) * breathGain);
            }

            // ---------- echo on the lead only: 0.2 and 0.4 beat taps, LP'd ----------
            int d1 = (int)(0.2 * beat * SR), d2 = (int)(0.4 * beat * SR);
            float aEL = Mathf.Exp(-2f * Mathf.PI * 2500f / SR);
            var lpLead = new float[n];
            float eLp = 0;
            for (int i = 0; i < n; i++)
            {
                eLp = aEL * eLp + (1f - aEL) * lead[i];
                lpLead[i] = eLp;
            }
            for (int i = 0; i < n; i++)
                buf[i] += lead[i] + lpLead[(i - d1 + n) % n] * 0.10f + lpLead[(i - d2 + n) % n] * 0.05f;

            return ToClip("Synth_Cozy", buf, 0.14f);
        }
    }
}
