#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Crunchee.UI;

namespace Crunchee.EditorTools
{
    /// <summary>
    /// In-game UI construction: canvas, joystick + jump button, progress
    /// pips, and the settings menu (volume sliders + map cards). Runtime
    /// behavior lives in Assets/Scripts/UI (SettingsMenu, MobileControls,
    /// VirtualJoystick, JumpButton, ProgressHud).
    /// </summary>
    public static partial class ButterPathBuilder
    {
        static void BuildUI(GameObject player)
        {
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Scale UI against iPhone portrait reference res so controls
            // aren't huge in landscape / the editor Game view.
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1170f, 2532f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
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

            BuildSettingsUI(canvasGo.transform, font);
        }

        /// <summary>
        /// Halohalo-style corner buttons: the menu (≡) opens the settings
        /// panel from the TOP-LEFT, the speaker mute toggle sits TOP-RIGHT.
        /// The panel keeps Music/ASMR sliders, the map cards and a close
        /// button. Behavior lives in SettingsMenu + MuteButton (runtime).
        /// </summary>
        static void BuildSettingsUI(Transform canvasT, Font font)
        {
            var menu = canvasT.gameObject.AddComponent<SettingsMenu>();

            // small round cream menu button, top-left corner
            var gear = MakeCircle(canvasT, "MenuButton", new Vector2(86f, -96f), 92f, new Color(0.99f, 0.95f, 0.9f, 0.85f));
            gear.anchorMin = gear.anchorMax = new Vector2(0f, 1f);
            gear.anchoredPosition = new Vector2(86f, -96f);
            var gb = gear.gameObject.AddComponent<Button>();
            gb.targetGraphic = gear.GetComponent<Image>();
            var gearIcon = new GameObject("GearIcon").AddComponent<Text>();
            gearIcon.transform.SetParent(gear, false);
            gearIcon.text = "≡"; gearIcon.font = font; gearIcon.fontSize = 52; gearIcon.fontStyle = FontStyle.Bold;
            gearIcon.alignment = TextAnchor.MiddleCenter; gearIcon.color = new Color(0.55f, 0.4f, 0.28f);
            gearIcon.raycastTarget = false;
            var gr = gearIcon.rectTransform; gr.anchorMin = Vector2.zero; gr.anchorMax = Vector2.one; gr.offsetMin = gr.offsetMax = Vector2.zero;
            gb.onClick.AddListener(menu.Toggle);

            // matching round speaker mute toggle, top-right corner
            var mute = MakeCircle(canvasT, "MuteButton", new Vector2(-86f, -96f), 92f, new Color(0.99f, 0.95f, 0.9f, 0.85f), anchorRight: true);
            mute.anchorMin = mute.anchorMax = new Vector2(1f, 1f);
            mute.anchoredPosition = new Vector2(-86f, -96f);
            var mb = mute.gameObject.AddComponent<Button>();
            mb.targetGraphic = mute.GetComponent<Image>();
            var speakerIcon = new GameObject("SpeakerIcon").AddComponent<Image>();
            speakerIcon.transform.SetParent(mute, false);
            speakerIcon.raycastTarget = false;
            var irt = speakerIcon.rectTransform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(20f, 20f); irt.offsetMax = new Vector2(-20f, -20f);
            var muteBtn = mute.gameObject.AddComponent<MuteButton>();
            muteBtn.icon = speakerIcon;
            muteBtn.soundOn = SpeakerSprite(false);
            muteBtn.soundOff = SpeakerSprite(true);

            // fullscreen soft-dim blocker that eats taps while the panel is open
            var blocker = MakeImage(canvasT, "SettingsPanel", Vector2.zero, Vector2.zero, new Color(0.25f, 0.18f, 0.12f, 0.35f));
            var brt = blocker.rectTransform; brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            var panel = blocker.gameObject;
            menu.panel = panel;

            // the soft cream rounded panel
            var card = MakeImage(panel.transform, "Card", Vector2.zero, new Vector2(860f, 1480f), new Color(0.99f, 0.95f, 0.9f, 0.97f));
            var ct = card.transform;

            var title = MakeText(ct, "Settings", new Vector2(0f, 660f), font, 64, new Color(0.45f, 0.3f, 0.2f));

            // --- volume sliders ---
            MakeText(ct, "MUSIC", new Vector2(0f, 520f), font, 40, new Color(0.5f, 0.38f, 0.28f));
            menu.musicSlider = MakeSlider(ct, "MusicSlider", new Vector2(0f, 460f), 640f, AsmrVolume_default: 0.6f);
            MakeText(ct, "ASMR", new Vector2(0f, 340f), font, 40, new Color(0.5f, 0.38f, 0.28f));
            menu.asmrSlider = MakeSlider(ct, "AsmrSlider", new Vector2(0f, 280f), 640f, AsmrVolume_default: 1f);

            // --- map cards ---
            MakeText(ct, "MAPS", new Vector2(0f, 140f), font, 40, new Color(0.5f, 0.38f, 0.28f));
            float cy = 40f;
            foreach (var map in Crunchee.Game.MapCatalog.Maps)
            {
                bool current = Crunchee.Game.MapCatalog.IsCurrent(map);
                var mc = MakeImage(ct, "Map_" + map.id, new Vector2(0f, cy), new Vector2(680f, 128f),
                                   map.available ? new Color(1f, 0.88f, 0.82f) : new Color(0.82f, 0.78f, 0.75f));
                var mt = MakeText(mc.transform, map.displayName, new Vector2(0f, 20f), font, 44,
                                  map.available ? new Color(0.4f, 0.28f, 0.18f) : new Color(0.55f, 0.5f, 0.47f));
                MakeText(mc.transform, map.available ? (current ? "Current" : "Play") : "Coming soon",
                         new Vector2(0f, -28f), font, 30,
                         map.available ? new Color(0.7f, 0.4f, 0.3f) : new Color(0.6f, 0.55f, 0.52f));
                if (map.available)
                {
                    var mb = mc.gameObject.AddComponent<Button>();
                    mb.targetGraphic = mc;
                    string id = map.id;
                    mb.onClick.AddListener(() => menu.SelectMap(id));
                }
                cy -= 160f;
            }

            // --- close ---
            var close = MakeCircle(ct, "CloseButton", Vector2.zero, 88f, new Color(1f, 0.62f, 0.72f, 0.9f));
            close.anchorMin = close.anchorMax = new Vector2(0.5f, 0.5f);
            close.anchoredPosition = new Vector2(0f, -640f);
            var cb = close.gameObject.AddComponent<Button>();
            cb.targetGraphic = close.GetComponent<Image>();
            cb.onClick.AddListener(menu.Close);
            var x = new GameObject("X").AddComponent<Text>();
            x.transform.SetParent(close, false);
            x.text = "×"; x.font = font; x.fontSize = 60; x.fontStyle = FontStyle.Bold;
            x.alignment = TextAnchor.MiddleCenter; x.color = Color.white; x.raycastTarget = false;
            var xr = x.rectTransform; xr.anchorMin = Vector2.zero; xr.anchorMax = Vector2.one; xr.offsetMin = xr.offsetMax = Vector2.zero;

            // built visible for wiring; SettingsMenu.Awake also hides it, but
            // edit-mode captures never run Awake - hide it here too.
            panel.SetActive(false);
        }

