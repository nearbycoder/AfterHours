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
        TextMeshProUGUI logo, foot;
        float t;
        bool leaving;
        /// <summary>The text size the menu is laid out for (it follows a change made in Settings).</summary>
        float laidOut;

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
            var tag = Ui.Label(root, "Clean the office. Learn its secrets. Decide what survives.", UiFont.Hand, 40, new Color(0.62f, 0.85f, 0.95f, 0.9f), TextAlignmentOptions.Left, "Tagline");
            Ui.Place(tag.rectTransform, new Vector2(0, 1), new Vector2(128, -280), new Vector2(1100, 60), new Vector2(0, 1));

            menu = Widgets.Column(root, "Menu", 14);
            var st = Story.State;
            bool canContinue = StoryState.Load() != null && st.Night >= 1 && st.Night <= NightDefs.Count && string.IsNullOrEmpty(st.Ending);
            if (canContinue)
                Widgets.Button(menu, $"Continue  ·  Night {st.Night}", () => Begin(st.Night), 460, 70, 30, true);
            Widgets.Button(menu, "New Game", NewGame, 460, 64, 30, !canContinue);
            Widgets.Button(menu, "Night Select", () => NightSelect.Show(), 460, 64);
            // Everything read so far, to look back over between sessions and after the ending.
            if (CaseFilePanel.HasEntries(StoryState.Load()))
                Widgets.Button(menu, "Case file", () => CaseFilePanel.Show(StoryState.Load()), 460, 64);
            Widgets.Button(menu, "Settings", () => SettingsPanel.Show(), 460, 64);
            Widgets.Button(menu, "Quit", Application.Quit, 460, 64);
            MenuFocus.AttachAll(menu.gameObject);

            int endings = Records.Current.Endings.Count;
            string footText = "BrightStar Janitorial · Meridian Tower, Suite 1408 · Night shift"
                              + (endings > 0 ? $"    <color=#FFD27Acc>Endings found {endings} / {Endings.Ids.Length}</color>" : "")
                              + (PlaytestLog.Enabled ? "    <color=#9FF5D8cc>● Playtest log on</color>" : "");
            foot = Ui.Label(root, footText, UiFont.Sans, 18, new Color(1, 1, 1, 0.35f), TextAlignmentOptions.BottomLeft, "Footer");
            Layout();

            group.alpha = 0;
            Tween.Run(1.2f, k => { if (group) group.alpha = k; }, Ease.OutCubic); // Continue can close the title first
            float y0 = -140;
            Tween.Run(1.6f, k => { if (logo) logo.rectTransform.anchoredPosition = new Vector2(120 + (1 - k) * -40, y0); }, Ease.OutCubic);
        }

        /// <summary>The menu's scale: the text size's, unless six buttons at Largest would reach the tagline.</summary>
        public float MenuScale { get; private set; } = 1f;

        /// <summary>
        /// The menu and footer at the current text size. Larger sizes scale the buttons from their
        /// bottom edge, so they grow up towards the tagline; the logo and tagline are already large.
        /// With a sixth button (Case file) at Largest, the menu first moves down towards the footer,
        /// then grows only as far as there's room below the tagline.
        /// </summary>
        void Layout()
        {
            float k = laidOut = Settings.TextScale;
            float colH = 0;
            foreach (RectTransform c in menu) colH += c.sizeDelta.y;
            colH += 14 * Mathf.Max(0, menu.childCount - 1);
            const float TaglineBottom = 740f, Gap = 24f;
            float bottom = 138f, km = k;
            if (k > 1f && bottom + colH * k > TaglineBottom - Gap)
            {
                bottom = 40f + 30f * k + Gap; // just above the footer
                km = Mathf.Clamp((TaglineBottom - Gap - bottom) / colH, 1f, k);
            }
            MenuScale = km;
            float top = Mathf.Max(520f, bottom + colH * km);
            Ui.Place(menu, new Vector2(0, 0), new Vector2(120, top), new Vector2(460, colH), new Vector2(0, 1));
            menu.localScale = Vector3.one * km;
            float canvasW = ((RectTransform)Ui.Canvas.transform).rect.width;
            foot.fontSize = 18 * k;
            Ui.Place(foot.rectTransform, new Vector2(0, 0), new Vector2(124, 40), new Vector2(k > 1f ? canvasW - 124 - 60 : 1200, 30 * k), new Vector2(0, 0));
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
            if (laidOut != Settings.TextScale && !SettingsPanel.IsOpen) Layout();
            var kb = Keyboard.current;
            if (((kb != null && kb.enterKey.wasPressedThisFrame) || GameInput.Menu.Start) && !ChoiceMenu.IsOpen && !SettingsPanel.IsOpen && !NightSelect.IsOpen && !BrightnessPanel.IsOpen && !CaseFilePanel.IsOpen)
                Begin(Story.State.Night >= 1 && Story.State.Night <= NightDefs.Count ? Story.State.Night : 1);
        }
    }

    // =============================================================================================

    /// <summary>Esc during a night.</summary>
    public class PauseMenu : MonoBehaviour
    {
        public static PauseMenu Instance { get; private set; }
        public static bool IsOpen => Instance != null;
        RectTransform root, menu;
        TextMeshProUGUI sub;
        float laidOut;

        public static void Show()
        {
            if (Instance) return;
            PlaytestLog.Log("pause");
            Hud.Instance?.ClearCaption(); // it would show under the menu (Night 1's first caption, a story sound)
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
                sub = Ui.Label(root, $"Night {dir.Def.Number} · {dir.Def.Day} · {dir.ClockText()} {dir.ClockSub().Split(' ')[0]}", UiFont.Sans, 26, Ui.TextDim, TextAlignmentOptions.Left, "Subtitle");
            }
            var col = menu = Widgets.Column(root, "Menu", 14);
            Widgets.Button(col, "Resume", Close, 460, 70, 30, true);
            Widgets.Button(col, "Shift sheet", () => { Close(); Clipboard.Instance?.Show(); }, 460, 64);
            Widgets.Button(col, "Settings", () => SettingsPanel.Show(), 460, 64);
            Widgets.Button(col, "Restart this night", () => ChoiceMenu.Show("Restart the night?", "Everything you did tonight is undone.", new List<ChoiceMenu.Option>
            {
                new("Restart", null, () =>
                {
                    int n = NightDirector.Instance.Def.Number;
                    PlaytestLog.Log("restart_night", ("open", PlaytestLog.OpenTasks()));
                    Close();
                    var snap = StoryState.LoadSnapshot(n);
                    if (snap != null) Story.State = snap;
                    GameRoot.Instance.StartNight(n);
                }),
            }), 460, 64);
            // Quitting mid-night drops tonight's progress (Continue starts the night again), so ask.
            Widgets.Button(col, "Quit to title", () => ChoiceMenu.Show("Quit to the title?", "Tonight starts over from 10 PM when you continue. Earlier nights are saved.", new List<ChoiceMenu.Option>
            {
                new("Quit to title", null, () => { Close(); GameRoot.Instance.ToTitle(); }),
            }), 460, 64);
            MenuFocus.AttachAll(col.gameObject);
            BuildControlsCard();
            Sfx.Play("ui_click", null, 0.5f, 0.8f, 0f, AudioBus.Ui);
        }

        TextMeshProUGUI controls;
        RectTransform controlsCard;
        float controlsRefresh;

        /// <summary>The right half: what each control does, with the keys or pad buttons in use now.</summary>
        void BuildControlsCard()
        {
            var card = controlsCard = Ui.Panel(root, "ControlsCard", new Color(0.06f, 0.08f, 0.12f, 0.78f), 18).rectTransform;
            Ui.Place(card, new Vector2(1, 0.5f), new Vector2(-110, -20), new Vector2(700, 720), new Vector2(1, 0.5f));
            var head = Ui.Label(card, "CONTROLS", UiFont.SansBold, 24, Ui.Accent, TextAlignmentOptions.TopLeft, "Title");
            Ui.Place(head.rectTransform, new Vector2(0, 1), new Vector2(36, -30), new Vector2(620, 34), new Vector2(0, 1));
            head.characterSpacing = 4;
            controls = Ui.Label(card, "", UiFont.Sans, 24, Ui.Text, TextAlignmentOptions.TopLeft, "ControlsList");
            Ui.Place(controls.rectTransform, new Vector2(0, 1), new Vector2(36, -82), new Vector2(630, 560), new Vector2(0, 1));
            controls.lineSpacing = 22;
            var foot = Ui.Label(card, "Keys and buttons can be changed in Settings.", UiFont.Sans, 20, Ui.TextDim, TextAlignmentOptions.BottomLeft, "Hint");
            Ui.Place(foot.rectTransform, new Vector2(0, 0), new Vector2(36, 26), new Vector2(570, 30), new Vector2(0, 0));
            RefreshControls();
            Layout();
        }

        /// <summary>The menu's scale at the current text size, and the card's (as far as the space beside the menu allows).</summary>
        public float MenuScale { get; private set; } = 1f;
        public float CardScale { get; private set; } = 1f;

        /// <summary>
        /// Text size scales the buttons and the subtitle, and the controls card as far as it fits
        /// beside them, never below Normal. On narrow screens the margins close up first, then the
        /// buttons grow less, so the card always fits at its Normal size.
        /// </summary>
        void Layout()
        {
            float k = laidOut = Settings.TextScale;
            float canvasW = ((RectTransform)Ui.Canvas.transform).rect.width;
            const float MenuW = 460, CardW = 700;
            float left = 120, right = 110, gap = 40, km = k, kc = 1f;
            if (k > 1f)
            {
                float Room(float menuScale) => canvasW - left - MenuW * menuScale - gap - right;
                if (Room(k) < CardW) { left = 60; right = 50; }
                km = Mathf.Clamp((canvasW - left - gap - right - CardW) / MenuW, 1f, k);
                kc = Mathf.Clamp(Mathf.Min(k, Room(km) / CardW, 1000f / Mathf.Max(1f, controlsCard.sizeDelta.y)), 1f, k);
            }
            MenuScale = km;
            CardScale = kc;
            if (sub)
            {
                sub.fontSize = 26 * km;
                Ui.Place(sub.rectTransform, new Vector2(0, 1), new Vector2(left + 4, -236), new Vector2(800 * km, 40 * km), new Vector2(0, 1));
            }
            Ui.Place(menu, new Vector2(0, 1), new Vector2(left, -320), new Vector2(MenuW, 500), new Vector2(0, 1));
            menu.localScale = Vector3.one * km;
            Ui.Place(controlsCard, new Vector2(1, 0.5f), new Vector2(-right, -20), controlsCard.sizeDelta, new Vector2(1, 0.5f));
            controlsCard.localScale = Vector3.one * kc;
        }

        /// <summary>The card's rows: the key or button as a cap, then what it does.</summary>
        public static string ControlsText()
        {
            var s = Settings.Current;
            bool pad = GameInput.UsingPad;
            static string Cap(string g) => $"<mark=#FFFFFF2E padding=\"10,10,5,5\"><b>{g}</b></mark>";
            string Key(Act a) => GameInput.ActGlyph(a);
            string Hold(bool toggle) => toggle ? "toggle" : "hold";
            var rows = new List<(string key, string what)>
            {
                (pad ? $"{GameInput.PadGlyph("L-STICK")}  {GameInput.PadGlyph("R-STICK")}" : $"{Key(Act.Forward)} {Key(Act.Left)} {Key(Act.Back)} {Key(Act.Right)}  ·  Mouse", "Move and look"),
                (Key(Act.Sprint), $"Brisk walk ({Hold(s.ToggleSprint)})"),
                (Key(Act.Crouch), $"Crouch ({Hold(s.ToggleCrouch)}): under desks"),
                (Key(Act.Use), $"Clean ({Hold(s.ToggleUse)}); throw when holding"),
                (Key(Act.Spray), "Spray: foam glass, then wipe"),
                (Key(Act.Interact), "Use, pick up, put back, read"),
                (Key(Act.Drop), "Drop what you're holding"),
            };
            if (Story.State.Has("has_uv_torch")) rows.Add((Key(Act.Torch), "UV torch: missed spots glow"));
            rows.Add((Key(Act.Clipboard), "Clipboard: tasks, case file"));
            rows.Add((pad ? "◀ ▶" : "1–4  ·  Wheel", "Pick a tool (or automatic)"));
            return string.Join("\n", rows.Select(r => $"{Cap(r.key)}<indent=40%>{r.what}</indent>"));
        }

        void RefreshControls()
        {
            if (!controls) return;
            string text = ControlsText();
            if (controls.text == text) return;
            controls.text = text;
            // The card fits its rows (the torch row comes and goes, labels may wrap).
            float h = controls.GetPreferredValues(text, 630f, 0f).y;
            controls.rectTransform.sizeDelta = new Vector2(630f, h);
            controlsCard.sizeDelta = new Vector2(700f, 82f + h + 76f);
            if (menu) Layout(); // the card's height decides how far it can grow
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
            if ((m.Back || m.Pause) && !SettingsPanel.IsOpen && !ChoiceMenu.IsOpen && Time.frameCount != ChoiceMenu.ClosedFrame) Close();
            // Follow rebinds made in Settings and a switch between keyboard and pad.
            if ((controlsRefresh -= GameTime.UnscaledDelta) <= 0f) { controlsRefresh = 0.25f; RefreshControls(); }
            if (laidOut != Settings.TextScale && !SettingsPanel.IsOpen) Layout();
        }
    }

    // =============================================================================================

    public class SettingsPanel : MonoBehaviour
    {
        static SettingsPanel instance;
        public static bool IsOpen => instance != null;
        RectTransform root, panel;
        /// <summary>The scrolling list at the larger text sizes (null at Normal, which keeps two columns).</summary>
        public static ScrollFollow List => instance != null ? instance.list : null;
        ScrollFollow list;
        bool rebuild;

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
            BuildPanel();
        }

        /// <summary>
        /// Normal: two columns, so everything fits from 4:3 up (the canvas is always 1080 units tall).
        /// Large and Largest: one column of rows at that size, which scrolls.
        /// </summary>
        void BuildPanel()
        {
            float k = Settings.TextScale;
            bool large = k > 1f;
            float canvasW = ((RectTransform)Ui.Canvas.transform).rect.width;
            const float ColW = 600;
            // Normal is 1040 tall (of the canvas's 1080): the right column ran under Done once it had a row for Handwriting.
            float W = large ? Mathf.Min(canvasW - 80f, ColW * k + 160f) : 1360, H = large ? 1000 : 1040;
            var p = Ui.Panel(root, "Panel", new Color(0.07f, 0.09f, 0.13f, 1f), 22);
            panel = p.rectTransform;
            Ui.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, H));
            var title = Ui.Label(panel, "Settings", UiFont.SansBold, 44, Ui.Text, TextAlignmentOptions.TopLeft);
            Ui.Place(title.rectTransform, new Vector2(0, 1), new Vector2(60, -36), new Vector2(700, 60), new Vector2(0, 1));
            RectTransform left, right;
            list = null;
            if (large)
            {
                left = right = Widgets.ScrollList(panel, "List", new Vector2(60 - ScrollFollow.Inset, -110), new Vector2(W - 140f + 2 * ScrollFollow.Inset, H - 110f - 150f), k, 6, out list);
            }
            else
            {
                left = Widgets.Column(panel, "Left", 4);
                Ui.Place(left, new Vector2(0, 1), new Vector2(60, -100), new Vector2(ColW, 680), new Vector2(0, 1));
                right = Widgets.Column(panel, "Right", 4);
                Ui.Place(right, new Vector2(0, 1), new Vector2(60 + ColW + 80, -100), new Vector2(ColW, 680), new Vector2(0, 1));
            }
            var s = Settings.Current;
            string Times(float v) => v.ToString("0.0") + "×";

            Widgets.Heading(left, "Controls", ColW);
            Widgets.Slider(left, "Mouse sensitivity", Mathf.InverseLerp(0.2f, 3f, s.MouseSensitivity), v => s.MouseSensitivity = Mathf.Lerp(0.2f, 3f, v), v => Times(Mathf.Lerp(0.2f, 3f, v)), ColW, 250);
            Widgets.Slider(left, "Stick sensitivity", Mathf.InverseLerp(0.3f, 3f, s.StickSensitivity), v => s.StickSensitivity = Mathf.Lerp(0.3f, 3f, v), v => Times(Mathf.Lerp(0.3f, 3f, v)), ColW, 250);
            Widgets.Toggle(left, "Invert look Y", s.InvertY, v => s.InvertY = v, ColW);
            Widgets.Toggle(left, "Controller vibration", s.Vibration, v => { s.Vibration = v; if (!v) Rumble.Stop(); else Rumble.Pulse(0.2f, 0.4f, 0.15f); }, ColW);
            Widgets.Slider(left, "Field of view", Mathf.InverseLerp(60f, 95f, s.Fov), v => s.Fov = Mathf.Round(Mathf.Lerp(60f, 95f, v)), v => Mathf.Round(Mathf.Lerp(60f, 95f, v)) + "°", ColW, 250);
            Widgets.Toggle(left, "Camera motion", s.HeadBob, v => s.HeadBob = v, ColW); // head bob, kicks and punches
            Widgets.Heading(left, "Sound", ColW);
            Widgets.Slider(left, "Master volume", s.MasterVolume, v => s.MasterVolume = v, null, ColW, 250);
            Widgets.Slider(left, "Music", s.MusicVolume, v => s.MusicVolume = v, null, ColW, 250);
            Widgets.Slider(left, "Effects", s.SfxVolume, v => s.SfxVolume = v, null, ColW, 250);
            Widgets.Slider(left, "Ambience", s.AmbienceVolume, v => s.AmbienceVolume = v, null, ColW, 250);
            Widgets.Toggle(left, "Mono audio", s.MonoAudio, v => s.MonoAudio = v, ColW);
            Widgets.Heading(left, "Feedback", ColW);
            Widgets.Toggle(left, "Playtest log (local file)", s.PlaytestLog, v => s.PlaytestLog = v, ColW);

            Widgets.Heading(right, "Display", ColW);
            Widgets.Button(right, "Brightness  ›", () => BrightnessPanel.Show(), ColW, 56, 26);
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
            // The page is laid out again at the new size straight away (next frame: this runs inside the row's own input event).
            Widgets.Choice(right, "Text size", Settings.TextSizes, Mathf.Clamp(s.TextSize, 0, 2), i => { s.TextSize = i; rebuild = true; }, ColW);
            Widgets.Choice(right, "Handwriting", Settings.Handwriting, s.PlainHandwriting ? 1 : 0, i => s.PlainHandwriting = i == 1, ColW);
            Widgets.Button(right, "Keyboard, mouse and controller  ›", () => ControlsPanel.Show(), ColW, 56, 26);

            var done = Widgets.Button(panel, "Done", Close, 220, 60, 28, true);
            Ui.Place(done, new Vector2(1, 0), new Vector2(-60, 40), new Vector2(220, 60), new Vector2(1, 0));
            done.localScale = Vector3.one * k;
            var focus = MenuFocus.AttachAll(panel.gameObject);
            if (list != null)
            {
                list.Focus = focus;
                focus.Chain = left.GetComponentsInChildren<Selectable>().ToList();
                focus.After.Add(done.GetComponent<Selectable>());
            }
        }

        /// <summary>Lay the page out again (text size changed), keeping the same row selected.</summary>
        void Rebuild()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            string selected = es != null && es.currentSelectedGameObject != null ? es.currentSelectedGameObject.name : null;
            panel.gameObject.SetActive(false); // out of navigation now; destroyed at the end of the frame
            Destroy(panel.gameObject);
            BuildPanel();
            var again = selected != null ? panel.GetComponentsInChildren<Selectable>().FirstOrDefault(x => x.name == selected) : null;
            if (again != null) es.SetSelectedGameObject(again.gameObject);
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
            if (rebuild) { rebuild = false; Rebuild(); }
            if (GameInput.Menu.Back && !ControlsPanel.IsOpen && Time.frameCount != ControlsPanel.ClosedFrame
                && !BrightnessPanel.IsOpen && Time.frameCount != BrightnessPanel.ClosedFrame) Close();
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
            // Text size grows the writing on the cards within them (the number photo gives up the room).
            float k = Settings.TextScale;
            var sub = Ui.Label(root, "Replay any night you've reached, from the state you started it in.", UiFont.Sans, 24 * k, Ui.TextDim, TextAlignmentOptions.Center, "Subtitle");
            Ui.Place(sub.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(1200 * k, 40 * k), new Vector2(0.5f, 1));
            float statsH = 28 * k, labelH = 76 * k, labelY = 12 + statsH + 6, photoH = 340 - 16 - (labelY + labelH) - 2;
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
                Ui.Place(photo.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(248, photoH), new Vector2(0.5f, 1));
                var num = Ui.Label(photo.rectTransform, unlocked ? n.ToString() : "?", UiFont.Type, 110 * photoH / 200f, unlocked ? new Color(0.95f, 0.85f, 0.6f) : new Color(1, 1, 1, 0.2f), TextAlignmentOptions.Center);
                Ui.Stretch(num.rectTransform);
                var label = Ui.Label(rt, unlocked ? $"{NightDefs.Days[n]}\n<size=80%>{NightDefs.Titles[n]}</size>" : "Locked", UiFont.Hand, 30 * k, unlocked ? Ui.Ink : Ui.TextDim, TextAlignmentOptions.Center, "Label");
                // Day and title sit between the photo and the stats line.
                Ui.Place(label.rectTransform, new Vector2(0.5f, 0), new Vector2(0, labelY), new Vector2(260, labelH), new Vector2(0.5f, 0));
                label.lineSpacing = -12;
                FitWithin(label, Ui.LetteringSize(UiFont.Hand, 30f));
                if (res != null && unlocked)
                {
                    var stats = Ui.Label(rt, $"<b>{res.Grade}</b>   secrets {res.Secrets}/{res.SecretsTotal}", UiFont.SansMedium, 19 * k, Palette.Hex("6A3FA0"), TextAlignmentOptions.Center, "Stats");
                    Ui.Place(stats.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(260, statsH), new Vector2(0.5f, 0));
                    FitWithin(stats, 19f);
                }
                if (unlocked)
                {
                    var b = card.gameObject.AddComponent<UnityEngine.UI.Button>();
                    b.transition = Selectable.Transition.None;
                    b.onClick.AddListener(() =>
                    {
                        // Pad A on a question's answer also reaches the card selected behind it.
                        if (ChoiceMenu.IsOpen || Time.frameCount == ChoiceMenu.ClosedFrame) return;
                        Sfx.Play("ui_click", null, 0.5f, 1f, 0f, AudioBus.Ui);
                        void Replay()
                        {
                            var snap = night == 1 ? null : StoryState.LoadSnapshot(night);
                            Story.State = snap ?? new StoryState();
                            Close();
                            if (TitleScreen.Instance) TitleScreen.Instance.Begin(night);
                            else GameRoot.Instance.StartNight(night);
                        }
                        // In the middle of the story, an earlier night rolls the save back to it: ask first.
                        if (ReplayAsks(saved, night))
                            ChoiceMenu.Show($"Replay Night {night}?", $"Continue will pick up from Night {night} afterwards. Night {saved.Night} stays in Night Select.", new List<ChoiceMenu.Option>
                            {
                                new($"Replay Night {night}", $"{NightDefs.Days[night]} · {NightDefs.Titles[night]}", Replay),
                            });
                        else Replay();
                    });
                    var hv = card.gameObject.AddComponent<HoverFx>();
                    hv.Init(card, label, true); // opaque warm highlight; the paper never turns see-through
                }
            }
            EndingsCard(k);
            var back = Widgets.Button(root, "Back", Close, 200, 60);
            Ui.Place(back, new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(200, 60), new Vector2(0.5f, 0));
            MenuFocus.AttachAll(root.gameObject);
        }

        /// <summary>At larger sizes, a card's text shrinks back (never below <paramref name="min"/>) rather than leave its box.</summary>
        static void FitWithin(TextMeshProUGUI t, float min)
        {
            if (t.fontSize <= min) return;
            var box = t.rectTransform.sizeDelta;
            while (t.fontSize > min && t.GetPreferredValues(t.text, box.x, 0f).y > box.y + 0.5f)
                t.fontSize = Mathf.Max(min, t.fontSize - 1f);
        }

        /// <summary>A story in progress further on than <paramref name="night"/> (after the ending, or on that night, nothing is lost).</summary>
        public static bool ReplayAsks(StoryState saved, int night) =>
            saved != null && string.IsNullOrEmpty(saved.Ending) && saved.Night > night && saved.Night <= NightDefs.Count;

        /// <summary>The endings you've seen, in the free slot at the end of the second row. Unseen ones stay unnamed.</summary>
        void EndingsCard(float k)
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
            var list = Ui.Label(rt, sb.ToString(), UiFont.Hand, 30 * k, Ui.Text, TextAlignmentOptions.Top, "List");
            Ui.Place(list.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(250, 180), new Vector2(0.5f, 1));
            list.lineSpacing = -6;
            FitWithin(list, Ui.LetteringSize(UiFont.Hand, 30f));
        }

        void Close()
        {
            GameRoot.Instance.SetBlocked("nightselect", false);
            instance = null;
            Destroy(root.gameObject);
        }

        void Update()
        {
            if (GameInput.Menu.Back && !ChoiceMenu.IsOpen && Time.frameCount != ChoiceMenu.ClosedFrame) Close();
        }
    }
}
