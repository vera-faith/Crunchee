#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Crunchee.Core;
using Crunchee.Game;

namespace Crunchee.EditorTools
{
    /// <summary>
    /// Renders review screenshots of the generated scene (phone-portrait) to
    /// Captures/ at the project root. Batch: -executeMethod Crunchee.EditorTools.CaptureTool.BuildAndCapture
    /// </summary>
    public static class CaptureTool
    {
        const int W = 590, H = 1278;

        // Headless-style trigger for the ALREADY-OPEN editor: an agent (or a
        // terminal) drops Temp/crunchee-capture.flag and the editor rebuilds
        // the scene + writes Captures/ on its next update tick.
        const string FlagPath = "Temp/crunchee-capture.flag";
        const string CaptureAllFlagPath = "Temp/crunchee-capture-all.flag";
        const string ExportFlagPath = "Temp/crunchee-export-sounds.flag";
        const string IosFlagPath = "Temp/crunchee-ios.flag";
        static int poll;
        static string captureDirectory = "Captures/Bakery";

        [InitializeOnLoadMethod]
        static void WatchFlag()
        {
            poll = 0;
            EditorApplication.update += Tick;
            Debug.Log("[Capture] watcher v2 ready");
        }

        static void Tick()
        {
            if (++poll % 45 != 0) return; // ~once a second, cheap
            if (!File.Exists(FlagPath) && !File.Exists(CaptureAllFlagPath) && !File.Exists(ExportFlagPath) && !File.Exists(IosFlagPath)) return;
            // Never clobber her Play session: wait until she exits Play mode.
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            AssetDatabase.Refresh();
            // The refresh may kick off a compile/import; a domain reload re-runs
            // WatchFlag and the next tick retries - keep the flag until then so
            // stale code never builds.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorUtility.scriptCompilationFailed) return;
            if (File.Exists(ExportFlagPath))
            {
                File.Delete(ExportFlagPath);
                try { SoundExport.Export(); }
                catch (System.Exception e) { Debug.LogError("[Export] " + e); }
            }
            if (File.Exists(IosFlagPath))
            {
                File.Delete(IosFlagPath);
                try
                {
                    ButterPathBuilder.Build();
                    ButterPathBuilder.BuildIOS();
                    Debug.Log("[Crunchee] iOS build finished OK");
                }
                catch (System.Exception e) { Debug.LogError("[Crunchee] iOS build FAILED: " + e); }
            }
            if (File.Exists(FlagPath))
            {
                File.Delete(FlagPath);
                try { BuildAndCapture(); }
                catch (System.Exception e) { Debug.LogError("[Capture] " + e); }
            }
            if (File.Exists(CaptureAllFlagPath))
            {
                File.Delete(CaptureAllFlagPath);
                try { BuildAndCaptureAll(); }
                catch (System.Exception e) { Debug.LogError("[Capture] " + e); }
            }
        }

        [MenuItem("Crunchee/Capture Review Shots")]
        public static void Capture() => Capture("Assets/Scenes/ButterPath.unity");

        public static void Capture(string scenePath)
        {
            captureDirectory = scenePath.Contains("ClassicButter") ? "Captures/Classic" : "Captures/Bakery";
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var cam = Camera.main;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (cam == null || player == null) { Debug.LogError("[Capture] camera/player missing"); return; }
            ValidateThemeScene();
            Directory.CreateDirectory(captureDirectory);

            var steps = new List<Transform>();
            foreach (var q in Object.FindObjectsByType<Squishy>())
                if (q.transform.parent != null) steps.Add(q.transform.parent);
            int Order(Transform t) { var q = t.GetComponentInChildren<Crunchee.Core.Squishy>(); return q != null ? q.pathOrder : 0; }
            steps.Sort((a, b) => Order(a).CompareTo(Order(b)));

            var startPos = player.transform.position;
            var startRot = player.transform.rotation;
            Shot(cam, "00_start", cam.transform.position, cam.transform.rotation);

            var follow = cam.GetComponent<Crunchee.CameraRig.ThirdPersonFollow>();
            Vector3 off = follow != null ? follow.viewOffset : new Vector3(0f, 4.6f, -4.8f);
            Quaternion vrot = follow != null ? follow.ViewRotation : cam.transform.rotation;
            int[] picks = { 0, 1, 2, 4, 7, 9, 14, 15, 16, 17, 20, 29 };
            var done = new HashSet<int>();
            foreach (var i in picks)
            {
                if (i < 0 || i >= steps.Count || !done.Add(i)) continue;
                var s = steps[i];
                var top = TopOf(s);
                player.transform.position = top;
                // She faces a different way in each shot: the view must not care.
                player.transform.rotation = Quaternion.Euler(0f, (i * 77) % 360, 0f);
                var courseYaw = Quaternion.Euler(0f, i * EndlessSquishyPath.AngleStep, 0f);
                var stepRot = follow != null ? Quaternion.LookRotation(courseYaw * (follow.aimOffset - follow.viewOffset)) : vrot;
                Shot(cam, $"{done.Count:00}_step{i + 1:00}_{s.name}", top + courseYaw * off, stepRot);
            }
            player.transform.SetPositionAndRotation(startPos, startRot);

            // Bunny close-ups: front 3/4, side, back
            var bp = startPos;
            Shot(cam, "90_bunny_back", bp + new Vector3(0f, 1.4f, -2.2f), Quaternion.LookRotation(new Vector3(0f, -0.5f, 2.2f)));
            Shot(cam, "91_bunny_front", bp + new Vector3(0.5f, 1.25f, 2.0f), Quaternion.LookRotation(new Vector3(-0.5f, -0.35f, -2.0f)));
            Shot(cam, "92_bunny_side", bp + new Vector3(2.1f, 1.2f, 0.4f), Quaternion.LookRotation(new Vector3(-2.1f, -0.3f, -0.4f)));

            // Wide side overview of the climb
            var b = new Bounds(startPos, Vector3.one);
            foreach (var s in steps) b.Encapsulate(s.position);
            var ov = b.center + new Vector3(-7.2f, 7f, -22f);
            Shot(cam, "99_overview", ov, Quaternion.LookRotation(b.center + Vector3.up * 2f - ov));
            var fx = cam.GetComponent<Crunchee.CameraRig.CruncheePostFX>();
            if (fx != null) { fx.enabled = false; Shot(cam, "97_nopost", cam.transform.position, cam.transform.rotation); fx.enabled = true; }

            // Cracked-state previews: split the wax on butters 1, 4 and 10 at
            // their top centre, then retake the same gameplay-angle shot.
            // Edit mode only - the scene is never saved, so reopening resets it.
            foreach (var i in new[] { 1, 3, 8, 9 })
            {
                if (i < 0 || i >= steps.Count) continue;
                var s = steps[i];
                var sq = s.GetComponentInChildren<Crunchee.Core.Squishy>();
                if (sq == null) continue;
                var top = TopOf(s);
                sq.PreviewCrack(top);
                player.transform.position = top;
                player.transform.rotation = Quaternion.Euler(0f, (i * 77) % 360, 0f);
                var courseYaw = Quaternion.Euler(0f, i * EndlessSquishyPath.AngleStep, 0f);
                var stepRot = follow != null ? Quaternion.LookRotation(courseYaw * (follow.aimOffset - follow.viewOffset)) : vrot;
                Shot(cam, $"50_cracked_step{i + 1:00}_{s.name}", top + courseYaw * off, stepRot);
            }
            player.transform.SetPositionAndRotation(startPos, startRot);
            ValidateRecycling();

            Debug.Log("[Capture] done: " + Path.GetFullPath(captureDirectory));
        }

