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
    /// At the larger text sizes it's one wide sheet with three pages: the shift sheet, the notes
    /// (what the side note shows at Normal) and the case file; long pages scroll.
    /// </summary>
    public class Clipboard : MonoBehaviour
    {
        public static Clipboard Instance { get; private set; }
        RectTransform root, board, paper, note;
        CanvasGroup group;
        TextMeshProUGUI tasks, side, header, files, footer;
        public bool Open { get; private set; }
        float refresh;

        public const int SheetPage = 0, CasePage = 1, NotesPage = 2;
        /// <summary>Which page is showing: the shift sheet, the notes (larger text sizes only) or the case file.</summary>
        public int Page { get; private set; }
        /// <summary>The case file's entries (newest night first) and the one selected.</summary>
        public readonly List<string> CaseEntries = new();
        public int Selected { get; private set; }
        int VisibleLines = 14;

        /// <summary>The wide three-page layout for Large and Largest text (laid out on Show).</summary>
        public bool Large { get; private set; }
        float boardX = -170f;
        /// <summary>The part of a long page that's showing (0 is the top), and how many parts it has.</summary>
        public int Part { get; private set; }
        public int Parts { get; private set; } = 1;
        /// <summary>Pages in reading order, left to right.</summary>
        int[] Order => Large ? new[] { SheetPage, NotesPage, CasePage } : new[] { SheetPage, CasePage };

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
            var clip = Ui.Panel(board, "Clip", Palette.Hex("B9C0C7"), 10);
            Ui.Place(clip.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 26), new Vector2(240, 66), new Vector2(0.5f, 1));
            paper = Ui.Panel(board, "Paper", Ui.Paper, 6).rectTransform;
            header = Ui.Label(paper, "", UiFont.Type, 34, Ui.Ink, TextAlignmentOptions.TopLeft, "Header");
            tasks = Ui.Label(paper, "", UiFont.Hand, 33, Ui.Ink, TextAlignmentOptions.TopLeft, "Sheet");
            tasks.lineSpacing = 4;
            files = Ui.Label(paper, "", UiFont.Hand, 31, Ui.Ink, TextAlignmentOptions.TopLeft, "CaseFile");
            files.lineSpacing = 2;
            files.richText = true;
            footer = Ui.Label(paper, "", UiFont.SansMedium, 20, new Color(0.35f, 0.32f, 0.27f), TextAlignmentOptions.BottomLeft, "PageHint");

            note = Ui.Panel(root, "Side", Palette.Hex("F7F2E2"), 8).rectTransform;
            Ui.Place(note, new Vector2(0.5f, 0.5f), new Vector2(420, 40), new Vector2(460, 760));
            note.localRotation = Quaternion.Euler(0, 0, -2f);
            side = Ui.Label(note, "", UiFont.Sans, 22, Ui.Ink, TextAlignmentOptions.TopLeft, "Notes");
            Layout(false);
        }

        /// <summary>
        /// Normal: the sheet on the clipboard with the side note beside it. Large and Largest: one
        /// wide sheet, the side note's contents moved onto it as a page, every text scaled, and
        /// long pages split into parts rather than shrunk.
        /// </summary>
        void Layout(bool large)
        {
            Large = large;
            float k = large ? Settings.TextScale : 1f;
            float canvasW = ((RectTransform)Ui.Canvas.transform).rect.width;
            float boardW = large ? Mathf.Min(1180f, canvasW - 80f) : 760f, boardH = large ? 1000f : 920f;
            boardX = large ? 0f : -170f;
            Ui.Place(board, new Vector2(0.5f, 0.5f), new Vector2(boardX, -10), new Vector2(boardW, boardH));
            float pw = boardW - 70f, ph = boardH - 90f;
            Ui.Place(paper, new Vector2(0.5f, 0.5f), new Vector2(0, -24), new Vector2(pw, ph));
            float headH = 90f * k, top = 34f + headH + 6f, foot = 36f * k, bottom = 16f + foot + 12f;
            header.fontSize = 34f * k;
            Ui.Place(header.rectTransform, new Vector2(0, 1), new Vector2(40, -34), new Vector2(pw - 70f, headH), new Vector2(0, 1));
            Ui.Place(tasks.rectTransform, new Vector2(0, 1), new Vector2(40, -top), new Vector2(pw - 70f, large ? ph - top - bottom : 630f), new Vector2(0, 1));
            Ui.Place(files.rectTransform, new Vector2(0, 1), new Vector2(40, -top), new Vector2(pw - 70f, large ? ph - top - bottom : 620f), new Vector2(0, 1));
            Ui.Place(footer.rectTransform, new Vector2(0, 0), new Vector2(40, 16), new Vector2(pw - 70f, foot), new Vector2(0, 0));
            footer.fontSize = 20f * k;
            files.fontSize = 31f * k;
            // Normal: long sheets (with where-is-it lines) shrink to fit the paper rather than run off
            // it. Larger sizes keep the size and turn the overflow into parts, as pages of a book.
            tasks.enableAutoSizing = !large;
            tasks.fontSize = 33f * k;
            tasks.fontSizeMin = 24;
            tasks.fontSizeMax = 33;
            tasks.overflowMode = large ? TextOverflowModes.Page : TextOverflowModes.Overflow;
            // The case file shows a window of lines around the selection (14 at Normal).
            VisibleLines = large ? Mathf.Max(5, Mathf.FloorToInt(files.rectTransform.sizeDelta.y / (44f * k))) : 14;
            // The notes: on the side note at Normal, a page of the sheet at larger sizes.
            note.gameObject.SetActive(!large);
            if (large)
            {
                side.rectTransform.SetParent(paper, false);
                Ui.Place(side.rectTransform, new Vector2(0, 1), new Vector2(40, -top), new Vector2(pw - 70f, ph - top - bottom), new Vector2(0, 1));
            }
            else
            {
                side.rectTransform.SetParent(note, false);
                Ui.Stretch(side.rectTransform, 30);
                side.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }
            side.fontSize = 22f * k;
            side.overflowMode = large ? TextOverflowModes.Page : TextOverflowModes.Overflow;
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
            bool large = Settings.TextScale > 1f;
            if (large || large != Large) Layout(large); // larger sizes follow the window's width too
            Page = page;
            Part = 0;
            Hud.Instance?.ClearCaption(); // e.g. Night 1's "shift sheet is on the clipboard", which would show at the edges
            Selected = 0;
            Refresh();
            PlaytestLog.Log("clipboard");
            GameRoot.Instance?.SetBlocked("clipboard", true);
            group.blocksRaycasts = true;
            Sfx.Play("ui_page", null, 0.5f, 1f, 0.1f, AudioBus.Ui);
            Tween.Run(0.3f, k =>
            {
                group.alpha = k;
                board.anchoredPosition = new Vector2(boardX, Mathf.LerpUnclamped(-500, -10, k));
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
                board.anchoredPosition = new Vector2(boardX, Mathf.Lerp(-10, -500, k));
            }, Ease.InCubic, owner: this);
        }

        void Refresh()
        {
            var dir = NightDirector.Instance;
            var def = dir.Def;
            RefreshSide(dir, def);
            bool sheet = Page == SheetPage, notes = Page == NotesPage;
            tasks.enabled = sheet;
            files.enabled = Page == CasePage;
            side.enabled = !Large || notes;
            if (!sheet && !notes) RefreshCaseFile();
            else if (notes) header.text = $"NOTES  ·  NIGHT {def.Number}\n<size=60%>Secrets, leads and your pocket</size>";
            else
            {
                header.text = $"SHIFT SHEET  ·  NIGHT {def.Number}\n<size=60%>{def.Day} — {def.Title}</size>";
                tasks.text = SheetText(dir, def);
            }
            // A long page at the larger sizes shows one part at a time.
            var paged = sheet ? tasks : notes ? side : null;
            Parts = 1;
            if (Large && paged != null)
            {
                paged.ForceMeshUpdate();
                Parts = Mathf.Max(1, paged.textInfo.pageCount);
                Part = Mathf.Clamp(Part, 0, Parts - 1);
                paged.pageToDisplay = Part + 1;
            }
            // Key caps drawn darker than the HUD's, to show on paper.
            static string Cap(string key) => $"<mark=#1B223026 padding=\"10,10,4,4\"><b>{GameInput.Glyph(key)}</b></mark>";
            string Tab(string name, int page) => Page == page ? $"<color=#1B2230>{name}</color>" : name;
            footer.text = $"{Cap("A / D")}  {Tab("Shift sheet", SheetPage)}  ·  {(Large ? Tab("Notes", NotesPage) + "  ·  " : "")}{Tab("Case file", CasePage)}"
                          + (Page == CasePage ? $"          {Cap("E")}  Read again" : "")
                          + (Parts > 1 ? $"          {Cap("W / S")}  {(Part < Parts - 1 ? "More" : "Back to the top")}  <color=#8A7A5A>{Part + 1}/{Parts}</color>" : "");
        }

        string SheetText(NightDirector dir, NightDef def)
        {
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
            return sb.ToString();
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

        /// <summary>Case-file entries for a morning's chat are "chat:N", N the night before it.</summary>
        public const string ChatPrefix = CaseFile.ChatPrefix;

        /// <summary>A past morning's messages, as they were shown.</summary>
        public static List<ChatLine> MorningLines(int night) => CaseFile.MorningLines(Story.State, night);

        public static string MorningLabel(int night) => CaseFile.MorningLabel(night);

        void RefreshCaseFile()
        {
            var entries = CaseFile.Entries(Story.State);
            CaseEntries.Clear();
            CaseEntries.AddRange(entries.Select(e => e.id));
            Selected = CaseEntries.Count == 0 ? 0 : Mathf.Clamp(Selected, 0, CaseEntries.Count - 1);
            header.text = $"CASE FILE\n<size=60%>{CaseFile.Summary(Story.State)}</size>";
            if (entries.Count == 0)
            {
                files.text = "<color=#8A7A5A>Nothing yet. Notes, letters and screens you read end up here, so you can read them again.</color>";
                return;
            }
            // Lines: a heading per night, then its documents; only a window around the selection fits.
            var lines = new List<string>();
            int selLine = 0, last = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                var (night, id) = entries[i];
                if (night != last)
                {
                    last = night;
                    var nd = NightDefs.Get(night);
                    lines.Add($"<size=62%><color=#8A7A5A>NIGHT {night}{(nd != null ? " · " + nd.Day.ToUpperInvariant() : "")}</color></size>");
                }
                var (title, note) = CaseFile.Describe(Story.State, id);
                string tail = note != null ? $"  <size=68%><color=#86765A>{note}</color></size>" : "";
                if (i == Selected) { selLine = lines.Count; lines.Add($"<mark=#FFC85766 padding=\"8,8,2,2\">▸ {title}</mark>{tail}"); }
                else lines.Add($"   {title}{tail}");
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
            Part = 0;
            Sfx.Play("ui_page", null, 0.4f, 1.1f, 0.1f, AudioBus.Ui);
            Refresh();
        }

        /// <summary>Turn to the next page (1) or the previous one (-1), stopping at either end.</summary>
        public void Turn(int by)
        {
            var order = Order;
            int i = System.Array.IndexOf(order, Page);
            FlipTo(order[Mathf.Clamp((i < 0 ? 0 : i) + by, 0, order.Length - 1)]);
        }

        /// <summary>The next part of a long page (from the last, back to the top), or the previous one.</summary>
        public void ScrollPart(int by)
        {
            if (Parts <= 1) return;
            int next = by > 0 ? (Part + 1) % Parts : Mathf.Max(0, Part - 1);
            if (next == Part) return;
            Part = next;
            Sfx.Play("ui_page", null, 0.3f, 1.25f, 0.1f, AudioBus.Ui);
            Refresh();
        }

        /// <summary>Read the selected case-file entry again: Close only, and nothing in the story changes.</summary>
        public void ReadSelected()
        {
            if (Page != CasePage || Selected < 0 || Selected >= CaseEntries.Count) return;
            int night = CaseFile.ChatNight(CaseEntries[Selected]);
            if (night >= 0)
            {
                Sfx.Play("notify", null, 0.3f);
                ChatInterlude.ShowAgain(MorningLabel(night), MorningLines(night), null);
                return;
            }
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
            group.alpha = Mathf.MoveTowards(group.alpha, InspectView.IsOpen || ChatInterlude.ReviewOpen ? 0.2f : 1f, GameTime.UnscaledDelta * 5f);
            if (InspectView.IsOpen || Time.frameCount == InspectView.ClosedFrame) return;
            if (ChatInterlude.ReviewOpen || Time.frameCount == ChatInterlude.ClosedFrame) return;
            var m = GameInput.Menu;
            if (m.Clipboard || m.Back) { Close(); return; }
            if (m.Left) Turn(-1);
            else if (m.Right) Turn(1);
            float wheel = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
            if (Page != CasePage)
            {
                if (m.Up || wheel > 0.1f) ScrollPart(-1);
                else if (m.Down || wheel < -0.1f) ScrollPart(1);
                return;
            }
            if (m.Up || wheel > 0.1f) Move(-1);
            else if (m.Down || wheel < -0.1f) Move(1);
            else if (m.Confirm) ReadSelected();
        }
    }
}
