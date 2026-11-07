#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Crunchee.Core;
using Crunchee.Game;

namespace Crunchee.EditorTools
{
    /// <summary>
    /// The warm golden-hour bakery around the climb (visual overhaul,
    /// "mix of both"): cream plaster walls with wood trims and shelves of
    /// pastries, warm window panes, a soft low-contrast checker floor kept
    /// as the pink accent, croissants + cinnamon rolls and the bakery prop
    /// scatter along the floor - plus the finish-cake dressing at the top.
    /// Everything sits BELOW the path's contrast/saturation so the butters
    /// and the bunny pop.
    /// </summary>
    public static partial class ButterPathBuilder
    {
        static void BuildMacarons(Vector3 at)
        {
            var cols = new[]
            {
                new Color(1f, 0.65f, 0.75f), new Color(0.65f, 0.9f, 0.78f),
                new Color(0.8f, 0.75f, 0.98f), new Color(1f, 0.88f, 0.55f),
            };
            var fill = Mat("MacaronFill", new Color(1f, 0.98f, 0.94f), 1f, 0.5f);
            var g = new GameObject("Macarons"); g.transform.position = at;
            for (int i = 0; i < 3; i++)
            {
                var m = Mat($"Macaron{i}", cols[(i + (int)(at.z)) % cols.Length], 1f, 0.55f);
                var one = new GameObject("Macaron"); one.transform.SetParent(g.transform, false);
                one.transform.localPosition = new Vector3((i - 1) * 0.42f, 0.02f, (i % 2) * 0.3f - 0.15f);
                Part(PrimitiveType.Sphere, one.transform, "ShellB", new Vector3(0f, 0.05f, 0f), new Vector3(0.36f, 0.1f, 0.36f), m);
                Part(PrimitiveType.Cylinder, one.transform, "Fill", new Vector3(0f, 0.1f, 0f), new Vector3(0.31f, 0.045f, 0.31f), fill);
                Part(PrimitiveType.Sphere, one.transform, "ShellT", new Vector3(0f, 0.15f, 0f), new Vector3(0.36f, 0.1f, 0.36f), m);
            }
        }

        static GameObject BuildCupcake(Vector3 at, Material frosting, Material cherryMat)
        {
            var wrap = Mat("CupcakeWrap", new Color(0.92f, 0.72f, 0.6f), 1f, 0.5f);
            var g = new GameObject("Cupcake"); g.transform.position = at;
            Part(PrimitiveType.Cylinder, g.transform, "Wrap", new Vector3(0f, 0.11f, 0f), new Vector3(0.34f, 0.22f, 0.34f), wrap);
            Part(PrimitiveType.Sphere, g.transform, "Frosting1", new Vector3(0f, 0.27f, 0f), new Vector3(0.42f, 0.2f, 0.42f), frosting);
            Part(PrimitiveType.Sphere, g.transform, "Frosting2", new Vector3(0f, 0.38f, 0f), new Vector3(0.28f, 0.16f, 0.28f), frosting);
            Part(PrimitiveType.Sphere, g.transform, "Cherry", new Vector3(0f, 0.48f, 0f), new Vector3(0.11f, 0.11f, 0.11f), cherryMat);
            return g;
        }

        static void BuildFlourBowl(Vector3 at)
        {
            var bowlMat = Mat("Bowl", new Color(0.94f, 0.93f, 0.9f), 1f, 0.6f);
            var flourMat = Mat("Flour", new Color(1f, 0.99f, 0.97f), 1f, 0.15f);
            var woodMat = Mat("SpoonWood", new Color(0.8f, 0.6f, 0.4f), 1f, 0.5f);
            var g = new GameObject("FlourBowl"); g.transform.position = at;
            Part(PrimitiveType.Cylinder, g.transform, "Bowl", new Vector3(0f, 0.16f, 0f), new Vector3(0.62f, 0.32f, 0.62f), bowlMat);
            Part(PrimitiveType.Sphere, g.transform, "Flour", new Vector3(0f, 0.3f, 0f), new Vector3(0.54f, 0.18f, 0.54f), flourMat);
            var spoon = Part(PrimitiveType.Capsule, g.transform, "Spoon", new Vector3(0.12f, 0.48f, 0.05f), new Vector3(0.06f, 0.4f, 0.06f), woodMat);
            spoon.transform.rotation = Quaternion.Euler(38f, 0f, -18f);
        }

