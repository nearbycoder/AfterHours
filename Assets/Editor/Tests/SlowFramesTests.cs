using NUnit.Framework;

namespace AfterHours.Tests
{
    /// <summary>"Running slowly": when a night's frame times call for a lower setting, and which one.</summary>
    public class SlowFramesTests
    {
        static SlowFrames Feed(float seconds, float frameMs, SlowFrames f = null)
        {
            f ??= new SlowFrames();
            for (float t = 0; t < seconds; t += frameMs / 1000f) f.Add(frameMs / 1000f);
            return f;
        }

        [Test]
        public void SlowOnlyAfterTheWarmUpAndAWholeWindow()
        {
            var f = Feed(SlowFrames.Warmup + SlowFrames.Window - 1f, 50f);
            Assert.IsFalse(f.Slow, "not before a whole window has been measured");
            Feed(1.5f, 50f, f);
            Assert.IsTrue(f.Slow, "20 fps over a whole window is slow");
            Assert.AreEqual(20f, f.Fps, 0.5f);
            f.Reset();
            Assert.IsFalse(f.Slow, "a reset starts over, warm-up and all");
            Feed(SlowFrames.Warmup, 50f, f);
            Assert.IsFalse(f.Full);
        }

        [Test]
        public void ThirtyFpsFromVSyncAndShortHitchesAreNotSlow()
        {
            Assert.IsFalse(Feed(30f, 16.7f).Slow, "60 fps");
            Assert.IsFalse(Feed(30f, 33.4f).Slow, "VSync halving a 60 Hz screen to 30 fps");
            Assert.IsTrue(Feed(30f, 40f).Slow, "25 fps");
            // Mostly fast, with a hitch every second: the median is the fast frames.
            var f = new SlowFrames();
            for (int s = 0; s < 30; s++) { Feed(0.9f, 16.7f, f); f.Add(0.1f); }
            Assert.IsFalse(f.Slow, "a hitch a second doesn't make it slow");
            // Single frames over a second (a load, a stall) aren't counted at all.
            var g = Feed(SlowFrames.Warmup + 1f, 16.7f);
            for (int i = 0; i < 40; i++) g.Add(2f);
            Assert.IsFalse(g.Full, "long stalls don't fill the window");
        }

        [Test]
        public void TheRateNamedIsTheRecentOne()
        {
            // Fast for a while, then 20 fps: by the time the median says slow, the window still
            // holds fast frames, but the rate named is what's on screen now.
            var f = Feed(SlowFrames.Warmup + SlowFrames.Window, 16.7f);
            for (int i = 0; i < 400 && !f.Slow; i++) f.Add(0.05f);
            Assert.IsTrue(f.Slow);
            Assert.AreEqual(20f, f.RecentFps(), 0.5f);
        }

        [Test]
        public void TheWindowRollsOn()
        {
            var f = Feed(SlowFrames.Warmup + 20f, 50f);
            Assert.IsTrue(f.Slow);
            Feed(SlowFrames.Window, 16.7f, f);
            Assert.IsFalse(f.Slow, "once it's fast again for a window, it isn't slow");
        }

        [Test]
        public void StepsGoDownThePresetThenTheRenderScale()
        {
            Assert.AreEqual((1, 1f), SlowFrames.NextStep(2, 1f));
            Assert.AreEqual((0, 0.9f), SlowFrames.NextStep(1, 0.9f), "the render scale is left alone while there's a lower preset");
            Assert.AreEqual((0, 0.75f), SlowFrames.NextStep(0, 1f));
            Assert.AreEqual((0, 0.5f), SlowFrames.NextStep(0, 0.75f));
            Assert.AreEqual((0, 0.5f), SlowFrames.NextStep(0, 0.6f));
            Assert.IsNull(SlowFrames.NextStep(0, 0.5f), "nothing lower than Low at 50%");
        }

        [Test]
        public void WorthWatchingOnlyWhenThereIsSomethingToOffer()
        {
            var s = new Settings();
            Assert.IsTrue(SlowFrames.Worth(s), "the defaults (High, 100%)");
            s.FrameCap = 30;
            Assert.IsFalse(SlowFrames.Worth(s), "a 30 fps limit the player chose");
            s.FrameCap = 60;
            Assert.IsTrue(SlowFrames.Worth(s));
            s.SlowFramesDeclined = true;
            Assert.IsFalse(SlowFrames.Worth(s), "after Keep, never again");
            s = new Settings { Quality = 0, RenderScale = 0.5f };
            Assert.IsFalse(SlowFrames.Worth(s), "nothing lower to offer");
            Assert.IsFalse(new Settings().SlowFramesDeclined, "a new or old settings file hasn't declined");
        }
    }
}
