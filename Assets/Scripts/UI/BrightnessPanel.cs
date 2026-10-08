using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// Brightness, set against the office itself: a small panel at the side with the menus behind
    /// it hidden, so the dark rooms are the reference. Offered once on first launch and opened from
    /// Settings.
    /// </summary>
    public class BrightnessPanel : MonoBehaviour
    {
        static BrightnessPanel instance;
        public static bool IsOpen => instance != null;
        /// <summary>The frame it closed on, so the key that closed it doesn't also close Settings.</summary>
        public static int ClosedFrame = -1;

        RectTransform root;
        readonly List<CanvasGroup> hidden = new();

        /// <summary>Menus that would cover the office while you judge it.</summary>
        static readonly string[] Behind = { "Title", "Pause", "Settings" };

        /// <summary>First launch: offer the page until it has been closed once. Never for automated runs.</summary>
        public static bool OfferOnLaunch(Settings s, bool automated) => !automated && !s.BrightnessChecked;

        public static void Show()
        {
            if (instance) return;
            var rt = Ui.Layer("Brightness", 60);
            instance = rt.gameObject.AddComponent<BrightnessPanel>();
            instance.root = rt;
            instance.Build();
            MenuFade.In(rt);
        }

        /// <summary>The value shown beside the slider: steps either side of the designed look.</summary>
        public static string Format(float v)
        {
            int k = Mathf.RoundToInt((v - 0.5f) * 20f);
            return k == 0 ? "0" : k > 0 ? "+" + k : "−" + -k;
        }

        void Build()
        {
            GameRoot.Instance.SetBlocked("brightness", true, true);
            foreach (var name in Behind)
            {
                var layer = Ui.Canvas.transform.Find(name);
                var g = layer ? layer.GetComponent<CanvasGroup>() : null;
                if (g != null) hidden.Add(g);
            }
            Hide();
            PostFx.Instance?.SetInspect(false); // the pause menu blurs the view; this needs it sharp
            // Clicks stop here, but nothing is drawn over the office.
            var catcher = Ui.Image(root, "Catcher", new Color(0, 0, 0, 0f));
            Ui.Stretch(catcher.rectTransform);
            catcher.raycastTarget = true;

            const float W = 760, H = 440;
            var panel = Ui.Panel(root, "Panel", new Color(0.07f, 0.09f, 0.13f, 0.94f), 22);
            Ui.Place(panel.rectTransform, new Vector2(1, 0.5f), new Vector2(-80, 0), new Vector2(W, H), new Vector2(1, 0.5f));
            var title = Ui.Label(panel.rectTransform, "Brightness", UiFont.SansBold, 44, Ui.Text, TextAlignmentOptions.TopLeft);
            Ui.Place(title.rectTransform, new Vector2(0, 1), new Vector2(50, -36), new Vector2(W - 100, 60), new Vector2(0, 1));
            var text = Ui.Label(panel.rectTransform,
                "Turn it up until you can make out the desks and chairs in the dark, but the shadows still look like night. You can change it later in Settings.",
                UiFont.Sans, 24, Ui.TextDim, TextAlignmentOptions.TopLeft);
            Ui.Place(text.rectTransform, new Vector2(0, 1), new Vector2(50, -104), new Vector2(W - 100, 120), new Vector2(0, 1));
            var s = Settings.Current;
            var slider = Widgets.Slider(panel.rectTransform, "Brightness", s.Brightness, v => s.Brightness = v, Format, W - 100, 180);
            Ui.Place(slider, new Vector2(0, 1), new Vector2(50, -240), new Vector2(W - 100, 56), new Vector2(0, 1));
            var reset = Widgets.Button(panel.rectTransform, "Default", () => SetValue(0.5f), 200, 60, 26);
            Ui.Place(reset, new Vector2(0, 0), new Vector2(50, 40), new Vector2(200, 60), new Vector2(0, 0));
            var done = Widgets.Button(panel.rectTransform, "Done", Close, 220, 60, 28, true);
            Ui.Place(done, new Vector2(1, 0), new Vector2(-50, 40), new Vector2(220, 60), new Vector2(1, 0));
            // Text size scales the whole panel, up to 60% of the screen's width so the office it's judged against stays in view.
            panel.rectTransform.localScale = Vector3.one * ScaleFor(((RectTransform)Ui.Canvas.transform).rect.width, Settings.TextScale);
            MenuFocus.AttachAll(panel.gameObject);
        }

        /// <summary>The panel's scale at a text size, on a canvas this many units wide (it's always 1080 tall).</summary>
        public static float ScaleFor(float canvasW, float k) => Mathf.Max(1f, Mathf.Min(k, 0.6f * canvasW / 760f, 1000f / 440f));

        /// <summary>Set the value and move the slider to match (the Default button).</summary>
        void SetValue(float v)
        {
            Settings.Current.Brightness = v;
            var slider = root.GetComponentInChildren<SliderNav>();
            if (slider) slider.Set(v);
        }

        void Close()
        {
            if (instance != this) return;
            Settings.Current.BrightnessChecked = true;
            Settings.Save();
            // Menus underneath are fully shown whenever they're up (the title may still have been fading in).
            foreach (var g in hidden)
                if (g) { g.alpha = 1f; g.blocksRaycasts = true; }
            if (PauseMenu.IsOpen) PostFx.Instance?.SetInspect(true);
            GameRoot.Instance.SetBlocked("brightness", false);
            ClosedFrame = Time.frameCount;
            instance = null;
            MenuFade.Out(root);
        }

        void Hide()
        {
            foreach (var g in hidden)
                if (g) { g.alpha = 0f; g.blocksRaycasts = false; }
        }

        void Update()
        {
            if (GameInput.Menu.Back) Close();
        }

        // After the title's fade-in tween, which would otherwise bring it back.
        void LateUpdate() => Hide();
    }
}
