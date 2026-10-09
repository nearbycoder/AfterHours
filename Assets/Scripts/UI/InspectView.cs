using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AfterHours
{
    public enum InspectMode { Read, Evidence, Screen }
    public enum InspectChoice { Close, Keep, Toss, SwitchOff }

    /// <summary>
    /// Full-screen document reader: the paper slides up over a blurred, dimmed world. Styles get
    /// their own paper, font and ink. Evidence offers Keep / Put back / Throw away.
    /// </summary>
    public class InspectView : MonoBehaviour
    {
        static InspectView instance;
        RectTransform root, card;
        CanvasGroup group, cardGroup;
        Image paper, screenImg;
        TextMeshProUGUI header, body, hints;
        Action<InspectChoice> callback;
        InspectMode mode;
        float openedAt;
        public static bool IsOpen => instance != null && instance.open;
        /// <summary>The document on screen, if any (automation checks this).</summary>
        public static string CurrentDoc => IsOpen ? instance.doc : null;
        /// <summary>How the open document is being read (the touch screen's soft keys follow it).</summary>
        public static InspectMode Mode => IsOpen ? instance.mode : InspectMode.Read;
        /// <summary>Frame the reader closed on, so the same key doesn't also reach whatever is underneath.</summary>
        public static int ClosedFrame = -1;
        bool open;
        string doc;

        static InspectView Instance
        {
            get
            {
                if (instance) return instance;
                var rt = Ui.Layer("Inspect", 40);
                instance = rt.gameObject.AddComponent<InspectView>();
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
            var dim = Ui.Image(root, "Dim", new Color(0.01f, 0.015f, 0.03f, 0.62f));
            Ui.Stretch(dim.rectTransform);
            var glow = Ui.Image(root, "Glow", new Color(1f, 0.85f, 0.6f, 0.08f), Ui.Glow());
            Ui.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 1100));

            header = Ui.Label(root, "", UiFont.SansMedium, 22, new Color(1, 1, 1, 0.7f), TextAlignmentOptions.Center, "Header");
            Ui.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(1200, 34), new Vector2(0.5f, 1f));
            header.characterSpacing = 6;

            card = Ui.Rect(root, "Card");
            Ui.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(760, 820));
            // The paper turns solid almost at once; only the backdrop fades slowly.
            cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            cardGroup.ignoreParentGroups = true;
            cardGroup.alpha = 0;
            var shadow = Ui.Panel(card, "Shadow", new Color(0, 0, 0, 0.45f), 18);
            Ui.Stretch(shadow.rectTransform, -6);
            shadow.rectTransform.anchoredPosition = new Vector2(10, -14);
            paper = Ui.Panel(card, "Paper", Ui.Paper, 6);
            Ui.Stretch(paper.rectTransform);
            screenImg = Ui.Image(card, "Screen", Color.white);
            Ui.Stretch(screenImg.rectTransform, 18);
            screenImg.preserveAspect = true;
            body = Ui.Label(card, "", UiFont.Hand, 40, Ui.Ink, TextAlignmentOptions.TopLeft, "Body");
            Ui.Stretch(body.rectTransform, 60);
            body.lineSpacing = 6;

            hints = Ui.Label(root, "", UiFont.SansMedium, 22, Ui.Text, TextAlignmentOptions.Center, "Hints");
            Ui.Place(hints.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 46), new Vector2(1400, 40), new Vector2(0.5f, 0f));
        }

        /// <summary>
        /// Open a document. Everything shown goes into the case file, except when the case file
        /// itself is showing it again (<paramref name="reread"/>).
        /// </summary>
        /// <param name="overMenus">Read from a menu (the title's case file): drawn above the menus, and a click also closes it.</param>
        public static void Show(DocDef d, InspectMode mode, Action<InspectChoice> done, bool reread = false, bool overMenus = false)
        {
            if (d == null) return;
            if (!reread) Story.State.NoteRead(d.Id);
            var v = Instance;
            v.overMenus = overMenus;
            v.root.GetComponent<Canvas>().sortingOrder = overMenus ? 75 : 40;
            v.Open(d, mode, done);
        }

        bool overMenus;

        void Open(DocDef d, InspectMode m, Action<InspectChoice> done)
        {
            open = true;
            doc = d.Id;
            mode = m;
            callback = done;
            openedAt = GameTime.Unscaled;
            header.text = d.Header.ToUpperInvariant();
            Style(d);
            // Keyboard key or pad button (Y keep, A close, X throw away / switch off).
            string key(string kb, string pad) => $"<mark=#FFFFFF33 padding=\"12,12,6,6\"><b>{(GameInput.UsingPad ? GameInput.PadGlyph(pad) : GameInput.Glyph(kb))}</b></mark>";
            hints.text = m switch
            {
                InspectMode.Evidence => $"{key("TAB", "Y")}  Keep it        {key("E", "A")}  Put it back        {key("X", "X")}  Throw it away",
                InspectMode.Screen => $"{key("E", "A")}  Close        {key("Q", "X")}  Switch off monitor",
                _ => $"{key("E", "A")}  Close",
            };
            // Text size (Settings) applies to the header and the key hints around the paper.
            float k = Settings.TextScale;
            float canvasW = ((RectTransform)Ui.Canvas.transform).rect.width;
            hints.rectTransform.localScale = Vector3.one * k;
            hints.rectTransform.sizeDelta = new Vector2(Mathf.Min(1400f, (canvasW - 80f) / k), 40f);
            header.rectTransform.localScale = Vector3.one * k;
            header.rectTransform.sizeDelta = new Vector2(Mathf.Min(1200f, (canvasW - 80f) / k), 34f);
            GameRoot.Instance?.SetBlocked("inspect", true);
            PostFx.Instance?.SetInspect(true);
            Sfx.Duck = 0.45f;
            group.blocksRaycasts = true;
            float rot = UnityEngine.Random.Range(-2.2f, 2.2f);
            Tween.Run(0.45f, k =>
            {
                group.alpha = Mathf.Clamp01(k * 2f);
                cardGroup.alpha = Mathf.Clamp01(k * 6f);
                card.anchoredPosition = new Vector2(0, Mathf.LerpUnclamped(-420, 10, k));
                card.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(rot * 4f, rot, k));
            }, Ease.OutBack, owner: this);
        }

        void Style(DocDef d)
        {
            bool screen = d.Style == DocStyle.Screen && !string.IsNullOrEmpty(d.Image);
            screenImg.enabled = screen;
            body.enabled = !screen;
            Vector2 size = new(760, 820);
            Color paperCol = Ui.Paper;
            UiFont f = UiFont.Hand;
            float fs = 40;
            Color ink = Ui.Ink;
            switch (d.Style)
            {
                case DocStyle.Sticky: size = new(560, 520); paperCol = Palette.Sticky; f = UiFont.Hand; fs = 46; break;
                case DocStyle.Note: size = new(760, 760); f = UiFont.Hand; fs = 42; ink = Palette.Hex("1E3A6E"); break;
                case DocStyle.Letter: size = new(820, 900); f = UiFont.Hand; fs = 36; ink = Palette.Hex("1E2E52"); break;
                case DocStyle.Printout: size = new(800, 860); f = UiFont.Sans; fs = 28; break;
                case DocStyle.Invoice: size = new(860, 760); f = UiFont.Type; fs = 28; paperCol = Palette.Hex("F6F3EA"); break;
                case DocStyle.Ledger: size = new(860, 800); f = UiFont.Mono; fs = 27; paperCol = Palette.Hex("EEF2E6"); break;
                case DocStyle.Log: size = new(860, 700); f = UiFont.Mono; fs = 28; paperCol = Palette.Hex("F2F2F0"); break;
                case DocStyle.Notepad: size = new(700, 860); f = UiFont.Hand; fs = 44; paperCol = Palette.Hex("FAEC96"); ink = Palette.Hex("2A2A3A"); break;
                case DocStyle.Card: size = new(820, 640); f = UiFont.Hand; fs = 40; paperCol = Palette.Hex("F7E6E0"); ink = Palette.Hex("5A2A3A"); break;
                case DocStyle.Email: size = new(900, 700); f = UiFont.Sans; fs = 28; paperCol = Palette.Hex("F5F7FA"); break;
                case DocStyle.Screen: size = new(960, 600); f = UiFont.Mono; fs = 32; paperCol = Palette.Hex("0E1622"); ink = Palette.Hex("9FF5D8"); break;
            }
            if (screen)
            {
                var tex = Res.Texture(d.Image);
                if (tex != null)
                {
                    screenImg.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    size = new Vector2(1100, 1100f * tex.height / tex.width + 36);
                    paperCol = Palette.Hex("15181C");
                }
            }
            card.sizeDelta = Grow(size, Settings.TextScale, screen);
            paper.color = paperCol;
            // Handwriting set Plain: handwritten styles in the plain font, sized to take the same width.
            fs = Ui.LetteringSize(f, fs);
            f = Ui.Lettering(f);
            body.font = Ui.Font(f);
            body.fontSize = fs;
            // Larger text sizes: the paper grows (above) and the text grows with it, up to the
            // setting's scale. At any size, text that would run past the paper (a few sticky notes
            // at Normal) shrinks a little to fit instead.
            float k = Settings.TextScale;
            body.enableAutoSizing = true;
            body.fontSizeMin = fs * 0.8f;
            body.fontSizeMax = fs * k;
            body.color = ink;
            body.text = GameInput.ExpandKeys(d.Body);
            body.margin = d.Style == DocStyle.Sticky ? new Vector4(20, 20, 20, 20) : Vector4.zero;
        }

        /// <summary>
        /// The paper's size at text scale <paramref name="k"/>: up to k times the designed size,
        /// within the screen's width and the height between the header and the key hints (both
        /// also scaled). Wider paper makes room for the larger text even where the height can't
        /// grow. Normal (k = 1) keeps the designed size; pictures keep their aspect.
        /// </summary>
        public static Vector2 Grow(Vector2 size, float k, bool keepAspect) =>
            Grow(size, k, keepAspect, k <= 1f ? 1920f : ((RectTransform)Ui.Canvas.transform).rect.width);

        /// <summary>As above, on a canvas <paramref name="canvasW"/> units wide (1080 tall).</summary>
        public static Vector2 Grow(Vector2 size, float k, bool keepAspect, float canvasW)
        {
            if (k <= 1f) return size;
            // The card sits 10 units above centre on a 1080-unit canvas; leave 20 units of air.
            float top = 60f + 34f * k + 20f, bottom = 46f + 40f * k + 20f;
            float maxH = 2f * Mathf.Min(530f - top, 550f - bottom);
            float maxW = canvasW - 160f;
            if (keepAspect) return size * Mathf.Max(1f, Mathf.Min(k, Mathf.Min(maxW / size.x, maxH / size.y)));
            return new Vector2(Mathf.Max(size.x, Mathf.Min(size.x * k, maxW)), Mathf.Min(size.y * k, maxH));
        }

        void Update()
        {
            if (!open) return;
            if (GameTime.Unscaled - openedAt < 0.25f) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var f = GameInput.Frame;
            var menu = GameInput.Menu;
            bool e = f.Interact || menu.Confirm;
            bool esc = menu.Back || (mouse != null && (mouse.rightButton.wasPressedThisFrame || overMenus && mouse.leftButton.wasPressedThisFrame));
            bool tab = menu.Keep || f.Clipboard;
            bool x = menu.Alt;
            bool q = Controls.Pressed(Act.Drop) || menu.Alt;
            if (AutoChoice.HasValue) { var c = AutoChoice.Value; AutoChoice = null; Close(c); return; }
            if (mode == InspectMode.Evidence)
            {
                if (tab) Close(InspectChoice.Keep);
                else if (x) Close(InspectChoice.Toss);
                else if (e || esc) Close(InspectChoice.Close);
            }
            else if (mode == InspectMode.Screen && q) Close(InspectChoice.SwitchOff);
            else if (e || esc) Close(InspectChoice.Close);
        }

        /// <summary>Automation hook: the next frame closes with this choice.</summary>
        public static InspectChoice? AutoChoice;

        void Close(InspectChoice c)
        {
            open = false;
            ClosedFrame = Time.frameCount;
            group.blocksRaycasts = false;
            PostFx.Instance?.SetInspect(false);
            Sfx.Duck = 1f;
            Sfx.Play("ui_page", null, 0.4f);
            Tween.Run(0.25f, k =>
            {
                group.alpha = 1 - k;
                cardGroup.alpha = 1 - k * k;
                card.anchoredPosition = new Vector2(0, Mathf.Lerp(10, -300, k));
            }, Ease.InCubic, owner: this);
            GameRoot.Instance?.SetBlocked("inspect", false);
            var cb = callback;
            callback = null;
            cb?.Invoke(c);
        }
    }
}
