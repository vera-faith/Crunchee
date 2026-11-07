#if UNITY_EDITOR
using UnityEngine;
using Crunchee.Player;

namespace Crunchee.EditorTools
{
    /// <summary>
    /// Pudding Bunny, the locked player character: a glossy caramel-glazed
    /// jelly pudding chibi built from SDF-meshed parts (v12).
    /// </summary>
    public static partial class ButterPathBuilder
    {
        /// <summary>
        /// Pudding Bunny (locked character) - v12 (visual overhaul):
        /// one smooth SDF body instead of stacked primitives, so cheeks melt
        /// into the head, the caramel reads as a poured glaze with ATTACHED
        /// drips (no more floating "!" marks), and the lop ears droop out to
        /// the sides like the reference. Face features are raymarched onto
        /// the surface and oriented to its normal - nothing clips or floats.
        /// Structure kept for gameplay: Model > BodyBob > EarPivotL/R,
        /// ArmPivotL/R, LegPivotL/R are what BunnyBody animates.
        /// </summary>
        static GameObject BuildPuddingBunny(Vector3 at)
        {
            // Cream/golden jelly (the whole skin), deep amber caramel glaze,
            // lighter honey for the thin coats.
            var cream = Mat("Cream", new Color(1f, 0.9f, 0.7f), 1f, 0.95f);
            cream.EnableKeyword("_EMISSION");
            cream.SetColor("_EmissionColor", new Color(0.06f, 0.04f, 0.02f)); // faint jelly glow so the face never goes grey
            cream.SetFloat("_RimStrength", 0.15f);
            cream.SetColor("_RimColor", new Color(1f, 0.9f, 0.75f));
            var caramel = Mat("Caramel", new Color(0.64f, 0.37f, 0.13f), 1f, 1f); // deep amber-brown caramel, not orange
            caramel.SetFloat("_SpecStrength", 1.8f);
            caramel.SetFloat("_RimStrength", 0.15f);
            caramel.SetColor("_RimColor", new Color(1f, 0.7f, 0.4f));
            var honey = Mat("CaramelHoney", new Color(0.84f, 0.56f, 0.26f), 1f, 1f);
            honey.SetFloat("_SpecStrength", 1.6f);
            honey.SetFloat("_RimStrength", 0.15f);
            var eyeM = Mat("EyeDark", new Color(0.12f, 0.06f, 0.03f), 1f, 1f);
            eyeM.SetFloat("_SpecStrength", 2.2f);
            var irisM = Mat("EyeIris", new Color(0.36f, 0.18f, 0.07f), 1f, 1f);
            var shineM = EmissiveMat("EyeShine", Color.white, 0.6f);
            var blushM = Mat("Blush", new Color(1f, 0.55f, 0.62f), 0.6f, 0.4f);
            NoSheen(blushM);
            var noseM = Mat("Nose", new Color(0.95f, 0.5f, 0.58f), 1f, 0.8f);
            var mouthM = Mat("Mouth", new Color(0.35f, 0.12f, 0.1f), 1f, 0.6f); // dark cocoa open smile, not red lips
            var tongueM = Mat("Tongue", new Color(1f, 0.56f, 0.62f), 1f, 0.7f);

            var player = new GameObject("Player_PuddingBunny") { tag = "Player" }; // FinishTrigger/Checkpoint find her by this tag
            player.transform.position = at;
            var cc = player.AddComponent<CharacterController>(); cc.height = 1.1f; cc.radius = 0.35f; cc.center = new Vector3(0f, 0.55f, 0f);
            player.AddComponent<PlayerController>();
            var model = new GameObject("Model"); model.transform.SetParent(player.transform, false);
            model.AddComponent<SquashAndStretch>();
            player.AddComponent<BunnyBody>();
            // BodyBob: BunnyBody bobs/sways this group; SquashAndStretch
            // scales Model above it - the two never fight over one transform.
            var bob = new GameObject("BodyBob"); bob.transform.SetParent(model.transform, false);
            var bt = bob.transform;

            // ------------------------------------------------ SDF fields (BodyBob-local space)
            var headC = new Vector3(0f, 0.98f, 0f);
            var headR = new Vector3(0.64f, 0.55f, 0.53f);
            var bodyC = new Vector3(0f, 0.28f, 0f);
            var bodyR = new Vector3(0.36f, 0.28f, 0.30f);

            // Cream skin: head + cheeks that melt in + body core + tail, one union.
            SdfMesher.Field creamF = p =>
            {
                float head = SdfMesher.Ellipsoid(p, headC, headR);
                float cheeks = Mathf.Min(
                    SdfMesher.Ellipsoid(p, headC + new Vector3(-0.30f, -0.20f, 0.16f), new Vector3(0.30f, 0.26f, 0.27f)),
                    SdfMesher.Ellipsoid(p, headC + new Vector3(0.30f, -0.20f, 0.16f), new Vector3(0.30f, 0.26f, 0.27f)));
                float body = SdfMesher.Ellipsoid(p, bodyC, bodyR);
                float tail = SdfMesher.Sphere(p, new Vector3(0f, 0.30f, -0.34f), 0.11f);
                return SdfMesher.Smin(SdfMesher.Smin(SdfMesher.Smin(head, cheeks, 0.12f), body, 0.10f), tail, 0.08f);
            };

            // Head caramel pour: a thin shell hugging the crown (+0.02 out,
            // ~0.026 thick), cut at a wavy scalloped edge that sits higher at
            // the back and lower over the face, with capsule drips hanging
            // off the edge - attached, not floating.
            SdfMesher.Field headCaramelF = p =>
            {
                float ang = Mathf.Atan2(p.x, p.z - headC.z);
                float front = Mathf.Clamp01((Mathf.Cos(ang) - 0.35f) / 0.65f); // 1 = right over the eyes
                float edgeY = Mathf.Lerp(1.30f, 1.14f, front) + 0.05f * Mathf.Sin(ang * 4f + 1.3f);
                // SOLID caramel cap (not a thin shell - shells pinholed on the
                // grid). The head surface inflated 0.03, cut at the wavy edge;
                // the buried inner surface is invisible inside the cream.
                float solid = SdfMesher.Ellipsoid(p, headC, headR) - 0.03f;
                float f = SdfMesher.Smax(solid, edgeY - p.y, 0.05f); // rounded lip at the pour edge
                for (int i = 0; i < 10; i++)
                {
                    float a = i * (Mathf.PI * 2f / 10f) + 0.31f;
                    float fr = Mathf.Clamp01((Mathf.Cos(a) - 0.35f) / 0.65f);
                    float len = Mathf.Lerp(0.20f + 0.05f * Mathf.Sin(i * 2.3f), 0.07f, fr);
                    float ey = Mathf.Lerp(1.30f, 1.14f, fr) + 0.05f * Mathf.Sin(a * 4f + 1.3f);
                    var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    float shrink = Mathf.Sqrt(Mathf.Max(0.05f, 1f - Mathf.Pow((ey - headC.y) / headR.y, 2f)));
                    var top = headC + new Vector3(dir.x * (headR.x * shrink + 0.02f), ey - headC.y, dir.z * (headR.z * shrink + 0.02f));
                    var tip = top + new Vector3(-dir.x * 0.03f, -len, -dir.z * 0.03f);
                    f = SdfMesher.Smin(f, SdfMesher.Capsule(p, top, tip, 0.045f), 0.03f);
                }
                return f;
            };

            // Body glaze: the whole body is coated caramel + small drips
            // running down from the neck.
            SdfMesher.Field bodyCaramelF = p =>
            {
                // Solid glazed body: the body ellipsoid inflated 0.028.
                float f = SdfMesher.Ellipsoid(p, bodyC, bodyR) - 0.028f;
                foreach (var deg in new[] { 30f, 95f, 150f, 210f, 265f, 330f })
                {
                    float a = deg * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    float shrink = Mathf.Sqrt(Mathf.Max(0.05f, 1f - Mathf.Pow((0.48f - bodyC.y) / bodyR.y, 2f)));
                    var top = bodyC + new Vector3(dir.x * (bodyR.x * shrink + 0.02f), 0.20f, dir.z * (bodyR.z * shrink + 0.02f));
                    var tip = top + new Vector3(dir.x * 0.02f, -0.09f, dir.z * 0.02f);
                    f = SdfMesher.Smin(f, SdfMesher.Capsule(p, top, tip, 0.038f), 0.025f);
                }
                return f;
            };

            // ------------------------------------------------ meshes
            var creamMesh = SdfMesher.Build(creamF, new Vector3(-0.85f, -0.05f, -0.75f), new Vector3(0.85f, 1.62f, 0.75f), 96);
            creamMesh.name = "BunnyCream";
            MeshPart(bt, "CreamBody", SaveMesh(creamMesh, "BunnyCream"), cream);

            var headCaramelMesh = SdfMesher.Build(headCaramelF, new Vector3(-0.8f, 0.95f, -0.65f), new Vector3(0.8f, 1.62f, 0.65f), 88);
            headCaramelMesh.name = "BunnyCaramelHead";
            MeshPart(bt, "CaramelHead", SaveMesh(headCaramelMesh, "BunnyCaramelHead"), caramel);

            var bodyCaramelMesh = SdfMesher.Build(bodyCaramelF, new Vector3(-0.55f, -0.02f, -0.5f), new Vector3(0.55f, 0.62f, 0.5f), 64);
            bodyCaramelMesh.name = "BunnyCaramelBody";
            MeshPart(bt, "CaramelBody", SaveMesh(bodyCaramelMesh, "BunnyCaramelBody"), caramel);

            // ------------------------------------------------ ears (own SDF meshes under the existing pivots)
            // Each ear: cream drooping blade + a honey caramel coat over its
            // upper/outer half with a drip. ~55-60 deg from vertical, tips
            // out and down like the reference.
            foreach (var side in new[] { -1f, 1f })
            {
                string sfx = side < 0f ? "L" : "R";
                var pivot = new GameObject("EarPivot" + sfx);
                pivot.transform.SetParent(bt, false);
                pivot.transform.localPosition = new Vector3(side * 0.42f, 1.26f, -0.02f);
                pivot.transform.localRotation = Quaternion.Euler(0f, 0f, side * 8f);
                var pt = pivot.transform;

                // Big wide lop ears (v12b): ~60 deg from vertical, sticking
                // out to the sides and drooping, clearly two ears from the
                // behind/above gameplay camera.
                var axis = new Vector3(side * 1.05f, -0.6f, 0f).normalized;
                var thin = (Vector3.forward - axis * Vector3.Dot(axis, Vector3.forward)).normalized;
                var earRot = Quaternion.LookRotation(thin, axis); // local y -> ear axis, local z -> thickness
                float EarSd(Vector3 p)
                {
                    float root = SdfMesher.Ellipsoid(p, axis * 0.12f, new Vector3(0.17f, 0.20f, 0.08f), earRot);
                    float mid = SdfMesher.Ellipsoid(p, axis * 0.50f, new Vector3(0.27f, 0.27f, 0.09f), earRot);
                    float tip = SdfMesher.Ellipsoid(p, axis * 0.85f, new Vector3(0.22f, 0.21f, 0.10f), earRot);
                    return SdfMesher.Smin(SdfMesher.Smin(root, mid, 0.08f), tip, 0.08f);
                }
                SdfMesher.Field earF = p => EarSd(p);
                SdfMesher.Field earCoatF = p =>
                {
                    // Solid honey glaze over the half nearer the head + a drip.
                    float along = Vector3.Dot(p, axis);
                    float f = SdfMesher.Smax(EarSd(p) - 0.028f, along - 0.5f, 0.05f);
                    float drip = SdfMesher.Capsule(p, axis * 0.45f, axis * 0.64f, 0.055f);
                    return SdfMesher.Smin(f, drip, 0.03f);
                };
                var em = SdfMesher.Build(earF, new Vector3(-0.25f, -0.85f, -0.35f), new Vector3(1.15f, 0.3f, 0.35f), 48);
                em.name = $"BunnyEar{sfx}";
                MeshPart(pt, "Ear", SaveMesh(em, $"BunnyEar{sfx}"), cream);
                var cm = SdfMesher.Build(earCoatF, new Vector3(-0.25f, -0.85f, -0.35f), new Vector3(1.15f, 0.3f, 0.35f), 40);
                cm.name = $"BunnyEarCaramel{sfx}";
                MeshPart(pt, "EarCaramel", SaveMesh(cm, $"BunnyEarCaramel{sfx}"), honey);
            }

            // ------------------------------------------------ face
            // Every feature is raymarched ONTO the cream surface along its
            // direction, oriented to the surface normal and inset - nothing
            // floats, nothing clips.
            void Face(string name, Vector3 dir, Vector3 scale, float outAmt, Material m)
            {
                var d = dir.normalized;
                var s = SdfMesher.SurfacePoint(creamF, headC + d * 1.7f, -d, out var n);
                var p = Part(PrimitiveType.Sphere, bt, name, s + n * outAmt, scale, m);
                p.transform.localRotation = Quaternion.LookRotation(n);
                // no shadow casting on face features - they streaked the cheeks
                p.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach (var side in new[] { -1f, 1f })
            {
                Face("Eye", new Vector3(side * 0.42f, -0.10f, 0.85f), new Vector3(0.2f, 0.26f, 0.09f), 0.02f, eyeM);
                Face("Iris", new Vector3(side * 0.42f, -0.14f, 0.85f), new Vector3(0.13f, 0.14f, 0.05f), 0.05f, irisM);
                Face("EyeShine", new Vector3(side * 0.34f, -0.02f, 0.9f), new Vector3(0.08f, 0.09f, 0.035f), 0.06f, shineM);
                Face("EyeShine2", new Vector3(side * 0.5f, -0.16f, 0.8f), new Vector3(0.035f, 0.035f, 0.02f), 0.065f, shineM);
                Face("Blush", new Vector3(side * 0.75f, -0.30f, 0.6f), new Vector3(0.18f, 0.1f, 0.05f), 0.015f, blushM);
            }
            Face("Nose", new Vector3(0f, -0.14f, 1f), new Vector3(0.07f, 0.05f, 0.04f), 0.018f, noseM);
            Face("Mouth", new Vector3(0f, -0.24f, 1f), new Vector3(0.1f, 0.042f, 0.03f), 0.008f, mouthM);
            Face("Tongue", new Vector3(0f, -0.265f, 1f), new Vector3(0.055f, 0.028f, 0.025f), 0.022f, tongueM);

            // ------------------------------------------------ limbs on the existing pivots
            // Tiny caramel arms with cream hands + cream legs/feet (BunnyBody
            // swings them). Bases sit inside the body surface: attached, not floating.
            foreach (var side in new[] { -1f, 1f })
            {
                string sfx = side < 0f ? "L" : "R";
                var armPivot = new GameObject("ArmPivot" + sfx);
                armPivot.transform.SetParent(bt, false);
                armPivot.transform.localPosition = new Vector3(side * 0.35f, 0.44f, 0.06f);
                armPivot.transform.localRotation = Quaternion.Euler(0f, 0f, side * 10f);
                Part(PrimitiveType.Capsule, armPivot.transform, "Arm", new Vector3(0f, -0.1f, 0f), new Vector3(0.17f, 0.18f, 0.16f), caramel);
                Part(PrimitiveType.Sphere, armPivot.transform, "Hand", new Vector3(0f, -0.22f, 0f), new Vector3(0.13f, 0.12f, 0.12f), cream);
            }
            foreach (var side in new[] { -1f, 1f })
            {
                string sfx = side < 0f ? "L" : "R";
                var legPivot = new GameObject("LegPivot" + sfx);
                legPivot.transform.SetParent(bt, false);
                legPivot.transform.localPosition = new Vector3(side * 0.2f, 0.16f, 0.03f);
                Part(PrimitiveType.Sphere, legPivot.transform, "Leg", new Vector3(0f, -0.05f, 0.01f), new Vector3(0.19f, 0.17f, 0.19f), cream);
                Part(PrimitiveType.Sphere, legPivot.transform, "Foot", new Vector3(0f, -0.1f, 0.08f), new Vector3(0.23f, 0.11f, 0.32f), cream);
            }
            return player;
        }

        /// <summary>A generated-mesh part under a parent (no collider).</summary>
        static GameObject MeshPart(Transform parent, string name, Mesh mesh, Material m)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }
    }
}
#endif
