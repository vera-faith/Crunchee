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
            QualitySettings.shadowDistance = 70f;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowResolution = ShadowResolution.High;
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
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f; // was 0.04: far too tight, acned the flat tops into camo blotches
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

            BuildCandyHall(zMin, zMax, topY);

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
            BuildDreamyRoom(finishPos, finishYaw, zMin, zMax);

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

            // Glowing bakery windows on the back wall (big build)
            var paneMat = EmissiveMat("WindowPane", new Color(1f, 0.85f, 0.6f), 0.9f);
            foreach (var wx in new[] { -5.6f, 5.6f })
            {
                Box("WindowFrame", finishPos + new Vector3(wx, 2.7f, 4.3f), new Vector3(1.5f, 2.3f, 0.08f), frameMat);
                Box("WindowPane", finishPos + new Vector3(wx, 2.7f, 4.24f), new Vector3(1.25f, 2.05f, 0.08f), paneMat);
                Box("WindowBar", finishPos + new Vector3(wx, 2.7f, 4.2f), new Vector3(1.25f, 0.07f, 0.06f), frameMat);
                Box("WindowBar", finishPos + new Vector3(wx, 2.7f, 4.2f), new Vector3(0.07f, 2.05f, 0.06f), frameMat);
            }

            // The FINISH CAKE waiting on the finish platform (big build)
            {
                var cakeBase = finishPos + new Vector3(0f, 0.2f, 1.15f);
                var g = new GameObject("FinishCake"); g.transform.position = cakeBase;
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

            // Soft frosting MOUNDS on the horizon - a dreamy bakery backdrop
            var moundCols = new[]
            {
                new Color(1f, 0.93f, 0.82f), new Color(1f, 0.8f, 0.85f),
                new Color(0.78f, 0.93f, 0.82f), new Color(0.85f, 0.82f, 0.95f),
            };
            var mounds = new[]
            {
                new Vector3(-11f, -0.5f, 27f), new Vector3(11.5f, -0.5f, 28f),
                new Vector3(-13f, -0.5f, 12f), new Vector3(13.5f, -0.5f, 9f),
                new Vector3(-12f, -0.5f, -4f), new Vector3(12.5f, -0.5f, -5f),
            };
            for (int i = 0; i < mounds.Length; i++)
            {
                var m = Mat($"Mound{i}", moundCols[i % moundCols.Length], 1f, 0.25f);
                float s = 5.5f + (i % 3) * 1.3f;
                Part(PrimitiveType.Sphere, null, "Mound", mounds[i], new Vector3(s, s * 0.52f, s), m);
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
        static void BuildDreamyRoom(Vector3 finishPos, float finishYaw, float zMin, float zMax)
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
                var fwd = Quaternion.Euler(0f, finishYaw, 0f) * Vector3.forward;
                var g = new GameObject("FinishCake");
                g.transform.position = finishPos + fwd * 1.15f + new Vector3(0f, 0.2f, 0f);
                g.transform.rotation = Quaternion.Euler(0f, finishYaw, 0f);
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
            for (int i = 0; i < 9; i++)
            {
                float z = Mathf.Lerp(zMin + 6f, zMax - 4f, i / 8f);
                float pathY = 0.35f + Mathf.Max(0f, z - PathStartZ) / PathAdvance * PathRise;
                var cp = new Vector3((i % 2 == 0 ? -1f : 1f) * Random.Range(4.8f, 6.6f), pathY - Random.Range(1.5f, 3f), z);
                BuildCloud(cp, Random.Range(1f, 1.5f), cloudMat);
                if (i % 3 == 1)
                {
                    var cup = BuildCupcake(cp + new Vector3(0.1f, 0.4f, 0f), cakeMats[i % 2], cherryMat);
                    foreach (var r in cup.GetComponentsInChildren<MeshRenderer>())
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
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

            // --- A few bakery props on the floor near the start ---
            BuildMacarons(new Vector3(5.6f, -0.2f, 1.5f));
            BuildCupcake(new Vector3(-5.4f, -0.2f, 2.2f), cakeMats[0], cherryMat);
            BuildHoneyJar(new Vector3(6.2f, -0.2f, 6f));
            BuildBerryBowl(new Vector3(-6f, -0.2f, 7f));
            BuildFlourBowl(new Vector3(-6.4f, -0.2f, 11f));
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

        /// <summary>The reference videos' room: soft pink + cream checkerboard.</summary>
        static Texture2D CheckerTexture()
        {
            return LoadOrCreateTexture("Assets/Textures/Checker.png", 256, 256, (x, y, w, h) =>
            {
                bool odd = ((x / 32) + (y / 32)) % 2 == 0;
                var c = odd ? new Color(0.97f, 0.70f, 0.82f) : new Color(1f, 0.96f, 0.93f);
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
