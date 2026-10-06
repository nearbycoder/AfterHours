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
            lines = Ui.Label(root, "", UiFont.Sans, 30, Ui.Text, TextAlignmentOptions.Top);
            Ui.Place(lines.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -510), new Vector2(1240, 480), new Vector2(0.5f, 1f));
            lines.lineSpacing = 4;
            lines.paragraphSpacing = 26;
            stats = Ui.Label(root, "", UiFont.Mono, 24, new Color(1f, 0.85f, 0.55f, 0.85f), TextAlignmentOptions.Center);
            Ui.Place(stats.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 100), new Vector2(1400, 40), new Vector2(0.5f, 0));
            stats.characterSpacing = 4;
            stats.alpha = 0;
            hint = Ui.Label(root, "", UiFont.SansMedium, 22, Ui.TextDim, TextAlignmentOptions.Center);
            Ui.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1400, 40), new Vector2(0.5f, 0));
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
                    hint.text = $"Thanks for playing.   Press {GameInput.KeyTag("E")} to return to the title.";
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
