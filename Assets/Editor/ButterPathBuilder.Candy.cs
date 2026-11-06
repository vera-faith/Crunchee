#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Crunchee.Core;
using Crunchee.Game;
using Crunchee.Player;
using Crunchee.CameraRig;
using Crunchee.Audio;
using Crunchee.UI;

namespace Crunchee.EditorTools
{
    /// <summary>
    /// One-click map build. Menu: Crunchee > Build Butter Path Scene
    /// Bakery map, Butter Path. DESIGN TARGET = Faith's UI mockup (Nov 3):
    /// long glossy butter bars on little trays, lined up on a warm peachy
    /// bakery COUNTER scattered with macarons, cupcakes, a flour bowl and a
    /// honey jar; the bunny starts on a white frosting slab with sprinkles;
    /// golden sunlight; UI = MOVE joystick + pink JUMP (already matching).
    /// ONLY the 10 wax butters - each one a little whiter than the last
    /// (wax layers 1..10, translucent build-up). One land per squishy ->
    /// coating cracks -> the character slowly sinks in, and cracks keep
    /// spreading + crackling underfoot wherever she walks on the butter.
    /// </summary>
    public static partial class ButterPathBuilder
    {
        enum CandyKind { CrumbleCake, BlossomJelly, Marshmallow, ChocolateBar, Macaron, CakeTop }

        struct CandyStepDef
        {
            public CandyKind kind; public Color tint;
            public CandyStepDef(CandyKind k, Color c) { kind = k; tint = c; }
        }

        /// <summary>Stage 2 (Candy Path): ten squishies from the reference
        /// videos, after the checkpoint that closes the 10-butter Butter Path.</summary>
        static CandyStepDef[] CandyPath() => new[]
        {
            new CandyStepDef(CandyKind.CrumbleCake, new Color(1f, 0.5f, 0.66f)),
            new CandyStepDef(CandyKind.BlossomJelly, new Color(1f, 0.56f, 0.72f)),
            new CandyStepDef(CandyKind.Marshmallow, new Color(1f, 0.97f, 0.95f)),
            new CandyStepDef(CandyKind.ChocolateBar, new Color(0.46f, 0.27f, 0.16f)),
            new CandyStepDef(CandyKind.Macaron, new Color(0.8f, 0.7f, 1f)),
            new CandyStepDef(CandyKind.CrumbleCake, new Color(0.42f, 0.84f, 0.64f)),
            new CandyStepDef(CandyKind.BlossomJelly, new Color(0.76f, 0.6f, 1f)),
            new CandyStepDef(CandyKind.Marshmallow, new Color(1f, 0.8f, 0.87f)),
            new CandyStepDef(CandyKind.ChocolateBar, new Color(1f, 0.68f, 0.76f)),
            new CandyStepDef(CandyKind.CakeTop, new Color(1f, 0.93f, 0.8f)),
        };

        const float CandyH = 0.8f;
        const float StepTop = ButterH / 2f + PatH; // walk height above a step's slot position

        static void BuildCandyStep(Vector3 pos, int n, CandyStepDef def, float yawDeg, float depth)
        {
            var kind = def.kind; var tint = def.tint;
            var group = new GameObject($"Candy_{n:00}_{kind}");
            group.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yawDeg, 0f));
            float W = ButterW, H = CandyH, D = depth, top = H / 2f;

            BaseMaterial bm; Coating coat = Coating.Plain; Material bodyMat;
            switch (kind)
            {
                case CandyKind.CrumbleCake:
                    bm = BaseMaterial.CakeSponge; coat = Coating.Sugar;
                    bodyMat = Mat($"Sponge{n}", Color.Lerp(tint, new Color(1f, 0.9f, 0.8f), 0.35f), 1f, 0.3f); break;
                case CandyKind.BlossomJelly:
                    bm = BaseMaterial.Jam;
                    bodyMat = Mat($"Jelly{n}", tint, 0.6f, 0.95f);
                    bodyMat.SetFloat("_RimStrength", 0.35f); break;
                case CandyKind.Marshmallow:
                    bm = BaseMaterial.Mochi;
                    bodyMat = Mat($"Marsh{n}", tint, 1f, 0.3f);
                    bodyMat.SetFloat("_RimStrength", 0.5f); break;
                case CandyKind.ChocolateBar:
                    bm = BaseMaterial.Chocolate;
                    bodyMat = Mat($"Choco{n}", new Color(tint.r * 0.82f, tint.g * 0.82f, tint.b * 0.82f), 1f, 0.75f); break;
                case CandyKind.Macaron:
                    bm = BaseMaterial.Macaron;
                    bodyMat = Mat($"MacCream{n}", new Color(1f, 0.97f, 0.92f), 1f, 0.4f); break;
                default:
                    bm = BaseMaterial.CakeSponge;
                    bodyMat = Mat($"CakeSponge{n}", tint, 1f, 0.3f); break;
            }

