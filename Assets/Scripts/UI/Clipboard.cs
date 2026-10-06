using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AfterHours
{
    /// <summary>
    /// Tab: the shift sheet on a clipboard (tasks with progress, secrets, leads, your pocket), and
    /// a second page, the case file: every document read so far, to read again.
    /// </summary>
    public class Clipboard : MonoBehaviour
    {
        public static Clipboard Instance { get; private set; }
        RectTransform root, board;
        CanvasGroup group;
        TextMeshProUGUI tasks, side, header, files, footer;
        public bool Open { get; private set; }
        float refresh;

        public const int SheetPage = 0, CasePage = 1;
        /// <summary>Which page is showing: the shift sheet or the case file.</summary>
        public int Page { get; private set; }
        /// <summary>The case file's entries (newest night first) and the one selected.</summary>
        public readonly List<string> CaseEntries = new();
        public int Selected { get; private set; }
        const int VisibleLines = 14;

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
            Ui.Place(tasks.rectTransform, new Vector2(0, 1), new Vector2(40, -130), new Vector2(620, 630), new Vector2(0, 1));
            tasks.lineSpacing = 4;
            // Long sheets (with where-is-it lines) shrink to fit the paper rather than run off it.
            tasks.enableAutoSizing = true;
            tasks.fontSizeMin = 24;
            tasks.fontSizeMax = 33;
            files = Ui.Label(paper.rectTransform, "", UiFont.Hand, 31, Ui.Ink, TextAlignmentOptions.TopLeft, "CaseFile");
            Ui.Place(files.rectTransform, new Vector2(0, 1), new Vector2(40, -130), new Vector2(620, 620), new Vector2(0, 1));
            files.lineSpacing = 2;
            files.richText = true;
            footer = Ui.Label(paper.rectTransform, "", UiFont.SansMedium, 20, new Color(0.35f, 0.32f, 0.27f), TextAlignmentOptions.BottomLeft, "PageHint");
            Ui.Place(footer.rectTransform, new Vector2(0, 0), new Vector2(40, 16), new Vector2(620, 36), new Vector2(0, 0));

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

        public void Show() => Show(SheetPage);

        public void Show(int page)
        {
            if (NightDirector.Instance == null || NightDirector.Instance.Def == null) return;
            Open = true;
            Page = page;
            Selected = 0;
            Refresh();
            PlaytestLog.Log("clipboard");
            GameRoot.Instance?.SetBlocked("clipboard", true);
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
            if (!Open) return;
            Open = false;
            group.blocksRaycasts = false;
            GameRoot.Instance?.SetBlocked("clipboard", false);
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
            RefreshSide(dir, def);
            bool sheet = Page == SheetPage;
            tasks.enabled = sheet;
            files.enabled = !sheet;
            // Key caps drawn darker than the HUD's, to show on paper.
            static string Cap(string key) => $"<mark=#1B223026 padding=\"10,10,4,4\"><b>{GameInput.Glyph(key)}</b></mark>";
            string Tab(string name, bool on) => on ? $"<color=#1B2230>{name}</color>" : name;
            footer.text = $"{Cap("A / D")}  {Tab("Shift sheet", sheet)}  ·  {Tab("Case file", !sheet)}"
                          + (sheet ? "" : $"          {Cap("E")}  Read again");
            if (!sheet) { RefreshCaseFile(); return; }
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
                sb.Append($"{box} {label}{count}{Where(dir, t, done)}\n");
            }
            tasks.text = sb.ToString();
        }

        void RefreshSide(NightDirector dir, NightDef def)
        {
            var s = new StringBuilder();
            s.Append($"<b>SECRETS</b>  {dir.SecretsFoundCount} / {def.Secrets.Count}\n");
            foreach (var sec in def.Secrets)
                s.Append(dir.HasSecret(sec.Id) ? $"<color=#6A3FA0>• {sec.Label}</color>\n" : "<color=#A0A0A0>• ???</color>\n");
            s.Append("\n<b>LEADS</b>\n");
            if (Story.State.Phrases.Count == 0) s.Append("<color=#A0A0A0>Nothing yet. Keep your eyes open.</color>\n");
            foreach (var p in Story.State.Phrases) s.Append($"<i>\"{Phrases.Text[p]}\"</i>\n");
            s.Append("\n<b>IN YOUR POCKET</b>\n");
            if (Story.State.Inventory.Count == 0 && !Story.State.Has("has_key_fc2")) s.Append("<color=#A0A0A0>Empty.</color>\n");
            foreach (var id in Story.State.Inventory) s.Append($"• {Docs.Get(id)?.Title}\n");
            if (Story.State.Has("has_key_fc2")) s.Append("• Small brass key (FC-2)\n");
            s.Append("\n<size=85%><color=#8A7A5A>Leave things in a person's inbox tray, or feed them to a shredder.</color></size>");
            side.text = s.ToString();
        }

        // ---- the case file ---------------------------------------------------------------------------

        /// <summary>Every document read so far that still exists, newest night first, in reading order within a night.</summary>
        static List<ReadRecord> Reads() => (Story.State.Read ?? new List<ReadRecord>())
            .Where(r => Docs.Get(r.Id) != null)
            .Select((r, i) => (r, i)).OrderByDescending(x => x.r.Night).ThenBy(x => x.i).Select(x => x.r).ToList();

        void RefreshCaseFile()
        {
            var reads = Reads();
            CaseEntries.Clear();
            CaseEntries.AddRange(reads.Select(r => r.Id));
            Selected = CaseEntries.Count == 0 ? 0 : Mathf.Clamp(Selected, 0, CaseEntries.Count - 1);
            header.text = $"CASE FILE\n<size=60%>{(reads.Count == 1 ? "One document" : reads.Count + " documents")} you've read</size>";
            if (reads.Count == 0)
            {
                files.text = "<color=#8A7A5A>Nothing yet. Notes, letters and screens you read end up here, so you can read them again.</color>";
                return;
            }
            // Lines: a heading per night, then its documents; only a window around the selection fits.
            var lines = new List<string>();
            int selLine = 0, last = -1;
            for (int i = 0; i < reads.Count; i++)
            {
                var r = reads[i];
                if (r.Night != last)
                {
                    last = r.Night;
                    var nd = NightDefs.Get(r.Night);
                    lines.Add($"<size=62%><color=#8A7A5A>NIGHT {r.Night}{(nd != null ? " · " + nd.Day.ToUpperInvariant() : "")}</color></size>");
                }
                var d = Docs.Get(r.Id);
                string fate = Story.State.FateLabel(d.Id, d.Evidence);
                string tail = fate != null ? $"  <size=68%><color=#86765A>{fate}</color></size>" : "";
                if (i == Selected) { selLine = lines.Count; lines.Add($"<mark=#FFC85766 padding=\"8,8,2,2\">▸ {d.Title}</mark>{tail}"); }
                else lines.Add($"   {d.Title}{tail}");
            }
            int start = Mathf.Clamp(selLine - VisibleLines / 2, 0, Mathf.Max(0, lines.Count - VisibleLines));
            var shown = lines.Skip(start).Take(VisibleLines).ToList();
            if (start > 0) shown[0] = "<size=62%><color=#8A7A5A>▲ more</color></size>";
            if (start + VisibleLines < lines.Count) shown[^1] = "<size=62%><color=#8A7A5A>▼ more</color></size>";
            files.text = string.Join("\n", shown);
        }

        void Move(int by)
        {
            if (CaseEntries.Count == 0) return;
            int next = Mathf.Clamp(Selected + by, 0, CaseEntries.Count - 1);
            if (next == Selected) return;
            Selected = next;
            Sfx.Play("ui_click", null, 0.25f, 1.2f, 0.05f, AudioBus.Ui);
            RefreshCaseFile();
        }

        public void FlipTo(int page)
        {
            if (page == Page) return;
            Page = page;
            Sfx.Play("ui_page", null, 0.4f, 1.1f, 0.1f, AudioBus.Ui);
            Refresh();
        }

        /// <summary>Read the selected case-file entry again: Close only, and nothing in the story changes.</summary>
        public void ReadSelected()
        {
            if (Page != CasePage || Selected < 0 || Selected >= CaseEntries.Count) return;
            var d = Docs.Get(CaseEntries[Selected]);
            if (d == null) return;
            Sfx.Play(d.Style == DocStyle.Screen ? "ui_click" : "ui_page", null, 0.5f);
            InspectView.Show(d, InspectMode.Read, null, reread: true);
        }

        /// <summary>" · bullpen 2 · reception 1": which rooms still have work on a task that spans several.</summary>
        static string Where(NightDirector dir, TaskDef t, bool done)
        {
            if (done || t.Kind == TaskKind.Lights || t.Kind == TaskKind.Flag) return "";
            var left = dir.Remaining(t);
            if (t.Kind == TaskKind.Clean)
            {
                // Surfaces report several spots each; count surfaces, not spots.
                var rooms = t.Targets.Select(id => dir.Office.Surfaces.TryGetValue(id, out var g) ? g : null)
                    .Where(g => g != null && g.gameObject.activeSelf && !g.Done)
                    .Select(g => Room.At(g.transform.position + Vector3.up * 0.2f)?.Id ?? "?").ToList();
                left = rooms.Select(r => (Vector3.zero, r)).ToList();
                bool spread = t.Targets.Select(id => dir.Office.Surfaces.TryGetValue(id, out var g) ? Room.At(g.transform.position + Vector3.up * 0.2f)?.Id : null).Distinct().Count() > 1;
                if (!spread) return "";
            }
            if (left.Count == 0) return "";
            var parts = left.GroupBy(x => x.room).OrderByDescending(g => g.Count())
                .Select(g => $"{RoomName(g.Key)} {g.Count()}");
            return $"\n<size=70%><indent=1.4em><color=#86765A>{string.Join(" · ", parts)}</color></indent></size>";
        }

        static string RoomName(string id) =>
            Room.All.TryGetValue(id ?? "", out var r) && r != null ? r.DisplayName.ToLowerInvariant() : "somewhere";

        void Update()
        {
            if (!Open) return;
            refresh -= GameTime.UnscaledDelta;
            if (refresh <= 0f) { refresh = 0.5f; Refresh(); }
            // A document read from the case file has the screen and the keys until it closes (and on that frame).
            group.alpha = Mathf.MoveTowards(group.alpha, InspectView.IsOpen ? 0.2f : 1f, GameTime.UnscaledDelta * 5f);
            if (InspectView.IsOpen || Time.frameCount == InspectView.ClosedFrame) return;
            var m = GameInput.Menu;
            if (m.Clipboard || m.Back) { Close(); return; }
            if (m.Left) FlipTo(SheetPage);
            else if (m.Right) FlipTo(CasePage);
            if (Page != CasePage) return;
            float wheel = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
            if (m.Up || wheel > 0.1f) Move(-1);
            else if (m.Down || wheel < -0.1f) Move(1);
            else if (m.Confirm) ReadSelected();
        }
    }
}