        static Image MakeImage(Transform parent, string name, Vector2 anchored, Vector2 size, Color c, string sprite = "UI/Skin/UISprite.psd")
        {
            var img = new GameObject(name).AddComponent<Image>();
            img.transform.SetParent(parent, false);
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(sprite);
            img.type = Image.Type.Sliced;
            img.color = c;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchored; rt.sizeDelta = size;
            return img;
        }

        static Text MakeText(Transform parent, string text, Vector2 anchored, Font font, int size, Color c)
        {
            var t = new GameObject(text.Replace(" ", "") + "Label").AddComponent<Text>();
            t.transform.SetParent(parent, false);
            t.text = text; t.font = font; t.fontSize = size; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.color = c; t.raycastTarget = false;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchored; rt.sizeDelta = new Vector2(700f, size + 16f);
            return t;
        }

        /// <summary>A pastel slider: soft track, pink fill, cream knob.</summary>
        static Slider MakeSlider(Transform parent, string name, Vector2 anchored, float width, float AsmrVolume_default)
        {
            var track = MakeImage(parent, name, anchored, new Vector2(width, 22f), new Color(0.85f, 0.75f, 0.68f));
            var fill = MakeImage(track.transform, "Fill", Vector2.zero, Vector2.zero, new Color(1f, 0.62f, 0.72f));
            var frt = fill.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(0f, 1f);
            frt.offsetMin = Vector2.zero; frt.offsetMax = new Vector2(0f, 0f);
            var knob = MakeCircle(track.transform, "Knob", Vector2.zero, 44f, new Color(0.99f, 0.95f, 0.9f));
            var slider = track.gameObject.AddComponent<Slider>();
            slider.minValue = 0f; slider.maxValue = 1f; slider.value = AsmrVolume_default;
            slider.fillRect = frt; slider.handleRect = knob;
            slider.targetGraphic = knob.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
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

        /// <summary>A white speaker glyph drawn in pixels: box + horn cone,
        /// then two sound arcs (or a big X when muted).</summary>
        static Sprite SpeakerSprite(bool muted)
        {
            return LoadOrCreateSprite(muted ? "Assets/Textures/IconMuted.png" : "Assets/Textures/IconSound.png",
                96, 96, (x, y, w, h) =>
            {
                bool inside = false;
                // speaker box
                if (x >= 14 && x <= 30 && y >= 36 && y <= 60) inside = true;
                // horn cone widening from the box to the right
                if (x > 30 && x <= 52)
                {
                    float top = 36f - (x - 30) * (18f / 22f);
                    float bot = 60f + (x - 30) * (18f / 22f);
                    if (y >= top && y <= bot) inside = true;
                }
                float d = Mathf.Sqrt((x - 52f) * (x - 52f) + (y - 48f) * (y - 48f));
                if (muted)
                {
                    // big X over the sound area
                    float d1 = Mathf.Abs(x - (118f - y));
                    float d2 = Mathf.Abs(x - (y + 8f));
                    if (x >= 58 && x <= 84 && y >= 28 && y <= 68 && (d1 < 5f || d2 < 5f)) inside = true;
                }
                else
                {
                    // two sound arcs fanning right of the horn
                    if (x > 53 && ((d >= 12f && d <= 16f) || (d >= 20f && d <= 24f))) inside = true;
                }
                return inside ? Color.white : new Color(1f, 1f, 1f, 0f);
            });
        }

        /// <summary>Like LoadOrCreateTexture but imports as a UI sprite.</summary>
        static Sprite LoadOrCreateSprite(string path, int w, int h, System.Func<int, int, int, int, Color> pixel)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Textures")) AssetDatabase.CreateFolder("Assets", "Textures");
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, pixel(x, y, w, h));
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath, path.Substring("Assets/".Length)), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.filterMode = FilterMode.Bilinear;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
#endif
