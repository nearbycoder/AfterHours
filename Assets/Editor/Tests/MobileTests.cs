using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace AfterHours.Tests
{
    /// <summary>
    /// The browser build on phones and tablets: grime patterns made in reused buffers come out the
    /// same as before, and what the game tells the page for the on-screen controls is valid JSON.
    /// </summary>
    public class MobileTests
    {
        static GrimeSpec Spec(int seed) => new GrimeSpec().WithSeed(seed).Add(GrimeStamp.Dust()).Add(GrimeStamp.Ring(0.4f, 0.6f))
            .Add(GrimeStamp.Steps(0.1f, 0.1f, 0.9f, 0.9f)).Add(GrimeStamp.Trail(0.3f, 0.5f, new Vector2(0, 0), new Vector2(1, 1)));

        [Test]
        public void ScratchPatternsMatchFreshOnes()
        {
            var scratch = new GrimePatternData.Scratch();
            // A larger pattern first, so the reused buffers hold leftovers from it.
            GrimePatternData.Generate(Spec(9), new Vector2(3, 2), 300, 200, scratch);
            var fresh = GrimePatternData.Generate(Spec(4), new Vector2(2, 1), 200, 100);
            var reused = GrimePatternData.Generate(Spec(4), new Vector2(2, 1), 200, 100, scratch);
            Assert.IsNull(reused.Pixels);
            using var px = new NativeArray<Color32>(200 * 100, Allocator.Temp);
            reused.WritePixels(px);
            for (int i = 0; i < 200 * 100; i++)
            {
                Assert.AreEqual(fresh.Coverage[i], reused.Coverage[i], 1e-6f, "coverage " + i);
                Assert.AreEqual(fresh.Toughness[i], reused.Toughness[i], 1e-6f, "toughness " + i);
                Assert.AreEqual(fresh.Pixels[i], px[i], "pixel " + i);
            }
        }

        [Test]
        public void ScratchIsKeptOnlyWhileHeld()
        {
            var scratch = new GrimePatternData.Scratch();
            scratch.Holds++;
            var a = GrimePatternData.Generate(Spec(1), new Vector2(1, 1), 64, 64, scratch);
            scratch.Release();
            var b = GrimePatternData.Generate(Spec(2), new Vector2(1, 1), 32, 32, scratch);
            Assert.AreSame(a.Coverage, b.Coverage, "a held batch reuses its buffers");
            scratch.Holds--;
            scratch.Release();
            var c = GrimePatternData.Generate(Spec(3), new Vector2(1, 1), 32, 32, scratch);
            Assert.AreNotSame(a.Coverage, c.Coverage, "released buffers are made again");
        }

        [Test]
        public void TouchReportIsJson()
        {
            var json = TouchInput.Describe(true, new[] { ("LMB", "Wipe"), ("E", "Pick up · \"Crumpled\" note <b>now</b>"), ("", "skipped") });
            Assert.DoesNotThrow(() => JsonUtility.FromJson<Shape>(json), json);
            var s = JsonUtility.FromJson<Shape>(json);
            Assert.IsTrue(s.play);
            StringAssert.Contains("\"LMB\":\"Wipe\"", json);
            StringAssert.Contains("Pick up · \\\"Crumpled\\\" note now", json, "tags stripped, quotes escaped");
            StringAssert.DoesNotContain("skipped", json);
            StringAssert.Contains("\"keys\":[]", json, "no soft keys while playing");
        }

        [System.Serializable]
        class Shape { public bool play, holding, torch; }

        [Test]
        public void TouchGlyphsNameTheButtons()
        {
            Assert.AreEqual("Use", TouchInput.Glyph("E"));
            Assert.AreEqual("Spray", TouchInput.Glyph("RMB"));
            Assert.AreEqual("Sheet", TouchInput.Glyph(Act.Clipboard));
            Assert.AreEqual("Clean", TouchInput.Glyph("LMB"), "nothing held");
            Assert.AreEqual("Back", TouchInput.Glyph("Esc"));
        }

        [Test]
        public void DesktopDefaultsAreUnchanged()
        {
            Assert.IsFalse(WebPlatform.Mobile, "outside the browser there's no phone");
            Assert.AreEqual(1f, WebPlatform.PatternScale);
            Assert.AreEqual(1, WebPlatform.DefaultQuality);
        }
    }
}
