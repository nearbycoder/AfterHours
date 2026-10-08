using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

namespace AfterHours
{
    /// <summary>
    /// Bindings (from Settings), on two pages: keyboard and mouse, and controller. Pick a row, press
    /// the new key, mouse button or pad button; one that's already in use swaps with the other
    /// action, Esc (or Start) cancels.
    /// </summary>
    public class ControlsPanel : MonoBehaviour
    {
        static ControlsPanel instance;
        public static bool IsOpen => instance != null;
        /// <summary>The action waiting for a key, if any (automation checks this).</summary>
        public static Act? Listening => instance != null ? instance.listening : null;

        RectTransform root;
        TextMeshProUGUI message;
        readonly Dictionary<Act, RectTransform> caps = new();
        Act? listening;
        IDisposable listener;
        float quietUntil;
        public const int KeyboardPage = 0, PadPage = 1;
        int page;
        /// <summary>The page showing (automation checks this).</summary>
        public static int Page => instance != null ? instance.page : -1;
        /// <summary>The scrolling list at the larger text sizes (null at Normal, which keeps two columns).</summary>
        public static ScrollFollow List => instance != null ? instance.list : null;
        ScrollFollow list;

        public static void Show(int page = KeyboardPage) => Show(page, true);

        static void Show(int page, bool fade)
        {
            if (instance) return;
            var rt = Ui.Layer("Controls", 59);
            instance = rt.gameObject.AddComponent<ControlsPanel>();
            instance.root = rt;
            instance.page = page;
            instance.Build();
            if (fade) MenuFade.In(rt); // a new tab swaps in place
        }

        /// <summary>Rebuild on the other page (the tabs).</summary>
        public void SwitchTo(int to)
        {
            if (to == page || listening.HasValue || GameTime.Unscaled < quietUntil) return;
            Sfx.Play("ui_click", null, 0.45f, 1f, 0f, AudioBus.Ui);
            listener?.Dispose();
            listener = null;
            instance = null;
            Destroy(root.gameObject);
            Show(to, false);
        }

