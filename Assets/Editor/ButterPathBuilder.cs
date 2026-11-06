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
        // Spiral staircase (Faith, Nov 3): the path winds up a center tower.
        const float SpiralCenterZ = 6.4f, SpiralR = 4.6f, SpiralStepDeg = 36f, SpiralRise = 0.55f, SpiralDepth = 3.1f;

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

            // --- Atmosphere: dreamy lavender-pink (the reference videos'
            // room) + soft distance fog. Exposure reined in vs the big build:
            // build 1 blew out on the phone (sun 1.35, reflections 1.15), so
            // both come down and the ambient lifts slightly instead. ---
            var bg = new Color(0.93f, 0.85f, 0.94f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = bg;
            RenderSettings.fogStartDistance = 42f;
            RenderSettings.fogEndDistance = 120f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.54f, 0.5f, 0.52f);
            RenderSettings.reflectionIntensity = 0.85f;

            // --- Render quality (big build): MSAA, crisp shadows, full aniso ---
            QualitySettings.antiAliasing = 4;
            QualitySettings.shadowDistance = 70f;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;

            // --- Lighting: warm golden bakery sun, gentler than the big build ---
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.12f;
            sun.color = new Color(1f, 0.88f, 0.7f);
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            sun.shadowBias = 0.02f;
            sun.shadowNormalBias = 0.04f;

            // --- The room (Faith, Nov 3): the path is now a SPIRAL STAIRCASE
            // winding up a center tower inside a round, dreamy checkered room
            // (the reference videos' pink checkerboard world). ---
            var center = new Vector3(0f, 0f, SpiralCenterZ);

            // Floor: one big round pastel plate the tower stands on.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "RoomFloor";
            floor.transform.position = center + new Vector3(0f, -0.21f, 0f);
            floor.transform.localScale = new Vector3(30f, 0.15f, 30f);
            floor.GetComponent<MeshRenderer>().material = Mat("RoomFloor", new Color(0.98f, 0.9f, 0.85f), 1f, 0.45f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.AddComponent<MeshCollider>(); // the floor MUST be solid - step off the path and you land on it. (Bug found Nov 3: the collider was destroyed with the props', and she fell 41,720 units into the void.)

            // Checkered room wall: a cylinder seen from inside (culling off).
            var wallMat = Mat("CheckerWall", Color.white, 1f, 0.35f);
            wallMat.mainTexture = CheckerTexture();
            wallMat.mainTextureScale = new Vector2(6f, 2f);
            wallMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wall.name = "CheckerWall";
            wall.transform.position = center + new Vector3(0f, 13f, 0f);
            wall.transform.localScale = new Vector3(33f, 17f, 33f);
            wall.GetComponent<MeshRenderer>().material = wallMat;
            Object.DestroyImmediate(wall.GetComponent<Collider>());

            var butterMat = Mat("Butter", new Color(1f, 0.9f, 0.5f), 1f, 0.75f);
            butterMat.mainTexture = ButterSwirlTexture();
            var patMat = Mat("ButterPat", new Color(1f, 0.92f, 0.55f), 1f, 0.8f);
            patMat.mainTexture = ButterSwirlTexture();
            var platformMat = Mat("Platform", new Color(0.98f, 0.94f, 0.86f), 1f, 0.5f);
            var frostingMat = Mat("Frosting", new Color(0.995f, 0.97f, 0.93f), 1f, 0.35f);

            // --- Start: a white FROSTING slab on a wooden board, sprinkles on
            // top. Top stays at 0.49 so the first climb is unchanged. ---
            var board = Box("StartBoard", new Vector3(0f, 0.03f, 0f), new Vector3(4.7f, 0.12f, 4.7f), Mat("Board", new Color(0.85f, 0.62f, 0.44f), 1f, 0.45f));
            Object.DestroyImmediate(board.GetComponent<Collider>());
            Box("StartPlatform", new Vector3(0f, 0.29f, 0f), new Vector3(4f, 0.4f, 4f), frostingMat);
            SprinkleTops(new Vector3(0f, 0.5f, 0f), 1.8f, 44);

            // --- The spiral: 10 wax butters winding UP around the tower.
            // Butter i sits at angle -90 + (i-1)*36 degrees on a 4.6-radius
            // circle, rising 0.55 per step (Faith, Nov 3: spread the path out -
            // farther apart, not stacked on top of each other). Depth 3.1 vs
            // the 2.84 chord keeps a whisker of overlap, so there is still
            // no gap to fall through on the walk band. ---
            Vector3 lastPos = Vector3.zero; float lastAngle = 0f;
            int idx = 0;
            foreach (var s in LevelPath.ButterPath())
            {
                float angDeg = -90f + idx * SpiralStepDeg;
                float ang = angDeg * Mathf.Deg2Rad;
                var p = new Vector3(center.x + Mathf.Cos(ang) * SpiralR,
                                    0.35f + idx * SpiralRise,
                                    center.z + Mathf.Sin(ang) * SpiralR);
                BuildButter(p, $"Butter_{s.waxLayers:00}_Wax{s.waxLayers}", s.coating, s.waxLayers, butterMat, patMat, -angDeg, SpiralDepth);
                lastPos = p; lastAngle = ang; idx++;
            }

            // --- Finish platform: one more rise past butter 10, along its tangent ---
            var tangent = new Vector3(-Mathf.Sin(lastAngle), 0f, Mathf.Cos(lastAngle));
            var finishPos = lastPos + tangent * 3.3f;
            finishPos.y = lastPos.y + SpiralRise + 0.15f;
            float finishYaw = -lastAngle * Mathf.Rad2Deg;
            var finish = Box("FinishPlatform", finishPos, new Vector3(4.2f, 0.4f, 4.2f), platformMat);
            finish.transform.rotation = Quaternion.Euler(0f, finishYaw, 0f);
            // The trigger is its own invisible zone ABOVE the solid platform,
            // so she can stand on the platform AND trip the finish.
            var zone = Box("FinishZone", finishPos + new Vector3(0f, 1.1f, 0f), new Vector3(4.2f, 2.2f, 4.2f), platformMat);
            Object.DestroyImmediate(zone.GetComponent<MeshRenderer>());
            zone.GetComponent<Collider>().isTrigger = true;
            zone.AddComponent<FinishTrigger>();
            BuildDreamyRoom(center, finishPos, finishYaw);

            // --- Player: Pudding Bunny ---
            var player = BuildPuddingBunny(new Vector3(0f, 1.0f, 0f));
            var pc = player.GetComponent<PlayerController>();

            // --- Camera: behind-the-bunny third person (Faith, Nov 3) - rides
            // directly behind her, turning with her facing, so she plays AS
            // the bunny and sees her back + where she's headed ---
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<ThirdPersonFollow>();
            follow.target = player.transform;
            follow.followDistance = 4.6f;
            follow.followHeight = 2.6f;
            follow.lookAhead = 1.8f;
            follow.aimHeight = 0.9f;
            camGo.transform.position = new Vector3(0f, 3.6f, -4.6f); // exactly the first-frame follow spot (she spawns facing +Z) - no snap
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

        /// <summary>One giant butter step: body + pat top + coating shell + particles + Squishy.
        /// Everything is built under an unscaled, yaw-rotated GROUP in LOCAL
        /// coordinates, so a step can sit at any angle (the spiral staircase).
        /// The Squishy and its decals stay parented under the body exactly as
        /// before, so all of their local-space math is unchanged.</summary>
        static void BuildButter(Vector3 pos, string name, Coating coating, int waxLayers, Material butterMat, Material patMat, float yawDeg = 0f, float depth = 0f, Color polishColor = default)
        {
            if (depth <= 0f) depth = ButterD;
            var group = new GameObject(name);
            group.transform.position = pos;
            group.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            var step = ChildBox(group.transform, "Body", Vector3.zero, new Vector3(ButterW, ButterH, depth), butterMat, true);

            // Little serving TRAY under the bar (mockup: every butter sits on one).
            ChildBox(step.transform, "Tray",
                     new Vector3(0f, (-ButterH / 2f - 0.035f) / ButterH, 0f),
                     new Vector3((ButterW + 0.55f) / ButterW, 0.07f / ButterH, (depth + 0.45f) / depth),
                     Mat("Tray", new Color(0.96f, 0.95f, 0.94f), 1f, 0.9f), false);

            // Butter pat: the thinner slab on top of a real butter stick.
            ChildBox(step.transform, "Pat",
                     new Vector3(0f, (ButterH / 2f + PatH / 2f) / ButterH, 0f),
                     new Vector3(0.94f, PatH / ButterH, 0.92f), patMat, false);

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
                default: // Wax: translucent build-up. Build-1 playtest fix: the
                    // old curve hit 0.88 alpha and read as opaque grey roof
                    // tiles on the phone - cap it at 0.6 and warm the color so
                    // the golden butter shows through every layer.
                    float alpha = Mathf.Min(0.6f, 0.08f + (waxLayers - 1) * 0.058f);
                    shellMat = Mat($"Wax{waxLayers}", new Color(1f, 0.97f, 0.9f), alpha, 0.45f);
                    particleColor = Color.white; shellName = "WaxShell"; crackable = true; break;
            }

            // Shell group sits on the pat. Wax/polish = a grid of small
            // plates (they split apart on the first landing); sugar/honey = one soft piece.
            var shell = new GameObject(shellName);
            shell.transform.SetParent(step.transform, false);
            shell.transform.localPosition = new Vector3(0f, (ButterH / 2f + PatH + ButterH * 0.2f) / ButterH, 0f);
            float shellW = ButterW * 1.02f, shellD = depth * 1.02f, shellH = ButterH * 0.5f;
            if (crackable)
            {
                // The shell is a GRID of small plates, not 4 big quadrants
                // (Faith, Nov 3: "its just breaking apart into 4 pieces, but
                // it needs to break more") - 4 across x 3 deep for wax,
                // 3 x 2 for polish. On landing every piece flies radially
                // out from the center (Squishy.CrackShell).
                int nx = coating == Coating.Wax ? 4 : 3;
                int nz = coating == Coating.Wax ? 3 : 2;
                float gap = 0.05f;
                for (int ix = 0; ix < nx; ix++)
                    for (int iz = 0; iz < nz; iz++)
                    {
                        float cx = ((ix + 0.5f) * (shellW / nx) - shellW / 2f) / ButterW;
                        float cz = ((iz + 0.5f) * (shellD / nz) - shellD / 2f) / depth;
                        ChildBox(shell.transform, "ShellPiece",
                                 new Vector3(cx, 0f, cz),
                                 new Vector3((shellW / nx - gap) / ButterW, shellH / ButterH, (shellD / nz - gap) / depth),
                                 shellMat, false);
                    }
            }
            else
            {
                ChildBox(shell.transform, "Shell", Vector3.zero,
                         new Vector3(shellW / ButterW, shellH / ButterH, shellD / depth), shellMat, false);
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
                decal.transform.SetParent(step.transform, false);
                decal.transform.localPosition = new Vector3(0f, (ButterH / 2f + PatH + ButterH * 0.2f + ButterH * 0.25f + 0.012f) / ButterH, 0f);
                decal.transform.localScale = new Vector3(0.1f, 1f, 0.1f);
                decal.GetComponent<MeshRenderer>().material = decalMat;
                Object.DestroyImmediate(decal.GetComponent<Collider>());
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
                dent.transform.SetParent(step.transform, false);
                dent.transform.localPosition = new Vector3(0f, (ButterH / 2f + PatH + ButterH * 0.2f + ButterH * 0.25f + 0.006f) / ButterH, 0f);
                dent.transform.localScale = new Vector3(0.15f / ButterW, 1f, 0.15f / depth);
                dent.GetComponent<MeshRenderer>().material = dentMat;
                Object.DestroyImmediate(dent.GetComponent<Collider>());
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
            return go;
        }

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
            var pudding = Mat("Pudding", new Color(1f, 0.93f, 0.78f), 1f, 1f);
            var caramel = Mat("Caramel", new Color(0.78f, 0.4f, 0.1f), 1f, 1f);
            var brown = Mat("FaceBrown", new Color(0.23f, 0.12f, 0.06f), 1f, 0.7f);
            var white = Mat("EyeShine", new Color(1f, 1f, 1f), 1f, 0.3f);
            var blushM = Mat("Blush", new Color(1f, 0.62f, 0.66f), 0.55f, 0.6f);
            var noseM = Mat("Nose", new Color(0.98f, 0.55f, 0.62f), 1f, 0.5f);
            var mouthM = Mat("Mouth", new Color(0.36f, 0.12f, 0.12f), 1f, 0.5f);
            var tongueM = Mat("Tongue", new Color(1f, 0.55f, 0.6f), 1f, 0.6f);

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

            // One-piece chibi body (v9): a small chubby belly under a HUGE
            // round head - the reference's proportions are almost all head.
            Part(PrimitiveType.Sphere, bt, "Body", new Vector3(0f, 0.3f, 0f), new Vector3(0.8f, 0.66f, 0.72f), pudding);
            Part(PrimitiveType.Sphere, bt, "Head", new Vector3(0f, 0.95f, 0f), new Vector3(1.18f, 1.04f, 1.02f), pudding);
            // Glossy caramel HELMET poured over the head (reference: it
            // covers the whole top like a cap, continuous with the ears)
            Part(PrimitiveType.Sphere, bt, "CaramelCap", new Vector3(0f, 1.3f, 0f), new Vector3(1.22f, 0.52f, 1.06f), caramel);
            // Wavy drip edge across the FOREHEAD - drips of uneven length,
            // the face below stays clean and open
            Part(PrimitiveType.Sphere, bt, "HairlineDrip", new Vector3(-0.32f, 1.08f, 0.4f), new Vector3(0.13f, 0.24f, 0.09f), caramel);
            Part(PrimitiveType.Sphere, bt, "HairlineDrip", new Vector3(-0.16f, 1.05f, 0.45f), new Vector3(0.13f, 0.19f, 0.09f), caramel);
            Part(PrimitiveType.Sphere, bt, "HairlineDrip", new Vector3(0f, 1.07f, 0.47f), new Vector3(0.13f, 0.22f, 0.09f), caramel);
            Part(PrimitiveType.Sphere, bt, "HairlineDrip", new Vector3(0.16f, 1.05f, 0.45f), new Vector3(0.13f, 0.19f, 0.09f), caramel);
            Part(PrimitiveType.Sphere, bt, "HairlineDrip", new Vector3(0.32f, 1.08f, 0.4f), new Vector3(0.13f, 0.24f, 0.09f), caramel);
            // Caramel COLLAR poured around her neck (reference), with drips
            // running down the chest - and down her BACK, which is what the
            // gameplay camera actually sees
            Part(PrimitiveType.Sphere, bt, "CaramelPour", new Vector3(0f, 0.56f, 0f), new Vector3(0.86f, 0.32f, 0.78f), caramel);
            foreach (var sx in new[] { -0.3f, -0.15f, 0f, 0.15f, 0.3f })
            {
                float zz = 0.31f - Mathf.Abs(sx) * 0.22f;
                Part(PrimitiveType.Sphere, bt, "CollarDrip", new Vector3(sx, 0.44f, zz), new Vector3(0.13f, 0.2f, 0.09f), caramel);
                Part(PrimitiveType.Sphere, bt, "CollarDripBack", new Vector3(sx, 0.44f, -zz), new Vector3(0.13f, 0.2f, 0.09f), caramel);
            }
            // THE ears (v9, the reference's signature): very long, WIDE lop
            // ears growing from the top of her head and flopping down beside
            // her cheeks, caramel poured over their top halves. Each ear
            // hangs off a PIVOT at the top of her head so BunnyBody can flop
            // it. No pink inner ear - the reference's ears are cream + caramel.
            foreach (var side in new[] { -1f, 1f })
            {
                string sfx = side < 0f ? "L" : "R";
                var pivot = new GameObject("EarPivot" + sfx);
                pivot.transform.SetParent(bt, false);
                pivot.transform.localPosition = new Vector3(side * 0.42f, 1.38f, -0.02f);
                pivot.transform.localRotation = Quaternion.Euler(0f, 0f, side * 20f);
                Part(PrimitiveType.Capsule, pivot.transform, "Ear", new Vector3(0f, -0.42f, 0f), new Vector3(0.34f, 0.48f, 0.17f), pudding);
                Part(PrimitiveType.Capsule, pivot.transform, "EarCaramel", new Vector3(0f, -0.2f, 0f), new Vector3(0.37f, 0.3f, 0.19f), caramel);
            }
            // The reference face: big glossy brown eyes with fat sparkles, a
            // tiny pink nose, an OPEN smile with a little pink tongue, blush
            foreach (var side in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Sphere, bt, "Eye", new Vector3(side * 0.215f, 0.97f, 0.465f), new Vector3(0.115f, 0.15f, 0.06f), brown);
                Part(PrimitiveType.Sphere, bt, "EyeShine", new Vector3(side * 0.195f, 1.035f, 0.51f), new Vector3(0.05f, 0.065f, 0.03f), white);
                Part(PrimitiveType.Sphere, bt, "EyeShine2", new Vector3(side * 0.24f, 0.925f, 0.515f), new Vector3(0.025f, 0.03f, 0.02f), white);
                Part(PrimitiveType.Sphere, bt, "Blush", new Vector3(side * 0.4f, 0.83f, 0.4f), new Vector3(0.18f, 0.11f, 0.03f), blushM);
                // Soft cheek fullness (reference cheeks are round and full)
                Part(PrimitiveType.Sphere, bt, "Cheek", new Vector3(side * 0.5f, 0.66f, 0.16f), new Vector3(0.15f, 0.13f, 0.13f), pudding);
            }
            Part(PrimitiveType.Sphere, bt, "Nose", new Vector3(0f, 0.885f, 0.505f), new Vector3(0.09f, 0.06f, 0.04f), noseM);
            // Open smile: a dark mouth sunk into the face so only its front
            // crescent shows, with the tongue peeking at the bottom
            Part(PrimitiveType.Sphere, bt, "Mouth", new Vector3(0f, 0.8f, 0.48f), new Vector3(0.14f, 0.11f, 0.05f), mouthM);
            Part(PrimitiveType.Sphere, bt, "Tongue", new Vector3(0f, 0.772f, 0.495f), new Vector3(0.085f, 0.05f, 0.035f), tongueM);
            // Tiny arms + tiny legs on PIVOTS (BunnyBody swings them: arms up
            // on jumps, legs stepping while she walks)
            foreach (var side in new[] { -1f, 1f })
            {
                string sfx = side < 0f ? "L" : "R";
                var armPivot = new GameObject("ArmPivot" + sfx);
                armPivot.transform.SetParent(bt, false);
                armPivot.transform.localPosition = new Vector3(side * 0.4f, 0.44f, 0.03f);
                armPivot.transform.localRotation = Quaternion.Euler(0f, 0f, side * 10f);
                Part(PrimitiveType.Capsule, armPivot.transform, "Arm", new Vector3(0f, -0.11f, 0f), new Vector3(0.14f, 0.24f, 0.14f), pudding);
                var legPivot = new GameObject("LegPivot" + sfx);
                legPivot.transform.SetParent(bt, false);
                legPivot.transform.localPosition = new Vector3(side * 0.17f, 0.18f, 0.02f);
                Part(PrimitiveType.Sphere, legPivot.transform, "Leg", new Vector3(0f, -0.05f, 0.01f), new Vector3(0.17f, 0.16f, 0.17f), pudding);
                Part(PrimitiveType.Sphere, legPivot.transform, "Foot", new Vector3(0f, -0.115f, 0.07f), new Vector3(0.19f, 0.1f, 0.3f), pudding);
            }
            // Small cream tail puff - the behind-camera sees it constantly
            Part(PrimitiveType.Sphere, bt, "Tail", new Vector3(0f, 0.3f, -0.36f), new Vector3(0.22f, 0.22f, 0.22f), pudding);
            return player;
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
        /// <summary>
        /// The dreamy room (Faith, Nov 3): a cake-tier TOWER at the center of
        /// the spiral with a glowing star on top, clouds floating around the
        /// stairs, a spiral strand of glowing orbs following the climb,
        /// twinkle dust drifting in the air, bakery props on the floor plate
        /// and on the clouds, and the finish cake crowning the top platform -
        /// the reference videos' soft pink world, rebuilt round.
        /// </summary>
        static void BuildDreamyRoom(Vector3 center, Vector3 finishPos, float finishYaw)
        {
            var cakeMats = new[]
            {
                Mat("CakePink", new Color(1f, 0.75f, 0.82f), 1f, 0.6f),
                Mat("CakeMint", new Color(0.72f, 0.95f, 0.8f), 1f, 0.6f),
                Mat("CakeCream", new Color(1f, 0.95f, 0.82f), 1f, 0.6f),
                Mat("CakeChoco", new Color(0.55f, 0.36f, 0.24f), 1f, 0.6f),
            };
            var cherryMat = Mat("Cherry", new Color(0.9f, 0.15f, 0.25f), 1f, 0.9f);
            var frostingWhite = Mat("RingFrosting", new Color(1f, 0.98f, 0.95f), 1f, 0.5f);

            // --- Center tower: stacked cake tiers the staircase wraps ---
            float[] tierR = { 1.3f, 1.05f, 0.8f };
            float[] tierH = { 2.1f, 1.9f, 1.9f };
            float y = -0.05f;
            for (int i = 0; i < 3; i++)
            {
                var tier = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tier.name = $"TowerTier{i + 1}";
                tier.transform.position = center + new Vector3(0f, y + tierH[i] / 2f, 0f);
                tier.transform.localScale = new Vector3(tierR[i] * 2f, tierH[i] / 2f, tierR[i] * 2f);
                tier.GetComponent<MeshRenderer>().material = cakeMats[i % cakeMats.Length];
                Object.DestroyImmediate(tier.GetComponent<Collider>());
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "FrostingRing";
                ring.transform.position = center + new Vector3(0f, y + tierH[i] - 0.03f, 0f);
                ring.transform.localScale = new Vector3((tierR[i] + 0.07f) * 2f, 0.06f, (tierR[i] + 0.07f) * 2f);
                ring.GetComponent<MeshRenderer>().material = frostingWhite;
                Object.DestroyImmediate(ring.GetComponent<Collider>());
                y += tierH[i];
            }
            // Glowing star-sphere on top + one warm point light
            var star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            star.name = "TowerStar";
            star.transform.position = center + new Vector3(0f, y + 0.5f, 0f);
            star.transform.localScale = new Vector3(0.62f, 0.62f, 0.62f);
            star.GetComponent<MeshRenderer>().material = EmissiveMat("TowerStar", new Color(1f, 0.85f, 0.45f), 2.2f);
            Object.DestroyImmediate(star.GetComponent<Collider>());
            var glow = new GameObject("TowerGlow").AddComponent<Light>();
            glow.type = LightType.Point; glow.color = new Color(1f, 0.8f, 0.55f);
            glow.intensity = 0.8f; glow.range = 13f;
            glow.transform.position = center + new Vector3(0f, y + 0.55f, 0f);

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

            // --- Clouds floating around the stairs (soft self-lit white: plain
            // lit matte read grey-beige against the dreamy fog) ---
            var cloudMat = Mat("Cloud", new Color(0.99f, 0.98f, 1f), 1f, 0.22f);
            cloudMat.EnableKeyword("_EMISSION");
            cloudMat.SetColor("_EmissionColor", new Color(0.55f, 0.52f, 0.58f));
            var clouds = new (float ang, float r, float y, float s)[]
            {
                (15f, 9.5f, 1.4f, 1.2f), (75f, 10.5f, 3.6f, 1.5f), (130f, 9.2f, 6.0f, 1.1f),
                (195f, 10.8f, 2.4f, 1.4f), (250f, 9.4f, 7.4f, 1.2f), (305f, 11.0f, 4.8f, 1.6f),
                (340f, 8.8f, 8.8f, 1.0f),
            };
            var cloudPos = new Vector3[clouds.Length];
            for (int i = 0; i < clouds.Length; i++)
            {
                float a = clouds[i].ang * Mathf.Deg2Rad;
                cloudPos[i] = center + new Vector3(Mathf.Cos(a) * clouds[i].r, clouds[i].y, Mathf.Sin(a) * clouds[i].r);
                BuildCloud(cloudPos[i], clouds[i].s, cloudMat);
            }
            // Cupcakes resting on two of the clouds
            BuildCupcake(cloudPos[1] + new Vector3(0.2f, 0.42f, 0.1f), cakeMats[0], cherryMat);
            BuildCupcake(cloudPos[4] + new Vector3(-0.15f, 0.34f, 0f), cakeMats[1], cherryMat);

            // --- Spiral strand of glowing orbs, following the climb ---
            var orbMat = EmissiveMat("LightOrb", new Color(1f, 0.8f, 0.45f), 1.8f);
            for (int i = 0; i <= 28; i++)
            {
                float deg = -90f + i * 11.25f;
                float a = deg * Mathf.Deg2Rad;
                float yy = 0.35f + ((deg + 90f) / SpiralStepDeg) * SpiralRise + 1.8f;
                var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.name = "LightOrb";
                orb.transform.position = center + new Vector3(Mathf.Cos(a) * 6.7f, yy, Mathf.Sin(a) * 6.7f);
                orb.transform.localScale = new Vector3(0.16f, 0.16f, 0.16f);
                orb.GetComponent<MeshRenderer>().material = orbMat;
                Object.DestroyImmediate(orb.GetComponent<Collider>());
            }

            // --- Twinkle dust drifting through the room ---
            var dust = new GameObject("TwinkleDust").AddComponent<ParticleSystem>();
            dust.transform.position = center + new Vector3(0f, 5f, 0f);
            var dmain = dust.main;
            dmain.loop = true; dmain.playOnAwake = true; dmain.startLifetime = 6f;
            dmain.startSpeed = 0.12f; dmain.startSize = 0.09f; dmain.maxParticles = 90;
            dmain.startColor = new Color(1f, 0.93f, 0.75f, 0.55f);
            var dem = dust.emission; dem.rateOverTime = 14f;
            var dshape = dust.shape; dshape.shapeType = ParticleSystemShapeType.Box;
            dshape.scale = new Vector3(17f, 11f, 17f);
            var dustMat = Mat("TwinkleDust", Color.white, 0.55f, 0.5f);
            dustMat.mainTexture = SparkleTexture();
            dust.GetComponent<ParticleSystemRenderer>().material = dustMat;

            // --- A few bakery props on the floor plate ---
            BuildMacarons(center + new Vector3(7.2f, -0.05f, -2.5f));
            BuildCupcake(center + new Vector3(-7.4f, -0.05f, -1.8f), cakeMats[0], cherryMat);
            BuildHoneyJar(center + new Vector3(7.6f, -0.05f, 3.4f));
            BuildBerryBowl(center + new Vector3(-6.9f, -0.05f, 3.8f));
            BuildFlourBowl(center + new Vector3(-7.8f, -0.05f, 6.8f));
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

        /// <summary>Glossy butter surface: soft golden swirls + a few bright streaks (mockup).</summary>
        static Texture2D ButterSwirlTexture()
        {
            return LoadOrCreateTexture("Assets/Textures/ButterSwirl.png", 512, 256, (x, y, w, h) =>
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
