using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AfterHours
{
    /// <summary>
    /// Title: the camera drifts through the dark office while the menu waits. Continue, New Game,
    /// Night Select, Settings, Quit.
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        public static TitleScreen Instance { get; private set; }
        RectTransform root, menu;
        CanvasGroup group;
        TextMeshProUGUI logo;
        float t;
        bool leaving;

        static readonly (Vector3 pos, float yaw, float pitch)[] Shots =
        {
            (new Vector3(8.0f, 0.1f, 6.0f), 38f, 4f), (new Vector3(9.5f, 0.1f, 7.5f), 30f, 6f),
            (new Vector3(13.0f, -0.1f, 14.6f), 200f, 10f), (new Vector3(11.6f, -0.1f, 13.6f), 210f, 8f),
            (new Vector3(6.2f, 0.0f, 15.2f), 225f, 12f), (new Vector3(5.3f, 0.0f, 13.6f), 210f, 8f),
            (new Vector3(14.6f, 0.0f, 0.6f), -60f, 6f), (new Vector3(13.4f, 0.0f, 1.6f), -50f, 4f),
        };

        public static void ShowTitle()
        {
            if (Instance) return;
            var gr = GameRoot.Instance;
            var rt = Ui.Layer("Title", 50);
            Instance = rt.gameObject.AddComponent<TitleScreen>();
            Instance.root = rt;
            Instance.Build();
            gr.SetBlocked("title", true, true);
            Hud.Instance?.SetVisible(false);
            gr.Rig.Hidden = true;
            AudioDirector.Instance?.PlayMusic("music_title", 0.6f);
        }

        void Build()
        {
            group = root.GetComponent<CanvasGroup>();
            var grad = Ui.Image(root, "Shade", new Color(0.01f, 0.015f, 0.03f, 0.75f), Ui.Glow());
            Ui.Place(grad.rectTransform, new Vector2(0, 0.5f), new Vector2(-300, 0), new Vector2(1800, 2000), new Vector2(0, 0.5f));
            logo = Ui.Label(root, "AFTER HOURS", UiFont.Type, 120, new Color(0.95f, 0.93f, 0.86f), TextAlignmentOptions.Left);
            Ui.Place(logo.rectTransform, new Vector2(0, 1), new Vector2(120, -140), new Vector2(1100, 140), new Vector2(0, 1));
            logo.characterSpacing = 6;
            var tag = Ui.Label(root, "Clean the office. Learn its secrets. Decide what survives.", UiFont.Hand, 40, new Color(0.62f, 0.85f, 0.95f, 0.9f), TextAlignmentOptions.Left);
            Ui.Place(tag.rectTransform, new Vector2(0, 1), new Vector2(128, -280), new Vector2(1100, 60), new Vector2(0, 1));

            menu = Widgets.Column(root, "Menu", 14);
            Ui.Place(menu, new Vector2(0, 0), new Vector2(120, 520), new Vector2(460, 460), new Vector2(0, 1));
            var st = Story.State;
            bool canContinue = StoryState.Load() != null && st.Night >= 1 && st.Night <= NightDefs.Count && string.IsNullOrEmpty(st.Ending);
            if (canContinue)
                Widgets.Button(menu, $"Continue  ·  Night {st.Night}", () => Begin(st.Night), 460, 70, 30, true);
            Widgets.Button(menu, "New Game", NewGame, 460, 64, 30, !canContinue);
            Widgets.Button(menu, "Night Select", () => NightSelect.Show(), 460, 64);
            Widgets.Button(menu, "Settings", () => SettingsPanel.Show(), 460, 64);
            Widgets.Button(menu, "Quit", Application.Quit, 460, 64);
            MenuFocus.AttachAll(menu.gameObject);

            int endings = Records.Current.Endings.Count;
            string footText = "BrightStar Janitorial · Meridian Tower, Suite 1408 · Night shift"
                              + (endings > 0 ? $"    <color=#FFD27Acc>Endings found {endings} / {Endings.Ids.Length}</color>" : "");
            var foot = Ui.Label(root, footText, UiFont.Sans, 18, new Color(1, 1, 1, 0.35f), TextAlignmentOptions.BottomLeft);
            Ui.Place(foot.rectTransform, new Vector2(0, 0), new Vector2(124, 40), new Vector2(1200, 30), new Vector2(0, 0));

            group.alpha = 0;
            Tween.Run(1.2f, k => group.alpha = k, Ease.OutCubic);
            float y0 = -140;
            Tween.Run(1.6f, k => logo.rectTransform.anchoredPosition = new Vector2(120 + (1 - k) * -40, y0), Ease.OutCubic);
        }

        void NewGame()
        {
            if (StoryState.Load() != null)
            {
                ChoiceMenu.Show("Start over?", "Your current story and night results will be replaced.", new List<ChoiceMenu.Option>
                {
                    new("Start a new shift", "Night 1, Monday", () => { StoryState.DeleteAll(); Story.State = new StoryState(); Begin(1); }),
                });
                return;
            }
            Story.State = new StoryState();
            Begin(1);
        }

        public void Begin(int night)
        {
            if (leaving) return;
            leaving = true;
            Tween.Run(0.5f, k => { if (group) group.alpha = 1 - k; }, Ease.InCubic, () =>
            {
                Instance = null;
                GameRoot.Instance.SetBlocked("title", false);
                GameRoot.Instance.Rig.Hidden = false;
                if (root) Destroy(root.gameObject);
                GameRoot.Instance.StartNight(night);
            });
        }

        void Update()
        {
            // Cinematic drift between pairs of keyframes.
            var p = GameRoot.Instance.Player;
            t += GameTime.UnscaledDelta;
            int shot = (int)(t / 12f) % (Shots.Length / 2);
            float k = Mathf.SmoothStep(0, 1, (t % 12f) / 12f);
            var a = Shots[shot * 2];
            var b = Shots[shot * 2 + 1];
            p.Teleport(Vector3.Lerp(a.pos, b.pos, k), Mathf.LerpAngle(a.yaw, b.yaw, k), Mathf.Lerp(a.pitch, b.pitch, k));
            // Logo flickers like a tube light warming up.
            float f = Mathf.PerlinNoise(GameTime.Unscaled * 3f, 0.5f);
            logo.alpha = f > 0.12f ? 1f : 0.55f;
            var kb = Keyboard.current;
            if (((kb != null && kb.enterKey.wasPressedThisFrame) || GameInput.Menu.Start) && !ChoiceMenu.IsOpen && !SettingsPanel.IsOpen && !NightSelect.IsOpen)
                Begin(Story.State.Night >= 1 && Story.State.Night <= NightDefs.Count ? Story.State.Night : 1);
        }
    }

    // =============================================================================================

    /// <summary>Esc during a night.</summary>
    public class PauseMenu : MonoBehaviour
    {
        public static PauseMenu Instance { get; private set; }
        public static bool IsOpen => Instance != null;
        RectTransform root;

        public static void Show()
        {
            if (Instance) return;
            var rt = Ui.Layer("Pause", 55);
            Instance = rt.gameObject.AddComponent<PauseMenu>();
            Instance.root = rt;
            Instance.Build();
        }

        void Build()
        {
            Time.timeScale = 0f;
            AudioListener.pause = true;
            GameRoot.Instance.SetBlocked("pause", true, true);
            PostFx.Instance?.SetInspect(true);
            var dim = Ui.Image(root, "Dim", new Color(0.01f, 0.015f, 0.03f, 0.7f));
            Ui.Stretch(dim.rectTransform);
            var title = Ui.Label(root, "PAUSED", UiFont.Type, 72, Ui.Text, TextAlignmentOptions.Left);
            Ui.Place(title.rectTransform, new Vector2(0, 1), new Vector2(120, -140), new Vector2(800, 90), new Vector2(0, 1));
            var dir = NightDirector.Instance;
            if (dir?.Def != null)
            {
                var sub = Ui.Label(root, $"Night {dir.Def.Number} · {dir.Def.Day} · {dir.ClockText()} {dir.ClockSub().Split(' ')[0]}", UiFont.Sans, 26, Ui.TextDim, TextAlignmentOptions.Left);
                Ui.Place(sub.rectTransform, new Vector2(0, 1), new Vector2(124, -236), new Vector2(800, 40), new Vector2(0, 1));
            }
            var col = Widgets.Column(root, "Menu", 14);
            Ui.Place(col, new Vector2(0, 1), new Vector2(120, -320), new Vector2(460, 500), new Vector2(0, 1));
            Widgets.Button(col, "Resume", Close, 460, 70, 30, true);
            Widgets.Button(col, "Shift sheet", () => { Close(); Clipboard.Instance?.Show(); }, 460, 64);
            Widgets.Button(col, "Settings", () => SettingsPanel.Show(), 460, 64);
            Widgets.Button(col, "Restart this night", () => ChoiceMenu.Show("Restart the night?", "Everything you did tonight is undone.", new List<ChoiceMenu.Option>
            {
                new("Restart", null, () =>
                {
                    int n = NightDirector.Instance.Def.Number;
                    Close();
                    var snap = StoryState.LoadSnapshot(n);
                    if (snap != null) Story.State = snap;
                    GameRoot.Instance.StartNight(n);
                }),
            }), 460, 64);
            Widgets.Button(col, "Quit to title", () => { Close(); GameRoot.Instance.ToTitle(); }, 460, 64);
            MenuFocus.AttachAll(col.gameObject);
            Sfx.Play("ui_click", null, 0.5f, 0.8f, 0f, AudioBus.Ui);
        }

        public void Close()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            PostFx.Instance?.SetInspect(false);
            GameRoot.Instance.SetBlocked("pause", false);
            Instance = null;
            Destroy(root.gameObject);
        }

        void Update()
        {
            var m = GameInput.Menu;
            if ((m.Back || m.Pause) && !SettingsPanel.IsOpen && !ChoiceMenu.IsOpen) Close();
        }
    }

    // =============================================================================================

    public class SettingsPanel : MonoBehaviour
    {
        static SettingsPanel instance;
        public static bool IsOpen => instance != null;
        RectTransform root;

        public static void Show()
        {
            if (instance) return;
            var rt = Ui.Layer("Settings", 58);
            instance = rt.gameObject.AddComponent<SettingsPanel>();
            instance.root = rt;
            instance.Build();
        }

        void Build()
        {
            GameRoot.Instance.SetBlocked("settings", true, true);
            var dim = Ui.Image(root, "Dim", new Color(0.01f, 0.015f, 0.03f, 0.93f));
            Ui.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            // Two columns so everything fits from 4:3 up (the canvas is always 1080 units tall).
            const float W = 1360, H = 900, ColW = 600;
            var panel = Ui.Panel(root, "Panel", new Color(0.07f, 0.09f, 0.13f, 1f), 22);
            Ui.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, H));
            var title = Ui.Label(panel.rectTransform, "Settings", UiFont.SansBold, 44, Ui.Text, TextAlignmentOptions.TopLeft);
            Ui.Place(title.rectTransform, new Vector2(0, 1), new Vector2(60, -36), new Vector2(700, 60), new Vector2(0, 1));
            var left = Widgets.Column(panel.rectTransform, "Left", 6);
            Ui.Place(left, new Vector2(0, 1), new Vector2(60, -100), new Vector2(ColW, 680), new Vector2(0, 1));
            var right = Widgets.Column(panel.rectTransform, "Right", 6);
            Ui.Place(right, new Vector2(0, 1), new Vector2(60 + ColW + 80, -100), new Vector2(ColW, 680), new Vector2(0, 1));
            var s = Settings.Current;
            string Times(float v) => v.ToString("0.0") + "×";

            Widgets.Heading(left, "Controls", ColW);
            Widgets.Slider(left, "Mouse sensitivity", Mathf.InverseLerp(0.2f, 3f, s.MouseSensitivity), v => s.MouseSensitivity = Mathf.Lerp(0.2f, 3f, v), v => Times(Mathf.Lerp(0.2f, 3f, v)), ColW, 250);
            Widgets.Slider(left, "Stick sensitivity", Mathf.InverseLerp(0.3f, 3f, s.StickSensitivity), v => s.StickSensitivity = Mathf.Lerp(0.3f, 3f, v), v => Times(Mathf.Lerp(0.3f, 3f, v)), ColW, 250);
            Widgets.Toggle(left, "Invert look Y", s.InvertY, v => s.InvertY = v, ColW);
            Widgets.Toggle(left, "Controller vibration", s.Vibration, v => { s.Vibration = v; if (!v) Rumble.Stop(); else Rumble.Pulse(0.2f, 0.4f, 0.15f); }, ColW);
            Widgets.Slider(left, "Field of view", Mathf.InverseLerp(60f, 95f, s.Fov), v => s.Fov = Mathf.Round(Mathf.Lerp(60f, 95f, v)), v => Mathf.Round(Mathf.Lerp(60f, 95f, v)) + "°", ColW, 250);
            Widgets.Toggle(left, "Head bob", s.HeadBob, v => s.HeadBob = v, ColW);
            Widgets.Heading(left, "Sound", ColW);
            Widgets.Slider(left, "Master volume", s.MasterVolume, v => s.MasterVolume = v, null, ColW, 250);
            Widgets.Slider(left, "Music", s.MusicVolume, v => s.MusicVolume = v, null, ColW, 250);
            Widgets.Slider(left, "Effects", s.SfxVolume, v => s.SfxVolume = v, null, ColW, 250);
            Widgets.Slider(left, "Ambience", s.AmbienceVolume, v => s.AmbienceVolume = v, null, ColW, 250);

            Widgets.Heading(right, "Display", ColW);
            Widgets.Choice(right, "Graphics quality", GraphicsQuality.Names, GraphicsQuality.Level, i => { s.Quality = i; Settings.ApplyGraphics(); }, ColW);
            Widgets.Slider(right, "Render scale", Mathf.InverseLerp(0.5f, 1f, s.RenderScale), v => { s.RenderScale = Mathf.Lerp(0.5f, 1f, v); Settings.ApplyGraphics(); }, v => Mathf.RoundToInt(Mathf.Lerp(50, 100, v)) + "%", ColW, 250);
            Widgets.Toggle(right, "Fullscreen", s.Fullscreen, v => { s.Fullscreen = v; Settings.ApplyGraphics(); }, ColW);
            Widgets.Toggle(right, "VSync", s.VSync, v => { s.VSync = v; Settings.ApplyGraphics(); }, ColW);
            var caps = Settings.FrameCaps.Select(c => c == 0 ? "No limit" : c + " fps").ToArray();
            Widgets.Choice(right, "Frame rate limit", caps, Mathf.Max(0, System.Array.IndexOf(Settings.FrameCaps, s.FrameCap)), i => { s.FrameCap = Settings.FrameCaps[i]; Settings.ApplyGraphics(); }, ColW);
            Widgets.Heading(right, "Accessibility", ColW);
            Widgets.Toggle(right, "Captions", s.Captions, v => s.Captions = v, ColW);
            Widgets.Toggle(right, "Reduce flashing and flicker", s.ReduceFlashing, v => s.ReduceFlashing = v, ColW);
            Widgets.Toggle(right, "Highlight what you're aiming at", s.AimHighlight, v => s.AimHighlight = v, ColW);
            Widgets.Button(right, "Keyboard and mouse controls  ›", ControlsPanel.Show, ColW, 56, 26);

            var done = Widgets.Button(panel.rectTransform, "Done", Close, 220, 60, 28, true);
            Ui.Place(done, new Vector2(1, 0), new Vector2(-60, 40), new Vector2(220, 60), new Vector2(1, 0));
            MenuFocus.AttachAll(panel.gameObject);
        }

        void Close()
        {
            Settings.Save();
            GameRoot.Instance.SetBlocked("settings", false);
            instance = null;
            Destroy(root.gameObject);
        }

        void Update()
        {
            if (GameInput.Menu.Back && !ControlsPanel.IsOpen && Time.frameCount != ControlsPanel.ClosedFrame) Close();
        }
    }

    // =============================================================================================

    public class NightSelect : MonoBehaviour
    {
        static NightSelect instance;
        public static bool IsOpen => instance != null;
        RectTransform root;

        public static void Show()
        {
            if (instance) return;
            var rt = Ui.Layer("NightSelect", 57);
            instance = rt.gameObject.AddComponent<NightSelect>();
            instance.root = rt;
            instance.Build();
        }

        void Build()
        {
            GameRoot.Instance.SetBlocked("nightselect", true, true);
            var dim = Ui.Image(root, "Dim", new Color(0.01f, 0.015f, 0.03f, 0.97f));
            Ui.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            var title = Ui.Label(root, "Night Select", UiFont.Type, 60, Ui.Text, TextAlignmentOptions.Center);
            Ui.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(1200, 80), new Vector2(0.5f, 1));
            var sub = Ui.Label(root, "Replay any night you've reached, from the state you started it in.", UiFont.Sans, 24, Ui.TextDim, TextAlignmentOptions.Center);
            Ui.Place(sub.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(1200, 40), new Vector2(0.5f, 1));
            var saved = StoryState.Load();
            for (int n = 1; n <= NightDefs.Count; n++)
            {
                int night = n;
                bool unlocked = n == 1 || StoryState.HasSnapshot(n);
                // Best ever, not just this run's: replays from a snapshot roll the save back.
                var best = Records.Current.Best(n);
                var cur = saved?.ResultFor(n);
                var res = best != null ? new NightResult { Grade = best.Grade, Secrets = best.Secrets, SecretsTotal = best.SecretsTotal }
                    : cur;
                int col = (n - 1) % 4, row = (n - 1) / 4;
                var card = Ui.Panel(root, "Night" + n, unlocked ? Palette.Hex("F2EEE2") : new Color(1, 1, 1, 0.08f), 8);
                var rt = card.rectTransform;
                Ui.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(-480 + col * 320 + (row == 1 ? 160 : 0), 120 - row * 380), new Vector2(280, 340));
                rt.localRotation = Quaternion.Euler(0, 0, (n * 37 % 7 - 3) * 0.7f);
                card.raycastTarget = unlocked;
                var photo = Ui.Image(rt, "Photo", unlocked ? Palette.Hex("1B2433") : new Color(0, 0, 0, 0.3f), Ui.Rounded(4));
                Ui.Place(photo.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(248, 200), new Vector2(0.5f, 1));
                var num = Ui.Label(photo.rectTransform, unlocked ? n.ToString() : "?", UiFont.Type, 110, unlocked ? new Color(0.95f, 0.85f, 0.6f) : new Color(1, 1, 1, 0.2f), TextAlignmentOptions.Center);
                Ui.Stretch(num.rectTransform);
                var label = Ui.Label(rt, unlocked ? $"{NightDefs.Days[n]}\n<size=80%>{NightDefs.Titles[n]}</size>" : "Locked", UiFont.Hand, 30, unlocked ? Ui.Ink : Ui.TextDim, TextAlignmentOptions.Center);
                // Day and title sit between the photo and the stats line.
                Ui.Place(label.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 46), new Vector2(260, 76), new Vector2(0.5f, 0));
                label.lineSpacing = -12;
                if (res != null && unlocked)
                {
                    var stats = Ui.Label(rt, $"<b>{res.Grade}</b>   secrets {res.Secrets}/{res.SecretsTotal}", UiFont.SansMedium, 19, Palette.Hex("6A3FA0"), TextAlignmentOptions.Center);
                    Ui.Place(stats.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(260, 28), new Vector2(0.5f, 0));
                }
                if (unlocked)
                {
                    var b = card.gameObject.AddComponent<UnityEngine.UI.Button>();
                    b.transition = Selectable.Transition.None;
                    b.onClick.AddListener(() =>
                    {
                        Sfx.Play("ui_click", null, 0.5f, 1f, 0f, AudioBus.Ui);
                        var snap = night == 1 ? null : StoryState.LoadSnapshot(night);
                        Story.State = snap ?? new StoryState();
                        Close();
                        if (TitleScreen.Instance) TitleScreen.Instance.Begin(night);
                        else GameRoot.Instance.StartNight(night);
                    });
                    var hv = card.gameObject.AddComponent<HoverFx>();
                    hv.Init(card, label, true); // opaque warm highlight; the paper never turns see-through
                }
            }
            EndingsCard();
            var back = Widgets.Button(root, "Back", Close, 200, 60);
            Ui.Place(back, new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(200, 60), new Vector2(0.5f, 0));
            MenuFocus.AttachAll(root.gameObject);
        }

        /// <summary>The endings you've seen, in the free slot at the end of the second row. Unseen ones stay unnamed.</summary>
        void EndingsCard()
        {
            var rec = Records.Current;
            var card = Ui.Panel(root, "Endings", new Color(0.1f, 0.12f, 0.17f, 1f), 8);
            var rt = card.rectTransform;
            Ui.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(640, -260), new Vector2(280, 340));
            rt.localRotation = Quaternion.Euler(0, 0, 1.2f);
            card.raycastTarget = false;
            var head = Ui.Label(rt, $"ENDINGS\n<size=150%>{rec.Endings.Count} / {Endings.Ids.Length}</size>", UiFont.Type, 26, Palette.Hex("F2EEE2"), TextAlignmentOptions.Top);
            Ui.Place(head.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(250, 100), new Vector2(0.5f, 1));
            var sb = new System.Text.StringBuilder();
            foreach (var id in Endings.Ids)
                sb.Append(rec.HasEnding(id) ? $"<color=#FFD27A>✓ {Endings.TitleOf(id)}</color>\n" : "<color=#FFFFFF55>· · ·</color>\n");
            var list = Ui.Label(rt, sb.ToString(), UiFont.Hand, 30, Ui.Text, TextAlignmentOptions.Top);
            Ui.Place(list.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(250, 180), new Vector2(0.5f, 1));
            list.lineSpacing = -6;
        }

        void Close()
        {
            GameRoot.Instance.SetBlocked("nightselect", false);
            instance = null;
            Destroy(root.gameObject);
        }

        void Update()
        {
            if (GameInput.Menu.Back) Close();
        }
    }
}
