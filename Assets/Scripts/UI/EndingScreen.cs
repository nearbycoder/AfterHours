using System.Linq;
using TMPro;
using UnityEngine;

namespace AfterHours
{
    /// <summary>The ending: a newspaper headline, epilogue lines one by one, then the night stats.</summary>
    public class EndingScreen : Interstitial
    {
        EndingDef ending;
        StoryState state;
        TextMeshProUGUI lines, stats, hint;
        int shown;
        float nextAt, shownAt;
        bool doneLines;

        /// <summary>Read again from the title's case file: everything at once; any close key returns.</summary>
        public bool Review { get; private set; }
        static EndingScreen reviewing;
        /// <summary>The ending is open again over the title's case file.</summary>
        public static bool ReviewOpen => reviewing != null;
        /// <summary>The frame it closed on, so the key that closed it goes no further.</summary>
        public static int ClosedFrame = -1;
        /// <summary>The headline, epilogue and stats on screen (for checks).</summary>
        public string Shown => lines ? $"{ending.Headline}\n{lines.text}\n{stats.text}" : "";
        /// <summary>Every epilogue line and the stats are on screen.</summary>
        public bool AllShown => doneLines;

        public static void Show(EndingDef e)
        {
            var go = new GameObject("Ending");
            var s = go.AddComponent<EndingScreen>();
            s.ending = e;
            s.state = Story.State;
            s.Build();
            Story.State.Save();
            Records.NoteEnding(e.Id);
            PlaytestLog.Log("ending", ("id", e.Id));
            Debug.Log($"[Ending] {e.Id}: {e.Title}");
        }

        /// <summary>
        /// The ending of a finished story, read again: the same headline, epilogue and stats as
        /// when it was reached, all at once. Nothing is saved or recorded.
        /// </summary>
        public static void ShowAgain(StoryState saved, System.Action onClosed)
        {
            var copy = saved.Clone(); // resolving notes the ending on the state it reads
            var go = new GameObject("Ending");
            var s = go.AddComponent<EndingScreen>();
            s.ending = Endings.Resolve(copy);
            s.state = saved;
            s.Review = true;
            reviewing = s;
            GameRoot.Instance?.SetBlocked("ending_again", true);
            s.done = () => { s.EndReview(); onClosed?.Invoke(); };
            s.Build();
            s.shown = s.ending.Lines.Count;
            s.doneLines = true;
            s.lines.text = string.Join("\n", s.ending.Lines);
            s.stats.text = StatsLine(saved);
            s.stats.alpha = s.hint.alpha = 1f;
            s.hint.text = $"{GameInput.MenuKeyTag("E")}  close";
        }

        static string StatsLine(StoryState st)
        {
            int secrets = st.Results.Sum(r => r.Secrets), total = st.Results.Sum(r => r.SecretsTotal);
            string grades = string.Join("  ", st.Results.OrderBy(r => r.Night).Select(r => $"N{r.Night} {r.Grade}"));
            return $"SECRETS {secrets}/{total}    ·    {grades}";
        }

        void EndReview()
        {
            if (reviewing != this) return;
            reviewing = null;
            ClosedFrame = Time.frameCount;
            GameRoot.Instance?.SetBlocked("ending_again", false);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            EndReview();
        }

