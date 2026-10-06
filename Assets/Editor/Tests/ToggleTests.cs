using NUnit.Framework;
using UnityEngine;

namespace AfterHours.Tests
{
    /// <summary>Hold or toggle for crouch and brisk walk.</summary>
    public class ToggleTests
    {
        [Test]
        public void HoldFollowsTheButton()
        {
            var l = new ToggleLatch();
            Assert.IsFalse(l.Step(false, false));
            Assert.IsTrue(l.Step(true, false));
            Assert.IsTrue(l.Step(true, false));
            Assert.IsFalse(l.Step(false, false));
        }

        [Test]
        public void TogglePressesOnceToSwitchOnAndAgainToSwitchOff()
        {
            var l = new ToggleLatch();
            Assert.IsTrue(l.Step(true, true), "press: on");
            Assert.IsTrue(l.Step(true, true), "still held: still on");
            Assert.IsTrue(l.Step(false, true), "released: stays on");
            Assert.IsFalse(l.Step(true, true), "second press: off");
            Assert.IsFalse(l.Step(false, true));
        }

        [Test]
        public void PressesWhileAMenuIsOpenAreIgnored()
        {
            var l = new ToggleLatch();
            Assert.IsFalse(l.Step(true, true, allowed: false));
            Assert.IsFalse(l.Step(false, true));
            Assert.IsTrue(l.Step(true, true));
        }

        [Test]
        public void ToggledBriskWalkEndsWhenYouStop()
        {
            var l = new ToggleLatch();
            l.Step(true, true);
            Assert.IsTrue(l.StopWhenIdle(false), "switched on while standing: waits for you to move");
            l.Step(false, true);
            Assert.IsTrue(l.StopWhenIdle(true));
            Assert.IsFalse(l.StopWhenIdle(false), "stopping switches it off");
            Assert.IsFalse(l.Step(false, true));
            Assert.IsTrue(l.Step(true, true), "the next press switches it on again");
        }

        [Test]
        public void OldSettingsFilesHold()
        {
            var old = JsonUtility.FromJson<Settings>("{\"MouseSensitivity\":1.5,\"Quality\":1,\"Bindings\":[]}");
            Assert.IsFalse(old.ToggleCrouch);
            Assert.IsFalse(old.ToggleSprint);
            var s = new Settings { ToggleCrouch = true };
            Assert.IsTrue(JsonUtility.FromJson<Settings>(JsonUtility.ToJson(s)).ToggleCrouch);
        }
    }
}