        void Build()
        {
            GameRoot.Instance.SetBlocked("controls", true, true);
            var dim = Ui.Image(root, "Dim", new Color(0.01f, 0.015f, 0.03f, 0.93f));
            Ui.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            // Normal: two columns. Large and Largest: one column of rows at that size, which scrolls,
            // with the tabs, the note and the buttons scaled around it as far as the panel allows.
            float k = Settings.TextScale;
            bool large = k > 1f;
            float canvasW = ((RectTransform)Ui.Canvas.transform).rect.width;
            const float ColW = 600;
            float W = large ? Mathf.Min(canvasW - 80f, ColW * k + 160f) : 1360, H = large ? 1000 : 900;
            float kt = large ? Mathf.Clamp((W - 140f) / 680f, 1f, k) : 1f;
            var panel = Ui.Panel(root, "Panel", new Color(0.07f, 0.09f, 0.13f, 1f), 22);
            Ui.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, H));
            // Tabs: the two pages. The current one is the primary (filled) button.
            var tabs = new[] { (KeyboardPage, "Keyboard and mouse"), (PadPage, "Controller") };
            float tx = 60;
            var tabSel = new List<Selectable>();
            foreach (var (p, name) in tabs)
            {
                int to = p;
                var tab = Widgets.Button(panel.rectTransform, name, () => SwitchTo(to), 330, 60, 28, p == page);
                tab.name = "Tab_" + name;
                Ui.Place(tab, new Vector2(0, 1), new Vector2(tx, -36), new Vector2(330, 60), new Vector2(0, 1));
                tab.localScale = Vector3.one * kt;
                tabSel.Add(tab.GetComponent<Selectable>());
                tx += 350 * kt;
            }
            RectTransform left, right;
            list = null;
            // The note under the rows gets the height its longest text needs.
            message = Ui.Label(panel.rectTransform, "", UiFont.Sans, 22 * k, Ui.TextDim, TextAlignmentOptions.TopLeft, "Note");
            float noteH = large ? new[] { DefaultNote(KeyboardPage), DefaultNote(PadPage) }.Max(t => message.GetPreferredValues(t, W - 120, 0).y) + 10 : 60;
            float buttonsTop = 40 + 60 * k;
            if (large)
            {
                float top = 36 + 60 * kt + 24;
                left = right = Widgets.ScrollList(panel.rectTransform, "List", new Vector2(60 - ScrollFollow.Inset, -top), new Vector2(W - 140f + 2 * ScrollFollow.Inset, H - top - (buttonsTop + 20 + noteH + 16)), k, 6, out list);
            }
            else
            {
                left = Widgets.Column(panel.rectTransform, "Left", 6);
                Ui.Place(left, new Vector2(0, 1), new Vector2(60, -116), new Vector2(ColW, 520), new Vector2(0, 1));
                right = Widgets.Column(panel.rectTransform, "Right", 6);
                Ui.Place(right, new Vector2(0, 1), new Vector2(60 + ColW + 80, -116), new Vector2(ColW, 520), new Vector2(0, 1));
            }
            var labels = page == PadPage ? Controls.PadLabels : Controls.Labels;
            int split = page == PadPage ? 4 : 7;
            for (int i = 0; i < labels.Length; i++)
            {
                var (act, label) = labels[i];
                Row(i < split ? left : right, act, label, ColW);
            }
            // Hold or toggle, for keys and pad alike (on both pages).
            var modes = new[] { "Hold", "Toggle" };
            var st = Settings.Current;
            var modeCol = page == PadPage ? left : right;
            Widgets.Choice(modeCol, "Crouch mode", modes, st.ToggleCrouch ? 1 : 0, i => { st.ToggleCrouch = i == 1; GameInput.ResetToggles(); }, ColW);
            Widgets.Choice(modeCol, "Brisk walk mode", modes, st.ToggleSprint ? 1 : 0, i => { st.ToggleSprint = i == 1; GameInput.ResetToggles(); }, ColW);
            Widgets.Choice(modeCol, "Clean and spray mode", modes, st.ToggleUse ? 1 : 0, i => { st.ToggleUse = i == 1; GameInput.ReleaseUse(); }, ColW);
            Ui.Place(message.rectTransform, new Vector2(0, 0), new Vector2(60, large ? buttonsTop + 20 : 110), new Vector2(W - 120, noteH), new Vector2(0, 0));
            Note(null);
            var reset = Widgets.Button(panel.rectTransform, "Reset to defaults", ResetAll, 300, 60, 26);
            Ui.Place(reset, new Vector2(0, 0), new Vector2(60, 40), new Vector2(300, 60), new Vector2(0, 0));
            var done = Widgets.Button(panel.rectTransform, "Done", Close, 220, 60, 28, true);
            Ui.Place(done, new Vector2(1, 0), new Vector2(-60, 40), new Vector2(220, 60), new Vector2(1, 0));
            reset.localScale = done.localScale = Vector3.one * k;
            Refresh();
            var focus = MenuFocus.AttachAll(panel.gameObject);
            if (list != null)
            {
                list.Focus = focus;
                focus.Chain = left.GetComponentsInChildren<Selectable>().ToList();
                focus.Before.AddRange(tabSel);
                focus.After.Add(done.GetComponent<Selectable>());
                focus.After.Add(reset.GetComponent<Selectable>());
            }
        }

        void Row(Transform parent, Act act, string label, float width)
        {
            var row = Ui.Panel(parent, "Bind_" + act, new Color(1, 1, 1, 0f), 10);
            row.raycastTarget = true;
            var rt = row.rectTransform;
            rt.sizeDelta = new Vector2(width, 56);
            var l = Ui.Label(rt, label, UiFont.Sans, 26, Ui.Text, TextAlignmentOptions.MidlineLeft);
            Ui.Place(l.rectTransform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(width - 200, 50), new Vector2(0, 0.5f));
            var cap = Ui.Rect(rt, "Cap");
            Ui.Place(cap, new Vector2(1, 0.5f), Vector2.zero, new Vector2(190, 44), new Vector2(1, 0.5f));
            var h = cap.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleRight;
            h.childControlWidth = h.childControlHeight = false;
            h.childForceExpandWidth = false;
            caps[act] = cap;
            row.gameObject.AddComponent<SelectGlow>().Target = row;
            var b = row.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => Listen(act));
        }

        /// <summary>Wait for the next key or mouse button to bind to <paramref name="act"/>.</summary>
        public void Listen(Act act)
        {
            if (GameTime.Unscaled < quietUntil || listening.HasValue) return;
            Sfx.Play("ui_click", null, 0.45f, 1.05f, 0f, AudioBus.Ui);
            listening = act;
            Refresh();
            Note(page == PadPage
                ? $"Press the controller button for <b>{LabelOf(act)}</b>.  Start or Esc cancels."
                : $"Press the key or mouse button for <b>{LabelOf(act)}</b>.  Esc cancels.");
            listener = InputSystem.onAnyButtonPress.CallOnce(OnButton);
        }

        void OnButton(InputControl c)
        {
            listener = null;
            if (!listening.HasValue) return;
            var act = listening.Value;
            if (page == PadPage) { OnPadButton(act, c); return; }
            // A pad button, or Esc / pad B: cancel (pad buttons are bound on the Controller page).
            if (c.device is not Keyboard && c.device is not Mouse)
            {
                Stop(c.device is Gamepad g && c == g.buttonEast ? null : "That's a controller button: bind it on the <b>Controller</b> page.");
                return;
            }
            string path = c.device is Keyboard ? "<Keyboard>/" + c.name : "<Mouse>/" + c.name;
            if (path == "<Keyboard>/escape") { Stop(null); return; }
            var before = Controls.ActionOn(Settings.Current, path);
            if (!Controls.Set(Settings.Current, act, path))
            {
                Stop($"<b>{Controls.Display(path)}</b> is fixed (Esc pauses, Enter confirms, 1–4 pick tools).");
                return;
            }
            Sfx.Play("snap_home", null, 0.4f, 1.1f, 0f, AudioBus.Ui);
            Stop(before.HasValue && before.Value != act
                ? $"<b>{LabelOf(act)}</b> is now <b>{Controls.Display(act)}</b>.  <b>{LabelOf(before.Value)}</b> moved to <b>{Controls.Display(before.Value)}</b>."
                : $"<b>{LabelOf(act)}</b> is now <b>{Controls.Display(act)}</b>.");
        }

        void OnPadButton(Act act, InputControl c)
        {
            if (c.device is Keyboard kb && c == kb.escapeKey) { Stop(null); return; }
            if (c.device is not Gamepad pad) { Stop("That's not a controller button. Press one on the controller, or Start to cancel."); return; }
            if (c == pad.startButton) { Stop(null); return; }
            string path = "<Gamepad>/" + c.path.Substring(c.device.path.Length + 1);
            if (!Array.Exists(Controls.PadButtons, p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)))
            {
                // Stick directions aren't buttons here; keep waiting. The d-pad's left and right pick tools.
                if (path.Contains("Stick/")) { listener = InputSystem.onAnyButtonPress.CallOnce(OnButton); return; }
                Stop($"<b>{GameInput.PadGlyph(Controls.PadName(path))}</b> is fixed (Start pauses, the d-pad's left and right pick tools).");
                return;
            }
            var before = Controls.PadActionOn(Settings.Current, path);
            Controls.SetPad(Settings.Current, act, path);
            Sfx.Play("snap_home", null, 0.4f, 1.1f, 0f, AudioBus.Ui);
            string Name(Act a) => GameInput.PadGlyph(Controls.PadName(Controls.PadPathOf(Settings.Current, a)));
            Stop(before.HasValue && before.Value != act
                ? $"<b>{LabelOf(act)}</b> is now <b>{Name(act)}</b>.  <b>{LabelOf(before.Value)}</b> moved to <b>{Name(before.Value)}</b>."
                : $"<b>{LabelOf(act)}</b> is now <b>{Name(act)}</b>.");
        }

        void Stop(string note)
        {
            listening = null;
            // The key that was just bound also reaches the menu this frame; don't let it re-open a row or close the panel.
            quietUntil = GameTime.Unscaled + 0.3f;
            Refresh();
            Note(note);
        }

        void ResetAll()
        {
            if (page == PadPage) Controls.ResetPad(Settings.Current);
            else Controls.ResetAll(Settings.Current);
            Refresh();
            Note("Back to the defaults.");
        }

        void Note(string text) => message.text = text ?? DefaultNote(page);

        static string DefaultNote(int page) => (page == PadPage
            ? "Start pauses and the d-pad's left and right pick tools; in menus and documents A, B, X and Y stay as they are. A button that's already in use swaps over. Toggle: press once to start crouching, walking briskly, cleaning or spraying, and again to stop."
            : "Esc pauses, Enter confirms and 1–4 pick tools; those stay fixed. A key that's already in use swaps over. Toggle: press once to start crouching, walking briskly, cleaning or spraying, and again to stop.");

        string LabelOf(Act a) => Array.Find(page == PadPage ? Controls.PadLabels : Controls.Labels, x => x.act == a).label;

        void Refresh()
        {
            foreach (var (act, cap) in caps)
            {
                foreach (Transform c in cap) Destroy(c.gameObject);
                if (listening == act)
                {
                    var t = Ui.Label(cap, page == PadPage ? "Press a button…" : "Press a key…", UiFont.SansMedium, 24, Ui.Accent, TextAlignmentOptions.MidlineRight);
                    t.rectTransform.sizeDelta = new Vector2(190, 44);
                }
                else if (page == PadPage) Ui.KeyCap(cap, GameInput.PadGlyph(Controls.PadName(Controls.PadPathOf(Settings.Current, act))), 38, pad: true);
                else Ui.KeyCap(cap, Controls.Display(act), 38, true);
            }
        }

        /// <summary>Frame the panel closed on, so the same Esc doesn't also close Settings underneath.</summary>
        public static int ClosedFrame = -1;

        void Close()
        {
            ClosedFrame = Time.frameCount;
            listener?.Dispose();
            listener = null;
            Settings.Save();
            GameRoot.Instance.SetBlocked("controls", false);
            instance = null;
            MenuFade.Out(root);
        }

        void Update()
        {
            if (listening.HasValue || GameTime.Unscaled < quietUntil) return;
            if (GameInput.Menu.Back) Close();
        }
    }
}