        void Build()
        {
            Setup("Ending", 90, new Color(0.012f, 0.016f, 0.03f, 1f));
            if (!Review) AudioDirector.Instance?.PlayMusic(ending.Id is "audit" or "spotless" ? "music_ending_warm" : "music_ending_cold", 0.55f);
            var paper = Ui.Panel(root, "Paper", Palette.Hex("E9E3D3"), 4);
            Ui.Place(paper.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(1240, 300), new Vector2(0.5f, 1f));
            paper.rectTransform.localRotation = Quaternion.Euler(0, 0, -0.8f);
            var mast = Ui.Label(paper.rectTransform, "THE MERIDIAN DAILY", UiFont.Type, 34, Palette.Hex("2A2A2A"), TextAlignmentOptions.Top);
            Ui.Place(mast.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(1200, 44), new Vector2(0.5f, 1));
            var rule = Ui.Image(paper.rectTransform, "Rule", new Color(0, 0, 0, 0.5f));
            Ui.Place(rule.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -66), new Vector2(1160, 3), new Vector2(0.5f, 1));
            var head = Ui.Label(paper.rectTransform, ending.Headline, UiFont.SansBold, 44, Palette.Hex("1A1A1A"), TextAlignmentOptions.Center);
            Ui.Place(head.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -24), new Vector2(1160, 190));
            var title = Ui.Label(root, ending.Title.ToUpperInvariant(), UiFont.Type, 64, Ui.Accent, TextAlignmentOptions.Center);
            Ui.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -400), new Vector2(1400, 80), new Vector2(0.5f, 1f));
            title.characterSpacing = 12;
            // Text size: the epilogue grows as far as all its lines fit above the stats; the stats and hint grow with it.
            float k = Settings.TextScale, canvasW = ((RectTransform)Ui.Canvas.transform).rect.width;
            float wide = k > 1f ? Mathf.Min(1400f, canvasW - 80f) : 1400f, linesH = k > 1f ? 570f - (110f + 40f * k) : 480f;
            lines = Ui.Label(root, "", UiFont.Sans, 30, Ui.Text, TextAlignmentOptions.Top, "Epilogue");
            Ui.Place(lines.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -510), new Vector2(k > 1f ? wide : 1240, linesH), new Vector2(0.5f, 1f));
            lines.lineSpacing = 4;
            lines.paragraphSpacing = 26;
            if (k > 1f)
            {
                string all = string.Join("\n", ending.Lines);
                lines.fontSize = 30 * k;
                while (lines.fontSize > 30 && lines.GetPreferredValues(all, wide, 0f).y > linesH) lines.fontSize -= 1f;
            }
            stats = Ui.Label(root, "", UiFont.Mono, 24, new Color(1f, 0.85f, 0.55f, 0.85f), TextAlignmentOptions.Center, "Stats");
            Ui.Place(stats.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 100), new Vector2(wide, 40 * k), new Vector2(0.5f, 0));
            stats.characterSpacing = 4;
            stats.alpha = 0;
            hint = Ui.Label(root, "", UiFont.SansMedium, 22, Ui.TextDim, TextAlignmentOptions.Center, "Hint");
            Ui.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(wide, 40 * k), new Vector2(0.5f, 0));
            foreach (var (t, size) in new[] { (stats, 24f), (hint, 22f) })
            {
                // Grow, but stay on one line (the stats line is long on a narrow screen).
                t.enableAutoSizing = k > 1f;
                if (k > 1f) t.textWrappingMode = TextWrappingModes.NoWrap;
                t.fontSizeMin = size;
                t.fontSizeMax = t.fontSize = size * k;
            }
            nextAt = 2.5f;
        }

        void Update()
        {
            if (Review)
            {
                // E, Enter, a click, Esc or Tab close it, back to the case file.
                var m = GameInput.Menu;
                if (done != null && age > 0.3f && (Advance() || m.Back || m.Clipboard)) Finish();
                return;
            }
            // The newest epilogue line fades in; earlier ones stay put.
            if (shown > 0)
            {
                float k = Mathf.Clamp01((age - shownAt) / 1.1f);
                var done = ending.Lines.Take(shown - 1);
                var last = $"<alpha=#{Mathf.RoundToInt(k * 255):X2}>{ending.Lines[shown - 1]}";
                lines.text = string.Join("\n", done.Append(last));
            }
            if (!doneLines && (age >= nextAt || (age > 1f && Advance())))
            {
                if (shown < ending.Lines.Count)
                {
                    shown++;
                    shownAt = age;
                    Sfx.Play("ui_page", null, 0.35f, 1f, 0.1f, AudioBus.Ui);
                    nextAt = age + 3.2f;
                }
                else
                {
                    doneLines = true;
                    nextAt = age;
                    stats.text = StatsLine(state);
                    hint.text = $"Thanks for playing.   Press {GameInput.MenuKeyTag("E")} to return to the title.";
                    Tween.Run(1.2f, k => { if (stats) { stats.alpha = k; hint.alpha = k; } }, Ease.OutCubic);
                }
            }
            else if (doneLines && age > nextAt + 1f && Advance())
            {
                Finish();
                TitleScreen.ShowTitle();
            }
        }
    }
}
