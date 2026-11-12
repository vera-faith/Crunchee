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
    /// Cube-box parkour map (Nov 9, reference video): the squishies line the
    /// sides of a giant square tower and spiral up - butter sticks plus
    /// pastries of other shapes, walk gaps and real jump gaps, some butters
    /// turntable under her feet. One land per squishy -> coating cracks ->
    /// the character slowly sinks in, and cracks keep crackling underfoot
    /// wherever she walks.
    /// </summary>
    public static partial class ButterPathBuilder
    {
        // A recognisable wrapped butter STICK: tall yellow sides and a long top,
        // rather than a thin yellow pat floating over a serving tray.
        const float ButterW = 2.8f, ButterH = 0.9f, ButterD = 3f, PatH = 0f;
        const float StepAdvance = ButterD + 0.18f; // edges nearly touch: walk straight across
        // Straight climb (Nov 6, "match the view of the video"): every step is
        // one slot further along +Z and 0.55 higher, under a fixed camera.
        // Slot 10 is the checkpoint pad (it gets extra room on both sides).
        const float StepDepth = 3f, PathAdvance = 3.18f, PathStartZ = 3.15f, PathRise = 0.55f, HallHalfW = 8f;
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
        public static void Build() => BuildTheme(SquishyTheme.Bakery);

        [MenuItem("Crunchee/Build Classic Butter Scene")]
        public static void BuildClassicButter() => BuildTheme(SquishyTheme.ClassicButter);

        [MenuItem("Crunchee/Build All Themes")]
        public static void BuildAllThemes()
        {
            Build();
            BuildClassicButter();
        }

        public static void BuildTheme(SquishyTheme theme)
        {
            var definition = ThemePath.Get(theme);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Random.InitState(11); // stable prop scatter across rebuilds
            pathCounter = 0;
            ResetWorldCaches();

            var bg = definition.horizon;
            SetupSunsetSky();
            RenderSettings.skybox.SetColor("_TopColor", definition.skyTop);
            RenderSettings.skybox.SetColor("_MidColor", Color.Lerp(definition.skyTop, definition.horizon, 0.6f));
            RenderSettings.skybox.SetColor("_HorizonColor", definition.horizon);
            RenderSettings.skybox.SetColor("_CloudColor", Color.Lerp(definition.accent, Color.white, 0.55f));
            string skyPath = $"Assets/Materials/{definition.id}Sky.mat";
            var themeSky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (themeSky == null)
            {
                themeSky = new Material(RenderSettings.skybox) { name = definition.id + "Sky" };
                AssetDatabase.CreateAsset(themeSky, skyPath);
            }
            else EditorUtility.CopySerialized(RenderSettings.skybox, themeSky);
            RenderSettings.skybox = themeSky;
            RenderSettings.fogColor = Color.Lerp(definition.horizon, Color.white, 0.2f);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 170f;
            RenderSettings.reflectionIntensity = 0.85f;

            QualitySettings.antiAliasing = 4;
            QualitySettings.shadowDistance = 30f;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowCascade4Split = new Vector3(0.05f, 0.18f, 0.45f);
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 0.94f;
            sun.color = new Color(1f, 0.9f, 0.78f);
            sun.transform.rotation = Quaternion.Euler(55f, 200f, 0f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.6f;
            sun.shadowBias = 0.03f;
            sun.shadowNormalBias = 0.65f;
            AddFillLight();
            // Cool rim from high behind - edges pop off the sky, the wax and
            // glaze sparkle more, and the whole course reads crisper.
            var rim = new GameObject("RimLight").AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = new Color(0.72f, 0.85f, 1f);
            rim.intensity = 0.26f;
            rim.shadows = LightShadows.None;
            rim.transform.rotation = Quaternion.Euler(25f, 330f, 0f);

            var butterMat = ButterBodyMat("Butter", new Color(1f, 0.82f, 0.3f), 0.66f);
            var patMat = ButterBodyMat("ButterPat", new Color(1f, 0.87f, 0.4f), 0.68f);
            var frostingMat = Mat("Frosting", new Color(0.995f, 0.97f, 0.93f), 1f, 0.35f);
            // The start platform hangs just before slot 0 along the course,
            // flush with the tower wall (Nov 9, cube-box map).
            var slot0 = EndlessSquishyPath.SlotPosition(0);
            var courseDir = EndlessSquishyPath.SlotPosition(1) - slot0;
            courseDir.y = 0f; courseDir.Normalize();
            var startCenter = slot0 - courseDir * 3.4f;
            var board = Box("StartBoard", new Vector3(startCenter.x, 0.03f, startCenter.z), new Vector3(4.7f, 0.12f, 4.7f), Mat("Board", new Color(0.85f, 0.62f, 0.44f), 1f, 0.45f));
            Object.DestroyImmediate(board.GetComponent<Collider>());
            Box("StartPlatform", new Vector3(startCenter.x, 0.29f, startCenter.z), new Vector3(4f, 0.4f, 4f), frostingMat);
            if (theme == SquishyTheme.Bakery) SprinkleTops(new Vector3(startCenter.x, 0.5f, startCenter.z), 1.8f, 44);

            var endless = new GameObject("EndlessSquishyPath").AddComponent<EndlessSquishyPath>();
            endless.ahead = 25; endless.keepBehind = 5; endless.environmentFirstIndex = -1;
            var templateRoot = new GameObject("StepTemplates").transform;
            templateRoot.SetParent(endless.transform, false);
            var definitions = definition.steps;
            var templates = new GameObject[definitions.Length];
            for (int i = 0; i < definitions.Length; i++)
            {
                var def = definitions[i];
                string name = $"{definition.id}_{i:00}_{def.item}_{def.coating}{def.waxLayers}";
                templates[i] = def.item == SquishyItem.Butter
                    ? BuildButter(Vector3.zero, name, def.coating, def.waxLayers, butterMat, patMat, 0f, StepDepth, default, def.wrapped, def.spin)
                    : BuildBakeryItem(Vector3.zero, name, def.item, 0f);
                templates[i].transform.SetParent(templateRoot, false);
            }

            const int InitialSteps = 30;
            var initial = new GameObject[InitialSteps];
            for (int i = 0; i < InitialSteps; i++)
            {
                var template = templates[i % templates.Length];
                var go = Object.Instantiate(template, EndlessSquishyPath.SlotPosition(i), EndlessSquishyPath.SlotRotation(i), endless.transform);
                go.name = $"Step_{i:000}_{template.name}";
                go.SetActive(true);
                var sq = go.GetComponentInChildren<Squishy>();
                if (sq != null) { sq.pathOrder = i + 1; sq.stage = 1; }
                initial[i] = go;
            }
            templateRoot.gameObject.SetActive(false);
            endless.stepTemplates = templates;
            endless.initialSteps = initial;

            var environment = new Transform[4];
            for (int i = 0; i < environment.Length; i++) environment[i] = BuildThemeEnvironmentChunk(definition, i - 1);
            endless.environmentChunks = environment;

            var player = BuildPuddingBunny(new Vector3(startCenter.x, 1.0f, startCenter.z));
            var pc = player.GetComponent<PlayerController>();
            endless.player = player.transform;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = bg;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.farClipPlane = 300f;
            cam.fieldOfView = 58f;
            var pfx = camGo.AddComponent<CruncheePostFX>();
            pfx.shader = Shader.Find("Hidden/Crunchee/PostFX");
            pfx.exposure = 0.84f; pfx.threshold = 1.3f; pfx.bloomIntensity = 0.15f;
            pfx.saturation = 1.16f; pfx.contrast = 1.11f; pfx.vignette = 0.24f;
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<ThirdPersonFollow>();
            follow.target = player.transform;
            follow.fixedYaw = EndlessSquishyPath.SideYawAt(0); // behind the course at the start wall
            follow.viewOffset = new Vector3(0f, 7.4f, -8f);
            follow.aimOffset = new Vector3(0f, 0.5f, 2.3f);
            endless.cameraFollow = follow;
            camGo.transform.SetPositionAndRotation(follow.DesiredPosition, follow.ViewRotation);
            pc.cameraRig = camGo.transform;

            // Soft glitter trail drifting off the bunny - the dreamy ASMR
            // sparkle that hangs in the air behind her as she climbs.
            var trail = new GameObject("BunnySparkles").AddComponent<ParticleSystem>();
            trail.transform.SetParent(player.transform, false);
            trail.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            var tm = trail.main;
            tm.simulationSpace = ParticleSystemSimulationSpace.World; // sparkles stay where she was
            tm.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            tm.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
            tm.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
            tm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.7f, 0.5f), new Color(1f, 1f, 1f, 0.85f));
            tm.maxParticles = 90; tm.gravityModifier = -0.05f; // they float up a touch
            var tem = trail.emission; tem.rateOverTime = 16f;
            var tshape = trail.shape; tshape.shapeType = ParticleSystemShapeType.Sphere; tshape.radius = 0.55f;
            trail.GetComponent<ParticleSystemRenderer>().material = SparkleParticleMat();

            var audio = new GameObject("AsmrAudio"); audio.AddComponent<AsmrAudio>();
            BuildUI(player);

            PlayerSettings.productName = "Crunchee";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.verafaith.crunchee");
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.iOS.buildNumber = "3";
            PlayerSettings.iOS.appleDeveloperTeamID = "738KBBGF8K";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            SetAppIcon();

            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            string scenePath = $"Assets/Scenes/{definition.sceneName}.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[Crunchee] {definition.displayName} scene built: {scenePath} — press Play.");
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
            var scenePaths = Crunchee.Game.MapCatalog.AvailableScenePaths();
            EditorBuildSettings.scenes = new EditorBuildSettingsScene[scenePaths.Length];
            for (int i = 0; i < scenePaths.Length; i++)
                EditorBuildSettings.scenes[i] = new EditorBuildSettingsScene(scenePaths[i], true);
            var opts = new BuildPlayerOptions
            {
                scenes = scenePaths,
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

    }
}
#endif
