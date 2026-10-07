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

        /// <summary>
        /// A list that scrolls, for menus at the larger text sizes: a masked viewport at
        /// <paramref name="pos"/> (top-left, in the parent) holding a column scaled by
        /// <paramref name="k"/>, with a thin bar beside it. Rows go in the returned column; the
        /// selected row is kept in view and the mouse wheel scrolls (see <see cref="ScrollFollow"/>).
        /// </summary>
        public static RectTransform ScrollList(Transform parent, string name, Vector2 pos, Vector2 size, float k, float spacing, out ScrollFollow follow)
        {
            var view = Ui.Rect(parent, name);
            Ui.Place(view, new Vector2(0, 1), pos, size, new Vector2(0, 1));
            view.gameObject.AddComponent<RectMask2D>();
            var content = Column(view, "Content", spacing);
            // Inset a little, so a selected button's hover lift isn't clipped at the sides.
            Ui.Place(content, new Vector2(0, 1), new Vector2(ScrollFollow.Inset, 0), new Vector2((size.x - 2 * ScrollFollow.Inset) / k, 10), new Vector2(0, 1));
            content.localScale = Vector3.one * k;
            var track = Ui.Panel(parent, name + "Bar", new Color(1, 1, 1, 0.06f), 3);
            Ui.Place(track.rectTransform, new Vector2(0, 1), pos + new Vector2(size.x + 14, 0), new Vector2(6, size.y), new Vector2(0, 1));
            var thumb = Ui.Panel(track.rectTransform, "Thumb", new Color(1f, 0.82f, 0.5f, 0.55f), 3);
            Ui.Place(thumb.rectTransform, new Vector2(0, 1), Vector2.zero, new Vector2(6, size.y), new Vector2(0, 1));
            follow = view.gameObject.AddComponent<ScrollFollow>();
            follow.Init(view, content, track.rectTransform, thumb.rectTransform, spacing);
            return content;
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
        Vector3 scaleHome = Vector3.one;
        float k;

        public void Init(Image b, TextMeshProUGUI l, bool p) { bg = b; label = l; primary = p; baseCol = b.color; labelHome = l.rectTransform.anchoredPosition; }

        public void OnPointerEnter(PointerEventData e)
        {
            over = true;
            Sfx.Play("ui_hover", null, 0.25f, 1f, 0.03f, AudioBus.Ui);
        }

        public void OnPointerExit(PointerEventData e) => over = false;

        // A button scaled as it was laid out (larger text sizes) keeps that scale under the hover lift.
        void Start() => scaleHome = transform.localScale;

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
            transform.localScale = scaleHome * (1f + e * 0.02f);
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

    /// <summary>
    /// Keeps the selected row of a <see cref="Widgets.ScrollList"/> in view when the selection
    /// moves (pad, arrow keys), scrolls with the mouse wheel while its menu is the one in front,
    /// and sizes the bar's thumb to the part showing.
    /// </summary>
    public class ScrollFollow : MonoBehaviour
    {
        RectTransform view, content, track, thumb;
        float spacing, target, offset;
        /// <summary>How far the rows sit in from the list's sides, in viewport units.</summary>
        public const float Inset = 14f;
        GameObject lastSelected;
        bool settled;

        public void Init(RectTransform v, RectTransform c, RectTransform tr, RectTransform th, float sp)
        {
            view = v; content = c; track = tr; thumb = th; spacing = sp;
        }

        /// <summary>The menu this list belongs to; the wheel only scrolls it while that menu is in front.</summary>
        public MenuFocus Focus;
        /// <summary>The column's height in viewport units (its rows at their scale).</summary>
        public float ContentHeight
        {
            get
            {
                float h = 0; int n = 0;
                foreach (RectTransform c in content) { if (!c.gameObject.activeSelf) continue; h += c.sizeDelta.y; n++; }
                return (h + spacing * Mathf.Max(0, n - 1)) * content.localScale.y;
            }
        }
        public float ViewHeight => view.rect.height;
        public float MaxScroll => Mathf.Max(0f, ContentHeight - ViewHeight);
        /// <summary>How far down the list is scrolled, in viewport units (0 is the top).</summary>
        public float Scroll => offset;
        /// <summary>True when the list has stopped moving.</summary>
        public bool Settled => Mathf.Abs(offset - target) < 0.5f;

        /// <summary>Scroll by a number of viewport units (positive goes down the list).</summary>
        public void ScrollBy(float units) => target = Mathf.Clamp(target + units, 0f, MaxScroll);

        /// <summary>Bring a row fully into view, with a little room around it.</summary>
        public void Reveal(RectTransform row)
        {
            var c = new Vector3[4];
            row.GetWorldCorners(c);
            float top = view.InverseTransformPoint(c[1]).y, bottom = view.InverseTransformPoint(c[0]).y;
            // Where the row would be with the list unscrolled (the viewport's top edge is y = 0).
            float top0 = top - offset, bottom0 = bottom - offset;
            const float M = 16f;
            float lo = -ViewHeight + M - bottom0, hi = -M - top0;
            target = Mathf.Clamp(Mathf.Clamp(target, lo, Mathf.Max(lo, hi)), 0f, MaxScroll);
        }

        void LateUpdate()
        {
            var es = EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            if (sel != lastSelected || !settled)
            {
                // A new selection (or the first frame, once the layout exists) is brought into view.
                if (!settled) LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                if (sel != null && sel.transform.IsChildOf(content)) Reveal((RectTransform)sel.transform);
                lastSelected = sel;
                settled = true;
            }
            if (Focus == null || MenuFocus.Top == Focus)
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;
                float wheel = mouse != null ? mouse.scroll.ReadValue().y : 0f;
                if (wheel > 0.1f) ScrollBy(-120f);
                else if (wheel < -0.1f) ScrollBy(120f);
            }
            target = Mathf.Clamp(target, 0f, MaxScroll);
            offset = Mathf.Abs(target - offset) < 0.5f ? target : Mathf.Lerp(offset, target, 1f - Mathf.Exp(-GameTime.UnscaledDelta * 18f));
            content.anchoredPosition = new Vector2(Inset, offset);
            float max = MaxScroll, vh = ViewHeight;
            track.gameObject.SetActive(max > 0.5f);
            if (max > 0.5f)
            {
                float th = Mathf.Max(40f, vh * vh / (vh + max));
                thumb.sizeDelta = new Vector2(6, th);
                thumb.anchoredPosition = new Vector2(0, -(vh - th) * offset / max);
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
