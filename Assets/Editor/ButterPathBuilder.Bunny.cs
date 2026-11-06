#if UNITY_EDITOR
using UnityEngine;
using Crunchee.Player;

namespace Crunchee.EditorTools
{
    /// <summary>
    /// Pudding Bunny, the locked player character: a glossy caramel-glazed
    /// jelly pudding chibi built procedurally from primitives.
    /// </summary>
    public static partial class ButterPathBuilder
    {
        /// <summary>
        /// Pudding Bunny (locked character) - v7 (Faith, Nov 3 evening,
        /// supersedes the v6 notes): she asked for "cuter and realer and
        /// like an actual bunny" - ROUND head, long LOP ears with pink
        /// inners flopping from the top of the head, bigger glossy eyes, a
        /// tiny pink nose, long bunny feet, big fluffy white tail. The
        /// caramel identity stays: thick glossy pour over head + shoulders,
        /// long side drips, scalloped hairline + chest edges, face center
        /// clean (v5's center drip read as a nose; v6 scallops fixed to
        /// read as ONE wavy edge). No cherry, no swirl, no cream patch.
        /// </summary>
        static GameObject BuildPuddingBunny(Vector3 at)
        {
            // v9 (Faith, Nov 3-5): rebuilt to match her reference image EXACTLY -
            // a glossy jelly pudding bunny: HUGE round head on a small chubby
            // body, very long WIDE lop ears with caramel poured over their tops,
            // a glossy caramel helmet-cap with a wavy drip edge, big glossy
            // brown eyes, tiny pink nose, an OPEN smile with a little tongue,
            // a caramel collar poured around her neck, tiny arms and legs.
            // Gameplay shows her BACK; ears/arms/legs hang off pivots so
            // BunnyBody can flop the ears and raise the arms on jumps and
            // swing the tiny legs while she walks.
            // v10 (Faith, Nov 6 reference image): a caramel-glazed chibi bunny.
            // Golden-cream jelly head that is almost all of her, a glossy
            // amber caramel pour over the crown with drips all the way round
            // (the top-down camera sees the crown and back most), long wide
            // lop ears glazed on top and flaring out at the tips, huge glossy
            // eyes with two highlights, pink blush, tiny nose and smile, and a
            // small caramel body with a cream belly.
            // v11 (Faith, Nov 6: "cuter and chubbier"): bigger rounder head
            // with cheeks that melt into it, the caramel cap hugs the crown
            // instead of floating like a pancake, drips are short and round,
            // the lop ears DROOP down the sides of the head instead of
            // winging out, eyes bigger, the body rounder, limbs stubbier.
            var pudding = Mat("Pudding", new Color(1f, 0.9f, 0.7f), 1f, 0.95f);
            pudding.EnableKeyword("_EMISSION");
            pudding.SetColor("_EmissionColor", new Color(0.2f, 0.14f, 0.07f)); // soft jelly glow so the face never goes grey
            pudding.SetFloat("_RimStrength", 0.28f);
            pudding.SetColor("_RimColor", new Color(1f, 0.9f, 0.75f));
            var caramel = Mat("Caramel", new Color(0.76f, 0.44f, 0.14f), 1f, 1f);
            caramel.SetFloat("_SpecStrength", 1.8f);
            caramel.SetFloat("_RimStrength", 0.2f);
            caramel.SetColor("_RimColor", new Color(1f, 0.7f, 0.4f));
            var caramelLight = Mat("CaramelLight", new Color(0.93f, 0.66f, 0.32f), 1f, 1f);
            caramelLight.SetFloat("_SpecStrength", 1.6f);
            var eyeM = Mat("EyeDark", new Color(0.12f, 0.06f, 0.03f), 1f, 1f);
            eyeM.SetFloat("_SpecStrength", 2.2f);
            var irisM = Mat("EyeIris", new Color(0.36f, 0.18f, 0.07f), 1f, 1f);
            var shineM = EmissiveMat("EyeShine", Color.white, 0.6f);
            var blushM = Mat("Blush", new Color(1f, 0.55f, 0.62f), 0.6f, 0.4f);
            NoSheen(blushM);
            var noseM = Mat("Nose", new Color(0.95f, 0.5f, 0.58f), 1f, 0.8f);
            var mouthM = Mat("Mouth", new Color(0.42f, 0.13f, 0.14f), 1f, 0.6f);
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

            // Body: round little caramel body with a cream belly (chubbier v11)
            Part(PrimitiveType.Sphere, bt, "Body", new Vector3(0f, 0.3f, 0f), new Vector3(0.72f, 0.58f, 0.62f), caramelLight);
            Part(PrimitiveType.Sphere, bt, "Belly", new Vector3(0f, 0.28f, 0.15f), new Vector3(0.5f, 0.46f, 0.42f), pudding);
            // HUGE round head + big low cheeks that melt into it
            Part(PrimitiveType.Sphere, bt, "Head", new Vector3(0f, 0.98f, 0f), new Vector3(1.3f, 1.12f, 1.08f), pudding);
            foreach (var side in new[] { -1f, 1f })
                Part(PrimitiveType.Sphere, bt, "Cheek", new Vector3(side * 0.3f, 0.76f, 0.18f), new Vector3(0.6f, 0.5f, 0.52f), pudding);
            // Caramel pour hugging the crown (a dome on top, not a pancake)
            Part(PrimitiveType.Sphere, bt, "CaramelCap", new Vector3(0f, 1.26f, -0.03f), new Vector3(1.12f, 0.62f, 0.92f), caramel);
            // Drips all the way round the crown edge: short over the face,
            // a little longer down the sides and back - small, round, hugging.
            // They hang from the cap's rim (topY ~1.2) so the pour reads connected.
            for (int i = 0; i < 12; i++)
            {
                float deg = i * (360f / 12f) + 15f;
                float a = deg * Mathf.Deg2Rad;
                float front = Mathf.Clamp01((Mathf.Cos(a) - 0.35f) / 0.65f); // 1 = right over the eyes
                float len = Mathf.Lerp(0.17f + 0.07f * Mathf.Sin(i * 2.3f), 0.07f, front);
                float topY = 1.2f;
                float y = topY - len / 2f;
                float shrink = Mathf.Sqrt(Mathf.Max(0.05f, 1f - Mathf.Pow((y - 0.98f) / 0.56f, 2f)));
                var outDir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var pos = new Vector3(outDir.x * 0.63f * shrink, y, outDir.z * 0.52f * shrink);
                var d = Part(PrimitiveType.Capsule, bt, "CrownDrip", pos, new Vector3(0.1f, Mathf.Max(0.05f, len * 0.5f), 0.06f), caramel);
                d.transform.localRotation = Quaternion.Euler(0f, deg, 0f);
                Part(PrimitiveType.Sphere, bt, "DripBead", pos + Vector3.down * (len * 0.5f - 0.01f), new Vector3(0.11f, 0.1f, 0.075f), caramel);
            }
            // Caramel collar pour with a few drips down the body
            Part(PrimitiveType.Sphere, bt, "CaramelPour", new Vector3(0f, 0.58f, 0f), new Vector3(0.78f, 0.22f, 0.68f), caramel);
            foreach (var deg in new[] { 30f, 95f, 150f, 210f, 265f, 330f })
            {
                float a = deg * Mathf.Deg2Rad;
                var d = Part(PrimitiveType.Capsule, bt, "CollarDrip", new Vector3(Mathf.Sin(a) * 0.32f, 0.48f, Mathf.Cos(a) * 0.28f), new Vector3(0.1f, 0.09f, 0.07f), caramel);
                d.transform.localRotation = Quaternion.Euler(0f, deg, 0f);
            }
            // Long thick LOP ears that hang down the sides of the head (v11:
            // droopy like the reference, not wings). Glazed with caramel over
            // their top halves where the pour runs onto them, plump rounded
            // tips. BunnyBody flops them from the same pivots.
            foreach (var side in new[] { -1f, 1f })
            {
                string sfx = side < 0f ? "L" : "R";
                var pivot = new GameObject("EarPivot" + sfx);
                pivot.transform.SetParent(bt, false);
                pivot.transform.localPosition = new Vector3(side * 0.42f, 1.26f, -0.02f);
                pivot.transform.localRotation = Quaternion.Euler(0f, 0f, side * 14f);
                var pt = pivot.transform;
                var ear = Part(PrimitiveType.Capsule, pt, "Ear", new Vector3(side * 0.03f, -0.36f, 0f), new Vector3(0.46f, 0.52f, 0.22f), pudding);
                ear.transform.localRotation = Quaternion.Euler(0f, 0f, side * 6f);
                var tip = Part(PrimitiveType.Sphere, pt, "EarTip", new Vector3(side * 0.08f, -0.78f, 0f), new Vector3(0.44f, 0.34f, 0.24f), pudding);
                tip.transform.localRotation = Quaternion.Euler(0f, 0f, side * 10f);
                var glaze = Part(PrimitiveType.Capsule, pt, "EarCaramel", new Vector3(0f, -0.15f, 0f), new Vector3(0.48f, 0.3f, 0.25f), caramel);
                glaze.transform.localRotation = Quaternion.Euler(0f, 0f, side * 6f);
                Part(PrimitiveType.Capsule, pt, "EarDrip", new Vector3(side * 0.16f, -0.34f, 0.06f), new Vector3(0.1f, 0.14f, 0.07f), caramel);
                Part(PrimitiveType.Sphere, pt, "EarDripBead", new Vector3(side * 0.17f, -0.43f, 0.06f), new Vector3(0.11f, 0.1f, 0.08f), caramel);
            }
            // Face: huge glossy eyes with two highlights, blush, nose, open smile
            foreach (var side in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Sphere, bt, "Eye", new Vector3(side * 0.24f, 0.9f, 0.46f), new Vector3(0.2f, 0.26f, 0.09f), eyeM);
                Part(PrimitiveType.Sphere, bt, "Iris", new Vector3(side * 0.24f, 0.86f, 0.495f), new Vector3(0.13f, 0.14f, 0.04f), irisM);
                Part(PrimitiveType.Sphere, bt, "EyeShine", new Vector3(side * 0.2f, 0.96f, 0.515f), new Vector3(0.08f, 0.09f, 0.03f), shineM);
                Part(PrimitiveType.Sphere, bt, "EyeShine2", new Vector3(side * 0.28f, 0.84f, 0.51f), new Vector3(0.035f, 0.035f, 0.02f), shineM);
                Part(PrimitiveType.Sphere, bt, "Blush", new Vector3(side * 0.38f, 0.76f, 0.46f), new Vector3(0.19f, 0.1f, 0.045f), blushM);
            }
            Part(PrimitiveType.Sphere, bt, "Nose", new Vector3(0f, 0.82f, 0.53f), new Vector3(0.075f, 0.055f, 0.045f), noseM);
            Part(PrimitiveType.Sphere, bt, "Mouth", new Vector3(0f, 0.755f, 0.51f), new Vector3(0.15f, 0.055f, 0.045f), mouthM);
            Part(PrimitiveType.Sphere, bt, "Tongue", new Vector3(0f, 0.744f, 0.527f), new Vector3(0.085f, 0.04f, 0.035f), tongueM);
            // Tiny stubby caramel arms + cream legs on pivots (BunnyBody swings them)
            foreach (var side in new[] { -1f, 1f })
            {
                string sfx = side < 0f ? "L" : "R";
                var armPivot = new GameObject("ArmPivot" + sfx);
                armPivot.transform.SetParent(bt, false);
                armPivot.transform.localPosition = new Vector3(side * 0.35f, 0.44f, 0.06f);
                armPivot.transform.localRotation = Quaternion.Euler(0f, 0f, side * 10f);
                Part(PrimitiveType.Capsule, armPivot.transform, "Arm", new Vector3(0f, -0.1f, 0f), new Vector3(0.17f, 0.18f, 0.16f), caramelLight);
                var legPivot = new GameObject("LegPivot" + sfx);
                legPivot.transform.SetParent(bt, false);
                legPivot.transform.localPosition = new Vector3(side * 0.2f, 0.16f, 0.03f);
                Part(PrimitiveType.Sphere, legPivot.transform, "Leg", new Vector3(0f, -0.05f, 0.01f), new Vector3(0.19f, 0.17f, 0.19f), pudding);
                Part(PrimitiveType.Sphere, legPivot.transform, "Foot", new Vector3(0f, -0.1f, 0.08f), new Vector3(0.23f, 0.11f, 0.32f), pudding);
            }
            // Cream tail puff
            Part(PrimitiveType.Sphere, bt, "Tail", new Vector3(0f, 0.3f, -0.34f), new Vector3(0.22f, 0.22f, 0.22f), pudding);
            return player;
        }
    }
}
#endif
