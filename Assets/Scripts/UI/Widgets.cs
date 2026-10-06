using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AfterHours
{
    /// <summary>Menu widgets: buttons, sliders and toggles with hover/press feedback.</summary>
    public static class Widgets
    {
        public static RectTransform Button(Transform parent, string label, Action onClick, float width = 420, float height = 64, int fontSize = 30, bool primary = false)
        {
            var bg = Ui.Panel(parent, "Btn_" + label, primary ? new Color(1f, 0.78f, 0.34f, 0.95f) : new Color(1, 1, 1, 0.06f), 14);
            bg.raycastTarget = true;
            var rt = bg.rectTransform;
            rt.sizeDelta = new Vector2(width, height);
            var t = Ui.Label(rt, label, UiFont.SansMedium, fontSize, primary ? Ui.Ink : Ui.Text, TextAlignmentOptions.Left);
            Ui.Stretch(t.rectTransform);
            t.margin = new Vector4(26, 0, 20, 0);
            t.alignment = TextAlignmentOptions.MidlineLeft;
            var b = bg.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() =>
            {
                Sfx.Play("ui_click", null, 0.5f, 1f, 0.02f, AudioBus.Ui);
                onClick?.Invoke();
            });
            var hover = bg.gameObject.AddComponent<HoverFx>();
            hover.Init(bg, t, primary);
            return rt;
        }

        public static RectTransform Slider(Transform parent, string label, float value, Action<float> onChange, Func<float, string> format = null, float width = 760, float labelWidth = 300)
        {
            var row = Ui.Rect(parent, "Slider_" + label);
            row.sizeDelta = new Vector2(width, 56);
            var glow = Ui.Panel(row, "Glow", new Color(1, 1, 1, 0f), 10);
            Ui.Stretch(glow.rectTransform, -8);
            glow.raycastTarget = false;
            var l = Ui.Label(row, label, UiFont.Sans, 26, Ui.Text, TextAlignmentOptions.MidlineLeft);
            Ui.Place(l.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(labelWidth, 50), new Vector2(0, 0.5f));
            var track = Ui.Panel(row, "Track", new Color(1, 1, 1, 0.12f), 6);
            Ui.Place(track.rectTransform, new Vector2(0, 0.5f), new Vector2(labelWidth + 20, 0), new Vector2(width - labelWidth - 140, 12), new Vector2(0, 0.5f));
            track.raycastTarget = true;
            var fill = Ui.Panel(track.rectTransform, "Fill", Ui.Accent, 6);
            fill.rectTransform.anchorMin = new Vector2(0, 0);
            fill.rectTransform.anchorMax = new Vector2(value, 1);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            var knob = Ui.Image(track.rectTransform, "Knob", Color.white, Ui.Circle(64));
            knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(value, 0.5f);
            knob.rectTransform.sizeDelta = new Vector2(30, 30);
            var val = Ui.Label(row, "", UiFont.Mono, 24, Ui.TextDim, TextAlignmentOptions.MidlineRight);
            Ui.Place(val.rectTransform, new Vector2(1, 0.5f), Vector2.zero, new Vector2(100, 50), new Vector2(1, 0.5f));
            format ??= v => Mathf.RoundToInt(v * 100) + "%";
            val.text = format(value);
            float current = value;
            void Set(float v)
            {
                current = v;
                fill.rectTransform.anchorMax = new Vector2(v, 1);
                knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(v, 0.5f);
                val.text = format(v);
                onChange(v);
            }
            var drag = track.gameObject.AddComponent<SliderDrag>();
            drag.Init(track.rectTransform, Set, value);
            // Selectable so the pad can reach it; left/right nudge the value.
            var sel = row.gameObject.AddComponent<Selectable>();
            sel.transition = Selectable.Transition.None;
            var nav = row.gameObject.AddComponent<SliderNav>();
            nav.Get = () => current;
            nav.Set = Set;
            row.gameObject.AddComponent<SelectGlow>().Target = glow;
            return row;
        }

        public static RectTransform Toggle(Transform parent, string label, bool value, Action<bool> onChange, float width = 760)
        {
            var row = Ui.Panel(parent, "Toggle_" + label, new Color(1, 1, 1, 0.0f), 10);
            row.raycastTarget = true;
            var rt = row.rectTransform;
            rt.sizeDelta = new Vector2(width, 56);
            var l = Ui.Label(rt, label, UiFont.Sans, 26, Ui.Text, TextAlignmentOptions.MidlineLeft);
            Ui.Place(l.rectTransform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(width - 140, 50), new Vector2(0, 0.5f));
            var pill = Ui.Panel(rt, "Pill", value ? Ui.Accent : new Color(1, 1, 1, 0.15f), 16);
            Ui.Place(pill.rectTransform, new Vector2(1, 0.5f), Vector2.zero, new Vector2(72, 34), new Vector2(1, 0.5f));
            var dot = Ui.Image(pill.rectTransform, "Dot", Color.white, Ui.Circle(64));
            Ui.Place(dot.rectTransform, new Vector2(0, 0.5f), new Vector2(value ? 54 : 18, 0), new Vector2(26, 26), new Vector2(0.5f, 0.5f));
            bool state = value;
            row.gameObject.AddComponent<SelectGlow>().Target = row;
            var b = row.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() =>
            {
                state = !state;
                Sfx.Play("ui_click", null, 0.45f, state ? 1.1f : 0.9f, 0f, AudioBus.Ui);
                var from = dot.rectTransform.anchoredPosition.x;
                Tween.Run(0.18f, k =>
                {
                    dot.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(from, state ? 54 : 18, k), 0);
                    pill.color = Color.Lerp(pill.color, state ? Ui.Accent : new Color(1, 1, 1, 0.15f), k);
                }, Ease.OutCubic);
                onChange(state);
            });
            return rt;
        }

        /// <summary>
        /// A row that steps through named options: ◀ value ▶. Click (or A) steps forward; the
        /// arrows, d-pad or arrow keys step either way.
        /// </summary>
        public static RectTransform Choice(Transform parent, string label, string[] options, int index, Action<int> onChange, float width = 760)
        {
            var row = Ui.Panel(parent, "Choice_" + label, new Color(1, 1, 1, 0f), 10);
            row.raycastTarget = true;
            var rt = row.rectTransform;
            rt.sizeDelta = new Vector2(width, 56);
            var l = Ui.Label(rt, label, UiFont.Sans, 26, Ui.Text, TextAlignmentOptions.MidlineLeft);
            Ui.Place(l.rectTransform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(width - 260, 50), new Vector2(0, 0.5f));
            var val = Ui.Label(rt, "", UiFont.SansMedium, 25, Ui.Accent, TextAlignmentOptions.Center);
            Ui.Place(val.rectTransform, new Vector2(1, 0.5f), new Vector2(-40, 0), new Vector2(170, 50), new Vector2(1, 0.5f));
            int current = Mathf.Clamp(index, 0, options.Length - 1);
            val.text = options[current];
            void Step(int d)
            {
                current = (current + d + options.Length) % options.Length;
                val.text = options[current];
                Sfx.Play("ui_click", null, 0.4f, d > 0 ? 1.08f : 0.94f, 0f, AudioBus.Ui);
                var from = d > 0 ? 14f : -14f;
                Tween.Run(0.16f, k => { if (val) val.rectTransform.anchoredPosition = new Vector2(-40 + from * (1 - k), 0); }, Ease.OutCubic);
                onChange(current);
            }
            foreach (var (glyph, x, d) in new[] { ("◀", -232f, -1), ("▶", -6f, 1) })
            {
                var a = Ui.Label(rt, glyph, UiFont.Sans, 22, Ui.TextDim, TextAlignmentOptions.Center);
                Ui.Place(a.rectTransform, new Vector2(1, 0.5f), new Vector2(x, 0), new Vector2(34, 50), new Vector2(1, 0.5f));
                a.raycastTarget = true;
                a.gameObject.AddComponent<ClickArea>().OnClick = () => Step(d);
            }
            row.gameObject.AddComponent<SelectGlow>().Target = row;
            var b = row.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => Step(1));
            var nav = row.gameObject.AddComponent<ChoiceNav>();
            nav.Step = Step;
            return rt;
        }

        /// <summary>A small caps heading above a group of rows.</summary>
        public static RectTransform Heading(Transform parent, string text, float width = 760)
        {
            var t = Ui.Label(parent, text.ToUpperInvariant(), UiFont.SansMedium, 18, new Color(1f, 0.82f, 0.5f, 0.75f), TextAlignmentOptions.BottomLeft);
            t.rectTransform.sizeDelta = new Vector2(width, 40);
            t.characterSpacing = 6;
            return t.rectTransform;
        }

        public static RectTransform Column(Transform parent, string name, float spacing = 12)
        {
            var rt = Ui.Rect(parent, name);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = false; v.childControlHeight = false;
            v.childForceExpandWidth = false; v.childForceExpandHeight = false;
            return rt;
        }
    }

    /// <summary>Hover lift + glow on menu buttons.</summary>
    public class HoverFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        Image bg;
        TextMeshProUGUI label;
        bool primary, over, selected;
        Color baseCol;
        Vector2 labelHome;
        float k;

        public void Init(Image b, TextMeshProUGUI l, bool p) { bg = b; label = l; primary = p; baseCol = b.color; labelHome = l.rectTransform.anchoredPosition; }

        public void OnPointerEnter(PointerEventData e)
        {
            over = true;
            Sfx.Play("ui_hover", null, 0.25f, 1f, 0.03f, AudioBus.Ui);
        }

        public void OnPointerExit(PointerEventData e) => over = false;

        public void OnSelect(BaseEventData e)
        {
            selected = true;
            if (GameInput.UsingPad) Sfx.Play("ui_hover", null, 0.25f, 1f, 0.03f, AudioBus.Ui);
        }

        public void OnDeselect(BaseEventData e) => selected = false;

        void Update()
        {
            // Mouse hover, or pad selection (a mouse click also selects, which shouldn't stick).
            bool on = over || (selected && GameInput.UsingPad);
            k = Mathf.MoveTowards(k, on ? 1f : 0f, GameTime.UnscaledDelta * 8f);
            float e = Ease.OutCubic(k);
            bg.color = primary ? Color.Lerp(baseCol, new Color(1f, 0.86f, 0.5f, 1f), e) : Color.Lerp(baseCol, new Color(1f, 0.78f, 0.34f, 0.18f), e);
            label.rectTransform.anchoredPosition = labelHome + new Vector2(e * 10f, 0);
            transform.localScale = Vector3.one * (1f + e * 0.02f);
        }
    }

    public class SliderDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        RectTransform track;
        Action<float> set;
        float lastSound;

        public void Init(RectTransform t, Action<float> s, float v) { track = t; set = s; }

        public void OnPointerDown(PointerEventData e) => Apply(e);
        public void OnDrag(PointerEventData e) => Apply(e);

        void Apply(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(track, e.position, e.pressEventCamera, out var lp);
            float v = Mathf.Clamp01((lp.x - track.rect.xMin) / track.rect.width);
            set(v);
            if (GameTime.Unscaled - lastSound > 0.06f)
            {
                lastSound = GameTime.Unscaled;
                Sfx.Play("ui_hover", null, 0.18f, 0.8f + v * 0.6f, 0f, AudioBus.Ui);
            }
        }
    }

    /// <summary>A plain click target that isn't a Selectable (so menu navigation skips it).</summary>
    public class ClickArea : MonoBehaviour, IPointerClickHandler
    {
        public Action OnClick;
        public void OnPointerClick(PointerEventData e) => OnClick?.Invoke();
    }
}
