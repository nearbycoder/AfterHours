using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AfterHours
{
    /// <summary>A small modal list of choices (W/S, d-pad or mouse to pick, E/Enter/A/click to choose, Esc/B to cancel).</summary>
    public class ChoiceMenu : MonoBehaviour
    {
        public class Option
        {
            public string Label, Sub;
            public Action Pick;
            public Option(string label, string sub, Action pick) { Label = label; Sub = sub; Pick = pick; }
        }

        static ChoiceMenu instance;
        RectTransform root, panel, list;
        CanvasGroup group;
        TextMeshProUGUI title, subtitle;
        readonly List<(Image bg, RectTransform rt)> rows = new();
        List<Option> options = new();
        int selected;
        float openedAt;
        public static bool IsOpen => instance != null && instance.open;
        bool open;

        /// <summary>Automation: choose this index on the next frame.</summary>
        public static int AutoPick = -1;

        static ChoiceMenu Instance
        {
            get
            {
                if (instance) return instance;
                var rt = Ui.Layer("Choice", 45);
                instance = rt.gameObject.AddComponent<ChoiceMenu>();
                instance.root = rt;
                instance.Build();
                return instance;
            }
        }

        void Build()
        {
            group = root.GetComponent<CanvasGroup>();
            group.alpha = 0;
            group.blocksRaycasts = false;
            var dim = Ui.Image(root, "Dim", new Color(0, 0, 0, 0.45f));
            Ui.Stretch(dim.rectTransform);
            var p = Ui.Panel(root, "Panel", new Color(0.07f, 0.09f, 0.13f, 0.96f), 18);
            panel = p.rectTransform;
            Ui.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 400));
            title = Ui.Label(panel, "", UiFont.SansBold, 32, Ui.Text, TextAlignmentOptions.TopLeft, "Title");
            Ui.Place(title.rectTransform, new Vector2(0, 1), new Vector2(36, -28), new Vector2(690, 40), new Vector2(0, 1));
            subtitle = Ui.Label(panel, "", UiFont.Sans, 20, Ui.TextDim, TextAlignmentOptions.TopLeft, "Sub");
            Ui.Place(subtitle.rectTransform, new Vector2(0, 1), new Vector2(36, -72), new Vector2(690, 28), new Vector2(0, 1));
            list = Ui.Rect(panel, "List");
            Ui.Place(list, new Vector2(0, 1), new Vector2(26, -116), new Vector2(708, 300), new Vector2(0, 1));
        }

        public static void Show(string title, string sub, List<Option> opts)
        {
            if (opts == null || opts.Count == 0) return;
            Instance.Open(title, sub, opts);
        }

        void Open(string t, string s, List<Option> opts)
        {
            options = new List<Option>(opts) { new("Never mind", null, null) };
            title.text = t;
            subtitle.text = s ?? "";
            foreach (Transform c in list) Destroy(c.gameObject);
            rows.Clear();
            float y = 0;
            for (int i = 0; i < options.Count; i++)
            {
                var o = options[i];
                float h = string.IsNullOrEmpty(o.Sub) ? 54 : 74;
                var bg = Ui.Panel(list, "Row", new Color(1, 1, 1, 0.04f), 12);
                var rt = bg.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(0, -y);
                rt.sizeDelta = new Vector2(708, h);
                bg.raycastTarget = true;
                int idx = i;
                var btn = bg.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.navigation = new Navigation { mode = Navigation.Mode.None }; // keys and pad go through Update
                btn.onClick.AddListener(() => Choose(idx));
                var trig = bg.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                var entry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
                entry.callback.AddListener(_ => { if (selected != idx) { selected = idx; Sfx.Play("ui_hover", null, 0.3f, 1f, 0f, AudioBus.Ui); } });
                trig.triggers.Add(entry);
                var num = Ui.Label(rt, (i + 1).ToString(), UiFont.SansBold, 22, Ui.Accent, TextAlignmentOptions.Center);
                Ui.Place(num.rectTransform, new Vector2(0, 1), new Vector2(14, -14), new Vector2(26, 26), new Vector2(0, 1));
                var l = Ui.Label(rt, o.Label, UiFont.SansMedium, 25, i == options.Count - 1 ? Ui.TextDim : Ui.Text, TextAlignmentOptions.TopLeft);
                Ui.Place(l.rectTransform, new Vector2(0, 1), new Vector2(54, -12), new Vector2(640, 32), new Vector2(0, 1));
                if (!string.IsNullOrEmpty(o.Sub))
                {
                    var sl = Ui.Label(rt, o.Sub, UiFont.Sans, 18, Ui.TextDim, TextAlignmentOptions.TopLeft);
                    Ui.Place(sl.rectTransform, new Vector2(0, 1), new Vector2(54, -44), new Vector2(640, 24), new Vector2(0, 1));
                }
                rows.Add((bg, rt));
                y += h + 8;
            }
            panel.sizeDelta = new Vector2(760, 140 + y);
            selected = 0;
            open = true;
            openedAt = Time.unscaledTime;
            group.blocksRaycasts = true;
            GameRoot.Instance?.SetBlocked("choice", true, true);
            Sfx.Play("ui_click", null, 0.4f, 1f, 0f, AudioBus.Ui);
            panel.localScale = Vector3.one * 0.9f;
            Tween.Run(0.25f, k => { group.alpha = k; panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.9f, 1f, k); }, Ease.OutBack, owner: this);
        }

        void Update()
        {
            if (!open) return;
            for (int i = 0; i < rows.Count; i++)
                rows[i].bg.color = Color.Lerp(rows[i].bg.color, i == selected ? new Color(1f, 0.78f, 0.34f, 0.22f) : new Color(1, 1, 1, 0.04f), 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f));
            if (AutoPick >= 0) { int p = AutoPick; AutoPick = -1; Choose(Mathf.Min(p, options.Count - 1)); return; }
            if (Time.unscaledTime - openedAt < 0.2f) return;
            var m = GameInput.Menu;
            if (m.Up) Move(-1);
            if (m.Down) Move(1);
            var kb = Keyboard.current;
            if (kb != null)
                for (int i = 0; i < Mathf.Min(9, options.Count); i++)
                    if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) { Choose(i); return; }
            if (m.Confirm) Choose(selected);
            else if (m.Back || m.Keep) Choose(options.Count - 1);
        }

        void Move(int d)
        {
            selected = (selected + d + options.Count) % options.Count;
            Sfx.Play("ui_hover", null, 0.3f, 1f, 0f, AudioBus.Ui);
        }

        void Choose(int i)
        {
            if (!open) return;
            open = false;
            group.blocksRaycasts = false;
            var pick = options[i].Pick;
            Tween.Run(0.18f, k => group.alpha = 1 - k, Ease.InCubic, owner: this);
            GameRoot.Instance?.SetBlocked("choice", false);
            Sfx.Play(pick == null ? "ui_back" : "ui_click", null, 0.45f, 1f, 0f, AudioBus.Ui);
            pick?.Invoke();
        }
    }
}