        public static void BuildAndCapture()
        {
            ButterPathBuilder.Build();
            Capture("Assets/Scenes/ButterPath.unity");
        }

        public static void BuildAndCaptureClassic()
        {
            ButterPathBuilder.BuildClassicButter();
            Capture("Assets/Scenes/ClassicButter.unity");
        }

        public static void BuildAndCaptureAll()
        {
            BuildAndCapture();
            BuildAndCaptureClassic();
        }

        static void ValidateRecycling()
        {
            var path = Object.FindFirstObjectByType<EndlessSquishyPath>();
            path.Initialize();
            path.Touch(45);
            path.Touch(75);
            if (path.FocusIndex != 75) throw new System.Exception("[Capture] endless focus did not advance");
            int expected = path.ahead + path.keepBehind + 1;
            if (path.ActiveCount != expected) throw new System.Exception($"[Capture] endless active count {path.ActiveCount}, expected {expected}");
            Debug.Log("[Capture] endless recycling validation passed");
        }

        static void ValidateThemeScene()
        {
            var path = Object.FindFirstObjectByType<EndlessSquishyPath>();
            if (path == null) throw new System.Exception("[Capture] endless path missing");
            if (path.stepTemplates == null || path.stepTemplates.Length != 15) throw new System.Exception("[Capture] expected 15 theme templates");
            if (path.initialSteps == null || path.initialSteps.Length != 30) throw new System.Exception("[Capture] expected 30 initial steps");
            if (path.player == null || path.cameraFollow == null || path.environmentChunks == null || path.environmentChunks.Length != 4)
                throw new System.Exception("[Capture] path references incomplete");
            string expectedSky = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "ClassicButter" ? "classicbutterSky" : "bakerySky";
            if (RenderSettings.skybox == null || RenderSettings.skybox.name != expectedSky)
                throw new System.Exception("[Capture] wrong theme sky material");
            foreach (var go in path.initialSteps)
                if (go == null || go.GetComponentInChildren<Squishy>() == null) throw new System.Exception("[Capture] invalid initial squishy");
            for (int i = 0; i < 30; i++)
            {
                var a = EndlessSquishyPath.SlotPosition(i);
                var b = EndlessSquishyPath.SlotPosition(i + EndlessSquishyPath.StepsPerTurn);
                var delta = b - a;
                if (Mathf.Abs(delta.x) > 0.001f || Mathf.Abs(delta.z) > 0.001f || Mathf.Abs(delta.y - EndlessSquishyPath.TurnHeight) > 0.001f)
                    throw new System.Exception("[Capture] helix does not close cleanly");
            }
            Debug.Log("[Capture] theme scene validation passed");
        }

        static Vector3 TopOf(Transform t)
        {
            var b = new Bounds(t.position, Vector3.zero); bool any = false;
            foreach (var c in t.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger) continue;
                if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
            }
            return new Vector3(t.position.x, any ? b.max.y : t.position.y, t.position.z);
        }

        static void Shot(Camera cam, string name, Vector3 pos, Quaternion rot)
        {
            var oldPos = cam.transform.position; var oldRot = cam.transform.rotation;
            cam.transform.SetPositionAndRotation(pos, rot);
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.aspect = (float)W / H;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"{captureDirectory}/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = prev;
            cam.ResetAspect();
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
            cam.transform.SetPositionAndRotation(oldPos, oldRot);
        }
    }
}
#endif
