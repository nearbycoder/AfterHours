using NUnit.Framework;

namespace AfterHours.Tests
{
    /// <summary>The shift grade and the report's explanation of it.</summary>
    public class GradingTests
    {
        static GradeParts P(int req, int reqTotal, int opt, int optTotal, float clean) =>
            new() { ReqDone = req, ReqTotal = reqTotal, OptDone = opt, OptTotal = optTotal, Clean = clean };

        [Test]
        public void GradesMatchTheOldFormula()
        {
            // The formula NightDirector used before the grading moved here.
            for (int req = 0; req <= 6; req++)
            for (int opt = 0; opt <= 2; opt++)
            for (int c = 0; c <= 20; c++)
            {
                float clean = c / 20f;
                float score = req / 6f * 0.6f + opt / 2f * 0.15f + clean * 0.25f;
                string old = req == 6 && score >= 0.97f ? "S" : score >= 0.9f ? "A" : score >= 0.75f ? "B" : "C";
                Assert.AreEqual(old, Grading.Grade(P(req, 6, opt, 2, clean)), $"req {req} opt {opt} clean {clean}");
            }
            Assert.AreEqual("S", Grading.Grade(P(5, 5, 0, 0, 1f)), "no bonus tasks count as all done");
        }

        [Test]
        public void SummaryShowsTheThreeParts()
        {
            Assert.AreEqual("Shift sheet 6/6  ·  bonus 1/2  ·  surfaces 94% clean", Grading.Summary(P(6, 6, 1, 2, 0.948f)));
            Assert.AreEqual("Shift sheet 5/5  ·  surfaces 100% clean", Grading.Summary(P(5, 5, 0, 0, 1f)));
            Assert.AreEqual("surfaces 99% clean", Grading.Summary(P(1, 1, 0, 0, 0.996f)).Split('·')[1].Trim(), "never rounds up to a spotless 100%");
        }

        [Test]
        public void AnSNeedsNoHint() => Assert.IsNull(Grading.Hint(P(6, 6, 2, 2, 0.9f)));

        [Test]
        public void HintsNameWhatFellShort()
        {
            // Sheet done, half the bonus, 95% clean: 0.6 + 0.075 + 0.2375 = 0.9125 (A). The bonus
            // alone reaches S; cleaning alone can't (0.925 at 100%).
            Assert.AreEqual("For an S: the bonus tasks.", Grading.Hint(P(6, 6, 1, 2, 0.95f)));
            // Bonus done, 85% clean: 0.9625 (A). Surfaces at 88% would do it.
            Assert.AreEqual("For an S: surfaces 88% clean.", Grading.Hint(P(6, 6, 2, 2, 0.85f)));
            // Half the bonus, 99% clean: 0.9225. Either the bonus, or... cleaning can't reach it.
            Assert.AreEqual("For an S: the bonus tasks.", Grading.Hint(P(6, 6, 1, 2, 0.99f)));
            // No bonus at all and 70% clean: the bonus alone gives 0.925, so both are needed.
            Assert.AreEqual("For an S: the bonus tasks and surfaces 88% clean.", Grading.Hint(P(4, 4, 0, 1, 0.7f)));
            // A night with no bonus tasks only ever asks for cleaner surfaces.
            Assert.AreEqual("For an S: surfaces 88% clean.", Grading.Hint(P(5, 5, 0, 0, 0.8f)));
        }

        [Test]
        public void TheSheetComesFirst()
        {
            // Everything else is spotless: only the sheet is missing.
            Assert.AreEqual("For an S: finish the shift sheet.", Grading.Hint(P(5, 6, 2, 2, 1f)));
            Assert.AreEqual("For an S: finish the shift sheet, then the bonus tasks.", Grading.Hint(P(5, 6, 0, 2, 0.95f)));
        }

        [Test]
        public void EveryHintActuallyReachesAnS()
        {
            for (int opt = 0; opt <= 2; opt++)
            for (int c = 0; c <= 100; c++)
            {
                var p = P(6, 6, opt, 2, c / 100f);
                string hint = Grading.Hint(p);
                if (hint == null) { Assert.AreEqual("S", Grading.Grade(p)); continue; }
                // Apply the advice: the bonus, and/or the named cleanliness.
                var fixedUp = p;
                if (hint.Contains("bonus") && !hint.Contains(", or ")) fixedUp.OptDone = 2;
                int at = hint.IndexOf("surfaces ");
                if (at >= 0 && !(hint.Contains("bonus") && hint.Contains(", or ")))
                    fixedUp.Clean = System.Math.Max(fixedUp.Clean, int.Parse(hint.Substring(at + 9).Split('%')[0]) / 100f);
                if (hint.Contains(", or ")) fixedUp.OptDone = 2; // either alternative is enough; check the first
                Assert.AreEqual("S", Grading.Grade(fixedUp), $"{hint} (from opt {opt}, clean {c}%)");
            }
        }
    }
}
