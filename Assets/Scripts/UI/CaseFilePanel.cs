using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AfterHours
{
    /// <summary>
    /// The case file from the title: the same list as the clipboard's (documents read and the
    /// morning chats, newest night first), as a menu that scrolls, built from the save on disk.
    /// So the story can be looked back over between sessions and after the ending, Night 7
    /// and the ending itself included. Reading never changes the story or the save.
    /// </summary>
    public class CaseFilePanel : MonoBehaviour
    {
        static CaseFilePanel instance;
        public static bool IsOpen => instance != null;
        /// <summary>The scrolling list of entries (for checks).</summary>
        public static ScrollFollow List => instance != null ? instance.list : null;
        /// <summary>The entry ids in list order (for checks).</summary>
        public static IReadOnlyList<string> Entries => instance != null ? instance.entries : new List<string>();

        RectTransform root, panel;
        CanvasGroup group;
        ScrollFollow list;
        TextMeshProUGUI hint;
        StoryState state;
        readonly List<string> entries = new();
        float hintRefresh;

        /// <summary>A document or a morning's chat is open over the list.</summary>
        public static bool Reading => InspectView.IsOpen || ChatInterlude.ReviewOpen || EndingScreen.ReviewOpen;

        /// <summary>Whether a saved story has anything to show (the title offers the button only then).</summary>
        public static bool HasEntries(StoryState s) => s != null && CaseFile.Entries(s).Count > 0;

        public static void Show(StoryState s)
        {
            if (instance || s == null) return;
            var rt = Ui.Layer("CaseFile", 57);
            instance = rt.gameObject.AddComponent<CaseFilePanel>();
            instance.root = rt;
            instance.state = s;
            instance.Build();
            MenuFade.In(rt);
        }

        void Build()
        {
            GameRoot.Instance.SetBlocked("casefile", true, true);
            group = root.GetComponent<CanvasGroup>();
            var dim = Ui.Image(root, "Dim", new Color(0.01f, 0.015f, 0.03f, 0.93f));
            Ui.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            // One column of rows at the text size, as Settings is at the larger sizes.
            float k = Settings.TextScale;
            float canvasW = ((RectTransform)Ui.Canvas.transform).rect.width;
            float W = Mathf.Min(canvasW - 80f, 900f * k + 160f), H = 1000f;
            float rowW = Mathf.Min(900f, (W - 140f) / k);
            panel = Ui.Panel(root, "Panel", new Color(0.07f, 0.09f, 0.13f, 1f), 22).rectTransform;
            Ui.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, H));
            var title = Ui.Label(panel, "Case file", UiFont.SansBold, 44, Ui.Text, TextAlignmentOptions.TopLeft, "Title");
            Ui.Place(title.rectTransform, new Vector2(0, 1), new Vector2(60, -36), new Vector2(700, 60), new Vector2(0, 1));
            var sub = Ui.Label(panel, CaseFile.Summary(state) + (string.IsNullOrEmpty(state.Ending) ? "" : ", the whole story"), UiFont.Sans, 22 * k, Ui.TextDim, TextAlignmentOptions.TopLeft, "Subtitle");
            Ui.Place(sub.rectTransform, new Vector2(0, 1), new Vector2(62, -100), new Vector2(W - 120f, 34f * k), new Vector2(0, 1));
            float listTop = 100f + 34f * k + 16f;
            var col = Widgets.ScrollList(panel, "List", new Vector2(60 - ScrollFollow.Inset, -listTop), new Vector2(W - 140f + 2 * ScrollFollow.Inset, H - listTop - 150f), k, 6, out list);

            var rows = new List<Selectable>();
            int last = -1;
            foreach (var (night, id) in CaseFile.Entries(state))
            {
                if (night != last)
                {
                    last = night;
                    var nd = NightDefs.Get(night);
                    Widgets.Heading(col, night == CaseFile.EndingNight ? "The ending" : $"Night {night}{(nd != null ? " · " + nd.Day : "")}", rowW);
                }
                var (name, note) = CaseFile.Describe(state, id);
                string label = note != null ? $"{name}   <size=75%><color=#FFFFFF80>{note}</color></size>" : name;
                string entry = id;
                var row = Widgets.Button(col, label, () => Read(entry), rowW, 56, 26);
                row.name = "Entry_" + id;
                var text = row.GetComponentInChildren<TextMeshProUGUI>();
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Ellipsis;
                rows.Add(row.GetComponent<Selectable>());
                entries.Add(id);
            }

            hint = Ui.Label(panel, "", UiFont.SansMedium, 22, Ui.TextDim, TextAlignmentOptions.MidlineLeft, "Hint");
            Ui.Place(hint.rectTransform, new Vector2(0, 0), new Vector2(60, 40), new Vector2((W - 120f - 220f * k - 40f) / k, 60), new Vector2(0, 0));
            hint.rectTransform.localScale = Vector3.one * k;
            RefreshHint();
            var back = Widgets.Button(panel, "Back", Close, 220, 60, 28, true);
            Ui.Place(back, new Vector2(1, 0), new Vector2(-60, 40), new Vector2(220, 60), new Vector2(1, 0));
            back.localScale = Vector3.one * k;
            var focus = MenuFocus.AttachAll(panel.gameObject);
            list.Focus = focus;
            // The first entry of a night brings its heading into view with it.
            list.Above = row =>
            {
                int i = row.GetSiblingIndex();
                var prev = i > 0 ? (RectTransform)row.parent.GetChild(i - 1) : null;
                return prev != null && prev.GetComponent<Selectable>() == null ? prev : null;
            };
            focus.Chain = rows;
            focus.After.Add(back.GetComponent<Selectable>());
            Sfx.Play("ui_page", null, 0.5f, 1f, 0.1f, AudioBus.Ui);
        }

        void RefreshHint()
        {
            string text = $"{GameInput.MenuKeyTag("Enter")}  Read again      {GameInput.MenuKeyTag("Esc")}  Back";
            if (hint.text != text) hint.text = text;
        }

        void Read(string id)
        {
            if (Reading || Time.frameCount == InspectView.ClosedFrame || Time.frameCount == ChatInterlude.ClosedFrame || Time.frameCount == EndingScreen.ClosedFrame) return;
            if (id == CaseFile.EndingId)
            {
                Sfx.Play("ui_page", null, 0.5f);
                EndingScreen.ShowAgain(state, null);
                return;
            }
            int night = CaseFile.ChatNight(id);
            if (night >= 0)
            {
                Sfx.Play("notify", null, 0.3f);
                ChatInterlude.ShowAgain(CaseFile.MorningLabel(night), CaseFile.MorningLines(state, night), null);
                return;
            }
            var d = Docs.Get(id);
            if (d == null) return;
            Sfx.Play(d.Style == DocStyle.Screen ? "ui_click" : "ui_page", null, 0.5f);
            InspectView.Show(d, InspectMode.Read, null, reread: true, overMenus: true);
        }

        void Close()
        {
            if (Reading) return;
            GameRoot.Instance.SetBlocked("casefile", false);
            instance = null;
            MenuFade.Out(root);
        }

        void Update()
        {
            // A document or chat read again has the keys until the frame after it closes, so the
            // press that closes it doesn't also open the row behind it or close the list.
            bool fresh = Time.frameCount > InspectView.ClosedFrame && Time.frameCount > ChatInterlude.ClosedFrame && Time.frameCount > EndingScreen.ClosedFrame;
            bool reading = Reading;
            group.interactable = !reading && fresh;
            list.enabled = !reading;
            if ((hintRefresh -= GameTime.UnscaledDelta) <= 0f) { hintRefresh = 0.25f; RefreshHint(); }
            if (!reading && fresh && GameInput.Menu.Back) Close();
        }
    }
}
