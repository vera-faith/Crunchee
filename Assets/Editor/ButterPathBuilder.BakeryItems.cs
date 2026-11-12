#if UNITY_EDITOR
using UnityEngine;
using Crunchee.Core;
using Crunchee.Game;

namespace Crunchee.EditorTools
{
    public static partial class ButterPathBuilder
    {
        const float BakeryItemTop = ButterH / 2f + PatH;

        static GameObject BuildBakeryItem(Vector3 pos, string name, SquishyItem item, float yawDeg)
        {
            switch (item)
            {
                case SquishyItem.FlourBag: return BuildFlourBagStep(pos, name, yawDeg);
                case SquishyItem.VanillaBowl: return BuildVanillaBowlStep(pos, name, yawDeg);
                default: return BuildCroissantStep(pos, name, yawDeg);
            }
        }

        /// <summary>
        /// A plump paper SACK of flour (Nov 9: only butters are sticks -
        /// everything else gets its own silhouette). Pillow body, gathered
        /// cuff + tied knot, folded flaps, stitched seam and a soft flour
        /// mound on top that her feet sink into.
        /// </summary>
        static GameObject BuildFlourBagStep(Vector3 pos, string name, float yawDeg)
        {
            var group = new GameObject(name);
            group.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yawDeg, 0f));
            const float W = 2.55f, H = 1.05f, D = 2.6f;
            var paper = Mat("FlourBagPaper", new Color(0.91f, 0.82f, 0.67f), 1f, 0.22f);
            paper.SetFloat("_DentAmount", 1f);
            var body = ChildBox(group.transform, "Body", new Vector3(0f, BakeryItemTop - H / 2f, 0f), new Vector3(W, H, D), paper, true);
            body.GetComponent<MeshRenderer>().enabled = false; // collider only; the sack is built from puffs
            var deco = UnscaledDeco(body.transform, W, H, D);
            var flour = Mat("FlourPowder", new Color(1f, 0.99f, 0.96f), 1f, 0.12f);
            flour.SetFloat("_DentAmount", 1f);
            var fold = Mat("FlourBagFold", new Color(0.96f, 0.89f, 0.76f), 1f, 0.25f);
            var seam = Mat("FlourBagSeam", new Color(0.78f, 0.66f, 0.5f), 1f, 0.2f);
            // the plump sack: two squashed pillows, wider at the bottom
            Ball(deco, "SackLow", new Vector3(0f, -0.02f, 0f), new Vector3(W * 0.98f, H * 0.58f, D * 0.97f), paper);
            Ball(deco, "SackTop", new Vector3(0f, 0.3f, 0f), new Vector3(W * 0.84f, H * 0.5f, D * 0.84f), paper);
            // soft flour mound on top - her feet sink into it
            Ball(deco, "FlourPowder", new Vector3(0.05f, H * 0.44f, 0.08f), new Vector3(W * 0.72f, 0.3f, D * 0.68f), flour);
            // gathered paper cuff + the little tied knot at the back
            var cuff = Cyl(deco, "PaperCuff", new Vector3(0f, H * 0.5f, -0.04f), new Vector3(W * 0.62f, 0.045f, D * 0.56f), fold);
            cuff.transform.localRotation = Quaternion.Euler(0f, 0f, 2f);
            var knot = Ball(deco, "TiedKnot", new Vector3(0.14f, H * 0.58f, -D * 0.28f), new Vector3(0.36f, 0.22f, 0.3f), fold);
            knot.transform.localRotation = Quaternion.Euler(-18f, 0f, 12f);
            // folded side flaps
            foreach (int side in new[] { -1, 1 })
            {
                var flap = ChildBox(deco, "FoldedCorner", new Vector3(side * W * 0.4f, H * 0.44f, -D * 0.1f), new Vector3(0.24f, 0.07f, D * 0.6f), fold, false);
                flap.transform.localRotation = Quaternion.Euler(-8f, 0f, side * 14f);
            }
            // stitched seam along the front bottom edge
            ChildBox(deco, "StitchSeam", new Vector3(0f, -H * 0.3f, D * 0.48f), new Vector3(W * 0.78f, 0.035f, 0.03f), seam, false);
            var print = new GameObject("FlourPrint");
            print.transform.SetParent(deco, false);
            print.transform.localPosition = new Vector3(0f, 0.02f, -D * 0.495f);
            var text = print.AddComponent<TextMesh>();
            text.text = "FLOUR";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 144; text.characterSize = 0.045f;
            text.anchor = TextAnchor.MiddleCenter; text.color = new Color(0.32f, 0.24f, 0.17f);
            print.GetComponent<MeshRenderer>().sharedMaterial = WorldLabelMaterial(text.font);

