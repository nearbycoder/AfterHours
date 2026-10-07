using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AfterHours
{
    /// <summary>
    /// Tape the shredded invoice back together: swap vertical strips until the page lines up.
    /// A/D (or click) to choose a strip, E to pick it up and E again to swap.
    /// </summary>
    public class ShredPuzzle : MonoBehaviour
    {
        const int N = 6;
        RectTransform root, board;
        CanvasGroup group;
        readonly List<RectTransform> strips = new();
        readonly List<Image> outlines = new();
        int[] order;
        int cursor, held = -1;
        Action solved;
        bool done;
        TextMeshProUGUI hint;
        float age;
        public static bool IsOpen { get; private set; }
        public static bool AutoSolve;

        const float W = 620f, H = 800f, SW = W / N;

        public static void Show(Action onSolved)
        {
            var rt = Ui.Layer("ShredPuzzle", 46);
            var p = rt.gameObject.AddComponent<ShredPuzzle>();
            p.root = rt;
            p.solved = onSolved;
            p.Build();
        }

        void Build()
        {
            IsOpen = true;
            group = root.GetComponent<CanvasGroup>();
            var dim = Ui.Image(root, "Dim", new Color(0.02f, 0.025f, 0.04f, 0.92f));
            Ui.Stretch(dim.rectTransform);
            var table = Ui.Panel(root, "Table", Palette.Hex("6E5A44"), 20);
            Ui.Place(table.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(W + 160, H + 120));
            board = Ui.Rect(root, "Board");
            Ui.Place(board, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(W, H));
            var tex = Res.Texture("Textures/Docs/shred_invoice");
            var rng = new Rng(Environment.TickCount);
            order = new int[N];
            for (int i = 0; i < N; i++) order[i] = i;
            do
            {
                for (int i = N - 1; i > 0; i--) { int j = rng.Range(0, i + 1); (order[i], order[j]) = (order[j], order[i]); }
            } while (Solved());
            for (int i = 0; i < N; i++)
            {
                var img = Ui.Image(board, "Strip" + i, Color.white);
                if (tex)
                {
                    float tw = tex.width / (float)N;
                    img.sprite = Sprite.Create(tex, new Rect(i * tw, 0, tw, tex.height), new Vector2(0.5f, 0.5f));
                }
                img.raycastTarget = true;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(SW - 6, H);
                strips.Add(rt);
                var ol = Ui.Image(rt, "Outline", new Color(1f, 0.8f, 0.35f, 0f), Ui.Rounded(6));
                Ui.Stretch(ol.rectTransform, -5);
                ol.transform.SetAsFirstSibling();
                outlines.Add(ol);
                int stripId = i;
                var btn = img.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => { cursor = Array.IndexOf(order, stripId); Pick(); });
            }
            var title = Ui.Label(root, "Tape the strips back together", UiFont.SansBold, 34, Ui.Text, TextAlignmentOptions.Center);
            Ui.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(1200, 50), new Vector2(0.5f, 1));
            hint = Ui.Label(root, $"{GameInput.MenuGlyph("A / D")}  choose   ·   {GameInput.MenuGlyph("E")}  pick up / swap   ·   {GameInput.MenuGlyph("Esc")}  give up for now", UiFont.SansMedium, 22, Ui.TextDim, TextAlignmentOptions.Center);
            Ui.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1200, 40), new Vector2(0.5f, 0));
            float k = Settings.TextScale;
            if (k > 1f)
            {
                // Text size: the key hint grows on one line, as far as the screen is wide.
                Ui.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(((RectTransform)Ui.Canvas.transform).rect.width - 80f, 40 * k), new Vector2(0.5f, 0));
                hint.enableAutoSizing = true;
                hint.textWrappingMode = TextWrappingModes.NoWrap;
                hint.fontSizeMin = 22;
                hint.fontSizeMax = hint.fontSize = 22 * k;
            }
            Layout(true);
            GameRoot.Instance?.SetBlocked("puzzle", true, true);
            group.alpha = 0;
            Tween.Run(0.3f, k => group.alpha = k, Ease.OutCubic);
        }

        bool Solved()
        {
            for (int i = 0; i < N; i++) if (order[i] != i) return false;
            return true;
        }

        void Layout(bool instant)
        {
            for (int slot = 0; slot < N; slot++)
            {
                var rt = strips[order[slot]];
                var target = new Vector2(SW * slot + SW / 2, held == slot ? 30 : 0);
                if (instant) rt.anchoredPosition = target;
                else
                {
                    var from = rt.anchoredPosition;
                    var r = rt;
                    Tween.Run(0.18f, k => { if (r) r.anchoredPosition = Vector2.LerpUnclamped(from, target, k); }, Ease.OutBack, owner: r);
                }
                rt.localRotation = Quaternion.Euler(0, 0, done ? 0 : (order[slot] * 37 % 7 - 3) * 0.6f);
            }
        }

        void Pick()
        {
            if (done) return;
            if (held < 0) { held = cursor; Sfx.Play("paper_unfold", null, 0.4f, 1.2f, 0.1f, AudioBus.Ui); }
            else
            {
                (order[held], order[cursor]) = (order[cursor], order[held]);
                held = -1;
                Sfx.Play("ui_page", null, 0.5f, 1f, 0.1f, AudioBus.Ui);
                if (Solved()) Solve();
            }
            Layout(false);
        }

        void Solve()
        {
            done = true;
            Sfx.Play("discover", null, 0.7f);
            hint.text = "It lines up.";
            for (int i = 0; i < N; i++) strips[i].localRotation = Quaternion.identity;
            Tween.Delay(1.6f, Close);
        }

        void Close()
        {
            IsOpen = false;
            GameRoot.Instance?.SetBlocked("puzzle", false);
            Tween.Run(0.3f, k => { if (group) group.alpha = 1 - k; }, Ease.InCubic, () => { if (root) Destroy(root.gameObject); });
            if (done) solved?.Invoke();
        }

        void Update()
        {
            age += Mathf.Min(GameTime.UnscaledDelta, 0.05f);
            for (int i = 0; i < N; i++)
                outlines[order[i]].color = new Color(1f, 0.8f, 0.35f, i == cursor && !done ? 0.9f : i == held ? 0.5f : 0f);
            if (done || age < 0.3f) return;
            if (AutoSolve)
            {
                AutoSolve = false;
                for (int i = 0; i < N; i++) order[i] = i;
                Layout(false);
                Solve();
                return;
            }
            var m = GameInput.Menu;
            if (m.Left) { cursor = (cursor + N - 1) % N; Sfx.Play("ui_hover", null, 0.3f, 1f, 0f, AudioBus.Ui); }
            if (m.Right) { cursor = (cursor + 1) % N; Sfx.Play("ui_hover", null, 0.3f, 1f, 0f, AudioBus.Ui); }
            if (m.Confirm) Pick();
            if (m.Back) Close();
        }
    }
}
