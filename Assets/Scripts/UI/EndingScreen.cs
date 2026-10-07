using System.Linq;
using TMPro;
using UnityEngine;

namespace AfterHours
{
    /// <summary>The ending: a newspaper headline, epilogue lines one by one, then the night stats.</summary>
    public class EndingScreen : Interstitial
    {
        EndingDef ending;
        TextMeshProUGUI lines, stats, hint;
        int shown;
        float nextAt, shownAt;
        bool doneLines;

        public static void Show(EndingDef e)
        {
            var go = new GameObject("Ending");
            var s = go.AddComponent<EndingScreen>();
            s.ending = e;
            s.Build();
            Story.State.Save();
            Records.NoteEnding(e.Id);
            PlaytestLog.Log("ending", ("id", e.Id));
            Debug.Log($"[Ending] {e.Id}: {e.Title}");
        }

        void Build()
        {
            Setup("Ending", 90, new Color(0.012f, 0.016f, 0.03f, 1f));
            AudioDirector.Instance?.PlayMusic(ending.Id is "audit" or "spotless" ? "music_ending_warm" : "music_ending_cold", 0.55f);
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
                    int secrets = Story.State.Results.Sum(r => r.Secrets), total = Story.State.Results.Sum(r => r.SecretsTotal);
                    string grades = string.Join("  ", Story.State.Results.OrderBy(r => r.Night).Select(r => $"N{r.Night} {r.Grade}"));
                    stats.text = $"SECRETS {secrets}/{total}    ·    {grades}";
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