        static void BuildHoneyJar(Vector3 at)
        {
            var honeyMat = Mat("HoneyJar", new Color(1f, 0.66f, 0.14f), 0.6f, 1f);
            var lidMat = Mat("JarLid", new Color(0.9f, 0.75f, 0.4f), 1f, 0.7f);
            var woodMat = Mat("DipperWood", new Color(0.8f, 0.6f, 0.4f), 1f, 0.5f);
            var g = new GameObject("HoneyJar"); g.transform.position = at;
            Part(PrimitiveType.Cylinder, g.transform, "Jar", new Vector3(0f, 0.24f, 0f), new Vector3(0.4f, 0.48f, 0.4f), honeyMat);
            Part(PrimitiveType.Cylinder, g.transform, "Lid", new Vector3(0f, 0.51f, 0f), new Vector3(0.43f, 0.07f, 0.43f), lidMat);
            var dip = Part(PrimitiveType.Capsule, g.transform, "Dipper", new Vector3(0.08f, 0.72f, 0f), new Vector3(0.06f, 0.42f, 0.06f), woodMat);
            dip.transform.rotation = Quaternion.Euler(0f, 0f, -14f);
            Part(PrimitiveType.Sphere, g.transform, "DipperHead", new Vector3(0.16f, 0.55f, 0f), new Vector3(0.13f, 0.13f, 0.13f), Mat("HoneyDip", new Color(1f, 0.7f, 0.2f), 0.85f, 1f));
        }

        static void BuildBreadRolls(Vector3 at)
        {
            var rollMat = Mat("BreadRoll", new Color(0.92f, 0.66f, 0.36f), 1f, 0.75f);
            var g = new GameObject("BreadRolls"); g.transform.position = at;
            Part(PrimitiveType.Sphere, g.transform, "Roll", new Vector3(-0.3f, 0.1f, 0f), new Vector3(0.55f, 0.3f, 0.42f), rollMat);
            Part(PrimitiveType.Sphere, g.transform, "Roll", new Vector3(0.32f, 0.1f, 0.18f), new Vector3(0.5f, 0.28f, 0.4f), rollMat);
            Part(PrimitiveType.Sphere, g.transform, "Roll", new Vector3(0.05f, 0.09f, -0.35f), new Vector3(0.46f, 0.26f, 0.38f), rollMat);
        }

        static void BuildBerryBowl(Vector3 at)
        {
            var bowlMat = Mat("BerryBowl", new Color(0.75f, 0.85f, 0.88f), 1f, 0.6f);
            var red = Mat("BerryRed", new Color(0.88f, 0.16f, 0.24f), 1f, 0.85f);
            var blue = Mat("BerryBlue", new Color(0.25f, 0.3f, 0.6f), 1f, 0.85f);
            var g = new GameObject("BerryBowl"); g.transform.position = at;
            Part(PrimitiveType.Cylinder, g.transform, "Bowl", new Vector3(0f, 0.13f, 0f), new Vector3(0.56f, 0.26f, 0.56f), bowlMat);
            var spots = new[] { new Vector3(0f, 0.3f, 0f), new Vector3(0.16f, 0.29f, 0.1f), new Vector3(-0.15f, 0.29f, 0.12f), new Vector3(0.08f, 0.3f, -0.14f), new Vector3(-0.1f, 0.3f, -0.1f) };
            for (int i = 0; i < spots.Length; i++)
                Part(PrimitiveType.Sphere, g.transform, "Berry", spots[i], new Vector3(0.13f, 0.13f, 0.13f), i % 2 == 0 ? red : blue);
        }

