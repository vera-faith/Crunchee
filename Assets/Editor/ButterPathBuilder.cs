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
    public static class ButterPathBuilder
    {
        // Mockup proportions: a butter bar is LONG across the path (~2.6x its depth).
        const float ButterW = 4.4f, ButterH = 0.62f, ButterD = 1.7f, PatH = 0.14f;
        const float StepAdvance = ButterD + 0.18f; // edges nearly touch: walk straight across

        static Material Mat(string name, Color c, float alpha = 1f, float smoothness = 0.35f)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(sh) { name = name, color = new Color(c.r, c.g, c.b, alpha) };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (alpha < 1f)
            {
                m.renderQueue = 3000;
                if (m.HasProperty("_Surface"))
                {
                    // URP Lit: transparent surface
                    m.SetFloat("_Surface", 1f);
                    m.SetFloat("_Blend", 0f);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                }
                else if (m.HasProperty("_Mode"))
                {
                    // Built-in Standard shader (this project): Fade/Transparent
                    // mode must be configured explicitly or the material renders
                    // OPAQUE despite its alpha (the black-decal bug, Nov 2).
                    m.SetFloat("_Mode", 3f); // Transparent
                    m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    m.SetInt("_ZWrite", 0);
                    m.DisableKeyword("_ALPHATEST_ON");
                    m.EnableKeyword("_ALPHABLEND_ON");
                    m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                }
            }
            return m;
        }

        static Material EmissiveMat(string name, Color c, float intensity)
        {
            var m = Mat(name, c, 1f, 0.4f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * intensity);
            return m;
        }

        [MenuItem("Crunchee/Build Butter Path Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Random.InitState(11); // stable prop scatter across rebuilds

            // --- Atmosphere: warm peachy backdrop + soft distance fog (mockup glow) ---
            var bg = new Color(0.99f, 0.89f, 0.8f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = bg;
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 105f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.47f, 0.41f);

            // --- Lighting: warm golden bakery sun ---
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.3f;
            sun.color = new Color(1f, 0.86f, 0.64f);
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.shadows = LightShadows.Soft;

            // --- The bakery COUNTER the whole path sits on (mockup: warm peachy
            // countertop, no checker floor) ---
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "BakeryFloor"; floor.transform.localScale = new Vector3(4f, 1f, 14f);
            floor.transform.position = new Vector3(0f, -0.06f, 26f);
            var floorMat = Mat("Countertop", Color.white, 1f, 0.5f);
            floorMat.mainTexture = CounterTexture();
            floorMat.mainTextureScale = new Vector2(5f, 18f);
            floor.GetComponent<MeshRenderer>().material = floorMat;

            var butterMat = Mat("Butter", new Color(1f, 0.9f, 0.5f), 1f, 0.75f);
            butterMat.mainTexture = ButterSwirlTexture();
            var patMat = Mat("ButterPat", new Color(1f, 0.92f, 0.55f), 1f, 0.8f);
            patMat.mainTexture = ButterSwirlTexture();
            var platformMat = Mat("Platform", new Color(0.98f, 0.94f, 0.86f), 1f, 0.5f);
            var frostingMat = Mat("Frosting", new Color(0.995f, 0.97f, 0.93f), 1f, 0.35f);

            // --- Start: a white FROSTING slab on a wooden board, sprinkles on top
            // (mockup). Top stays at 0.49 so the first climb is unchanged. ---
            Vector3 pos = Vector3.zero;
            var board = Box("StartBoard", new Vector3(0f, 0.03f, 0f), new Vector3(4.7f, 0.12f, 4.7f), Mat("Board", new Color(0.85f, 0.62f, 0.44f), 1f, 0.45f));
            Object.DestroyImmediate(board.GetComponent<Collider>());
            var start = Box("StartPlatform", new Vector3(0f, 0.29f, 0f), new Vector3(4f, 0.4f, 4f), frostingMat);
            SprinkleTops(new Vector3(0f, 0.5f, 0f), 1.8f, 44);

            // --- The path: just the 10 wax butters, whiter each step (layers 1..10) ---
            foreach (var s in LevelPath.ButterPath())
            {
                pos += new Vector3(s.sideOffset, s.heightOffset, StepAdvance);
                BuildButter(pos, $"Butter_{s.waxLayers:00}_Wax{s.waxLayers}", s.coating, s.waxLayers, butterMat, patMat);
            }

            // --- Finish platform + dressing ---
            pos += new Vector3(0f, 0.35f, 3.4f);
            var finish = Box("FinishPlatform", pos, new Vector3(4.2f, 0.4f, 4.2f), platformMat);
            finish.AddComponent<FinishTrigger>();
            BuildBakeryDressing(pos);

            // --- Player: Pudding Bunny ---
            var player = BuildPuddingBunny(new Vector3(0f, 1.0f, 0f));
            var pc = player.GetComponent<PlayerController>();

            // --- Camera: follow a little lower + closer than bird's-eye, like the
            // mockup's over-the-shoulder perspective down the path ---
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<ThirdPersonFollow>();
            follow.target = player.transform;
            follow.offset = new Vector3(0f, 5.8f, -5.6f);
            follow.lookUpBias = 1.5f;
            pc.cameraRig = camGo.transform;

            // --- Audio root ---
            var audio = new GameObject("AsmrAudio"); audio.AddComponent<AsmrAudio>();

            // --- UI ---
            BuildUI(player);

            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/ButterPath.unity");
            Debug.Log("[Crunchee] Butter Path scene built: Assets/Scenes/ButterPath.unity — press Play.");
        }

        /// <summary>One giant butter step: body + pat top + coating shell + particles + Squishy.</summary>
        static void BuildButter(Vector3 pos, string name, Coating coating, int waxLayers, Material butterMat, Material patMat, Color polishColor = default)
        {
            var step = Box(name, pos, new Vector3(ButterW, ButterH, ButterD), butterMat);

            // Little serving TRAY under the bar (mockup: every butter sits on one).
            var tray = Box("Tray", pos + new Vector3(0f, -ButterH / 2f - 0.035f, 0f),
                           new Vector3(ButterW + 0.55f, 0.07f, ButterD + 0.45f),
                           Mat("Tray", new Color(0.96f, 0.95f, 0.94f), 1f, 0.9f));
            tray.transform.SetParent(step.transform, true);
            Object.DestroyImmediate(tray.GetComponent<Collider>());

            // Butter pat: the thinner slab on top of a real butter stick.
            var pat = Box("Pat", pos + new Vector3(0f, ButterH / 2f + PatH / 2f, 0f),
                          new Vector3(ButterW * 0.94f, PatH, ButterD * 0.92f), patMat);
            pat.transform.SetParent(step.transform, true);
            Object.DestroyImmediate(pat.GetComponent<Collider>());

            // Collider reaches the pat top so the player stands on the pat/shell.
            var col = step.GetComponent<BoxCollider>();
            col.size = new Vector3(1f, (ButterH + PatH) / ButterH, 1f);
            col.center = new Vector3(0f, (PatH / 2f) / ButterH, 0f);

            Material shellMat; Color particleColor; string shellName; bool crackable;
            switch (coating)
            {
                case Coating.Sugar:
                    shellMat = Mat("SugarShell", new Color(1f, 0.93f, 0.95f), 0.88f, 0.9f);
                    particleColor = new Color(1f, 0.8f, 0.88f); shellName = "SugarShell"; crackable = false; break;
                case Coating.Honey:
                    shellMat = Mat("HoneyShell", new Color(1f, 0.62f, 0.12f), 0.5f, 1f);
                    particleColor = new Color(1f, 0.7f, 0.25f); shellName = "HoneyShell"; crackable = false; break;
                case Coating.NailPolish:
                    shellMat = Mat("PolishShell", polishColor == default ? new Color(1f, 0.62f, 0.72f) : polishColor, 1f, 1f);
                    particleColor = shellMat.color; shellName = "PolishShell"; crackable = true; break;
                default: // Wax: translucent build-up - thin golden veil on butter 1,
                    // near-opaque white by butter 10 (mockup gradient), never fully opaque
                    float alpha = Mathf.Min(0.88f, 0.1f + (waxLayers - 1) * 0.085f);
                    shellMat = Mat($"Wax{waxLayers}", new Color(1f, 0.99f, 0.95f), alpha, 0.55f);
                    particleColor = Color.white; shellName = "WaxShell"; crackable = true; break;
            }

            // Shell group sits on the pat. Wax/polish = 4 quadrant pieces (they split
            // apart on the first landing); sugar/honey = one soft piece.
            var shell = new GameObject(shellName);
            shell.transform.position = pos + new Vector3(0f, ButterH / 2f + PatH + ButterH * 0.2f, 0f);
            shell.transform.SetParent(step.transform, true);
            float shellW = ButterW * 1.02f, shellD = ButterD * 1.02f, shellH = ButterH * 0.5f;
            if (crackable)
            {
                foreach (var sx in new[] { -1f, 1f })
                    foreach (var sz in new[] { -1f, 1f })
                    {
                        var piece = Box("ShellPiece", shell.transform.position + new Vector3(sx * shellW / 4f, 0f, sz * shellD / 4f),
                                        new Vector3(shellW / 2f - 0.045f, shellH, shellD / 2f - 0.045f), shellMat);
                        piece.transform.SetParent(shell.transform, true);
                        Object.DestroyImmediate(piece.GetComponent<Collider>());
                    }
            }
            else
            {
                var piece = Box("Shell", shell.transform.position, new Vector3(shellW, shellH, shellD), shellMat);
                piece.transform.SetParent(shell.transform, true);
                Object.DestroyImmediate(piece.GetComponent<Collider>());
            }

            // Crack decal: an invisible plane hugging the shell top. The Squishy
            // draws crack lines into its texture wherever the character walks,
            // so cracks visibly spread and follow her across the butter.
            MeshRenderer decalRend = null;
            if (crackable)
            {
                var decalMat = Mat("CrackDecal", Color.white, 0.99f, 0.5f);
                decalMat.renderQueue = 3002;
                var decal = GameObject.CreatePrimitive(PrimitiveType.Plane);
                decal.name = "CrackDecal";
                decal.transform.position = pos + new Vector3(0f, ButterH / 2f + PatH + ButterH * 0.2f + ButterH * 0.25f + 0.012f, 0f);
                decal.transform.localScale = new Vector3(ButterW / 10f, 1f, ButterD / 10f);
                decal.GetComponent<MeshRenderer>().material = decalMat;
                Object.DestroyImmediate(decal.GetComponent<Collider>());
                decal.transform.SetParent(step.transform, true);
                decalRend = decal.GetComponent<MeshRenderer>();
            }

            // Dent decal: a soft shadow disc that follows the character on the
            // squishy, deepening as she sinks - you can SEE her sinking into it
            // wherever she moves (Faith, Nov 3). Sits just under the crack plane.
            MeshRenderer dentRend = null;
            {
                var dentMat = Mat("Dent", Color.white, 0.99f, 0.5f);
                dentMat.mainTexture = DentTexture();
                dentMat.renderQueue = 3001;
                var dent = GameObject.CreatePrimitive(PrimitiveType.Plane);
                dent.name = "DentDecal";
                dent.transform.position = pos + new Vector3(0f, ButterH / 2f + PatH + ButterH * 0.2f + ButterH * 0.25f + 0.006f, 0f);
                dent.transform.localScale = new Vector3(0.15f, 1f, 0.15f);
                dent.GetComponent<MeshRenderer>().material = dentMat;
                Object.DestroyImmediate(dent.GetComponent<Collider>());
                dent.transform.SetParent(step.transform, true);
                dentRend = dent.GetComponent<MeshRenderer>();
            }

            var sq = step.AddComponent<Squishy>();
            sq.baseMaterial = BaseMaterial.Butter; sq.coating = coating; sq.waxLayers = waxLayers;
            sq.coatingShell = shell;
            sq.crackDecal = decalRend;
            sq.dentDecal = dentRend;

            var ps = new GameObject("StepParticles").AddComponent<ParticleSystem>();
            ps.transform.SetParent(step.transform, false);
            ps.transform.localPosition = new Vector3(0f, ButterH * 0.6f, 0f);
            var main = ps.main; main.startColor = particleColor; main.startSize = 0.09f;
            main.startSpeed = 2.2f; main.startLifetime = 0.7f; main.playOnAwake = false;
            var em = ps.emission; em.enabled = false;
            sq.stepParticles = ps;
        }

        /// <summary>
        /// Pudding Bunny (locked character), rebuilt to the mockup (Nov 3): a big
        /// round glossy head on a small flan body, caramel poured over the head
        /// and ears with drips, long floppy caramel-tipped ears, BIG shiny eyes
        /// with highlights, open smile with a tiny tongue, blush, little arms.
        /// Built from primitives until the model lands.
        /// </summary>
        static GameObject BuildPuddingBunny(Vector3 at)
        {
            // Built to the LOCKED concept art (Faith, Nov 3): a one-piece flan -
            // wide flat-fronted head flowing into a big round belly, thick glossy
            // caramel poured over the head AND shoulders with long drips, very
            // long plain pudding ears hanging straight down, and a tiny simple
            // face (small brown dot eyes, tiny smile, soft blush). No extras.
            var pudding = Mat("Pudding", new Color(1f, 0.89f, 0.7f), 1f, 1f);
            var caramel = Mat("Caramel", new Color(0.85f, 0.47f, 0.13f), 1f, 1f);
            var brown = Mat("FaceBrown", new Color(0.3f, 0.17f, 0.08f), 1f, 0.35f);
            var white = Mat("EyeShine", new Color(1f, 1f, 1f), 1f, 0.3f);
            var blushM = Mat("Blush", new Color(1f, 0.62f, 0.66f), 0.55f, 0.6f);

            var player = new GameObject("Player_PuddingBunny");
            player.transform.position = at;
            var cc = player.AddComponent<CharacterController>(); cc.height = 1.1f; cc.radius = 0.35f; cc.center = new Vector3(0f, 0.55f, 0f);
            player.AddComponent<PlayerController>();
            var model = new GameObject("Model"); model.transform.SetParent(player.transform, false);
            model.AddComponent<SquashAndStretch>();

            // One-piece flan: big round belly + wide flat-fronted head (cylinder
            // gives the concept's flatter face, so drips and face sit right)
            Part(PrimitiveType.Sphere, model.transform, "Body", new Vector3(0f, 0.3f, 0f), new Vector3(0.8f, 0.74f, 0.72f), pudding);
            Part(PrimitiveType.Cylinder, model.transform, "Head", new Vector3(0f, 0.88f, 0f), new Vector3(0.84f, 0.31f, 0.78f), pudding);
            Part(PrimitiveType.Sphere, model.transform, "HeadTop", new Vector3(0f, 1.14f, 0f), new Vector3(0.8f, 0.3f, 0.74f), pudding);
            // Thick caramel poured over the head, with LONG drips down the face
            Part(PrimitiveType.Sphere, model.transform, "CaramelCap", new Vector3(0f, 1.1f, 0f), new Vector3(0.9f, 0.46f, 0.8f), caramel);
            foreach (var side in new[] { -1f, 1f })
                Part(PrimitiveType.Sphere, model.transform, "Drip", new Vector3(side * 0.28f, 0.74f, 0.29f), new Vector3(0.11f, 0.4f, 0.09f), caramel);
            Part(PrimitiveType.Sphere, model.transform, "Drip", new Vector3(0.03f, 0.78f, 0.35f), new Vector3(0.1f, 0.3f, 0.09f), caramel);
            foreach (var side in new[] { -1f, 1f })
                Part(PrimitiveType.Sphere, model.transform, "DripSide", new Vector3(side * 0.4f, 0.82f, 0.05f), new Vector3(0.09f, 0.3f, 0.11f), caramel);
            // Caramel poured over the shoulders too, scalloped drip edge in front
            Part(PrimitiveType.Sphere, model.transform, "CaramelPour", new Vector3(0f, 0.56f, 0f), new Vector3(0.86f, 0.34f, 0.78f), caramel);
            foreach (var sx in new[] { -0.3f, -0.15f, 0f, 0.15f, 0.3f })
            {
                float zz = 0.35f - Mathf.Abs(sx) * 0.36f;
                float yy = 0.42f - Mathf.Abs(sx) * 0.2f;
                Part(PrimitiveType.Sphere, model.transform, "Scallop", new Vector3(sx, yy, zz), new Vector3(0.13f, 0.17f, 0.09f), caramel);
            }
            // Very long plain ears hanging straight down beside the head
            foreach (var side in new[] { -1f, 1f })
            {
                var ear = Part(PrimitiveType.Capsule, model.transform, "Ear", new Vector3(side * 0.45f, 0.74f, -0.02f), new Vector3(0.17f, 0.48f, 0.13f), pudding);
                ear.transform.rotation = Quaternion.Euler(0f, 0f, -side * 5f);
            }
            // Tiny simple face: small brown dot eyes (one sparkle each), a tiny
            // smile line, soft round blush low on the cheeks
            foreach (var side in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Sphere, model.transform, "Eye", new Vector3(side * 0.17f, 0.84f, 0.35f), new Vector3(0.075f, 0.1f, 0.04f), brown);
                Part(PrimitiveType.Sphere, model.transform, "EyeShine", new Vector3(side * 0.17f + 0.016f, 0.875f, 0.365f), new Vector3(0.024f, 0.034f, 0.02f), white);
                Part(PrimitiveType.Sphere, model.transform, "Blush", new Vector3(side * 0.31f, 0.76f, 0.255f), new Vector3(0.13f, 0.085f, 0.03f), blushM);
            }
            Part(PrimitiveType.Sphere, model.transform, "Mouth", new Vector3(0f, 0.73f, 0.382f), new Vector3(0.1f, 0.028f, 0.02f), brown);
            // Stubby arms at her sides, little legs + round feet, tiny tail
            foreach (var side in new[] { -1f, 1f })
            {
                var arm = Part(PrimitiveType.Capsule, model.transform, "Arm", new Vector3(side * 0.44f, 0.3f, 0.04f), new Vector3(0.13f, 0.3f, 0.13f), pudding);
                arm.transform.rotation = Quaternion.Euler(0f, 0f, -side * 8f);
                Part(PrimitiveType.Sphere, model.transform, "Leg", new Vector3(side * 0.17f, 0.14f, 0.05f), new Vector3(0.17f, 0.15f, 0.17f), pudding);
                Part(PrimitiveType.Sphere, model.transform, "Foot", new Vector3(side * 0.17f, 0.05f, 0.09f), new Vector3(0.2f, 0.11f, 0.26f), pudding);
            }
            Part(PrimitiveType.Sphere, model.transform, "Tail", new Vector3(0f, 0.3f, -0.34f), new Vector3(0.17f, 0.17f, 0.17f), pudding);
            return player;
        }

        static GameObject Part(PrimitiveType t, Transform parent, string name, Vector3 localPos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos; go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().material = m;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>
        /// Bakery dressing, mockup layout (Nov 3): no long counters - the props sit
        /// right on the countertop around the path: macarons, cupcakes, a flour
        /// bowl, a honey jar, bread rolls, a berry bowl, cake stands, flour-dust
        /// patches. Pendants + back wall/awning stay as the far backdrop.
        /// </summary>
        static void BuildBakeryDressing(Vector3 finishPos)
        {
            float pathLen = finishPos.z;
            var cakeMats = new[]
            {
                Mat("CakePink", new Color(1f, 0.75f, 0.82f), 1f, 0.6f),
                Mat("CakeMint", new Color(0.72f, 0.95f, 0.8f), 1f, 0.6f),
                Mat("CakeCream", new Color(1f, 0.95f, 0.82f), 1f, 0.6f),
                Mat("CakeChoco", new Color(0.55f, 0.36f, 0.24f), 1f, 0.6f),
            };
            var standMat = Mat("Stand", new Color(0.92f, 0.92f, 0.95f), 1f, 0.9f);
            var cherryMat = Mat("Cherry", new Color(0.9f, 0.15f, 0.25f), 1f, 0.9f);
            int k = 0;
            for (float z = 5.5f; z < pathLen; z += 6.5f, k++)
            {
                float side = (k % 2 == 0) ? -4.4f : 4.4f;
                var g = new GameObject("CakeStand"); g.transform.position = new Vector3(side, -0.05f, z);
                Part(PrimitiveType.Cylinder, g.transform, "Stem", new Vector3(0f, 0.16f, 0f), new Vector3(0.12f, 0.32f, 0.12f), standMat);
                Part(PrimitiveType.Cylinder, g.transform, "Plate", new Vector3(0f, 0.34f, 0f), new Vector3(0.95f, 0.05f, 0.95f), standMat);
                Part(PrimitiveType.Cylinder, g.transform, "Cake", new Vector3(0f, 0.48f, 0f), new Vector3(0.68f, 0.24f, 0.68f), cakeMats[k % cakeMats.Length]);
                Part(PrimitiveType.Sphere, g.transform, "Cherry", new Vector3(0f, 0.66f, 0f), new Vector3(0.13f, 0.13f, 0.13f), cherryMat);
            }

            // --- Countertop props (mockup scatter) ---
            BuildFlourBowl(new Vector3(-4.3f, -0.05f, 3.2f));
            BuildMacarons(new Vector3(3.7f, -0.05f, 3.8f));
            BuildHoneyJar(new Vector3(4.3f, -0.05f, 6.4f));
            BuildCupcake(new Vector3(-3.8f, -0.05f, 6.8f), cakeMats[0], cherryMat);
            BuildCupcake(new Vector3(-4.6f, -0.05f, 7.7f), cakeMats[2], cherryMat);
            BuildMacarons(new Vector3(-3.9f, -0.05f, 10.2f));
            BuildBreadRolls(new Vector3(4.1f, -0.05f, 9.8f));
            BuildCupcake(new Vector3(3.8f, -0.05f, 13.2f), cakeMats[1], cherryMat);
            BuildHoneyJar(new Vector3(-4.4f, -0.05f, 13.8f));
            BuildMacarons(new Vector3(4.2f, -0.05f, 16.6f));
            BuildBreadRolls(new Vector3(-4.2f, -0.05f, 17.2f));
            BuildBerryBowl(new Vector3(-3.7f, -0.05f, 20.2f));
            BuildCupcake(new Vector3(4.0f, -0.05f, 19.6f), cakeMats[3], cherryMat);
            BuildRollingPin(new Vector3(-4.5f, -0.05f, 11.5f));
            BuildMilkBottles(new Vector3(4.5f, -0.05f, 4.9f));
            BuildJamJar(new Vector3(-4.2f, -0.05f, 15.6f));
            BuildJamJar(new Vector3(4.4f, -0.05f, 21.4f));

            // Crumbs scattered near the path edges - a lived-in bakery counter
            var crumbMat = Mat("Crumb", new Color(0.88f, 0.66f, 0.4f), 1f, 0.6f);
            for (int i = 0; i < 16; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Part(PrimitiveType.Sphere, null, "Crumb",
                     new Vector3(side * Random.Range(2.8f, 5.6f), -0.03f, Random.Range(1f, pathLen)),
                     new Vector3(Random.Range(0.04f, 0.08f), 0.03f, Random.Range(0.04f, 0.08f)), crumbMat);
            }

            // Bakery shelves framing the counter (mockup's background shelves):
            // one wide unit against the back wall, two along the sides.
            BuildShelf(finishPos + new Vector3(0f, -finishPos.y - 0.05f, 3.1f), cakeMats, cherryMat, wide: true);
            var shelfL = BuildShelf(new Vector3(-7.8f, -0.05f, 9f), cakeMats, cherryMat);
            shelfL.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            var shelfR = BuildShelf(new Vector3(7.8f, -0.05f, 17f), cakeMats, cherryMat);
            shelfR.transform.rotation = Quaternion.Euler(0f, -90f, 0f);

            // Framed pictures on the back wall
            var frameMat = Mat("Frame", new Color(0.8f, 0.58f, 0.38f), 1f, 0.5f);
            for (int i = 0; i < 3; i++)
            {
                float fx = -3.6f + i * 3.6f;
                Box("Frame", finishPos + new Vector3(fx, 2.9f, 4.28f), new Vector3(1.7f, 2.1f, 0.07f), frameMat);
                Box("FrameArt", finishPos + new Vector3(fx, 2.9f, 4.23f), new Vector3(1.45f, 1.85f, 0.07f), cakeMats[i % cakeMats.Length]);
            }

            // Flour-dust patches sprinkled on the counter
            var dustMat = Mat("FlourDust", Color.white, 0.32f, 0.2f);
            for (int i = 0; i < 8; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var d = Part(PrimitiveType.Cylinder, null, "FlourDust",
                             new Vector3(side * Random.Range(3.1f, 5.4f), -0.045f, Random.Range(1.5f, pathLen)),
                             new Vector3(Random.Range(0.7f, 1.5f), 0.012f, Random.Range(0.7f, 1.5f)), dustMat);
                d.transform.parent = null;
            }
            // Pendant lights down the middle of the path
            var cordMat = Mat("Cord", new Color(0.35f, 0.25f, 0.2f), 1f, 0.4f);
            var bulbMat = EmissiveMat("Bulb", new Color(1f, 0.78f, 0.5f), 1.6f);
            for (float z = 9f; z < pathLen; z += 11f)
            {
                float stepTop = (z / StepAdvance) * 0.35f + 0.8f; // the path climbs 0.35 per step
                var g = new GameObject("Pendant"); g.transform.position = new Vector3(0f, stepTop + 5.6f, z);
                Part(PrimitiveType.Cylinder, g.transform, "Cord", new Vector3(0f, -1.1f, 0f), new Vector3(0.035f, 2.2f, 0.035f), cordMat);
                Part(PrimitiveType.Sphere, g.transform, "Bulb", new Vector3(0f, -2.35f, 0f), new Vector3(0.34f, 0.34f, 0.34f), bulbMat);
            }
            // Back wall + striped awning + bunting
            var wall = Box("BakeryWall", finishPos + new Vector3(0f, 2.2f, 4.5f), new Vector3(16f, 6f, 0.3f), Mat("Wall", new Color(0.99f, 0.87f, 0.9f), 1f, 0.25f));
            var awnPink = Mat("AwningPink", new Color(0.97f, 0.6f, 0.7f), 1f, 0.45f);
            var awnWhite = Mat("AwningWhite", new Color(1f, 0.98f, 0.95f), 1f, 0.45f);
            for (int i = 0; i < 8; i++)
            {
                var stripe = Box("Awning", finishPos + new Vector3(-7f + i * 2f + 1f, 5.6f, 3.9f), new Vector3(2f, 0.08f, 1.6f), i % 2 == 0 ? awnPink : awnWhite);
                stripe.transform.rotation = Quaternion.Euler(-22f, 0f, 0f);
            }
            var buntMats = new[] { cakeMats[0], cakeMats[1], cakeMats[2], cherryMat };
            for (int i = 0; i < 9; i++)
            {
                float x = -4f + i;
                float y = finishPos.y + 4.6f - Mathf.Sin(i / 8f * Mathf.PI) * 0.35f;
                Part(PrimitiveType.Sphere, wall.transform.parent, "Bunting", new Vector3(x, y, finishPos.z + 4.3f), new Vector3(0.22f, 0.3f, 0.1f), buntMats[i % buntMats.Length]);
            }
        }

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

        static void BuildCupcake(Vector3 at, Material frosting, Material cherryMat)
        {
            var wrap = Mat("CupcakeWrap", new Color(0.92f, 0.72f, 0.6f), 1f, 0.5f);
            var g = new GameObject("Cupcake"); g.transform.position = at;
            Part(PrimitiveType.Cylinder, g.transform, "Wrap", new Vector3(0f, 0.11f, 0f), new Vector3(0.34f, 0.22f, 0.34f), wrap);
            Part(PrimitiveType.Sphere, g.transform, "Frosting1", new Vector3(0f, 0.27f, 0f), new Vector3(0.42f, 0.2f, 0.42f), frosting);
            Part(PrimitiveType.Sphere, g.transform, "Frosting2", new Vector3(0f, 0.38f, 0f), new Vector3(0.28f, 0.16f, 0.28f), frosting);
            Part(PrimitiveType.Sphere, g.transform, "Cherry", new Vector3(0f, 0.48f, 0f), new Vector3(0.11f, 0.11f, 0.11f), cherryMat);
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
        static Texture2D LoadOrCreateTexture(string path, int w, int h, System.Func<int, int, int, int, Color> pixel)
        {
            // Always regenerate: the pixel functions in code are the single
            // source of truth, so color tweaks apply on the next build.
            if (!AssetDatabase.IsValidFolder("Assets/Textures")) AssetDatabase.CreateFolder("Assets", "Textures");
            var tex = new Texture2D(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, pixel(x, y, w, h));
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath, path.Substring("Assets/".Length)), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Glossy butter surface: soft golden swirls + a few bright streaks (mockup).</summary>
        static Texture2D ButterSwirlTexture()
        {
            return LoadOrCreateTexture("Assets/Textures/ButterSwirl.png", 256, 128, (x, y, w, h) =>
            {
                float u = x / (float)w, v = y / (float)h;
                float wave = Mathf.Sin((v * 3.2f + Mathf.Sin(u * 6.28f) * 0.35f) * 6.28f) * 0.5f + 0.5f;
                var deep = new Color(0.96f, 0.74f, 0.2f);
                var lite = new Color(1f, 0.87f, 0.42f);
                var c = Color.Lerp(deep, lite, wave * 0.65f + 0.18f);
                float streak = Mathf.Pow(Mathf.Max(0f, Mathf.Sin((u * 0.8f + v) * 10f)), 10f) * 0.13f;
                return new Color(Mathf.Min(1f, c.r + streak), Mathf.Min(1f, c.g + streak), Mathf.Min(1f, c.b + streak * 0.8f));
            });
        }

        /// <summary>Warm peachy countertop: layered mottle, fine speckle, faint plank seams.</summary>
        static Texture2D CounterTexture()
        {
            return LoadOrCreateTexture("Assets/Textures/Countertop.png", 256, 256, (x, y, w, h) =>
            {
                float n = Mathf.Sin(x * 0.11f) * Mathf.Sin(y * 0.13f) * 0.5f
                        + Mathf.Sin((x + y) * 0.021f) * 0.5f
                        + Mathf.Sin(x * 0.53f + y * 0.31f) * 0.12f;
                var a = new Color(0.965f, 0.79f, 0.6f);
                var b = new Color(0.915f, 0.69f, 0.49f);
                var c = Color.Lerp(a, b, Mathf.Clamp01(n * 0.5f + 0.5f));
                float hash = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
                float speck = (hash - Mathf.Floor(hash) - 0.5f) * 0.045f; // fine grain
                float seam = (y % 64) < 2 ? 0.93f : 1f;                    // plank seams
                float sheen = Mathf.Pow(Mathf.Max(0f, Mathf.Sin((x / (float)w) * 3.14159f)), 2f) * 0.03f;
                return new Color(
                    Mathf.Clamp01((c.r + speck + sheen) * seam),
                    Mathf.Clamp01((c.g + speck + sheen) * seam),
                    Mathf.Clamp01((c.b + speck + sheen * 0.7f) * seam));
            });
        }

        /// <summary>Soft radial shadow for the dent she presses into a squishy.</summary>
        static Texture2D DentTexture()
        {
            return LoadOrCreateTexture("Assets/Textures/Dent.png", 128, 128, (x, y, w, h) =>
            {
                float dx = (x - w / 2f) / (w / 2f), dy = (y - h / 2f) / (h / 2f);
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float a = Mathf.Pow(1f - d, 1.7f) * 0.85f;
                return new Color(0.36f, 0.22f, 0.12f, a);
            });
        }

        /// <summary>Tiny pastel sprinkles scattered on a surface (start frosting slab).</summary>
        static void SprinkleTops(Vector3 center, float radius, int count)
        {
            var mats = new[]
            {
                Mat("SprinklePink", new Color(1f, 0.6f, 0.72f), 1f, 0.6f),
                Mat("SprinkleMint", new Color(0.6f, 0.9f, 0.75f), 1f, 0.6f),
                Mat("SprinkleBlue", new Color(0.6f, 0.8f, 1f), 1f, 0.6f),
                Mat("SprinkleYellow", new Color(1f, 0.88f, 0.45f), 1f, 0.6f),
                Mat("SprinkleWhite", new Color(1f, 1f, 1f), 1f, 0.6f),
            };
            var holder = new GameObject("Sprinkles");
            for (int i = 0; i < count; i++)
            {
                var s = Part(PrimitiveType.Capsule, holder.transform, "Sprinkle",
                             center + new Vector3(Random.Range(-radius, radius), 0.012f, Random.Range(-radius, radius)),
                             new Vector3(0.035f, 0.05f, 0.035f), mats[i % mats.Length]);
                s.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 180f), 0f);
            }
        }

        static GameObject Box(string name, Vector3 p, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.position = p; go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().material = m;
            return go;
        }

        static void BuildUI(GameObject player)
        {
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();
            var controls = canvasGo.AddComponent<MobileControls>();
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var joyBase = MakeCircle(canvasGo.transform, "JoystickBase", new Vector2(150f, 210f), 190f, new Color(1, 1, 1, 0.28f));
            var knob = MakeCircle(joyBase.transform, "Knob", Vector2.zero, 96f, new Color(1, 1, 1, 0.55f));
            var joy = joyBase.gameObject.AddComponent<VirtualJoystick>();
            joy.baseRect = joyBase; joy.knobRect = knob;
            MakeLabel(canvasGo.transform, "MOVE", new Vector2(150f, 78f), font);

            var jump = MakeCircle(canvasGo.transform, "JumpButton", new Vector2(-150f, 210f), 210f, new Color(1f, 0.62f, 0.72f, 0.45f), anchorRight: true);
            jump.gameObject.AddComponent<JumpButton>();
            var arrow = new GameObject("Arrow").AddComponent<Text>();
            arrow.transform.SetParent(jump, false);
            arrow.text = "▲"; arrow.font = font; arrow.fontSize = 84; arrow.alignment = TextAnchor.MiddleCenter;
            arrow.color = Color.white;
            var art = arrow.rectTransform; art.anchorMin = art.anchorMax = new Vector2(0.5f, 0.5f); art.sizeDelta = new Vector2(140f, 140f);
            MakeLabel(canvasGo.transform, "JUMP", new Vector2(-150f, 78f), font, anchorRight: true);
            _ = controls;
        }

        static RectTransform MakeCircle(Transform parent, string name, Vector2 anchored, float size, Color c, bool anchorRight = false)
        {
            var img = new GameObject(name).AddComponent<Image>();
            img.transform.SetParent(parent, false);
            var tex = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            img.sprite = tex; img.color = c;
            var rt = img.rectTransform;
            float ax = anchorRight ? 1f : 0f;
            rt.anchorMin = rt.anchorMax = new Vector2(ax, 0f);
            rt.anchoredPosition = anchored; rt.sizeDelta = new Vector2(size, size);
            return rt;
        }

        static void MakeLabel(Transform parent, string text, Vector2 anchored, Font font, bool anchorRight = false)
        {
            var t = new GameObject(text + "Label").AddComponent<Text>();
            t.transform.SetParent(parent, false);
            t.text = text; t.font = font; t.fontSize = 36; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.color = Color.white;
            var rt = t.rectTransform;
            float ax = anchorRight ? 1f : 0f;
            rt.anchorMin = rt.anchorMax = new Vector2(ax, 0f);
            rt.anchoredPosition = anchored; rt.sizeDelta = new Vector2(170f, 52f);
        }
    }
}
#endif
