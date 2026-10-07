using NUnit.Framework;
using UnityEngine;

namespace AfterHours.Tests
{
    /// <summary>The brightness setting and the page offered on first launch.</summary>
    public class BrightnessTests
    {
        [Test]
        public void TheDefaultChangesNothing()
        {
            Assert.AreEqual(0.5f, new Settings().Brightness);
            Assert.AreEqual(0f, PostFx.GammaFor(0.5f));
        }

        [Test]
        public void TheSliderDarkensAndLiftsInOrder()
        {
            Assert.Less(PostFx.GammaFor(0f), 0f);
            Assert.Greater(PostFx.GammaFor(1f), 0f);
            float last = float.MinValue;
            for (int i = 0; i <= 20; i++)
            {
                float g = PostFx.GammaFor(i / 20f);
                Assert.Greater(g, last, $"step {i}");
                last = g;
            }
            Assert.AreEqual(PostFx.GammaFor(1f), PostFx.GammaFor(3f), "out-of-range values are clamped");
        }

        [Test]
        public void TheValueReadsAsStepsAroundZero()
        {
            Assert.AreEqual("0", BrightnessPanel.Format(0.5f));
            Assert.AreEqual("+10", BrightnessPanel.Format(1f));
            Assert.AreEqual("−10", BrightnessPanel.Format(0f));
            Assert.AreEqual("+1", BrightnessPanel.Format(0.55f));
        }

        [Test]
        public void OldSettingsFilesKeepTheLookAndSeeThePageOnce()
        {
            var old = JsonUtility.FromJson<Settings>("{\"MouseSensitivity\":1.5,\"Quality\":1,\"ToggleCrouch\":true,\"Bindings\":[]}");
            Assert.AreEqual(0.5f, old.Brightness);
            Assert.IsTrue(old.ToggleCrouch);
            Assert.IsTrue(BrightnessPanel.OfferOnLaunch(old, automated: false), "players from before this setting see the page once");
            Assert.IsFalse(BrightnessPanel.OfferOnLaunch(old, automated: true), "automated runs never see it");
            old.BrightnessChecked = true;
            old.Brightness = 0.8f;
            var back = JsonUtility.FromJson<Settings>(JsonUtility.ToJson(old));
            Assert.AreEqual(0.8f, back.Brightness, 1e-6f);
            Assert.IsFalse(BrightnessPanel.OfferOnLaunch(back, automated: false), "once closed, it isn't offered again");
        }
    }
}