        /// <summary>A bakery shelf unit: 3 boards of jars, milk bottles and mini cakes.</summary>
        static GameObject BuildShelf(Vector3 at, Material[] cakeMats, Material cherryMat, bool wide = false)
        {
            var wood = Mat("ShelfWood", new Color(0.85f, 0.62f, 0.42f), 1f, 0.55f);
            var glass = Mat("JarGlass", new Color(0.96f, 0.91f, 0.8f), 0.45f, 0.9f);
            var milk = Mat("Milk", new Color(1f, 0.99f, 0.96f), 1f, 0.7f);
            float halfW = wide ? 2.6f : 1.35f;
            var g = new GameObject("Shelf"); g.transform.position = at;
            foreach (var sx in new[] { -halfW, halfW })
                Part(PrimitiveType.Cube, g.transform, "Upright", new Vector3(sx, 1.5f, 0f), new Vector3(0.12f, 3f, 0.5f), wood);
            var rng = new System.Random((int)(at.z * 10f) + (at.x > 0 ? 7 : 3) + (wide ? 21 : 0));
            for (int s = 0; s < 3; s++)
            {
                float y = 0.55f + s * 0.95f;
                Part(PrimitiveType.Cube, g.transform, "Board", new Vector3(0f, y, 0f), new Vector3(halfW * 2f + 0.25f, 0.09f, 0.55f), wood);
                for (float lx = -halfW + 0.35f; lx <= halfW - 0.3f; lx += 0.46f)
                {
                    int pick = rng.Next(3);
                    if (pick == 0)
                    {
                        Part(PrimitiveType.Cylinder, g.transform, "Jar", new Vector3(lx, y + 0.22f, 0f), new Vector3(0.3f, 0.36f, 0.3f), glass);
                        Part(PrimitiveType.Cylinder, g.transform, "JarLid", new Vector3(lx, y + 0.43f, 0f), new Vector3(0.32f, 0.06f, 0.32f), cakeMats[rng.Next(cakeMats.Length)]);
                    }
                    else if (pick == 1)
                    {
                        Part(PrimitiveType.Cylinder, g.transform, "Bottle", new Vector3(lx, y + 0.24f, 0f), new Vector3(0.24f, 0.4f, 0.24f), milk);
                        Part(PrimitiveType.Cylinder, g.transform, "BottleNeck", new Vector3(lx, y + 0.5f, 0f), new Vector3(0.13f, 0.14f, 0.13f), milk);
                    }
                    else
                    {
                        Part(PrimitiveType.Cylinder, g.transform, "MiniCake", new Vector3(lx, y + 0.17f, 0f), new Vector3(0.34f, 0.26f, 0.34f), cakeMats[rng.Next(cakeMats.Length)]);
                        Part(PrimitiveType.Sphere, g.transform, "MiniCherry", new Vector3(lx, y + 0.34f, 0f), new Vector3(0.08f, 0.08f, 0.08f), cherryMat);
                    }
                }
            }
            return g;
        }

        static void BuildRollingPin(Vector3 at)
        {
            var wood = Mat("RollingPin", new Color(0.85f, 0.64f, 0.42f), 1f, 0.6f);
            var g = new GameObject("RollingPin"); g.transform.position = at;
            var body = Part(PrimitiveType.Cylinder, g.transform, "Pin", new Vector3(0f, 0.14f, 0f), new Vector3(0.26f, 1.1f, 0.26f), wood);
            body.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            foreach (var s in new[] { -1f, 1f })
            {
                var h = Part(PrimitiveType.Capsule, g.transform, "Handle", new Vector3(s * 0.68f, 0.14f, 0f), new Vector3(0.07f, 0.3f, 0.07f), wood);
                h.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            }
            g.transform.rotation = Quaternion.Euler(0f, 24f, 0f);
        }

        static void BuildMilkBottles(Vector3 at)
        {
            var milk = Mat("Milk", new Color(1f, 0.99f, 0.96f), 1f, 0.7f);
            var cap = Mat("MilkCap", new Color(0.95f, 0.45f, 0.5f), 1f, 0.6f);
            var g = new GameObject("MilkBottles"); g.transform.position = at;
            foreach (var o in new[] { new Vector3(0f, 0f, 0f), new Vector3(0.42f, 0f, 0.2f) })
            {
                Part(PrimitiveType.Cylinder, g.transform, "Bottle", o + new Vector3(0f, 0.3f, 0f), new Vector3(0.3f, 0.6f, 0.3f), milk);
                Part(PrimitiveType.Cylinder, g.transform, "Neck", o + new Vector3(0f, 0.66f, 0f), new Vector3(0.16f, 0.16f, 0.16f), milk);
                Part(PrimitiveType.Cylinder, g.transform, "Cap", o + new Vector3(0f, 0.76f, 0f), new Vector3(0.18f, 0.06f, 0.18f), cap);
            }
        }

