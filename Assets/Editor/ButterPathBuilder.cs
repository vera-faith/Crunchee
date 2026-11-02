#if UNITY_EDITOR
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
    /// Bakery map layout (one long path, Faith's design):
    ///   start platform
    ///   -> 10 WAX butters, layers 1..10, LOW-OPACITY wax (alpha 0.12/layer, capped 0.75)
    ///   -> 4 SUGAR butters (sparkly shell, crunch)
    ///   -> 4 HONEY butters (glossy amber shell, sticky pull)
    ///   -> 4 NAIL POLISH butters (glossy pastel shells, hard tap)
    ///   -> finish platform, Pudding Bunny player, bakery dressing,
    ///      bird's-eye camera, joystick + JUMP UI.
    /// </summary>
    public static class ButterPathBuilder
    {
        const float ButterW = 2.6f, ButterH = 0.7f, ButterD = 1.6f;

        static Material Mat(string name, Color c, float alpha = 1f, float smoothness = 0.35f)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(sh) { name = name, color = new Color(c.r, c.g, c.b, alpha) };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (alpha < 1f)
            {
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // transparent (URP)
                m.renderQueue = 3000;
            }
            return m;
        }

        [MenuItem("Crunchee/Build Butter Path Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Lighting: warm bakery sun ---
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.9f, 0.75f);
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.shadows = LightShadows.Soft;

            // --- Floor: cream/pink checker counter top ---
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "BakeryFloor"; floor.transform.localScale = new Vector3(4f, 1f, 14f);
            floor.transform.position = new Vector3(0f, -0.06f, 30f);
            var floorMat = Mat("Floor", Color.white);
            floorMat.mainTexture = CheckerTexture();
            floorMat.mainTextureScale = new Vector2(8f, 30f);
            floor.GetComponent<MeshRenderer>().material = floorMat;

            var butterMat = Mat("Butter", new Color(1f, 0.85f, 0.35f), 1f, 0.45f);
            var platformMat = Mat("Platform", new Color(0.98f, 0.94f, 0.86f), 1f, 0.5f);

            // --- Start platform ---
            Vector3 pos = Vector3.zero;
            var start = Box("StartPlatform", pos, new Vector3(3.4f, 0.4f, 3.4f), platformMat);

            // --- The path: wax 1..10, then sugar / honey / nail-polish stretches ---
            int stepNo = 0;
            foreach (var s in LevelPath.ButterPath())
            {
                pos += new Vector3(s.sideOffset, s.heightOffset, ButterD + s.gap * 0.55f);
                BuildButter(pos, $"Butter_{s.waxLayers:00}_Wax{s.waxLayers}", s.coating, s.waxLayers, butterMat);
                stepNo++;
            }
            for (int i = 1; i <= 4; i++)
            {
                pos += new Vector3(0f, 0.35f, ButterD + 1.21f);
                BuildButter(pos, $"Butter_Sugar_{i}", Coating.Sugar, 1, butterMat);
            }
            for (int i = 1; i <= 4; i++)
            {
                pos += new Vector3(0f, 0.35f, ButterD + 1.21f);
                BuildButter(pos, $"Butter_Honey_{i}", Coating.Honey, 1, butterMat);
            }
            var polishColors = new[]
            {
                new Color(1f, 0.62f, 0.72f),   // pink
                new Color(0.62f, 0.9f, 0.75f), // mint
                new Color(0.78f, 0.7f, 0.98f), // lavender
                new Color(0.6f, 0.82f, 1f),    // baby blue
            };
            for (int i = 1; i <= 4; i++)
            {
                pos += new Vector3(0f, 0.35f, ButterD + 1.21f);
                BuildButter(pos, $"Butter_NailPolish_{i}", Coating.NailPolish, 1, butterMat, polishColors[i - 1]);
            }

            // --- Finish platform + bakery back wall ---
            pos += new Vector3(0f, 0.35f, 3.2f);
            var finish = Box("FinishPlatform", pos, new Vector3(3.6f, 0.4f, 3.6f), platformMat);
            finish.AddComponent<FinishTrigger>();
            BuildBakeryDressing(pos);

            // --- Player: Pudding Bunny ---
            var player = BuildPuddingBunny(new Vector3(0f, 1.0f, 0f));
            var pc = player.GetComponent<PlayerController>();

            // --- Camera: bird's-eye-ish follow ---
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<ThirdPersonFollow>();
            follow.target = player.transform;
            pc.cameraRig = camGo.transform;

            // --- Audio root ---
            var audio = new GameObject("AsmrAudio"); audio.AddComponent<AsmrAudio>();

            // --- UI: joystick bottom-left + JUMP bottom-right (labels BELOW buttons) ---
            BuildUI(player);

            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/ButterPath.unity");
            Debug.Log("[Crunchee] Butter Path scene built: Assets/Scenes/ButterPath.unity — press Play.");
        }

        /// <summary>One butter step with its coating shell + particles + Squishy wiring.</summary>
        static void BuildButter(Vector3 pos, string name, Coating coating, int waxLayers, Material butterMat, Color polishColor = default)
        {
            var step = Box(name, pos, new Vector3(ButterW, ButterH, ButterD), butterMat);

            Material shellMat; Color particleColor; string shellName;
            switch (coating)
            {
                case Coating.Sugar:
                    shellMat = Mat("SugarShell", new Color(1f, 0.93f, 0.95f), 0.88f, 0.9f);
                    particleColor = new Color(1f, 0.8f, 0.88f); shellName = "SugarShell"; break;
                case Coating.Honey:
                    shellMat = Mat("HoneyShell", new Color(1f, 0.62f, 0.12f), 0.5f, 1f);
                    particleColor = new Color(1f, 0.7f, 0.25f); shellName = "HoneyShell"; break;
                case Coating.NailPolish:
                    shellMat = Mat("PolishShell", polishColor == default ? new Color(1f, 0.62f, 0.72f) : polishColor, 1f, 1f);
                    particleColor = shellMat.color; shellName = "PolishShell"; break;
                default: // Wax: low-opacity build-up, butter always shows through
                    float alpha = Mathf.Min(0.75f, 0.12f * waxLayers);
                    shellMat = Mat($"Wax{waxLayers}", Color.white, alpha, 0.55f);
                    particleColor = Color.white; shellName = "WaxShell"; break;
            }

            var shell = Box(shellName, pos + new Vector3(0f, ButterH * 0.28f, 0f),
                            new Vector3(ButterW * 1.03f, ButterH * 0.55f, ButterD * 1.03f), shellMat);
            shell.transform.SetParent(step.transform, true);
            Object.DestroyImmediate(shell.GetComponent<Collider>());

            var sq = step.AddComponent<Squishy>();
            sq.baseMaterial = BaseMaterial.Butter; sq.coating = coating; sq.waxLayers = waxLayers;
            sq.coatingShell = shell;

            var ps = new GameObject("StepParticles").AddComponent<ParticleSystem>();
            ps.transform.SetParent(step.transform, false);
            ps.transform.localPosition = new Vector3(0f, ButterH * 0.6f, 0f);
            var main = ps.main; main.startColor = particleColor; main.startSize = 0.09f;
            main.startSpeed = 2.2f; main.startLifetime = 0.7f; main.playOnAwake = false;
            var em = ps.emission; em.enabled = false;
            sq.stepParticles = ps;
        }

        /// <summary>
        /// Pudding Bunny (locked character): glossy caramel flan body, caramel drip cap,
        /// floppy ears, simple cute face. Built from primitives until the model lands.
        /// Visuals hang off "Model" so SquashAndStretch can deform them.
        /// </summary>
        static GameObject BuildPuddingBunny(Vector3 at)
        {
            var pudding = Mat("Pudding", new Color(1f, 0.93f, 0.72f), 1f, 0.9f);
            var caramel = Mat("Caramel", new Color(0.85f, 0.52f, 0.2f), 1f, 0.95f);
            var dark = Mat("FaceDark", new Color(0.16f, 0.1f, 0.08f), 1f, 0.4f);
            var pink = Mat("Blush", new Color(1f, 0.62f, 0.68f), 1f, 0.6f);

            var player = new GameObject("Player_PuddingBunny");
            player.transform.position = at;
            var cc = player.AddComponent<CharacterController>(); cc.height = 1.1f; cc.radius = 0.35f; cc.center = new Vector3(0f, 0.55f, 0f);
            player.AddComponent<PlayerController>();
            var model = new GameObject("Model"); model.transform.SetParent(player.transform, false);
            model.AddComponent<SquashAndStretch>();

            // Body: plump flan
            var body = Part(PrimitiveType.Sphere, model.transform, "Body", new Vector3(0f, 0.52f, 0f), new Vector3(0.66f, 0.6f, 0.62f), pudding);
            // Caramel drip cap: squashed glossy dome + drips at the rim
            Part(PrimitiveType.Sphere, model.transform, "CaramelCap", new Vector3(0f, 0.78f, 0f), new Vector3(0.7f, 0.3f, 0.66f), caramel);
            foreach (var dx in new[] { -0.24f, 0f, 0.24f })
                Part(PrimitiveType.Sphere, model.transform, "Drip", new Vector3(dx, 0.66f, 0.26f), new Vector3(0.09f, 0.2f, 0.09f), caramel);
            // Floppy ears: flop down-back, caramel tips
            foreach (var side in new[] { -1f, 1f })
            {
                var ear = Part(PrimitiveType.Capsule, model.transform, "Ear", new Vector3(side * 0.2f, 0.78f, -0.06f), new Vector3(0.15f, 0.44f, 0.07f), pudding);
                ear.transform.rotation = Quaternion.Euler(28f, 0f, side * -24f);
                var tip = Part(PrimitiveType.Sphere, model.transform, "EarTip", new Vector3(side * 0.31f, 0.52f, -0.18f), new Vector3(0.13f, 0.16f, 0.06f), caramel);
                tip.transform.rotation = ear.transform.rotation;
            }
            // Face: round eyes, tiny nose, blush - the locked cute face
            foreach (var side in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Sphere, model.transform, "Eye", new Vector3(side * 0.13f, 0.58f, 0.29f), new Vector3(0.07f, 0.09f, 0.04f), dark);
                Part(PrimitiveType.Sphere, model.transform, "Blush", new Vector3(side * 0.24f, 0.48f, 0.27f), new Vector3(0.09f, 0.05f, 0.03f), pink);
                // Feet
                Part(PrimitiveType.Sphere, model.transform, "Foot", new Vector3(side * 0.16f, 0.06f, 0.16f), new Vector3(0.2f, 0.1f, 0.3f), pudding);
            }
            Part(PrimitiveType.Sphere, model.transform, "Nose", new Vector3(0f, 0.52f, 0.31f), new Vector3(0.06f, 0.045f, 0.03f), pink);
            // Tail puff
            Part(PrimitiveType.Sphere, model.transform, "Tail", new Vector3(0f, 0.34f, -0.3f), new Vector3(0.18f, 0.18f, 0.18f), pudding);
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

        /// <summary>Bakery dressing: counters flanking the path, cake stands, back wall + bunting.</summary>
        static void BuildBakeryDressing(Vector3 finishPos)
        {
            var wood = Mat("Counter", new Color(0.87f, 0.64f, 0.47f), 1f, 0.4f);
            float pathLen = finishPos.z;
            foreach (var side in new[] { -1f, 1f })
            {
                var counter = Box("Counter", new Vector3(side * 3.6f, 0.42f, pathLen * 0.5f), new Vector3(1.3f, 0.9f, pathLen + 8f), wood);
                counter.GetComponent<Collider>().enabled = true;
            }
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
            for (float z = 5f; z < pathLen; z += 6.5f, k++)
            {
                float side = (k % 2 == 0) ? -3.6f : 3.6f;
                var g = new GameObject("CakeStand"); g.transform.position = new Vector3(side, 0.9f, z);
                Part(PrimitiveType.Cylinder, g.transform, "Stem", new Vector3(0f, 0.16f, 0f), new Vector3(0.12f, 0.32f, 0.12f), standMat);
                Part(PrimitiveType.Cylinder, g.transform, "Plate", new Vector3(0f, 0.34f, 0f), new Vector3(0.95f, 0.05f, 0.95f), standMat);
                Part(PrimitiveType.Cylinder, g.transform, "Cake", new Vector3(0f, 0.48f, 0f), new Vector3(0.68f, 0.24f, 0.68f), cakeMats[k % cakeMats.Length]);
                Part(PrimitiveType.Sphere, g.transform, "Cherry", new Vector3(0f, 0.66f, 0f), new Vector3(0.13f, 0.13f, 0.13f), cherryMat);
            }
            // Back wall behind the finish + bunting row
            var wall = Box("BakeryWall", finishPos + new Vector3(0f, 2.2f, 4.5f), new Vector3(16f, 6f, 0.3f), Mat("Wall", new Color(0.99f, 0.87f, 0.9f), 1f, 0.25f));
            var buntMats = new[] { cakeMats[0], cakeMats[1], cakeMats[2], cherryMat };
            for (int i = 0; i < 9; i++)
            {
                float x = -4f + i;
                float y = finishPos.y + 4.6f - Mathf.Abs(i - 4) * -0.06f - Mathf.Sin(i / 8f * Mathf.PI) * 0.35f;
                Part(PrimitiveType.Sphere, wall.transform.parent, "Bunting", new Vector3(x, y, finishPos.z + 4.3f), new Vector3(0.22f, 0.3f, 0.1f), buntMats[i % buntMats.Length]);
            }
        }

        static Texture2D CheckerTexture()
        {
            var tex = new Texture2D(128, 128) { wrapMode = TextureWrapMode.Repeat };
            var cream = new Color(0.99f, 0.93f, 0.83f); var pink = new Color(0.97f, 0.8f, 0.8f);
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                    tex.SetPixel(x, y, ((x / 64) + (y / 64)) % 2 == 0 ? cream : pink);
            tex.Apply();
            return tex;
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

            // Joystick base (bottom-left)
            var joyBase = MakeCircle(canvasGo.transform, "JoystickBase", new Vector2(150f, 210f), 190f, new Color(1, 1, 1, 0.28f));
            var knob = MakeCircle(joyBase.transform, "Knob", Vector2.zero, 96f, new Color(1, 1, 1, 0.55f));
            var joy = joyBase.gameObject.AddComponent<VirtualJoystick>();
            joy.baseRect = joyBase; joy.knobRect = knob;
            MakeLabel(canvasGo.transform, "MOVE", new Vector2(150f, 78f), font);

            // JUMP button (bottom-right): circle w/ centered arrow, JUMP label BELOW like MOVE
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
