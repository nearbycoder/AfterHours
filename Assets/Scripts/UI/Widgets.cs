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

        public static RectTransform Slider(Transform parent, string label, float value, Action<float> onChange, Func<float, string> format = null, float width = 760)
        {
            var row = Ui.Rect(parent, "Slider_" + label);
            row.sizeDelta = new Vector2(width, 56);
            var l = Ui.Label(row, label, UiFont.Sans, 26, Ui.Text, TextAlignmentOptions.MidlineLeft);
            Ui.Place(l.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(300, 50), new Vector2(0, 0.5f));
            var track = Ui.Panel(row, "Track", new Color(1, 1, 1, 0.12f), 6);
            Ui.Place(track.rectTransform, new Vector2(0, 0.5f), new Vector2(320, 0), new Vector2(width - 440, 12), new Vector2(0, 0.5f));
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
            var drag = track.gameObject.AddComponent<SliderDrag>();
            drag.Init(track.rectTransform, v =>
            {
                fill.rectTransform.anchorMax = new Vector2(v, 1);
                knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(v, 0.5f);
                val.text = format(v);
                onChange(v);
            }, value);
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
    public class HoverFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        Image bg;
        TextMeshProUGUI label;
        bool primary, over;
        Color baseCol;
        float k;

        public void Init(Image b, TextMeshProUGUI l, bool p) { bg = b; label = l; primary = p; baseCol = b.color; }

        public void OnPointerEnter(PointerEventData e)
        {
            over = true;
            Sfx.Play("ui_hover", null, 0.25f, 1f, 0.03f, AudioBus.Ui);
        }

        public void OnPointerExit(PointerEventData e) => over = false;

        void Update()
        {
            k = Mathf.MoveTowards(k, over ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            float e = Ease.OutCubic(k);
            bg.color = primary ? Color.Lerp(baseCol, new Color(1f, 0.86f, 0.5f, 1f), e) : Color.Lerp(baseCol, new Color(1f, 0.78f, 0.34f, 0.18f), e);
            label.rectTransform.anchoredPosition = new Vector2(e * 10f, 0);
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
            if (Time.unscaledTime - lastSound > 0.06f)
            {
                lastSound = Time.unscaledTime;
                Sfx.Play("ui_hover", null, 0.18f, 0.8f + v * 0.6f, 0f, AudioBus.Ui);
            }
        }
    }
}
