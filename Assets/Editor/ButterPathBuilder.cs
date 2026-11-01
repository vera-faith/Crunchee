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
    /// One-click map build (Faith: "start building this map exactly right now").
    /// Menu: Crunchee > Build Butter Path Scene
    /// Generates Assets/Scenes/ButterPath.unity exactly per the locked design:
    /// start platform -> 10 wax butters (layers 1..10, LOW-OPACITY wax: alpha 0.12/layer,
    /// capped 0.75, butter always visible underneath) -> finish platform,
    /// bird's-eye camera, bunny placeholder player, joystick + JUMP UI.
    /// Pure greybox geometry in v1 — art models replace the boxes in M3.
    /// </summary>
    public static class ButterPathBuilder
    {
        const float ButterW = 2.6f, ButterH = 0.7f, ButterD = 1.6f;

        static Material Mat(string name, Color c, float alpha = 1f)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(sh) { name = name, color = new Color(c.r, c.g, c.b, alpha) };
            if (alpha < 1f)
            {
                m.SetFloat("_Surface", 1f); // transparent (URP); Standard ignores harmlessly
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

            // --- Floor (bakery counter) ---
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "BakeryFloor"; floor.transform.localScale = new Vector3(4f, 1f, 14f);
            floor.transform.position = new Vector3(0f, -0.06f, 16f);
            floor.GetComponent<MeshRenderer>().material = Mat("Floor", new Color(0.96f, 0.85f, 0.72f));

            var butterMat = Mat("Butter", new Color(1f, 0.85f, 0.35f));
            var platformMat = Mat("Platform", new Color(0.98f, 0.94f, 0.86f));

            // --- Start platform ---
            Vector3 pos = Vector3.zero;
            var start = Box("StartPlatform", pos, new Vector3(3.4f, 0.4f, 3.4f), platformMat);

            // --- 10 butters, wax layers 1..10, low-opacity build-up ---
            var steps = LevelPath.ButterPath();
            foreach (var s in steps)
            {
                pos += new Vector3(s.sideOffset, s.heightOffset, ButterD + s.gap * 0.55f);
                var step = Box($"Butter_{s.waxLayers:00}_Wax{s.waxLayers}", pos, new Vector3(ButterW, ButterH, ButterD), butterMat);

                // Wax shell: slightly larger hollow-ish cap over the top, TRANSLUCENT.
                float alpha = Mathf.Min(0.75f, 0.12f * s.waxLayers);   // butter ALWAYS shows through
                var shell = Box("WaxShell", pos + new Vector3(0f, ButterH * 0.28f, 0f),
                                new Vector3(ButterW * 1.03f, ButterH * 0.55f, ButterD * 1.03f),
                                Mat($"Wax{s.waxLayers}", Color.white, alpha));
                shell.transform.SetParent(step.transform, true);
                Object.DestroyImmediate(shell.GetComponent<Collider>()); // steps land on the butter collider

                var sq = step.AddComponent<Squishy>();
                sq.baseMaterial = s.material; sq.coating = s.coating; sq.waxLayers = s.waxLayers;
                sq.coatingShell = shell;

                // wax flake particles
                var ps = new GameObject("WaxFlakes").AddComponent<ParticleSystem>();
                ps.transform.SetParent(step.transform, false);
                ps.transform.localPosition = new Vector3(0f, ButterH * 0.6f, 0f);
                var main = ps.main; main.startColor = Color.white; main.startSize = 0.09f;
                main.startSpeed = 2.2f; main.startLifetime = 0.7f; main.playOnAwake = false;
                var em = ps.emission; em.enabled = false;
                sq.stepParticles = ps;
            }

            // --- Finish platform ---
            pos += new Vector3(0f, 0.35f, 3.2f);
            var finish = Box("FinishPlatform", pos, new Vector3(3.6f, 0.4f, 3.6f), platformMat);
            finish.AddComponent<FinishTrigger>();

            // --- Player (bunny placeholder: capsule + ears, replaced by model in M4) ---
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player_BunnyPlaceholder";
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.transform.position = new Vector3(0f, 1.2f, 0f);
            player.GetComponent<MeshRenderer>().material = Mat("Bunny", new Color(1f, 0.93f, 0.78f));
            var cc = player.AddComponent<CharacterController>(); cc.height = 1.1f; cc.radius = 0.35f;
            var model = new GameObject("Model"); model.transform.SetParent(player.transform, false);
            model.AddComponent<SquashAndStretch>();
            foreach (var side in new[] { -1f, 1f })
            {
                var ear = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                ear.name = "Ear"; ear.transform.SetParent(model.transform, false);
                ear.transform.localPosition = new Vector3(side * 0.18f, 0.62f, 0f);
                ear.transform.localScale = new Vector3(0.16f, 0.42f, 0.08f);
                ear.transform.rotation = Quaternion.Euler(0f, 0f, side * -18f);
                ear.GetComponent<MeshRenderer>().material = Mat("Caramel", new Color(0.78f, 0.47f, 0.2f));
                Object.DestroyImmediate(ear.GetComponent<Collider>());
            }
            var pc = player.AddComponent<PlayerController>();

            // --- Camera: bird's-eye-ish follow ---
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<ThirdPersonFollow>();
            follow.target = player.transform; // offset defaults are the bird's-eye values
            pc.cameraRig = camGo.transform;

            // --- Audio root ---
            var audio = new GameObject("AsmrAudio"); audio.AddComponent<AsmrAudio>();

            // --- UI: joystick bottom-left + JUMP bottom-right (labels BELOW buttons) ---
            BuildUI(player);

            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/ButterPath.unity");
            Debug.Log("[Crunchee] Butter Path scene built: Assets/Scenes/ButterPath.unity — press Play.");
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