            var sq = body.AddComponent<Squishy>();
            sq.baseMaterial = BaseMaterial.Flour; sq.coating = Coating.Plain;
            sq.pathOrder = ++pathCounter;
            sq.dentDecal = AttachDentDecal(body.transform, 0.5f, W, D);
            sq.stepParticles = BakeryParticles(group.transform, new Color(1f, 0.99f, 0.96f, 0.8f), false);
            return group;
        }

        /// <summary>
        /// The vanilla-extract bowl (Nov 9 polish): saucer plate, foot ring,
        /// ceramic bowl with a glossy amber extract surface, an extract drip
        /// on the rim and two crossed vanilla pods.
        /// </summary>
        static GameObject BuildVanillaBowlStep(Vector3 pos, string name, float yawDeg)
        {
            var group = new GameObject(name);
            group.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yawDeg, 0f));
            var ceramic = Mat("VanillaBowlCeramic", new Color(0.96f, 0.88f, 0.75f), 1f, 0.62f);
            ceramic.SetFloat("_DentAmount", 1f);
            var body = Cyl(group.transform, "Body", new Vector3(0f, BakeryItemTop - 0.36f, 0f), new Vector3(1.45f, 0.36f, 1.45f), ceramic);
            var col = body.AddComponent<BoxCollider>();
            col.size = new Vector3(1.8f, 2f, 1.8f);
            var rim = Mat("VanillaBowlRim", new Color(1f, 0.96f, 0.86f), 1f, 0.72f);
            Cyl(body.transform, "Rim", new Vector3(0f, 1.01f, 0f), new Vector3(0.98f, 0.03f / 0.36f, 0.98f), rim);
            Cyl(body.transform, "FootRing", new Vector3(0f, -0.9f, 0f), new Vector3(0.62f, 0.05f / 0.36f, 0.62f), rim);
            // saucer plate the bowl sits on
            Cyl(body.transform, "Saucer", new Vector3(0f, -1.02f, 0f), new Vector3(1.75f, 0.14f / 0.36f, 1.75f), rim);
            var liquid = Mat("VanillaExtract", new Color(0.38f, 0.14f, 0.045f), 0.74f, 0.96f);
            liquid.SetFloat("_DentAmount", 1f);
            liquid.SetFloat("_EnvStrength", 0.3f);
            Cyl(body.transform, "Extract", new Vector3(0f, 1.06f, 0f), new Vector3(0.85f, 0.018f / 0.36f, 0.85f), liquid);
            // a slow amber drip running down the outside of the bowl
            var drip = Ball(body.transform, "ExtractDrip", new Vector3(0.97f, 0.55f, 0.2f), new Vector3(0.09f, 0.5f, 0.09f), liquid);
            var pod = Mat("VanillaPod", new Color(0.16f, 0.07f, 0.025f), 1f, 0.45f);
            foreach (int side in new[] { -1, 1 })
            {
                var p = Part(PrimitiveType.Capsule, body.transform, "VanillaPod", new Vector3(side * 0.32f, 1.12f, 0.45f), new Vector3(0.07f, 0.35f, 0.07f), pod);
                p.transform.localRotation = Quaternion.Euler(65f, side * 18f, 0f);
            }

            var sq = body.AddComponent<Squishy>();
            sq.baseMaterial = BaseMaterial.Vanilla; sq.coating = Coating.Plain;
            sq.pathOrder = ++pathCounter;
            sq.dentDecal = AttachDentDecal(body.transform, 1.09f, 1.45f, 1.45f, 1.3f);
            sq.stepParticles = BakeryParticles(group.transform, new Color(0.85f, 0.52f, 0.2f, 0.9f), true);
            return group;
        }

        /// <summary>
        /// The crescent croissant (Nov 9 polish): darker toasted tips, warm
        /// egg-wash glaze, lamination ridges and the crackable crust flakes.
        /// </summary>
        static GameObject BuildCroissantStep(Vector3 pos, string name, float yawDeg)
        {
            var group = new GameObject(name);
            group.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yawDeg, 0f));
            const float W = 2.65f, H = 0.55f, D = 2.5f;
            var dough = Mat("CroissantDough", new Color(0.82f, 0.47f, 0.17f), 1f, 0.56f);
            dough.SetFloat("_DentAmount", 1f);
            var body = ChildBox(group.transform, "Body", new Vector3(0f, BakeryItemTop - H / 2f, 0f), new Vector3(W, H, D), dough, true);
            Roundify(body, 0.24f);
            body.GetComponent<MeshRenderer>().enabled = false;
            var deco = UnscaledDeco(body.transform, W, H, D);
            var golden = Mat("CroissantGolden", new Color(0.95f, 0.62f, 0.24f), 1f, 0.62f);
            golden.SetColor("_EmissionColor", new Color(0.12f, 0.06f, 0.01f)); // warm egg-wash glow
            var toasted = Mat("CroissantToasted", new Color(0.62f, 0.32f, 0.12f), 1f, 0.5f);
            var light = Mat("CroissantLamination", new Color(1f, 0.75f, 0.36f), 1f, 0.66f);
            var crust = new GameObject("CroissantCrust").transform;
            crust.SetParent(deco, false);
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(-1.35f, 1.35f, i / 8f);
                float taper = 0.42f + 0.58f * (1f - Mathf.Abs(a) / 1.35f);
                var center = new Vector3(Mathf.Sin(a) * 1.02f, 0.22f, Mathf.Cos(a) * 0.68f - 0.38f);
                var lobeMat = i == 0 || i == 8 ? toasted : (i % 2 == 0 ? golden : dough);
                var lobe = Ball(deco, "DoughLobe", center, new Vector3(0.5f * taper + 0.16f, 0.38f * taper + 0.12f, 0.56f * taper + 0.16f), lobeMat);
                lobe.transform.localRotation = Quaternion.Euler(0f, -a * 32f, 0f);
                var ridge = Ball(deco, "Lamination", center + new Vector3(0f, 0.17f * taper + 0.04f, -0.03f), new Vector3(0.36f * taper + 0.1f, 0.035f, 0.18f * taper + 0.06f), light);
                ridge.transform.localRotation = Quaternion.Euler(0f, -a * 32f, 0f);
                var flake = ChildBox(crust, "ShellPiece", center + new Vector3(0f, 0.23f * taper + 0.08f, 0f), new Vector3(0.34f * taper + 0.12f, 0.055f, 0.3f * taper + 0.1f), light, false);
                flake.transform.localRotation = Quaternion.Euler(0f, -a * 32f, 0f);
            }

            var sq = body.AddComponent<Squishy>();
            sq.baseMaterial = BaseMaterial.Croissant; sq.coating = Coating.Plain;
            sq.crackableCrust = true; sq.waxLayers = 3;
            sq.coatingShell = crust.gameObject; sq.shardMaterial = light;
            sq.pathOrder = ++pathCounter;
            sq.dentDecal = AttachDentDecal(body.transform, 0.5f, W, D);
            sq.stepParticles = BakeryParticles(group.transform, new Color(1f, 0.75f, 0.36f), false);
            return group;
        }

        static Transform UnscaledDeco(Transform body, float w, float h, float d)
        {
            var deco = new GameObject("Deco").transform;
            deco.SetParent(body, false);
            deco.localScale = new Vector3(1f / w, 1f / h, 1f / d);
            return deco;
        }

        /// <summary>The following squish dent, for non-box tops: a soft
        /// shadow disc parented to the body at local height localTopY.
        /// w/d = the body's world footprint (the plane mesh is 10 units
        /// across, so its local scale must undo both).</summary>
        static MeshRenderer AttachDentDecal(Transform body, float localTopY, float w, float d, float worldSize = 1.5f)
        {
            var dentMat = Mat("Dent", Color.white, 0.99f, 0.5f);
            dentMat.mainTexture = DentTexture();
            dentMat.renderQueue = 3001;
            NoSheen(dentMat);
            dentMat.SetFloat("_DentAmount", 1f);
            var dent = GameObject.CreatePrimitive(PrimitiveType.Plane);
            dent.name = "DentDecal";
            dent.transform.SetParent(body, false);
            dent.transform.localPosition = new Vector3(0f, localTopY, 0f);
            dent.transform.localScale = new Vector3(worldSize / (10f * w), 1f, worldSize / (10f * d));
            dent.GetComponent<MeshRenderer>().material = dentMat;
            Object.DestroyImmediate(dent.GetComponent<Collider>());
            var r = dent.GetComponent<MeshRenderer>();
            r.enabled = false; // only shows while she stands on it
            return r;
        }

        static ParticleSystem BakeryParticles(Transform parent, Color color, bool liquid)
        {
            var ps = new GameObject("StepParticles").AddComponent<ParticleSystem>();
            ps.transform.SetParent(parent, false);
            ps.transform.localPosition = new Vector3(0f, BakeryItemTop + 0.12f, 0f);
            var main = ps.main;
            main.playOnAwake = false; main.maxParticles = 200;
            main.startColor = color;
            main.startSize = liquid ? new ParticleSystem.MinMaxCurve(0.06f, 0.17f) : new ParticleSystem.MinMaxCurve(0.09f, 0.27f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, liquid ? 4.0f : 2.8f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, liquid ? 0.9f : 1.25f);
            main.gravityModifier = liquid ? 1.4f : 0.3f;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(1.65f, 0.05f, 1.65f);
            ps.GetComponent<ParticleSystemRenderer>().material = SparkleParticleMat(false);
            return ps;
        }
    }
}
#endif
