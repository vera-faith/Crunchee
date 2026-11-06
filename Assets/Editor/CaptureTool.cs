#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Crunchee.EditorTools
{
    /// <summary>
    /// Renders review screenshots of the generated scene (phone-portrait) to
    /// Captures/ at the project root. Batch: -executeMethod Crunchee.EditorTools.CaptureTool.BuildAndCapture
    /// </summary>
    public static class CaptureTool
    {
        const int W = 590, H = 1278;

        [MenuItem("Crunchee/Capture Review Shots")]
        public static void Capture()
        {
            var scenePath = "Assets/Scenes/ButterPath.unity";
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var cam = Camera.main;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (cam == null || player == null) { Debug.LogError("[Capture] camera/player missing"); return; }
            Directory.CreateDirectory("Captures");

            var steps = new List<Transform>();
            foreach (var go in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (go.parent == null && (go.name.StartsWith("Butter_") || go.name.StartsWith("Candy_"))) steps.Add(go);
            int Order(Transform t) { var q = t.GetComponentInChildren<Crunchee.Core.Squishy>(); return q != null ? q.pathOrder : 0; }
            steps.Sort((a, b) => Order(a).CompareTo(Order(b)));

            var startPos = player.transform.position;
            var startRot = player.transform.rotation;
            Shot(cam, "00_start", cam.transform.position, cam.transform.rotation);

            int[] picks = { 3, 8, 9, 10, 11, 12, 13, 14, 15, 16, 18 };
            var done = new HashSet<int>();
            foreach (var i in picks)
            {
                if (i < 0 || i >= steps.Count || !done.Add(i)) continue;
                var s = steps[i];
                var next = steps[Mathf.Min(i + 1, steps.Count - 1)];
                var top = TopOf(s);
                var dir = next.position - s.position; dir.y = 0f;
                if (dir.sqrMagnitude < 0.01f) dir = s.forward;
                dir.Normalize();
                player.transform.position = top;
                player.transform.rotation = Quaternion.LookRotation(dir);
                var cpos = top - dir * 4.6f + Vector3.up * 2.6f;
                var crot = Quaternion.LookRotation(top + dir * 1.8f + Vector3.up * 0.9f - cpos);
                Shot(cam, $"{done.Count:00}_step{i + 1:00}_{s.name}", cpos, crot);
            }
            player.transform.SetPositionAndRotation(startPos, startRot);

            // Bunny close-up from behind and front
            var bp = startPos;
            Shot(cam, "90_bunny_back", bp + new Vector3(0f, 1.4f, -2.2f), Quaternion.LookRotation(new Vector3(0f, -0.5f, 2.2f)));
            Shot(cam, "91_bunny_front", bp + new Vector3(0f, 1.2f, 2.0f), Quaternion.LookRotation(new Vector3(0f, -0.3f, -2.0f)));

            // Wide overview
            var b = new Bounds(startPos, Vector3.one);
            foreach (var s in steps) b.Encapsulate(s.position);
            var ov = b.center + new Vector3(-4f, 9f, -11.5f);
            Shot(cam, "99_overview", ov, Quaternion.LookRotation(b.center - ov));
            Shot(cam, "98_tower_view", startPos + new Vector3(0f, 2.2f, -3.5f), Quaternion.Euler(-12f, 20f, 0f));
            var fx = cam.GetComponent<Crunchee.CameraRig.CruncheePostFX>();
            if (fx != null) { fx.enabled = false; Shot(cam, "97_nopost", startPos + new Vector3(0f, 2.2f, -3.5f), Quaternion.Euler(-12f, 20f, 0f)); fx.enabled = true; }
            Debug.Log("[Capture] done: " + Path.GetFullPath("Captures"));
        }

        public static void BuildAndCapture()
        {
            ButterPathBuilder.Build();
            Capture();
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
            File.WriteAllBytes($"Captures/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = prev;
            cam.ResetAspect();
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
            cam.transform.SetPositionAndRotation(oldPos, oldRot);
        }
    }
}
#endif
