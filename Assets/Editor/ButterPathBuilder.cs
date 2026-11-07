#if UNITY_EDITOR
using System.Collections.Generic;
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
        // Mockup proportions: a butter bar is LONG across the path (~2.6x its depth).
        const float ButterW = 4.4f, ButterH = 0.62f, ButterD = 1.7f, PatH = 0.14f;
        const float StepAdvance = ButterD + 0.18f; // edges nearly touch: walk straight across
        // Straight climb (Nov 6, "match the view of the video"): every step is
        // one slot further along +Z and 0.55 higher, under a fixed camera.
        // Slot 10 is the checkpoint pad (it gets extra room on both sides).
        const float StepDepth = 2.2f, PathAdvance = 2.3f, PathStartZ = 3.15f, PathRise = 0.55f, HallHalfW = 8f;
        static float SlotZ(int i) => PathStartZ + i * PathAdvance + (i >= 10 ? 1f : 0f) + (i >= 11 ? 1f : 0f);
        static Vector3 SlotPos(int i) => new Vector3(i == 10 ? 0f : Mathf.Sin(i * 1.37f) * 0.6f, 0.35f + i * PathRise, SlotZ(i));
        static float SlotYaw(int i) => i == 10 ? 0f : Mathf.Sin(i * 0.83f + 0.6f) * 4f;

        static Material Mat(string name, Color c, float alpha = 1f, float smoothness = 0.35f)
        {
            // Crunchee candy shaders: soft wrap light, glossy highlight and a
            // rim glow - desserts read glazed and squishy, not flat plastic.
            var sh = Shader.Find(alpha < 1f ? "Crunchee/CandyTransparent" : "Crunchee/Candy");
            var m = new Material(sh) { name = name, color = new Color(c.r, c.g, c.b, alpha) };
            m.SetFloat("_Glossiness", smoothness);
            // Low default rim: the candy shader adds rim into BOTH emission and
            // alpha, so a high default hazed every surface white. Materials
            // that want more set it explicitly.
            m.SetFloat("_RimStrength", 0.12f);
            if (alpha < 1f) m.renderQueue = 3000;
            return m;
        }

        static int pathCounter;

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
            pathCounter = 0;
            ResetWorldCaches();

            // --- Atmosphere: dreamy lavender-pink (the reference videos'
            // room) + soft distance fog. Exposure reined in vs the big build:
            // build 1 blew out on the phone (sun 1.35, reflections 1.15), so
            // both come down and the ambient lifts slightly instead. ---
            var bg = new Color(0.93f, 0.85f, 0.94f);
            SetupSunsetSky();
            RenderSettings.reflectionIntensity = 0.85f;

            // --- Render quality (big build): MSAA, crisp shadows, full aniso ---
            QualitySettings.antiAliasing = 4;
            QualitySettings.shadowDistance = 30f; // the camera is close - short range = dense shadow texels
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowCascade4Split = new Vector3(0.05f, 0.18f, 0.45f); // splits weighted near the camera
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;

            // --- Lighting: warm golden-hour bakery sun coming from the FAR
            // end of the climb, high and slightly to the side - its specular
            // glints rake across the glossy butter tops toward the camera
            // like window light. Elevation 55 keeps a step's shadow to ~0.4 m
            // on the step below (the climb rises 0.55 per 2.3 m). ---
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.88f, 0.72f);
            sun.transform.rotation = Quaternion.Euler(55f, 200f, 0f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.6f;
            sun.shadowBias = 0.03f;
            sun.shadowNormalBias = 0.5f; // curved surfaces (bunny, SDF) need the offset to not self-shadow-band; still tight enough to avoid peter-panning
            AddFillLight();

            // --- The candy hall (Nov 6): one straight climb up +Z ---
            int slots = 10 + 1 + CandyPath().Length;
            float zMin = -7f, zMax = SlotZ(slots) + 9f;
            float topY = SlotPos(slots).y;

            // Floor: long checker slab, solid (step off the path and you land on it).
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "RoomFloor";
            floor.transform.position = new Vector3(0f, -0.3f, (zMin + zMax) / 2f);
            floor.transform.localScale = new Vector3(HallHalfW * 2f, 0.2f, zMax - zMin);
            var floorMat = CheckerFloorMat();
            floorMat.mainTextureScale = new Vector2(HallHalfW * 2f / 3f, (zMax - zMin) / 3f);
            floor.GetComponent<MeshRenderer>().material = floorMat;

            BuildBakeryHall(zMin, zMax, topY);
            BuildBakeryProps(zMin, zMax);

            // Butter: soft pale-yellow blocks (Faith's reference image), squishy dent on.
            // _EnvStrength low: glossy butter that doesn't mirror the cloudy
            // sunset skybox (cloud reflections read as camo blotches, Nov 6).
            var butterMat = ButterBodyMat("Butter", new Color(0.99f, 0.86f, 0.43f), 0.7f);
            var patMat = ButterBodyMat("ButterPat", new Color(1f, 0.92f, 0.55f), 0.75f);
            var platformMat = Mat("Platform", new Color(0.98f, 0.94f, 0.86f), 1f, 0.5f);
            var frostingMat = Mat("Frosting", new Color(0.995f, 0.97f, 0.93f), 1f, 0.35f);

            // --- Start: a white FROSTING slab on a wooden board, sprinkles on
            // top. Top stays at 0.49 so the first climb is unchanged. ---
            var board = Box("StartBoard", new Vector3(0f, 0.03f, 0f), new Vector3(4.7f, 0.12f, 4.7f), Mat("Board", new Color(0.85f, 0.62f, 0.44f), 1f, 0.45f));
            Object.DestroyImmediate(board.GetComponent<Collider>());
            Box("StartPlatform", new Vector3(0f, 0.29f, 0f), new Vector3(4f, 0.4f, 4f), frostingMat);
            SprinkleTops(new Vector3(0f, 0.5f, 0f), 1.8f, 44);

            // --- Stage 1: the 10 wax butters ---
            Vector3 lastPos = Vector3.zero;
            int idx = 0;
            foreach (var s in LevelPath.ButterPath())
            {
                var p = SlotPos(idx);
                BuildButter(p, $"Butter_{s.waxLayers:00}_Wax{s.waxLayers}", s.coating, s.waxLayers, butterMat, patMat, SlotYaw(idx), StepDepth);
                lastPos = p; idx++;
            }

            // --- Checkpoint pad, then Stage 2 (Candy Path) ---
            BuildCheckpointPad(SlotPos(idx), SlotYaw(idx));
            idx++;
            int candyN = 0;
            foreach (var def in CandyPath())
            {
                var p = SlotPos(idx);
                BuildCandyStep(p, ++candyN, def, SlotYaw(idx), StepDepth);
                lastPos = p; idx++;
            }

            // --- Finish platform: one more rise past the last step ---
            var finishPos = new Vector3(0f, lastPos.y + PathRise + 0.15f, lastPos.z + StepDepth / 2f + 0.1f + 2.1f);
            float finishYaw = 0f;
            var finish = Box("FinishPlatform", finishPos, new Vector3(4.2f, 0.4f, 4.2f), platformMat);
            // The trigger is its own invisible zone ABOVE the solid platform,
            // so she can stand on the platform AND trip the finish.
            var zone = Box("FinishZone", finishPos + new Vector3(0f, 1.1f, 0f), new Vector3(4.2f, 2.2f, 4.2f), platformMat);
            Object.DestroyImmediate(zone.GetComponent<MeshRenderer>());
            zone.GetComponent<Collider>().isTrigger = true;
            zone.AddComponent<FinishTrigger>();
            BuildFinishDressing(finishPos, zMin, zMax);

            // --- Player: Pudding Bunny ---
            var player = BuildPuddingBunny(new Vector3(0f, 1.0f, 0f));
            var pc = player.GetComponent<PlayerController>();

            // --- Camera: FIXED high view looking up the climb (Nov 6). It
            // follows her position only - never her facing. ---
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = bg;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.farClipPlane = 260f;
            cam.fieldOfView = 58f;
            var pfx = camGo.AddComponent<CruncheePostFX>();
            pfx.shader = Shader.Find("Hidden/Crunchee/PostFX");
            // Golden-hour grade: mostly-neutral, low bloom, gentle vignette.
            pfx.exposure = 0.9f; pfx.threshold = 1.2f; pfx.bloomIntensity = 0.15f;
            pfx.saturation = 1.08f; pfx.contrast = 1.1f; pfx.vignette = 0.3f;
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<ThirdPersonFollow>();
            follow.target = player.transform;
            camGo.transform.SetPositionAndRotation(follow.DesiredPosition, follow.ViewRotation);
            pc.cameraRig = camGo.transform;

            // --- Audio root ---
            var audio = new GameObject("AsmrAudio"); audio.AddComponent<AsmrAudio>();

            // --- UI ---
            BuildUI(player);

            // --- iOS / App Store identity (set now so the TestFlight build is
            // a straight export later) ---
            PlayerSettings.productName = "Crunchee";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.verafaith.crunchee");
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.iOS.buildNumber = "2";
            PlayerSettings.iOS.appleDeveloperTeamID = "738KBBGF8K";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            SetAppIcon();

            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/ButterPath.unity");
            Debug.Log("[Crunchee] Butter Path scene built: Assets/Scenes/ButterPath.unity — press Play.");
        }

        /// <summary>Primitive cube as a LOCAL child - no world-space parenting
        /// surprises under rotated or scaled parents.</summary>
        static GameObject ChildBox(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material m, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().material = m;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            Roundify(go);
            return go;
        }

        /// <summary>
        /// One-click iOS build for TestFlight: exports the Xcode project to
        /// Builds/iOS (gitignored). Run the scene build first so the icon and
        /// identity settings are baked in. Archive/upload happens in Xcode.
        /// </summary>
        [MenuItem("Crunchee/Build iOS (Xcode project)")]
        public static void BuildIOS()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/ButterPath.unity", true) };
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/ButterPath.unity" },
                locationPathName = "Builds/iOS",
                target = BuildTarget.iOS,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[Crunchee] iOS build result: {report.summary.result}, size {report.summary.totalSize} bytes, errors {report.summary.totalErrors}");
        }

        /// <summary>
        /// App icon (Icon G, picked by Faith Nov 3): applied when Assets/AppIcon.png
        /// is present in the project. Runs inside Build() so every scene rebuild
        /// keeps the icon wired.
        /// </summary>
        static void SetAppIcon()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AppIcon.png");
            if (icon == null) { Debug.Log("[Crunchee] No Assets/AppIcon.png - icon skipped."); return; }
            // Unity 6.6: the legacy setter is the working path, but it demands
            // an array of the platform's exact slot count (iPhone: 9,
            // Standalone: 8) - a 1-element array errors out and writes nothing.
            foreach (var group in new[] { BuildTargetGroup.iOS, BuildTargetGroup.Standalone })
            {
                int count = PlayerSettings.GetIconsForTargetGroup(group, IconKind.Application).Length;
                if (count == 0) count = group == BuildTargetGroup.iOS ? 9 : 8;
                var arr = new Texture2D[count];
                for (int i = 0; i < count; i++) arr[i] = icon;
                PlayerSettings.SetIconsForTargetGroup(group, arr, IconKind.Application);
                Debug.Log($"[Crunchee] App icon applied to {group}: {count} slot(s) filled.");
            }
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

        /// <summary>The reference videos' room: soft pink + cream checkerboard.</summary>
        static Texture2D CheckerTexture()
        {
            return LoadOrCreateTexture("Assets/Textures/Checker.png", 256, 256, (x, y, w, h) =>
            {
                bool odd = ((x / 32) + (y / 32)) % 2 == 0;
                // softer blush pink + warm cream (the pink accent, low contrast)
                var c = odd ? new Color(0.96f, 0.8f, 0.84f) : new Color(0.99f, 0.95f, 0.9f);
                float hash = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
                float n = 0.97f + (hash - Mathf.Floor(hash)) * 0.05f; // fine grain, never flat
                return new Color(c.r * n, c.g * n, c.b * n);
            });
        }

        /// <summary>Soft round sparkle sprite for the twinkle dust.</summary>
        static Texture2D SparkleTexture()
        {
            return LoadOrCreateTexture("Assets/Textures/Sparkle.png", 64, 64, (x, y, w, h) =>
            {
                float dx = (x - w / 2f) / (w / 2f), dy = (y - h / 2f) / (h / 2f);
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float a = Mathf.Pow(1f - d, 2f);
                return new Color(1f, 1f, 1f, a);
            });
        }

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
            imp.mipmapEnabled = true;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 4;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Warm peachy countertop: layered mottle, fine speckle, faint plank seams.</summary>
        static Texture2D CounterTexture()
        {
            return LoadOrCreateTexture("Assets/Textures/Countertop.png", 512, 512, (x, y, w, h) =>
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
            Roundify(go);
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

            // --- Progress pips (top center) ---
            var hud = canvasGo.AddComponent<ProgressHud>();
            var pips = new UnityEngine.UI.Image[10];
            for (int i = 0; i < 10; i++)
            {
                var pip = MakeCircle(canvasGo.transform, $"Pip{i}", Vector2.zero, 26f, new Color(1, 1, 1, 0.35f));
                pip.anchorMin = pip.anchorMax = new Vector2(0.5f, 1f);
                pip.anchoredPosition = new Vector2((i - 4.5f) * 36f, -46f);
                pips[i] = pip.GetComponent<UnityEngine.UI.Image>();
            }
            hud.pips = pips;
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
