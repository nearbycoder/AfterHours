using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    /// <summary>What a night's grade is made of.</summary>
    public struct GradeParts
    {
        public int ReqDone, ReqTotal, OptDone, OptTotal;
        /// <summary>Average completion of every dirty surface tonight, 0 to 1.</summary>
        public float Clean;
    }

    /// <summary>
    /// The shift grade: the sheet's required tasks count 60%, bonus tasks 15% and how clean every
    /// surface is 25%. S needs every required task and 97%; A 90%, B 75%, otherwise C. The shift
    /// report explains a grade with the same numbers.
    /// </summary>
    public static class Grading
    {
        public const float S = 0.97f, A = 0.9f, B = 0.75f;
        const float ReqWeight = 0.6f, OptWeight = 0.15f, CleanWeight = 0.25f;

        static float Frac(int done, int total) => total > 0 ? done / (float)total : 1f;

        public static float Score(GradeParts p) => Score(Frac(p.ReqDone, p.ReqTotal), Frac(p.OptDone, p.OptTotal), p.Clean);

        static float Score(float req, float opt, float clean) => req * ReqWeight + opt * OptWeight + clean * CleanWeight;

        public static string Grade(GradeParts p)
        {
            float s = Score(p);
            return p.ReqDone >= p.ReqTotal && s >= S ? "S" : s >= A ? "A" : s >= B ? "B" : "C";
        }

        /// <summary>"Shift sheet 6/6 · bonus 1/2 · surfaces 94% clean".</summary>
        public static string Summary(GradeParts p)
        {
            var parts = new List<string> { $"Shift sheet {p.ReqDone}/{p.ReqTotal}" };
            if (p.OptTotal > 0) parts.Add($"bonus {p.OptDone}/{p.OptTotal}");
            parts.Add($"surfaces {Percent(p.Clean)}% clean");
            return string.Join("  ·  ", parts);
        }

        /// <summary>Shown percentages round down, so 99.6% never reads as a spotless 100%.</summary>
        static int Percent(float f) => Mathf.FloorToInt(Mathf.Clamp01(f) * 100f + 1e-4f);

        /// <summary>The lowest whole percentage of clean surfaces that makes an S with the sheet done; 101 if none does.</summary>
        static int CleanNeeded(float opt)
        {
            for (int k = 0; k <= 100; k++)
                if (Score(1f, opt, k / 100f) >= S) return k;
            return 101;
        }

        /// <summary>
        /// What would have made it an S, in as few changes as the numbers allow; null for an S.
        /// The finished sheet is always needed; after that, the bonus tasks or cleaner surfaces,
        /// whichever is enough on its own, or both.
        /// </summary>
        public static string Hint(GradeParts p)
        {
            if (Grade(p) == "S") return null;
            float opt = Frac(p.OptDone, p.OptTotal);
            bool sheet = p.ReqDone < p.ReqTotal;
            bool bonusLeft = p.OptDone < p.OptTotal;
            var needs = new List<string>();
            if (sheet) needs.Add("finish the shift sheet");
            if (Score(1f, opt, p.Clean) < S)
            {
                // The cleanest surfaces needed (whole percent) with the bonus as it was, and with all of it.
                int cleanAsIs = CleanNeeded(opt), cleanWithBonus = CleanNeeded(1f);
                bool bonusEnough = bonusLeft && Score(1f, 1f, p.Clean) >= S;
                bool cleanEnough = cleanAsIs <= 100;
                if (bonusEnough && cleanEnough) needs.Add($"the bonus tasks, or surfaces {cleanAsIs}% clean");
                else if (bonusEnough) needs.Add("the bonus tasks");
                else if (cleanEnough) needs.Add($"surfaces {cleanAsIs}% clean");
                else needs.Add($"the bonus tasks and surfaces {cleanWithBonus}% clean");
            }
            return "For an S: " + string.Join(", then ", needs) + ".";
        }
    }
}
