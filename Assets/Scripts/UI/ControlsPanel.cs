using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

namespace AfterHours
{
    /// <summary>
    /// Keyboard and mouse bindings (from Settings). Pick a row, press the new key or mouse button;
    /// a key that's already in use swaps with the other action, Esc cancels.
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

        public static void Show()
        {
            if (instance) return;
            var rt = Ui.Layer("Controls", 59);
            instance = rt.gameObject.AddComponent<ControlsPanel>();
            instance.root = rt;
            instance.Build();
        }

        void Build()
        {
            GameRoot.Instance.SetBlocked("controls", true, true);
            var dim = Ui.Image(root, "Dim", new Color(0.01f, 0.015f, 0.03f, 0.93f));
            Ui.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            const float W = 1360, H = 820, ColW = 600;
            var panel = Ui.Panel(root, "Panel", new Color(0.07f, 0.09f, 0.13f, 1f), 22);
            Ui.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, H));
            var title = Ui.Label(panel.rectTransform, "Keyboard and mouse", UiFont.SansBold, 44, Ui.Text, TextAlignmentOptions.TopLeft);
            Ui.Place(title.rectTransform, new Vector2(0, 1), new Vector2(60, -36), new Vector2(900, 60), new Vector2(0, 1));
            var left = Widgets.Column(panel.rectTransform, "Left", 6);
            Ui.Place(left, new Vector2(0, 1), new Vector2(60, -116), new Vector2(ColW, 520), new Vector2(0, 1));
            var right = Widgets.Column(panel.rectTransform, "Right", 6);
            Ui.Place(right, new Vector2(0, 1), new Vector2(60 + ColW + 80, -116), new Vector2(ColW, 520), new Vector2(0, 1));
            for (int i = 0; i < Controls.Labels.Length; i++)
            {
                var (act, label) = Controls.Labels[i];
                Row(i < 7 ? left : right, act, label, ColW);
            }
            message = Ui.Label(panel.rectTransform, "", UiFont.Sans, 22, Ui.TextDim, TextAlignmentOptions.TopLeft);
            Ui.Place(message.rectTransform, new Vector2(0, 0), new Vector2(60, 110), new Vector2(1240, 60), new Vector2(0, 0));
            Note(null);
            var reset = Widgets.Button(panel.rectTransform, "Reset to defaults", ResetAll, 300, 60, 26);
            Ui.Place(reset, new Vector2(0, 0), new Vector2(60, 40), new Vector2(300, 60), new Vector2(0, 0));
            var done = Widgets.Button(panel.rectTransform, "Done", Close, 220, 60, 28, true);
            Ui.Place(done, new Vector2(1, 0), new Vector2(-60, 40), new Vector2(220, 60), new Vector2(1, 0));
            Refresh();
            MenuFocus.AttachAll(panel.gameObject);
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
            Note($"Press the key or mouse button for <b>{LabelOf(act)}</b>.  Esc cancels.");
            listener = InputSystem.onAnyButtonPress.CallOnce(OnButton);
        }

        void OnButton(InputControl c)
        {
            listener = null;
            if (!listening.HasValue) return;
            var act = listening.Value;
            // A pad button, or Esc / pad B: cancel (pad buttons aren't rebindable).
            if (c.device is not Keyboard && c.device is not Mouse)
            {
                Stop(c.device is Gamepad g && c == g.buttonEast ? null : "Pad buttons are fixed. Use a key or a mouse button.");
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
            Controls.ResetAll(Settings.Current);
            Refresh();
            Note("Back to the defaults.");
        }

        void Note(string text) => message.text = text ?? "Esc pauses, Enter confirms and 1–4 pick tools; those stay fixed, and so do pad buttons. A key that's already in use swaps over.";

        static string LabelOf(Act a) => Array.Find(Controls.Labels, x => x.act == a).label;

        void Refresh()
        {
            foreach (var (act, cap) in caps)
            {
                foreach (Transform c in cap) Destroy(c.gameObject);
                if (listening == act)
                {
                    var t = Ui.Label(cap, "Press a key…", UiFont.SansMedium, 24, Ui.Accent, TextAlignmentOptions.MidlineRight);
                    t.rectTransform.sizeDelta = new Vector2(190, 44);
                }
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
            Destroy(root.gameObject);
        }

        void Update()
        {
            if (listening.HasValue || GameTime.Unscaled < quietUntil) return;
            if (GameInput.Menu.Back) Close();
        }
    }
}