            bodyMat.SetFloat("_DentAmount", 1f);
            var body = ChildBox(group.transform, "Body", new Vector3(0f, StepTop - H / 2f, 0f), new Vector3(W, H, D), bodyMat, true);
            // Decorations are authored in metres around the body centre and
            // still squash with the body as she sinks in.
            var deco = new GameObject("Deco").transform;
            deco.SetParent(body.transform, false);
            deco.localScale = new Vector3(1f / W, 1f / H, 1f / D);

            float extraTop = 0f;
            GameObject crust = null; Material crustMat = null;
            switch (kind)
            {
                case CandyKind.CrumbleCake:
                {
                    // Sugar crust in uneven chunks that split apart on landing (Squishy.CrackShell).
                    crustMat = Mat($"Crust{n}", tint, 1f, 0.55f);
                    crust = new GameObject("CrumbleCrust");
                    crust.transform.SetParent(deco, false);
                    const int nx = 5, nz = 3; const float ch = 0.16f, gap = 0.06f;
                    for (int ix = 0; ix < nx; ix++)
                        for (int iz = 0; iz < nz; iz++)
                        {
                            float cx = (ix + 0.5f) * W / nx - W / 2f, cz = (iz + 0.5f) * D / nz - D / 2f;
                            var piece = ChildBox(crust.transform, "Crumb",
                                new Vector3(cx, top + ch / 2f - 0.03f + Random.Range(-0.02f, 0.02f), cz),
                                new Vector3(W / nx - gap, ch * Random.Range(0.85f, 1.15f), D / nz - gap), crustMat, false);
                            piece.transform.localRotation = Quaternion.Euler(0f, Random.Range(-6f, 6f), 0f);
                        }
                    extraTop = ch - 0.03f;
                    break;
                }
                case CandyKind.BlossomJelly:
                {
                    var petal = Mat($"Blossom{n}", Color.Lerp(tint, Color.white, 0.55f), 1f, 0.6f);
                    var heart = Mat($"BlossomHeart{n}", new Color(1f, 0.85f, 0.4f), 1f, 0.6f);
                    for (int i = 0; i < 9; i++)
                    {
                        var b = new GameObject("Blossom").transform;
                        b.SetParent(deco, false);
                        b.localPosition = new Vector3(Random.Range(-W * 0.4f, W * 0.4f), Random.Range(-H * 0.28f, H * 0.3f), Random.Range(-D * 0.4f, D * 0.4f));
                        b.localRotation = Quaternion.Euler(Random.Range(-40f, 40f), Random.Range(0f, 360f), Random.Range(-40f, 40f));
                        float s = Random.Range(0.24f, 0.36f);
                        for (int k = 0; k < 5; k++)
                        {
                            float a = k * 72f * Mathf.Deg2Rad;
                            var p = Ball(b, "Petal", new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * s * 0.42f,
                                         new Vector3(s * 0.55f, s * 0.12f, s * 0.42f), petal);
                            p.transform.localRotation = Quaternion.Euler(0f, -k * 72f, 0f);
                        }
                        Ball(b, "Heart", Vector3.zero, Vector3.one * s * 0.25f, heart);
                    }
                    break;
                }
                case CandyKind.Marshmallow:
                {
                    ChildBox(deco, "Puff", new Vector3(0f, top - 0.02f, 0f), new Vector3(W * 0.94f, 0.2f, D * 0.92f), bodyMat, false);
                    var eye = Mat("MarshEye", new Color(0.18f, 0.1f, 0.12f), 1f, 0.9f);
                    var shine = Mat("MarshEyeShine", Color.white, 1f, 0.9f);
                    var blush = Mat("MarshBlush", new Color(1f, 0.55f, 0.65f), 1f, 0.3f);
                    float fz = -D / 2f - 0.02f, fy = 0.15f;
                    for (int sd = -1; sd <= 1; sd += 2)
                    {
                        Ball(deco, "Eye", new Vector3(sd * 0.42f, fy, fz), new Vector3(0.15f, 0.19f, 0.06f), eye);
                        Ball(deco, "EyeShine", new Vector3(sd * 0.42f + 0.03f, fy + 0.04f, fz - 0.025f), new Vector3(0.05f, 0.05f, 0.02f), shine);
                        Ball(deco, "Blush", new Vector3(sd * 0.75f, fy - 0.1f, fz + 0.01f), new Vector3(0.26f, 0.13f, 0.04f), blush);
                    }
                    extraTop = 0.06f;
                    break;
                }
                case CandyKind.ChocolateBar:
                {
                    var segMat = Mat($"ChocoSeg{n}", tint, 1f, 0.85f);
                    const int nx = 4, nz = 3; const float sh = 0.12f, gap = 0.12f;
                    for (int ix = 0; ix < nx; ix++)
                        for (int iz = 0; iz < nz; iz++)
                            ChildBox(deco, "Segment",
                                new Vector3((ix + 0.5f) * W / nx - W / 2f, top + sh / 2f - 0.01f, (iz + 0.5f) * D / nz - D / 2f),
                                new Vector3(W / nx - gap, sh, D / nz - gap), segMat, false);
                    extraTop = sh - 0.01f;
                    break;
                }
                case CandyKind.Macaron:
                {
                    var shellM = Mat($"MacShell{n}", tint, 1f, 0.45f);
                    var footM = Mat($"MacFoot{n}", Color.Lerp(tint, Color.white, 0.25f), 1f, 0.2f);
                    ChildBox(deco, "ShellBottom", new Vector3(0f, -top + H * 0.19f, 0f), new Vector3(W + 0.12f, H * 0.38f, D + 0.12f), shellM, false);
                    ChildBox(deco, "ShellTop", new Vector3(0f, top - H * 0.2f + 0.04f, 0f), new Vector3(W + 0.12f, H * 0.4f + 0.08f, D + 0.12f), shellM, false);
                    ChildBox(deco, "Foot", new Vector3(0f, top - H * 0.4f, 0f), new Vector3(W + 0.2f, 0.06f, D + 0.2f), footM, false);
                    extraTop = 0.04f;
                    break;
                }
                default: // CakeTop
                {
                    var frost = Mat("CakeFrostPink", new Color(1f, 0.72f, 0.82f), 1f, 0.7f);
                    var cream = Mat("WhipCream", new Color(1f, 0.98f, 0.95f), 1f, 0.45f);
                    var cherry = Mat("CakeCherry", new Color(0.88f, 0.1f, 0.2f), 1f, 0.95f);
                    ChildBox(deco, "Frosting", new Vector3(0f, top + 0.05f, 0f), new Vector3(W + 0.08f, 0.16f, D + 0.08f), frost, false);
                    for (int i = 0; i < 9; i++)
                    {
                        float x = Mathf.Lerp(-W / 2f + 0.25f, W / 2f - 0.25f, i / 8f), len = Random.Range(0.12f, 0.3f);
                        Ball(deco, "Drip", new Vector3(x, top - len * 0.5f, -D / 2f - 0.03f), new Vector3(0.22f, len + 0.12f, 0.1f), frost);
                    }
                    for (int i = 0; i < 5; i++)
                        for (int sd = -1; sd <= 1; sd += 2)
                        {
                            var p = new Vector3(Mathf.Lerp(-W / 2f + 0.4f, W / 2f - 0.4f, i / 4f), top + 0.22f, sd * (D / 2f - 0.32f));
                            Ball(deco, "Dollop", p, new Vector3(0.38f, 0.26f, 0.38f), cream);
                            Ball(deco, "DollopTip", p + new Vector3(0f, 0.13f, 0f), new Vector3(0.18f, 0.16f, 0.18f), cream);
                            if ((i + (sd > 0 ? 1 : 0)) % 2 == 0)
                                Ball(deco, "Cherry", p + new Vector3(0f, 0.3f, 0f), Vector3.one * 0.2f, cherry);
                        }
                    var sprinkles = new[]
                    {
                        Mat("SprinklePink", new Color(1f, 0.6f, 0.72f), 1f, 0.6f),
                        Mat("SprinkleMint", new Color(0.6f, 0.9f, 0.75f), 1f, 0.6f),
                        Mat("SprinkleBlue", new Color(0.6f, 0.8f, 1f), 1f, 0.6f),
                        Mat("SprinkleYellow", new Color(1f, 0.88f, 0.45f), 1f, 0.6f),
                    };
                    for (int i = 0; i < 46; i++)
                    {
                        var s = Ball(deco, "Sprinkle", new Vector3(Random.Range(-W * 0.42f, W * 0.42f), top + 0.135f, Random.Range(-D * 0.3f, D * 0.3f)),
                                     new Vector3(0.04f, 0.035f, 0.13f), sprinkles[i % sprinkles.Length]);
                        s.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 180f), 0f);
                    }
                    extraTop = 0.13f;
                    break;
                }
            }

            if (extraTop > 0f)
            {
                var col = body.GetComponent<BoxCollider>();
                col.size = new Vector3(1f, (H + extraTop) / H, 1f);
                col.center = new Vector3(0f, extraTop / 2f / H, 0f);
            }

            var sq = body.AddComponent<Squishy>();
            sq.baseMaterial = bm; sq.coating = coat;
            sq.stage = 2;
            sq.pathOrder = ++pathCounter;
            if (crust != null) { sq.coatingShell = crust; sq.shardMaterial = crustMat; }

            var ps = new GameObject("StepParticles").AddComponent<ParticleSystem>();
            ps.transform.SetParent(deco, false);
            ps.transform.localPosition = new Vector3(0f, top + 0.1f, 0f);
            var main = ps.main; main.startColor = Color.Lerp(tint, Color.white, 0.4f); main.startSize = 0.1f;
            main.startSpeed = 2.2f; main.startLifetime = 0.7f; main.playOnAwake = false;
            var em = ps.emission; em.enabled = false;
            ps.GetComponent<ParticleSystemRenderer>().material = SparkleParticleMat(false);
            sq.stepParticles = ps;
        }

        /// <summary>Pink checkpoint pad between stages: respawn point plus a
        /// one-time sparkle burst when she first reaches it.</summary>
        static void BuildCheckpointPad(Vector3 slotPos, float yawDeg)
        {
            var root = new GameObject("Checkpoint_CandyPath").transform;
            root.SetPositionAndRotation(slotPos, Quaternion.Euler(0f, yawDeg, 0f));
            const float S = 4.2f, T = 0.4f;
            float top = StepTop;
            var padMat = Mat("CheckpointPink", new Color(1f, 0.62f, 0.78f), 1f, 0.7f);
            ChildBox(root, "Pad", new Vector3(0f, top - T / 2f, 0f), new Vector3(S, T, S), padMat, true);
            Cyl(root, "PadRing", new Vector3(0f, top + 0.004f, 0f), new Vector3(3.6f, 0.006f, 3.6f), EmissiveMat("CheckpointGlow", new Color(1f, 0.9f, 0.96f), 1.6f));
            Cyl(root, "PadInset", new Vector3(0f, top + 0.012f, 0f), new Vector3(3.2f, 0.006f, 3.2f), Mat("CheckpointInset", new Color(1f, 0.78f, 0.88f), 1f, 0.8f));

            // Lollipop marker on the outer edge so the checkpoint reads from below.
            var stickMat = Mat("LollipopStick", new Color(1f, 0.98f, 0.95f), 1f, 0.5f);
            Cyl(root, "LollipopStick", new Vector3(S / 2f - 0.35f, top + 0.9f, 0f), new Vector3(0.08f, 0.9f, 0.08f), stickMat);
            var pop = Ball(root, "Lollipop", new Vector3(S / 2f - 0.35f, top + 1.95f, 0f), new Vector3(0.25f, 0.75f, 0.75f), EmissiveMat("LollipopGlow", new Color(1f, 0.55f, 0.75f), 1.3f));
            Cyl(root, "LollipopSwirl", pop.transform.localPosition, new Vector3(0.6f, 0.13f, 0.6f), stickMat).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            var zone = new GameObject("CheckpointZone");
            zone.transform.SetParent(root, false);
            zone.transform.localPosition = new Vector3(0f, top + 1.1f, 0f);
            var bc = zone.AddComponent<BoxCollider>();
            bc.size = new Vector3(S, 2.2f, S); bc.isTrigger = true;
            var cp = zone.AddComponent<Checkpoint>();
            cp.killHeight = -4.5f;

            var burst = new GameObject("CheckpointBurst").AddComponent<ParticleSystem>();
            burst.transform.SetParent(root, false);
            burst.transform.localPosition = new Vector3(0f, top + 0.3f, 0f);
            burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = burst.main;
            m.playOnAwake = false; m.loop = false; m.duration = 1f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6.5f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.8f), new Color(1f, 0.97f, 0.85f));
            m.gravityModifier = 0.7f;
            var em = burst.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)80) });
            var sh = burst.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.8f; sh.rotation = new Vector3(-90f, 0f, 0f);
            burst.GetComponent<ParticleSystemRenderer>().material = SparkleParticleMat(true);
            cp.burst = burst;

            var idle = new GameObject("CheckpointSparkle").AddComponent<ParticleSystem>();
            idle.transform.SetParent(root, false);
            idle.transform.localPosition = new Vector3(0f, top + 0.1f, 0f);
            var im = idle.main;
            im.startLifetime = 1.8f; im.startSpeed = 0f; im.gravityModifier = -0.06f;
            im.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            im.startColor = new Color(1f, 0.92f, 0.97f);
            var iem = idle.emission; iem.rateOverTime = 7f;
            var ish = idle.shape; ish.shapeType = ParticleSystemShapeType.Box; ish.scale = new Vector3(3.4f, 0.05f, 3.4f);
            idle.GetComponent<ParticleSystemRenderer>().material = SparkleParticleMat(true);
        }
    }
}
#endif