        static void BuildJamJar(Vector3 at)
        {
            var jam = Mat("Jam", new Color(0.85f, 0.2f, 0.3f), 0.55f, 0.95f);
            var lid = Mat("JamLid", new Color(0.95f, 0.8f, 0.45f), 1f, 0.7f);
            var label = Mat("JamLabel", new Color(1f, 0.97f, 0.9f), 1f, 0.5f);
            var g = new GameObject("JamJar"); g.transform.position = at;
            Part(PrimitiveType.Cylinder, g.transform, "Jar", new Vector3(0f, 0.26f, 0f), new Vector3(0.44f, 0.52f, 0.44f), jam);
            Part(PrimitiveType.Cylinder, g.transform, "Label", new Vector3(0f, 0.26f, 0f), new Vector3(0.455f, 0.2f, 0.455f), label);
            Part(PrimitiveType.Cylinder, g.transform, "Lid", new Vector3(0f, 0.55f, 0f), new Vector3(0.47f, 0.07f, 0.47f), lid);
        }

        /// <summary>Load a generated texture asset, creating it (PNG in Assets/Textures) on first build.</summary>
        /// <summary>
        /// Room dressing (Nov 6, straight hall): finish cake, clouds beside the
        /// climb, twinkle dust and floor props. Older notes: a cake-tier TOWER at the center of
        /// the spiral with a glowing star on top, clouds floating around the
        /// stairs, a spiral strand of glowing orbs following the climb,
        /// twinkle dust drifting in the air, bakery props on the floor plate
        /// and on the clouds, and the finish cake crowning the top platform -
        /// the reference videos' soft pink world, rebuilt round.
        /// </summary>
        static void BuildFinishDressing(Vector3 finishPos, float zMin, float zMax)
        {
            var cakeMats = new[]
            {
                Mat("CakePink", new Color(1f, 0.75f, 0.82f), 1f, 0.6f),
                Mat("CakeMint", new Color(0.72f, 0.95f, 0.8f), 1f, 0.6f),
                Mat("CakeCream", new Color(1f, 0.95f, 0.82f), 1f, 0.6f),
                Mat("CakeChoco", new Color(0.55f, 0.36f, 0.24f), 1f, 0.6f),
            };
            var cherryMat = Mat("Cherry", new Color(0.9f, 0.15f, 0.25f), 1f, 0.9f);

            // --- The FINISH CAKE on the finish platform (from the big build) ---
            {
                var fwd = Vector3.forward;
                var g = new GameObject("FinishCake");
                g.transform.position = finishPos + fwd * 1.15f + new Vector3(0f, 0.2f, 0f);
                g.transform.rotation = Quaternion.identity;
                Part(PrimitiveType.Cylinder, g.transform, "Tier1", new Vector3(0f, 0.26f, 0f), new Vector3(2.2f, 0.52f, 2.2f), cakeMats[2]);
                Part(PrimitiveType.Cylinder, g.transform, "Tier2", new Vector3(0f, 0.72f, 0f), new Vector3(1.5f, 0.44f, 1.5f), cakeMats[0]);
                Part(PrimitiveType.Cylinder, g.transform, "Tier3", new Vector3(0f, 1.1f, 0f), new Vector3(0.95f, 0.36f, 0.95f), cakeMats[1]);
                for (int i = 0; i < 6; i++)
                {
                    float a = i / 6f * Mathf.PI * 2f;
                    Part(PrimitiveType.Sphere, g.transform, "CakeCherry", new Vector3(Mathf.Cos(a) * 0.82f, 0.58f, Mathf.Sin(a) * 0.82f), new Vector3(0.13f, 0.13f, 0.13f), cherryMat);
                }
                foreach (var cx in new[] { -0.22f, 0f, 0.22f })
                {
                    Part(PrimitiveType.Cylinder, g.transform, "Candle", new Vector3(cx, 1.42f, 0f), new Vector3(0.05f, 0.3f, 0.05f), Mat("Candle", new Color(1f, 0.97f, 0.92f), 1f, 0.5f));
                    Part(PrimitiveType.Sphere, g.transform, "Flame", new Vector3(cx, 1.62f, 0f), new Vector3(0.055f, 0.09f, 0.055f), EmissiveMat("Flame", new Color(1f, 0.72f, 0.3f), 2.2f));
                }
            }

            // --- Soft clouds drifting beside the climb ---
            var cloudMat = Mat("Cloud", new Color(0.99f, 0.98f, 1f), 1f, 0.22f);
            cloudMat.EnableKeyword("_EMISSION");
            cloudMat.SetColor("_EmissionColor", new Color(0.4f, 0.37f, 0.42f));
            for (int i = 0; i < 4; i++) // a few soft clouds - pink accent, not clutter
            {
                float z = Mathf.Lerp(zMin + 8f, zMax - 6f, i / 3f);
                float pathY = 0.35f + Mathf.Max(0f, z - PathStartZ) / PathAdvance * PathRise;
                var cp = new Vector3((i % 2 == 0 ? -1f : 1f) * Random.Range(5.5f, 7f), Mathf.Max(0.6f, pathY - Random.Range(0.8f, 1.5f)), z); // never below the floor
                BuildCloud(cp, Random.Range(1f, 1.5f), cloudMat);
            }

            // --- Twinkle dust drifting down the hall ---
            var dust = new GameObject("TwinkleDust").AddComponent<ParticleSystem>();
            dust.transform.position = new Vector3(0f, finishPos.y * 0.5f + 2f, (zMin + zMax) / 2f);
            var dmain = dust.main;
            dmain.loop = true; dmain.playOnAwake = true; dmain.startLifetime = 6f;
            dmain.startSpeed = 0.12f; dmain.startSize = 0.08f; dmain.maxParticles = 120;
            dmain.startColor = new Color(1f, 0.93f, 0.8f, 0.45f);
            var dem = dust.emission; dem.rateOverTime = 18f;
            var dshape = dust.shape; dshape.shapeType = ParticleSystemShapeType.Box;
            dshape.scale = new Vector3(HallHalfW * 1.8f, finishPos.y + 6f, zMax - zMin);
            dust.GetComponent<ParticleSystemRenderer>().material = SparkleParticleMat();
        }

