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
    }
}
