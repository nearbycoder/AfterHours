using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AfterHours
{
    public enum UiFont { Sans, SansMedium, SansBold, SansLight, Hand, Marker, Type, Mono, Print, Scrawl }

    /// <summary>
    /// Runtime uGUI kit: one overlay canvas, font assets made from bundled TTFs, generated
    /// rounded/ring sprites and helpers for every widget the game uses.
    /// </summary>
    public static class Ui
    {
        static Canvas canvas;
        static readonly Dictionary<UiFont, TMP_FontAsset> fonts = new();
        static readonly Dictionary<string, Sprite> sprites = new();

        public static readonly Color Ink = Palette.Hex("1B2230");
        public static readonly Color Paper = Palette.Hex("F4EFE3");
        public static readonly Color PaperDark = Palette.Hex("E6DECB");
        public static readonly Color Glass = new(0.06f, 0.08f, 0.12f, 0.82f);
        public static readonly Color Text = Palette.Hex("EEF2F7");
        public static readonly Color TextDim = new(0.85f, 0.88f, 0.93f, 0.6f);
        public static readonly Color Accent = Palette.Hex("FFC857");
        public static readonly Color Good = Palette.Hex("7EE0B5");

        public static Canvas Canvas
        {
            get
            {
                if (canvas) return canvas;
                var go = new GameObject("UI");
                Object.DontDestroyOnLoad(go);
                canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
                var scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 1f; // landscape only: always 1080 units tall, width varies with aspect
                go.AddComponent<GraphicRaycaster>();
                if (Object.FindAnyObjectByType<EventSystem>() == null)
                {
                    var es = new GameObject("EventSystem");
                    Object.DontDestroyOnLoad(es);
                    es.AddComponent<EventSystem>();
                    es.AddComponent<InputSystemUIInputModule>();
                }
                return canvas;
            }
        }

        static TMP_FontAsset symbols;

        /// <summary>DejaVu Sans as a last-resort fallback for symbols (✓ ✗ ☀ ♥ ★ ☺ …).</summary>
        static TMP_FontAsset Symbols
        {
            get
            {
                if (symbols) return symbols;
                var ttf = Resources.Load<Font>("Fonts/DejaVuSans");
                symbols = TMP_FontAsset.CreateFontAsset(ttf, 64, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 512, 512);
                symbols.name = "DejaVu SDF";
                return symbols;
            }
        }

        /// <summary>
        /// Handwriting set Plain (Settings → Accessibility): what's handwritten (Caveat) is set in
        /// Fira Sans instead, at <see cref="PlainScale"/> of the size, where its lines take about
        /// the same width. Typewritten, printed and screen text keep their fonts.
        /// </summary>
        public static UiFont Lettering(UiFont f, bool plain) => plain && f == UiFont.Hand ? UiFont.Sans : f;
        public static float LetteringSize(UiFont f, float size, bool plain) => plain && f == UiFont.Hand ? size * PlainScale : size;
        public static UiFont Lettering(UiFont f) => Lettering(f, Settings.Current.PlainHandwriting);
        public static float LetteringSize(UiFont f, float size) => LetteringSize(f, size, Settings.Current.PlainHandwriting);
        /// <summary>Fira Sans runs about 1.32 times as wide as Caveat at the same size.</summary>
        public const float PlainScale = 0.76f;

        public static TMP_FontAsset Font(UiFont f)
        {
            if (fonts.TryGetValue(f, out var fa) && fa != null) return fa;
            string file = f switch
            {
                UiFont.Sans => "FiraSans-Regular",
                UiFont.SansMedium => "FiraSans-Medium",
                UiFont.SansBold => "FiraSans-Bold",
                UiFont.SansLight => "FiraSans-Light",
                UiFont.Hand => "Caveat",
                UiFont.Marker => "PermanentMarker",
                UiFont.Type => "SpecialElite",
                UiFont.Mono => "CourierPrime",
                UiFont.Print => "PatrickHand",
                UiFont.Scrawl => "ReenieBeanie",
                _ => "FiraSans-Regular",
            };
            var ttf = Resources.Load<Font>("Fonts/" + file);
            fa = TMP_FontAsset.CreateFontAsset(ttf, 72, 7, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
            fa.name = file + " SDF";
            fa.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (f != UiFont.Sans) fa.fallbackFontAssetTable.Add(Font(UiFont.Sans));
            fa.fallbackFontAssetTable.Add(Symbols);
            fonts[f] = fa;
            return fa;
        }

        // ---- sprites ---------------------------------------------------------------------------

        /// <summary>9-sliced rounded rectangle (white) with the given corner radius in pixels.</summary>
        public static Sprite Rounded(int radius)
        {
            string key = "round" + radius;
            if (sprites.TryGetValue(key, out var sp)) return sp;
            int size = radius * 2 + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float c = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - c) - 2f, 0f), dy = Mathf.Max(Mathf.Abs(y + 0.5f - c) - 2f, 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - d + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect,
                new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
            sprites[key] = sp;
            return sp;
        }

        public static Sprite Ring(int size = 128, float thickness = 0.12f)
        {
            string key = $"ring{size}_{thickness}";
            if (sprites.TryGetValue(key, out var sp)) return sp;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color32[size * size];
            float c = size / 2f, r = c - 1.5f, inner = r * (1f - thickness * 2f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                float a = Mathf.Clamp01(r - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            sprites[key] = sp;
            return sp;
        }

        public static Sprite Circle(int size = 64)
        {
            string key = "circle" + size;
            if (sprites.TryGetValue(key, out var sp)) return sp;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color32[size * size];
            float c = size / 2f, r = c - 1f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d + 0.5f) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            sprites[key] = sp;
            return sp;
        }

        /// <summary>Soft radial glow (for vignettes behind cards, light pools in UI).</summary>
        public static Sprite Glow(int size = 128)
        {
            string key = "glow" + size;
            if (sprites.TryGetValue(key, out var sp)) return sp;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float c = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c)) / c;
                float a = Mathf.Clamp01(1f - d);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            sprites[key] = sp;
            return sp;
        }

        // ---- widgets ---------------------------------------------------------------------------

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent ? parent : Canvas.transform, false);
            return rt;
        }

        public static RectTransform Layer(string name, int order)
        {
            var rt = Rect(Canvas.transform, name);
            Stretch(rt);
            var c = rt.gameObject.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = order;
            rt.gameObject.AddComponent<GraphicRaycaster>();
            rt.gameObject.AddComponent<CanvasGroup>();
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Image(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Image Panel(Transform parent, string name, Color color, int radius = 14) =>
            Image(parent, name, color, Rounded(radius));

        public static TextMeshProUGUI Label(Transform parent, string text, UiFont font, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Left, string name = "Text")
        {
            var rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Font(Lettering(font));
            t.fontSize = LetteringSize(font, size);
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>A key glyph like [E] or [LMB] drawn as a small keycap.</summary>
        public static RectTransform KeyCap(Transform parent, string key, float height = 34f, bool keyboard = false, bool pad = false)
        {
            // Pad face buttons are round and coloured like the controller; everything else is a key cap.
            Color? face = !pad && (keyboard || !GameInput.UsingPad) ? null : key switch
            {
                "A" => new Color(0.36f, 0.72f, 0.33f), "B" => new Color(0.86f, 0.3f, 0.27f),
                "X" => new Color(0.27f, 0.52f, 0.9f), "Y" => new Color(0.95f, 0.76f, 0.2f),
                // PlayStation face buttons.
                "✕" => new Color(0.42f, 0.58f, 0.95f), "○" => new Color(0.9f, 0.36f, 0.38f),
                "□" => new Color(0.86f, 0.48f, 0.78f), "△" => new Color(0.3f, 0.78f, 0.62f), _ => null,
            };
            var bg = Panel(parent, "Key_" + key, face ?? new Color(1, 1, 1, 0.92f), face.HasValue ? (int)(height / 2) : 8);
            var t = Label(bg.transform, key, UiFont.SansBold, height * 0.5f, face.HasValue ? Color.white : Ink, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);
            t.margin = new Vector4(6, 0, 6, 0);
            float w = face.HasValue ? height : Mathf.Max(height, t.GetPreferredValues(key).x + 16f);
            bg.rectTransform.sizeDelta = new Vector2(w, height);
            return bg.rectTransform;
        }

        public static void SetAlpha(Graphic g, float a)
        {
            var c = g.color;
            c.a = a;
            g.color = c;
        }
    }
}