        /// <summary>One soft cloud: a cluster of flattened matte puffs.</summary>
        static void BuildCloud(Vector3 at, float scale, Material cloudMat)
        {
            var g = new GameObject("Cloud");
            g.transform.position = at;
            var blobs = new (float x, float y, float z, float s)[]
            {
                (0f, 0f, 0f, 1.0f), (0.8f, -0.05f, 0.1f, 0.75f), (-0.75f, -0.04f, -0.08f, 0.8f),
                (0.25f, 0.14f, -0.25f, 0.65f), (-0.3f, 0.1f, 0.3f, 0.6f),
            };
            foreach (var b in blobs)
                Part(PrimitiveType.Sphere, g.transform, "Puff",
                     new Vector3(b.x * scale, b.y * scale, b.z * scale),
                     new Vector3(1.1f * b.s * scale, 0.55f * b.s * scale, 0.85f * b.s * scale), cloudMat);
            foreach (var r in g.GetComponentsInChildren<MeshRenderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ======================================================== bakery hall

        /// <summary>Warm bakery hall (replaces the pink checker candy hall):
        /// cream plaster walls, wooden pilasters + trims, glowing window
        /// panes, shelves of pastries, at most 8 floating candies.</summary>
        static void BuildBakeryHall(float zMin, float zMax, float topY)
        {
            var root = new GameObject("BakeryHall").transform;
            float len = zMax - zMin, zc = (zMin + zMax) / 2f, wallH = topY + 9f;
            var plaster = PropMat("Plaster", new Color(0.94f, 0.87f, 0.78f), 0.12f, 0.04f);
            var wood = PropMat("WoodTrim", new Color(0.66f, 0.46f, 0.3f), 0.5f, 0.05f);
            var woodDark = PropMat("WoodDark", new Color(0.5f, 0.33f, 0.2f), 0.45f, 0.05f);
            var paneMat = EmissiveMat("BakeryPane", new Color(1f, 0.82f, 0.55f), 0.85f);
            var frameMat = PropMat("PaneFrame", new Color(0.55f, 0.38f, 0.24f), 0.5f, 0.05f);
            foreach (var side in new[] { -1f, 1f })
            {
                var wall = Box("HallWall", new Vector3(side * (HallHalfW + 0.15f), wallH / 2f - 0.3f, zc), new Vector3(0.3f, wallH, len), plaster);
                wall.transform.SetParent(root, true);
                Staticize(wall);
                // wooden pilasters + horizontal trims (were glowing pillars)
                for (float z = zMin + 3f; z < zMax; z += 6f)
                {
                    var p = Box("Pilaster", new Vector3(side * (HallHalfW - 0.1f), wallH / 2f - 0.3f, z), new Vector3(0.34f, wallH, 0.34f), wood);
                    p.transform.SetParent(root, true);
                    Object.DestroyImmediate(p.GetComponent<Collider>());
                    Staticize(p);
                }
                foreach (float ty in new[] { 0.15f, 3.2f, topY + 2.5f })
                {
                    var t = Box("WoodTrim", new Vector3(side * (HallHalfW - 0.05f), ty, zc), new Vector3(0.16f, 0.16f, len), woodDark);
                    t.transform.SetParent(root, true);
                    Object.DestroyImmediate(t.GetComponent<Collider>());
                    Staticize(t);
                }
                // glowing bakery windows high on the walls, wood-framed
                for (float z = zMin + 6f; z < zMax - 3f; z += 9f)
                {
                    var wf = Box("WindowFrame", new Vector3(side * (HallHalfW - 0.02f), topY * 0.55f + 2.4f, z), new Vector3(0.1f, 2.4f, 1.7f), frameMat);
                    wf.transform.SetParent(root, true); Staticize(wf);
                    var wp = Box("WindowPane", new Vector3(side * (HallHalfW - 0.07f), topY * 0.55f + 2.4f, z), new Vector3(0.08f, 2.1f, 1.4f), paneMat);
                    wp.transform.SetParent(root, true); Staticize(wp);
                    var wb = Box("WindowBar", new Vector3(side * (HallHalfW - 0.1f), topY * 0.55f + 2.4f, z), new Vector3(0.06f, 2.1f, 0.06f), frameMat);
                    wb.transform.SetParent(root, true); Staticize(wb);
                }
                // shelves of pastries against the walls
                for (float z = zMin + 8f; z < zMax - 4f; z += 12f)
                {
                    var shelf = BuildShelf(new Vector3(side * (HallHalfW - 0.9f), -0.05f, z), BakeryCakeMats(), PropMat("Cherry", new Color(0.9f, 0.15f, 0.25f), 0.9f, 0.05f));
                    shelf.transform.rotation = Quaternion.Euler(0f, side < 0f ? 90f : -90f, 0f);
                    shelf.transform.SetParent(root, true);
                    Staticize(shelf);
                }
                // pink bunting swagged along the walls (pink accent)
                var bunt = PropMat("BuntingPink", new Color(0.98f, 0.62f, 0.74f), 0.5f, 0.1f);
                var buntCream = PropMat("BuntingCream", new Color(1f, 0.95f, 0.88f), 0.5f, 0.1f);
                for (int i = 0; i < 14; i++)
                {
                    float bz = Mathf.Lerp(zMin + 2f, zMax - 2f, i / 13f);
                    float by = topY + 3.2f - Mathf.Sin(i / 13f * Mathf.PI * 4f) * 0.5f;
                    var b = Part(PrimitiveType.Sphere, root, "Bunting", new Vector3(side * (HallHalfW - 0.3f), by, bz), new Vector3(0.22f, 0.3f, 0.1f), i % 2 == 0 ? bunt : buntCream);
                    b.transform.localRotation = Quaternion.Euler(0f, side < 0f ? 90f : -90f, 0f);
                }
            }
            // big warm window at the far end, behind the finish cake
            var farW = Box("FarWindowFrame", new Vector3(0f, topY * 0.5f + 2.8f, zMax - 0.2f), new Vector3(4.5f, 3.4f, 0.15f), frameMat);
            farW.transform.SetParent(root, true); Staticize(farW);
            var farP = Box("FarWindowPane", new Vector3(0f, topY * 0.5f + 2.8f, zMax - 0.28f), new Vector3(4f, 3f, 0.1f), paneMat);
            farP.transform.SetParent(root, true); Staticize(farP);

            // at most 8 floating candies, high and off to the sides
            for (int i = 0; i < 8; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var pos = new Vector3(side * Random.Range(4.5f, 7f), topY * 0.6f + Random.Range(1.5f, 5f), Mathf.Lerp(zMin + 4f, zMax - 4f, (i + 0.5f) / 8f));
                var prop = CandyProp(i % 7, pos, Random.Range(0.7f, 1.1f), root);
                var bob = prop.gameObject.AddComponent<FloatBob>();
                bob.amplitude = Random.Range(0.15f, 0.3f); bob.speed = Random.Range(0.4f, 0.8f);
                bob.spin = Random.Range(-14f, 14f); bob.phase = Random.Range(0f, 6.28f);
            }
        }

        static Material[] BakeryCakeMats() => new[]
        {
            PropMat("CakePink", new Color(0.98f, 0.72f, 0.8f), 0.6f, 0.1f),
            PropMat("CakeCream", new Color(0.98f, 0.92f, 0.8f), 0.6f, 0.1f),
            PropMat("CakeChoco", new Color(0.55f, 0.36f, 0.24f), 0.6f, 0.1f),
        };

        /// <summary>Flags a prop + children static for batching.</summary>
        static void Staticize(GameObject g)
        {
            foreach (var t in g.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }

        // ======================================================== floor props

        /// <summary>Bakery props scattered on the floor along both sides of
        /// the climb (never on the path): SDF croissants + cinnamon rolls,
        /// macarons, cupcakes, honey jars, bowls, milk bottles, some on
        /// cake stands. Varied heights, muted next to the path.</summary>
        static void BuildBakeryProps(float zMin, float zMax)
        {
            var cakeMats = BakeryCakeMats();
            var cherryMat = PropMat("Cherry", new Color(0.9f, 0.15f, 0.25f), 0.9f, 0.05f);
            int k = 0;
            for (float z = zMin + 3.5f; z < zMax - 3f; z += 4.2f, k++)
            {
                float side = k % 2 == 0 ? -1f : 1f;
                float x = side * Random.Range(4.6f, 6.8f);
                float y = -0.2f;
                var at = new Vector3(x, y, z + Random.Range(-0.8f, 0.8f));
                GameObject g;
                switch (k % 13)
                {
                    case 0: g = BuildCroissant(at); break;
                    case 1: g = BuildCinnamonRoll(at); break;
                    case 2: BuildMacarons(at); continue;
                    case 3: BuildHoneyJar(at); continue;
                    case 4: g = BuildCroissant(at); break;
                    case 5: BuildFlourBowl(at); continue;
                    case 6: g = BuildCinnamonRoll(at); break;
                    case 7: BuildBerryBowl(at); continue;
                    case 8: BuildBreadRolls(at); continue;
                    case 9: BuildMilkBottles(at); continue;
                    case 10: BuildJamJar(at); continue;
                    case 11: BuildRollingPin(at); continue;
                    default: g = BuildCupcake(at, cakeMats[k % 3], cherryMat); break;
                }
                if (g != null && k % 4 == 0) LiftOnStand(g, at);
            }
            // a couple of pink cake stands as accents
            var standMat = PropMat("StandPink", new Color(0.97f, 0.7f, 0.78f), 0.9f, 0.1f);
            foreach (var at in new[] { new Vector3(-5.6f, -0.2f, zMin + 10f), new Vector3(6f, -0.2f, zMax - 8f) })
                CakeStand(at, standMat, cakeMats[0], cherryMat);
        }

        /// <summary>Small display stand under a prop.</summary>
        static void LiftOnStand(GameObject prop, Vector3 at)
        {
            var standMat = PropMat("Stand", new Color(0.93f, 0.92f, 0.95f), 0.9f, 0.1f);
            var g = new GameObject("PropStand"); g.transform.position = at;
            Part(PrimitiveType.Cylinder, g.transform, "Stem", new Vector3(0f, 0.14f, 0f), new Vector3(0.12f, 0.28f, 0.12f), standMat);
            Part(PrimitiveType.Cylinder, g.transform, "Plate", new Vector3(0f, 0.3f, 0f), new Vector3(0.7f, 0.05f, 0.7f), standMat);
            prop.transform.position = at + new Vector3(0f, 0.33f, 0f);
        }

        static void CakeStand(Vector3 at, Material standMat, Material cakeMat, Material cherryMat)
        {
            var g = new GameObject("CakeStand"); g.transform.position = at;
            Part(PrimitiveType.Cylinder, g.transform, "Stem", new Vector3(0f, 0.16f, 0f), new Vector3(0.12f, 0.32f, 0.12f), standMat);
            Part(PrimitiveType.Cylinder, g.transform, "Plate", new Vector3(0f, 0.34f, 0f), new Vector3(0.95f, 0.05f, 0.95f), standMat);
            Part(PrimitiveType.Cylinder, g.transform, "Cake", new Vector3(0f, 0.5f, 0f), new Vector3(0.68f, 0.26f, 0.68f), cakeMat);
            Part(PrimitiveType.Sphere, g.transform, "Cherry", new Vector3(0f, 0.68f, 0f), new Vector3(0.13f, 0.13f, 0.13f), cherryMat);
            Staticize(g);
        }

        // -------------------------------------------------- SDF baked goods

        /// <summary>A croissant: a crescent of tapering dough lobes, smooth
        /// SDF so it reads glossy-baked.</summary>
        static GameObject BuildCroissant(Vector3 at)
        {
            SdfMesher.Field f = p =>
            {
                float d = 999f;
                for (int i = 0; i < 7; i++)
                {
                    float a = Mathf.Lerp(-1.85f, 1.85f, i / 6f); // crescent arc
                    float taper = 1f - Mathf.Pow(Mathf.Abs(a) / 1.85f, 1.6f) * 0.75f;
                    var c = new Vector3(Mathf.Sin(a) * 0.42f, 0f, (1f - Mathf.Cos(a)) * 0.42f - 0.2f);
                    d = SdfMesher.Smin(d, SdfMesher.Sphere(p, c, 0.16f * taper + 0.03f), 0.1f);
                }
                return d;
            };
            var m = SdfMesher.Build(f, new Vector3(-0.55f, -0.2f, -0.5f), new Vector3(0.55f, 0.25f, 0.45f), 40);
            m.name = "Croissant";
            var g = MeshPart(null, "Croissant", SaveMesh(m, "Croissant"), PropMat("Croissant", new Color(0.85f, 0.55f, 0.26f), 0.55f, 0.12f));
            g.transform.position = at;
            g.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Staticize(g);
            return g;
        }

        /// <summary>A cinnamon roll: a fat dough disc with a raised spiral
        /// ridge baked in (SDF spiral of lobes).</summary>
        static GameObject BuildCinnamonRoll(Vector3 at)
        {
            SdfMesher.Field f = p =>
            {
                float disc = SdfMesher.Ellipsoid(p, new Vector3(0f, 0f, 0f), new Vector3(0.34f, 0.14f, 0.34f));
                float d = disc;
                for (int i = 0; i < 12; i++)
                {
                    float t = i / 11f;
                    float a = t * Mathf.PI * 3.4f;
                    float r = 0.05f + t * 0.24f;
                    var c = new Vector3(Mathf.Cos(a) * r, 0.1f + t * 0.02f, Mathf.Sin(a) * r);
                    d = SdfMesher.Smin(d, SdfMesher.Sphere(p, c, 0.055f), 0.05f);
                }
                return d;
            };
            var m = SdfMesher.Build(f, new Vector3(-0.4f, -0.2f, -0.4f), new Vector3(0.4f, 0.25f, 0.4f), 36);
            m.name = "CinnamonRoll";
            var g = MeshPart(null, "CinnamonRoll", SaveMesh(m, "CinnamonRoll"), PropMat("Cinnamon", new Color(0.72f, 0.45f, 0.25f), 0.5f, 0.12f));
            g.transform.position = at;
            g.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Staticize(g);
            return g;
        }
    }
}
#endif
