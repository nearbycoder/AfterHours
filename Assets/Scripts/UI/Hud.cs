using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AfterHours
{
    /// <summary>
    /// In-play HUD: reticle that becomes a progress ring over dirty surfaces, a context prompt with
    /// keycaps, toasts for completions, and the wristwatch clock.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public static Hud Instance { get; private set; }

        RectTransform root, reticleRoot, promptRoot, toastRoot;
        Image dot, ring, ringBack;
        TextMeshProUGUI targetLabel, clock, clockSub;
        CanvasGroup group, promptGroup;
        readonly List<RectTransform> toasts = new();
        string promptKey;
        float ringShow, ringValue, dotPulse;
        CleaningController cleaning;

        public static Hud Create(CleaningController cc)
        {
            var rt = Ui.Layer("HUD", 10);
            var hud = rt.gameObject.AddComponent<Hud>();
            hud.root = rt;
            hud.cleaning = cc;
            hud.Build();
            Instance = hud;
            return hud;
        }

        void Build()
        {
            group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            reticleRoot = Ui.Place(Ui.Rect(root, "Reticle"), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80, 80));
            ringBack = Ui.Image(reticleRoot, "RingBack", new Color(1, 1, 1, 0.18f), Ui.Ring(128, 0.09f));
            Ui.Stretch(ringBack.rectTransform);
            ring = Ui.Image(reticleRoot, "Ring", Color.white, Ui.Ring(128, 0.09f));
            Ui.Stretch(ring.rectTransform);
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            dot = Ui.Image(reticleRoot, "Dot", new Color(1, 1, 1, 0.85f), Ui.Circle(32));
            Ui.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7, 7));

            targetLabel = Ui.Label(root, "", UiFont.SansMedium, 21, Ui.Text, TextAlignmentOptions.Center, "TargetLabel");
            Ui.Place(targetLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -62), new Vector2(600, 30));
            targetLabel.fontStyle = FontStyles.Normal;
            targetLabel.characterSpacing = 2f;

            promptRoot = Ui.Place(Ui.Rect(root, "Prompt"), new Vector2(0.5f, 0f), new Vector2(0, 70), new Vector2(900, 44), new Vector2(0.5f, 0.5f));
            promptGroup = promptRoot.gameObject.AddComponent<CanvasGroup>();
            var hl = promptRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 10;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = false; hl.childControlHeight = false;
            hl.childForceExpandWidth = false;

            var toastLayer = Ui.Layer("Toasts", 48);
            toastLayer.GetComponent<CanvasGroup>().blocksRaycasts = false;
            toastRoot = Ui.Place(Ui.Rect(toastLayer, "Toasts"), new Vector2(0f, 1f), new Vector2(36, -30), new Vector2(600, 400), new Vector2(0f, 1f));

            var watch = Ui.Panel(root, "Watch", new Color(0.05f, 0.07f, 0.1f, 0.55f), 16);
            Ui.Place(watch.rectTransform, new Vector2(1, 1), new Vector2(-34, -30), new Vector2(178, 66), new Vector2(1, 1));
            clock = Ui.Label(watch.transform, "10:00", UiFont.Mono, 34, Palette.Hex("9FF5D8"), TextAlignmentOptions.Right, "Clock");
            Ui.Place(clock.rectTransform, new Vector2(1, 0.5f), new Vector2(-18, 9), new Vector2(150, 36), new Vector2(1, 0.5f));
            clockSub = Ui.Label(watch.transform, "PM  MON", UiFont.SansMedium, 13, new Color(0.62f, 0.96f, 0.85f, 0.6f), TextAlignmentOptions.Right, "ClockSub");
            Ui.Place(clockSub.rectTransform, new Vector2(1, 0), new Vector2(-18, 6), new Vector2(150, 16), new Vector2(1, 0));
            clockSub.characterSpacing = 6f;
        }

        public void SetVisible(bool v) => Tween.Run(0.25f, t => group.alpha = v ? t : 1 - t, Ease.OutCubic, owner: group);

        public void SetClock(string hhmm, string sub)
        {
            clock.text = hhmm;
            clockSub.text = sub;
        }

        /// <summary>Show a prompt like ("E", "Pick up · Crumpled note"). Empty key hides it.</summary>
        public void Prompt(params (string key, string label)[] items)
        {
            string signature = string.Join("|", System.Array.ConvertAll(items, i => i.key + i.label));
            signature += GameInput.UsingPad ? "|pad" : "";
            if (signature == promptKey) return;
            promptKey = signature;
            foreach (Transform c in promptRoot) Destroy(c.gameObject);
            foreach (var (key, label) in items)
            {
                if (string.IsNullOrEmpty(key)) continue;
                Ui.KeyCap(promptRoot, GameInput.Glyph(key), 34);
                var t = Ui.Label(promptRoot, label, UiFont.SansMedium, 22, Ui.Text, TextAlignmentOptions.Left);
                t.rectTransform.sizeDelta = new Vector2(t.GetPreferredValues(label).x + 18, 34);
                t.fontMaterial.EnableKeyword("UNDERLAY_ON");
            }
            promptGroup.alpha = 0f;
            Tween.Run(0.18f, a => promptGroup.alpha = a, Ease.OutCubic, owner: promptGroup);
        }

        public void Toast(string title, string sub = null, Color? accent = null, float hold = 2.2f)
        {
            var card = Ui.Panel(toastRoot, "Toast", new Color(0.06f, 0.08f, 0.12f, 0.86f), 14);
            var rt = card.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            var col = accent ?? Ui.Good;
            var bar = Ui.Image(rt, "Accent", col, Ui.Rounded(3));
            Ui.Place(bar.rectTransform, new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(5, 30), new Vector2(0, 0.5f));
            var t = Ui.Label(rt, title, UiFont.SansBold, 24, Ui.Text, TextAlignmentOptions.Left);
            Ui.Place(t.rectTransform, new Vector2(0, 1), new Vector2(30, -12), new Vector2(520, 30), new Vector2(0, 1));
            float h = 54;
            if (!string.IsNullOrEmpty(sub))
            {
                var s = Ui.Label(rt, sub, UiFont.Sans, 18, Ui.TextDim, TextAlignmentOptions.Left);
                Ui.Place(s.rectTransform, new Vector2(0, 1), new Vector2(30, -42), new Vector2(520, 24), new Vector2(0, 1));
                h = 78;
            }
            float w = Mathf.Max(t.GetPreferredValues(title).x, string.IsNullOrEmpty(sub) ? 0 : t.GetPreferredValues(sub).x * 0.75f, 220) + 60;
            rt.sizeDelta = new Vector2(Mathf.Min(w, 600), h);
            toasts.Insert(0, rt);
            Relayout();
            var cg = rt.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0;
            Tween.Run(0.35f, k => { if (!rt) return; cg.alpha = k; rt.anchoredPosition = new Vector2(Mathf.LerpUnclamped(-40f, 0f, k), rt.anchoredPosition.y); }, Ease.OutCubic);
            Tween.Run(0.4f, k => cg.alpha = 1 - k, Ease.InCubic, () =>
            {
                toasts.Remove(rt);
                if (rt) Destroy(rt.gameObject);
                Relayout();
            }, hold);
        }

        void Relayout()
        {
            float y = 0;
            foreach (var t in toasts)
            {
                if (!t) continue;
                var target = new Vector2(0, -y);
                var from = t.anchoredPosition;
                var tt = t;
                Tween.Run(0.25f, k => { if (tt) tt.anchoredPosition = Vector2.LerpUnclamped(from, target, k); }, Ease.OutCubic, owner: tt.gameObject);
                y += t.sizeDelta.y + 10;
            }
        }

        public void PulseDot() => dotPulse = 1f;

        TextMeshProUGUI toolNote;

        /// <summary>A brief line under the reticle when the pinned tool changes ("Vacuum · pinned").</summary>
        public void ToolNote(string text)
        {
            if (toolNote == null)
            {
                toolNote = Ui.Label(root, "", UiFont.SansMedium, 22, Ui.Accent, TextAlignmentOptions.Center, "ToolNote");
                Ui.Place(toolNote.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -100), new Vector2(600, 30));
                toolNote.characterSpacing = 2f;
                toolNote.fontMaterial.EnableKeyword("UNDERLAY_ON");
            }
            toolNote.text = text.ToUpperInvariant();
            toolNote.alpha = 0;
            Tween.Run(0.15f, k => toolNote.alpha = k, Ease.OutCubic, owner: toolNote);
            Tween.Run(0.4f, k => toolNote.alpha = 1 - k, Ease.InCubic, null, 1.1f, toolNote.gameObject);
        }

        TextMeshProUGUI caption;

        /// <summary>A subtitle line at the bottom of the screen (sounds, voices, inner thoughts).</summary>
        public void Caption(string text, float hold = 3f)
        {
            if (!Settings.Current.Captions && text.StartsWith("[")) return;
            if (caption == null)
            {
                caption = Ui.Label(root, "", UiFont.SansMedium, 26, Ui.Text, TextAlignmentOptions.Center, "Caption");
                Ui.Place(caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 140), new Vector2(1400, 80), new Vector2(0.5f, 0f));
                caption.fontMaterial.EnableKeyword("UNDERLAY_ON");
            }
            caption.text = text;
            caption.alpha = 0;
            Tween.Run(0.3f, k => caption.alpha = k, Ease.OutCubic, owner: caption);
            Tween.Run(0.6f, k => caption.alpha = 1 - k, Ease.InCubic, null, hold, caption.gameObject);
        }

        void Update()
        {
            float dt = GameTime.UnscaledDelta;
            bool busy = GameRoot.Instance != null && GameRoot.Instance.Blocked;
            // Scale rather than deactivate: TMP can't measure labels built while inactive.
            promptRoot.localScale = busy ? Vector3.zero : Vector3.one;
            reticleRoot.gameObject.SetActive(!busy);
            var cc = cleaning;
            bool show = !busy && cc != null && cc.Target != null && !cc.Suspended;
            float targetShow = show ? (cc.InReach ? 1f : 0.45f) : 0f;
            ringShow = Mathf.Lerp(ringShow, targetShow, 1f - Mathf.Exp(-dt * 14f));
            if (show) ringValue = Mathf.Lerp(ringValue, cc.Target.Completion, 1f - Mathf.Exp(-dt * 16f));
            var accent = show ? ToolDefs.Accent(cc.Target.Tool) : Color.white;
            float s = Mathf.Lerp(0.55f, 1f, ringShow) * (1f + (cc != null && cc.Cleaning ? Mathf.Sin(GameTime.Unscaled * 18f) * 0.03f : 0f));
            reticleRoot.localScale = Vector3.one * (s * 0.62f);
            ring.fillAmount = ringValue;
            ring.color = new Color(accent.r, accent.g, accent.b, ringShow);
            ringBack.color = new Color(1, 1, 1, 0.16f * ringShow);
            dotPulse = Mathf.MoveTowards(dotPulse, 0f, dt * 3f);
            dot.rectTransform.sizeDelta = Vector2.one * (7f + dotPulse * 8f) / Mathf.Max(0.4f, reticleRoot.localScale.x);

            if (show)
            {
                var name = string.IsNullOrEmpty(cc.Target.DisplayName) ? cc.Target.Id : cc.Target.DisplayName;
                targetLabel.text = cc.InReach ? $"{name.ToUpperInvariant()}  <color=#{ColorUtility.ToHtmlStringRGB(accent)}>{Mathf.FloorToInt(cc.Target.Completion * 100)}%</color>" : $"<alpha=#88>{name.ToUpperInvariant()} — get closer";
            }
            targetLabel.alpha = Mathf.Lerp(targetLabel.alpha, show ? 0.9f : 0f, 1f - Mathf.Exp(-dt * 12f));
        }
    }
}
