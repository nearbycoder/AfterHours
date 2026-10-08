using NUnit.Framework;
using UnityEngine;

namespace AfterHours.Tests
{
    /// <summary>The HUD text size setting.</summary>
    public class TextSizeTests
    {
        [Test]
        public void SizesScaleByAQuarterAndAHalf()
        {
            Assert.AreEqual(1f, Settings.TextScaleOf(0));
            Assert.AreEqual(1.25f, Settings.TextScaleOf(1));
            Assert.AreEqual(1.5f, Settings.TextScaleOf(2));
            Assert.AreEqual(1f, Settings.TextScaleOf(7), "an unknown value reads as Normal");
            Assert.AreEqual(3, Settings.TextSizes.Length);
        }

        [Test]
        public void OldSettingsFilesGetNormal()
        {
            var old = JsonUtility.FromJson<Settings>("{\"MouseSensitivity\":1.5,\"Captions\":false,\"Bindings\":[]}");
            Assert.AreEqual(0, old.TextSize);
            Assert.IsFalse(old.Captions);
            var s = new Settings { TextSize = 2 };
            Assert.AreEqual(2, JsonUtility.FromJson<Settings>(JsonUtility.ToJson(s)).TextSize);
        }

        [Test]
        public void PlainHandwritingSetsOnlyHandwritingInTheUiFont()
        {
            Assert.AreEqual(UiFont.Sans, Ui.Lettering(UiFont.Hand, true));
            Assert.AreEqual(UiFont.Hand, Ui.Lettering(UiFont.Hand, false), "as written keeps the handwriting");
            foreach (var kept in new[] { UiFont.Type, UiFont.Mono, UiFont.Sans, UiFont.SansMedium, UiFont.Marker })
            {
                Assert.AreEqual(kept, Ui.Lettering(kept, true), $"{kept} isn't handwriting you read");
                Assert.AreEqual(28f, Ui.LetteringSize(kept, 28f, true));
            }
            Assert.AreEqual(40f, Ui.LetteringSize(UiFont.Hand, 40f, false));
            Assert.AreEqual(40f * Ui.PlainScale, Ui.LetteringSize(UiFont.Hand, 40f, true), 1e-4f);
            Assert.That(Ui.PlainScale, Is.InRange(0.7f, 0.85f), "Fira Sans at this size takes about Caveat's width");
            Assert.AreEqual(new[] { "As written", "Plain" }, Settings.Handwriting);
        }

        [Test]
        public void OldSettingsFilesKeepHandwriting()
        {
            var old = JsonUtility.FromJson<Settings>("{\"MouseSensitivity\":1.5,\"TextSize\":2,\"Bindings\":[]}");
            Assert.IsFalse(old.PlainHandwriting);
            Assert.AreEqual(2, old.TextSize);
            var s = new Settings { PlainHandwriting = true };
            Assert.IsTrue(JsonUtility.FromJson<Settings>(JsonUtility.ToJson(s)).PlainHandwriting);
        }

        [Test]
        public void DocumentsKeepTheirSizeAtNormal()
        {
            var letter = new Vector2(820, 900);
            Assert.AreEqual(letter, InspectView.Grow(letter, 1f, false, 1920f));
            Assert.AreEqual(letter, InspectView.Grow(letter, 1f, true, 1440f));
        }

        [Test]
        public void DocumentsGrowWithinTheScreen()
        {
            foreach (float w in new[] { 1920f, 1728f, 1440f }) // 16:9, 16:10, 4:3
            foreach (float k in new[] { 1.25f, 1.5f })
            foreach (var size in new[] { new Vector2(560, 520), new Vector2(760, 820), new Vector2(820, 900), new Vector2(900, 700) })
            {
                var g = InspectView.Grow(size, k, false, w);
                Assert.LessOrEqual(g.x, w - 160f + 0.01f, $"{size} x{k} on {w}: width");
                Assert.GreaterOrEqual(g.x, size.x, $"{size} x{k} on {w}: never narrower than Normal");
                Assert.LessOrEqual(g.x, size.x * k + 0.01f);
                // Clear of the header above and the key hints below, both scaled by k.
                float top = 10f + g.y / 2f, bottom = 10f - g.y / 2f;
                Assert.LessOrEqual(top, 540f - 60f - 34f * k - 20f + 0.01f, $"{size} x{k}: clear of the header");
                Assert.GreaterOrEqual(bottom, -540f + 46f + 40f * k + 20f - 0.01f, $"{size} x{k}: clear of the hints");
                // The area for text never shrinks.
                Assert.GreaterOrEqual(g.x * g.y, size.x * size.y, $"{size} x{k} on {w}: area");
            }
            var small = InspectView.Grow(new Vector2(560, 520), 1.5f, false, 1920f);
            Assert.AreEqual(new Vector2(840, 780), small, "a sticky note grows the full 1.5x");
        }

        [Test]
        public void PicturesKeepTheirShape()
        {
            var screen = new Vector2(1100, 1100f * 10f / 16f + 36f);
            var g = InspectView.Grow(screen, 1.5f, true, 1920f);
            Assert.AreEqual(screen.x / screen.y, g.x / g.y, 0.001f);
            Assert.Greater(g.x, screen.x);
            Assert.LessOrEqual(g.y, 2f * (530f - 60f - 51f - 20f) + 0.01f);
        }
    }
}
