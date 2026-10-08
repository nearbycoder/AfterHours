using NUnit.Framework;
using UnityEngine;

namespace AfterHours.Tests
{
    /// <summary>Mono audio: the downmix, and settings files from before it existed.</summary>
    public class MonoAudioTests
    {
        [Test]
        public void StereoFoldsToTheAverageOnBothSides()
        {
            var d = new[] { 0f, 0.8f, 0.5f, -0.5f, 0.2f, 0.2f };
            MonoMix.Downmix(d, 2);
            CollectionAssert.AreEqual(new[] { 0.4f, 0.4f, 0f, 0f, 0.2f, 0.2f }, d);
        }

        [Test]
        public void MoreChannelsFoldTheSameWayAndMonoIsLeftAlone()
        {
            var six = new[] { 0.6f, 0f, 0f, 0f, 0f, 0f };
            MonoMix.Downmix(six, 6);
            foreach (var v in six) Assert.AreEqual(0.1f, v, 1e-6f);
            var one = new[] { 0.3f, -0.2f };
            MonoMix.Downmix(one, 1);
            CollectionAssert.AreEqual(new[] { 0.3f, -0.2f }, one);
        }

        [Test]
        public void OldSettingsFilesLoadWithMonoOff()
        {
            Assert.IsFalse(new Settings().MonoAudio);
            var old = JsonUtility.FromJson<Settings>("{\"MouseSensitivity\":1.5,\"Captions\":false,\"Bindings\":[]}");
            Assert.IsFalse(old.MonoAudio);
            Assert.AreEqual(1.5f, old.MouseSensitivity);
            old.MonoAudio = true;
            Assert.IsTrue(JsonUtility.FromJson<Settings>(JsonUtility.ToJson(old)).MonoAudio);
        }
    }
}
