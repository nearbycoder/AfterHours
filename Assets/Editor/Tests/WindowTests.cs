using NUnit.Framework;

namespace AfterHours.Tests
{
    /// <summary>Leaving fullscreen opens a window that fits on the screen, title bar and all.</summary>
    public class WindowTests
    {
        [Test]
        public void LeavingFullscreenOpensAWindowThatFits()
        {
            Assert.AreEqual((1280, 720), Settings.WindowedSize(1600, 900));
            Assert.AreEqual((1536, 864), Settings.WindowedSize(1920, 1080));
            Assert.AreEqual((3072, 1728), Settings.WindowedSize(3840, 2160));
            foreach (var (w, h) in new[] { (1280, 800), (1366, 768), (1024, 768), (2560, 1080), (3440, 1440) })
            {
                var (ww, wh) = Settings.WindowedSize(w, h);
                // Room for a title bar (about 30 px) and a panel (about 45 px), and even sizes.
                Assert.IsTrue(ww <= w - 100 && wh <= h - 75 && ww % 2 == 0 && wh % 2 == 0, $"{w}x{h} -> {ww}x{wh}");
                Assert.AreEqual((float)w / h, (float)ww / wh, 0.01f, "the screen's shape");
            }
        }
    }
}
