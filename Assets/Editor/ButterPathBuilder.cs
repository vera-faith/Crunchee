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
    /// Bakery map (Faith's design + reference videos): ONE long path of GIANT
    /// butters the character walks across. One land per squishy -> coating
    /// cracks -> character slowly sinks in.
    ///   start platform
    ///   -> 10 WAX butters, layers 1..10, LOW-OPACITY wax (alpha 0.12/layer, capped 0.75),
    ///      shell built as 4 pieces so it can visibly split open
    ///   -> 4 SUGAR, 4 HONEY, 4 NAIL POLISH (polish shells crack too)
    ///   -> finish platform, Pudding Bunny, bakery dressing, warm fog + pendants,
    ///      bird's-eye camera, joystick + JUMP UI.
    /// </summary>
    public static class ButterPathBuilder
    {
        const float ButterW = 3.2f, ButterH = 0.7f, ButterD = 2.0f, PatH = 0.14f;
        const float StepAdvance = ButterD + 0.18f; // edges nearly touch: walk straight across

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

            // --- Atmosphere: warm cream backdrop + distance fog (sunset bakery) ---
            var bg = new Color(0.99f, 0.9f, 0.84f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = bg;
            RenderSettings.fogStartDistance = 42f;
            RenderSettings.fogEndDistance = 115f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.56f, 0.52f);

            // --- Lighting: warm bakery sun ---
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.9f, 0.75f);
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.shadows = LightShadows.Soft;

            // --- Floor: big cream/pink checker tiles like the reference room ---
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "BakeryFloor"; floor.transform.localScale = new Vector3(4f, 1f, 14f);
            floor.transform.position = new Vector3(0f, -0.06f, 26f);
            var floorMat = Mat("Floor", Color.white, 1f, 0.3f);
            floorMat.mainTexture = CheckerTexture();
            floorMat.mainTextureScale = new Vector2(6f, 22f);
            floor.GetComponent<MeshRenderer>().material = floorMat;

            var butterMat = Mat("Butter", new Color(1f, 0.85f, 0.35f), 1f, 0.6f);
            var patMat = Mat("ButterPat", new Color(1f, 0.89f, 0.45f), 1f, 0.65f);
            var platformMat = Mat("Platform", new Color(0.98f, 0.94f, 0.86f), 1f, 0.5f);

            // --- Start platform ---
            Vector3 pos = Vector3.zero;
            var start = Box("StartPlatform", pos, new Vector3(4f, 0.4f, 4f), platformMat);

            // --- The path ---
            foreach (var s in LevelPath.ButterPath())
            {
                pos += new Vector3(s.sideOffset, s.heightOffset, StepAdvance);
                BuildButter(pos, $"Butter_{s.waxLayers:00}_Wax{s.waxLayers}", s.coating, s.waxLayers, butterMat, patMat);
            }
            for (int i = 1; i <= 4; i++)
            {
                pos += new Vector3(0f, 0.35f, StepAdvance);
                BuildButter(pos, $"Butter_Sugar_{i}", Coating.Sugar, 1, butterMat, patMat);
            }
            for (int i = 1; i <= 4; i++)
            {
                pos += new Vector3(0f, 0.35f, StepAdvance);
                BuildButter(pos, $"Butter_Honey_{i}", Coating.Honey, 1, butterMat, patMat);
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
                pos += new Vector3(0f, 0.35f, StepAdvance);
                BuildButter(pos, $"Butter_NailPolish_{i}", Coating.NailPolish, 1, butterMat, patMat, polishColors[i - 1]);
            }

            // --- Finish platform + dressing ---
            pos += new Vector3(0f, 0.35f, 3.4f);
            var finish = Box("FinishPlatform", pos, new Vector3(4.2f, 0.4f, 4.2f), platformMat);
            finish.AddComponent<FinishTrigger>();
            BuildBakeryDressing(pos);

            // --- Player: Pudding Bunny ---
            var player = BuildPuddingBunny(new Vector3(0f, 1.0f, 0f));
            var pc = player.GetComponent<PlayerController>();

            // --- Camera: bird's-eye-ish follow over the cream backdrop ---
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<ThirdPersonFollow>();
            follow.target = player.transform;
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
                default: // Wax: low-opacity build-up, butter always shows through
                    float alpha = Mathf.Min(0.75f, 0.12f * waxLayers);
                    shellMat = Mat($"Wax{waxLayers}", Color.white, alpha, 0.55f);
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

            var body = Part(PrimitiveType.Sphere, model.transform, "Body", new Vector3(0f, 0.52f, 0f), new Vector3(0.66f, 0.6f, 0.62f), pudding);
            Part(PrimitiveType.Sphere, model.transform, "CaramelCap", new Vector3(0f, 0.78f, 0f), new Vector3(0.7f, 0.3f, 0.66f), caramel);
            foreach (var dx in new[] { -0.24f, 0f, 0.24f })
                Part(PrimitiveType.Sphere, model.transform, "Drip", new Vector3(dx, 0.66f, 0.26f), new Vector3(0.09f, 0.2f, 0.09f), caramel);
            foreach (var side in new[] { -1f, 1f })
            {
                var ear = Part(PrimitiveType.Capsule, model.transform, "Ear", new Vector3(side * 0.2f, 0.78f, -0.06f), new Vector3(0.15f, 0.44f, 0.07f), pudding);
                ear.transform.rotation = Quaternion.Euler(28f, 0f, side * -24f);
                var tip = Part(PrimitiveType.Sphere, model.transform, "EarTip", new Vector3(side * 0.31f, 0.52f, -0.18f), new Vector3(0.13f, 0.16f, 0.06f), caramel);
                tip.transform.rotation = ear.transform.rotation;
            }
            foreach (var side in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Sphere, model.transform, "Eye", new Vector3(side * 0.13f, 0.58f, 0.29f), new Vector3(0.07f, 0.09f, 0.04f), dark);
                Part(PrimitiveType.Sphere, model.transform, "Blush", new Vector3(side * 0.24f, 0.48f, 0.27f), new Vector3(0.09f, 0.05f, 0.03f), pink);
                Part(PrimitiveType.Sphere, model.transform, "Foot", new Vector3(side * 0.16f, 0.06f, 0.16f), new Vector3(0.2f, 0.1f, 0.3f), pudding);
            }
            Part(PrimitiveType.Sphere, model.transform, "Nose", new Vector3(0f, 0.52f, 0.31f), new Vector3(0.06f, 0.045f, 0.03f), pink);
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

        /// <summary>Bakery dressing: counters + cake stands, pendant lights, back wall, awning, bunting.</summary>
        static void BuildBakeryDressing(Vector3 finishPos)
        {
            var wood = Mat("Counter", new Color(0.87f, 0.64f, 0.47f), 1f, 0.4f);
            var woodTop = Mat("CounterTop", new Color(0.97f, 0.88f, 0.76f), 1f, 0.55f);
            var ribbon = Mat("Ribbon", new Color(0.96f, 0.55f, 0.66f), 1f, 0.5f);
            float pathLen = finishPos.z;
            foreach (var side in new[] { -1f, 1f })
            {
                Box("Counter", new Vector3(side * 3.9f, 0.42f, pathLen * 0.5f), new Vector3(1.3f, 0.9f, pathLen + 8f), wood);
                Box("CounterTop", new Vector3(side * 3.9f, 0.93f, pathLen * 0.5f), new Vector3(1.5f, 0.08f, pathLen + 8f), woodTop);
                Box("Ribbon", new Vector3(side * 3.22f, 0.62f, pathLen * 0.5f), new Vector3(0.05f, 0.16f, pathLen + 8f), ribbon);
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
                float side = (k % 2 == 0) ? -3.9f : 3.9f;
                var g = new GameObject("CakeStand"); g.transform.position = new Vector3(side, 0.97f, z);
                Part(PrimitiveType.Cylinder, g.transform, "Stem", new Vector3(0f, 0.16f, 0f), new Vector3(0.12f, 0.32f, 0.12f), standMat);
                Part(PrimitiveType.Cylinder, g.transform, "Plate", new Vector3(0f, 0.34f, 0f), new Vector3(0.95f, 0.05f, 0.95f), standMat);
                Part(PrimitiveType.Cylinder, g.transform, "Cake", new Vector3(0f, 0.48f, 0f), new Vector3(0.68f, 0.24f, 0.68f), cakeMats[k % cakeMats.Length]);
                Part(PrimitiveType.Sphere, g.transform, "Cherry", new Vector3(0f, 0.66f, 0f), new Vector3(0.13f, 0.13f, 0.13f), cherryMat);
            }
            // Pendant lights down the middle of the path
            var cordMat = Mat("Cord", new Color(0.35f, 0.25f, 0.2f), 1f, 0.4f);
            var bulbMat = EmissiveMat("Bulb", new Color(1f, 0.78f, 0.5f), 1.6f);
            for (float z = 9f; z < pathLen; z += 11f)
            {
                float stepTop = (z / StepAdvance) * 0.35f + 0.84f; // the path climbs 0.35 per step
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

        static Texture2D CheckerTexture()
        {
            var tex = new Texture2D(128, 128) { wrapMode = TextureWrapMode.Repeat };
            var cream = new Color(0.99f, 0.94f, 0.86f); var pink = new Color(0.96f, 0.74f, 0.78f);
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
