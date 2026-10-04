using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AfterHours
{
    /// <summary>Tab: the shift sheet on a clipboard (tasks with progress, secrets, leads, your pocket).</summary>
    public class Clipboard : MonoBehaviour
    {
        public static Clipboard Instance { get; private set; }
        RectTransform root, board;
        CanvasGroup group;
        TextMeshProUGUI tasks, side, header;
        public bool Open { get; private set; }
        float refresh;

        public static Clipboard Create()
        {
            var rt = Ui.Layer("Clipboard", 30);
            var c = rt.gameObject.AddComponent<Clipboard>();
            c.root = rt;
            c.Build();
            Instance = c;
            return c;
        }

        void Build()
        {
            group = root.GetComponent<CanvasGroup>();
            group.alpha = 0;
            group.blocksRaycasts = false;
            var dim = Ui.Image(root, "Dim", new Color(0.01f, 0.015f, 0.03f, 0.55f));
            Ui.Stretch(dim.rectTransform);
            var b = Ui.Panel(root, "Board", Palette.Hex("8A5A34"), 22);
            board = b.rectTransform;
            Ui.Place(board, new Vector2(0.5f, 0.5f), new Vector2(-170, -10), new Vector2(760, 920));
            var clip = Ui.Panel(board, "Clip", Palette.Hex("B9C0C7"), 10);
            Ui.Place(clip.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 26), new Vector2(240, 66), new Vector2(0.5f, 1));
            var paper = Ui.Panel(board, "Paper", Ui.Paper, 6);
            Ui.Place(paper.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -24), new Vector2(690, 830));
            header = Ui.Label(paper.rectTransform, "", UiFont.Type, 34, Ui.Ink, TextAlignmentOptions.TopLeft);
            Ui.Place(header.rectTransform, new Vector2(0, 1), new Vector2(40, -34), new Vector2(620, 90), new Vector2(0, 1));
            tasks = Ui.Label(paper.rectTransform, "", UiFont.Hand, 33, Ui.Ink, TextAlignmentOptions.TopLeft);
            Ui.Place(tasks.rectTransform, new Vector2(0, 1), new Vector2(40, -130), new Vector2(620, 660), new Vector2(0, 1));
            tasks.lineSpacing = 4;

            var note = Ui.Panel(root, "Side", Palette.Hex("F7F2E2"), 8);
            Ui.Place(note.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(420, 40), new Vector2(460, 760));
            note.rectTransform.localRotation = Quaternion.Euler(0, 0, -2f);
            side = Ui.Label(note.rectTransform, "", UiFont.Sans, 22, Ui.Ink, TextAlignmentOptions.TopLeft);
            Ui.Stretch(side.rectTransform, 30);
        }

        public void Toggle()
        {
            if (Open) Close(); else Show();
        }

        public void Show()
        {
            if (NightDirector.Instance == null || NightDirector.Instance.Def == null) return;
            Open = true;
            Refresh();
            GameRoot.Instance?.SetGameplayBlocked(true, false);
            group.blocksRaycasts = true;
            Sfx.Play("ui_page", null, 0.5f, 1f, 0.1f, AudioBus.Ui);
            Tween.Run(0.3f, k =>
            {
                group.alpha = k;
                board.anchoredPosition = new Vector2(-170, Mathf.LerpUnclamped(-500, -10, k));
            }, Ease.OutBack, owner: this);
        }

        public void Close()
        {
            Open = false;
            group.blocksRaycasts = false;
            GameRoot.Instance?.SetGameplayBlocked(false, false);
            Sfx.Play("ui_page", null, 0.35f, 0.9f, 0.1f, AudioBus.Ui);
            Tween.Run(0.2f, k =>
            {
                group.alpha = 1 - k;
                board.anchoredPosition = new Vector2(-170, Mathf.Lerp(-10, -500, k));
            }, Ease.InCubic, owner: this);
        }

        void Refresh()
        {
            var dir = NightDirector.Instance;
            var def = dir.Def;
            header.text = $"SHIFT SHEET  ·  NIGHT {def.Number}\n<size=60%>{def.Day} — {def.Title}</size>";
            var sb = new StringBuilder();
            string lastRoom = null;
            foreach (var t in def.Tasks.OrderBy(t => t.Optional))
            {
                if (t.Optional && lastRoom != "__bonus") { sb.Append("\n<size=70%><color=#8A7A5A>IF YOU HAVE TIME</color></size>\n"); lastRoom = "__bonus"; }
                var (d, n) = dir.Progress(t);
                bool done = dir.IsDone(t);
                string box = done ? "<color=#2E8B57>☑</color>" : "☐";
                string label = done ? $"<color=#7A8070><s>{t.Label}</s></color>" : t.Label;
                string count = n > 1 && !done ? $" <size=75%><color=#8A7A5A>{d}/{n}</color></size>" : "";
                sb.Append($"{box} {label}{count}\n");
            }
            tasks.text = sb.ToString();

            var s = new StringBuilder();
            s.Append($"<b>SECRETS</b>  {dir.SecretsFoundCount} / {def.Secrets.Count}\n");
            foreach (var sec in def.Secrets)
                s.Append(dir.HasSecret(sec.Id) ? $"<color=#6A3FA0>• {sec.Label}</color>\n" : "<color=#A0A0A0>• ???</color>\n");
            s.Append("\n<b>LEADS</b>\n");
            if (Story.State.Phrases.Count == 0) s.Append("<color=#A0A0A0>Nothing yet. Keep your eyes open.</color>\n");
            foreach (var p in Story.State.Phrases) s.Append($"<i>\"{Phrases.Text[p]}\"</i>\n");
            s.Append("\n<b>IN YOUR POCKET</b>\n");
            if (Story.State.Inventory.Count == 0) s.Append("<color=#A0A0A0>Empty.</color>\n");
            foreach (var id in Story.State.Inventory) s.Append($"• {Docs.Get(id)?.Title}\n");
            if (Story.State.Has("has_key_fc2")) s.Append("• Small brass key (FC-2)\n");
            s.Append("\n<size=85%><color=#8A7A5A>Leave things in a person's inbox tray, or feed them to a shredder.</color></size>");
            side.text = s.ToString();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (Open)
            {
                refresh -= Time.unscaledDeltaTime;
                if (refresh <= 0f) { refresh = 0.5f; Refresh(); }
                if (kb != null && (kb.tabKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) Close();
            }
        }
    }
}
