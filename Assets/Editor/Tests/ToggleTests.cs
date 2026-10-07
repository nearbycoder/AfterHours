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
        public void ToggledCleanStaysOnUntilTheNextPress()
        {
            var l = new UseLatches();
            Assert.AreEqual((true, false), l.Step(true, false, true), "tap: cleaning");
            Assert.AreEqual((true, false), l.Step(false, false, true), "let go: still cleaning");
            Assert.AreEqual((true, false), l.Step(false, false, true));
            Assert.AreEqual((false, false), l.Step(true, false, true), "tap again: stops");
            Assert.AreEqual((false, false), l.Step(false, false, true));
        }

        [Test]
        public void ToggledCleanAndSprayAreOneSwitch()
        {
            var l = new UseLatches();
            l.Step(true, false, true);
            l.Step(false, false, true);
            Assert.AreEqual((false, true), l.Step(false, true, true), "spray while cleaning: spraying instead");
            l.Step(false, false, true);
            Assert.AreEqual((true, false), l.Step(true, false, true), "clean while spraying: cleaning instead");
            l.Step(false, false, true);
            l.Reset();
            Assert.AreEqual((false, false), l.Step(false, false, true), "picking something up or a menu switches it off");
        }

        [Test]
        public void HeldCleanAndSprayFollowTheButtons()
        {
            var l = new UseLatches();
            Assert.AreEqual((true, true), l.Step(true, true, false), "held, both at once, as before");
            Assert.AreEqual((false, false), l.Step(false, false, false));
            Assert.AreEqual((false, false), l.Step(true, false, true, allowed: false), "a press during a menu doesn't latch");
        }

        [Test]
        public void OldSettingsFilesHold()
        {
            var old = JsonUtility.FromJson<Settings>("{\"MouseSensitivity\":1.5,\"Quality\":1,\"Bindings\":[]}");
            Assert.IsFalse(old.ToggleCrouch);
            Assert.IsFalse(old.ToggleSprint);
            Assert.IsFalse(old.ToggleUse);
            var s = new Settings { ToggleCrouch = true };
            Assert.IsTrue(JsonUtility.FromJson<Settings>(JsonUtility.ToJson(s)).ToggleCrouch);
        }
    }
}
