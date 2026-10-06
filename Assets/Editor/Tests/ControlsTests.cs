using System;
using NUnit.Framework;
using UnityEngine;

namespace AfterHours.Tests
{
    /// <summary>Key bindings: defaults, swapping on conflict, reserved keys, saving and old settings files.</summary>
    public class ControlsTests
    {
        [Test]
        public void EveryActionHasADefaultAndALabel()
        {
            foreach (Act a in Enum.GetValues(typeof(Act)))
            {
                Assert.IsTrue(Controls.Defaults.ContainsKey(a), $"{a} has a default");
                Assert.IsTrue(Array.Exists(Controls.Labels, x => x.act == a), $"{a} has a label");
            }
            // No two actions share a default key.
            var s = new Settings();
            foreach (Act a in Enum.GetValues(typeof(Act)))
                Assert.AreEqual(a, Controls.ActionOn(s, Controls.PathOf(s, a)));
        }

        [Test]
        public void ANewSettingsFileUsesTheDefaults()
        {
            var s = new Settings();
            Assert.AreEqual("<Keyboard>/e", Controls.PathOf(s, Act.Interact));
            Assert.AreEqual("<Mouse>/leftButton", Controls.PathOf(s, Act.Use));
        }

        [Test]
        public void TakingAKeyInUseSwapsTheTwoActions()
        {
            var s = new Settings();
            Assert.IsTrue(Controls.Set(s, Act.Interact, "<Keyboard>/f"));
            Assert.AreEqual("<Keyboard>/f", Controls.PathOf(s, Act.Interact));
            Assert.AreEqual("<Keyboard>/e", Controls.PathOf(s, Act.Torch), "the torch takes Interact's old key");
            Assert.IsTrue(Controls.Set(s, Act.Use, "<Keyboard>/space"));
            Assert.AreEqual("<Keyboard>/space", Controls.PathOf(s, Act.Use));
            // Space was free, so nothing else moved.
            Assert.AreEqual("<Mouse>/rightButton", Controls.PathOf(s, Act.Spray));
            Assert.IsNull(Controls.ActionOn(s, "<Mouse>/leftButton"), "the left button is free now");
        }

        [Test]
        public void ReservedKeysAreRefused()
        {
            var s = new Settings();
            Assert.IsFalse(Controls.Set(s, Act.Interact, "<Keyboard>/escape"));
            Assert.IsFalse(Controls.Set(s, Act.Interact, "<Keyboard>/1"));
            Assert.IsFalse(Controls.Set(s, Act.Interact, "<Keyboard>/enter"));
            Assert.AreEqual("<Keyboard>/e", Controls.PathOf(s, Act.Interact));
        }

        [Test]
        public void OnlyChangesAreStoredAndResetClearsThem()
        {
            var s = new Settings();
            Controls.Set(s, Act.Forward, "<Keyboard>/z");
            Controls.Set(s, Act.Forward, "<Keyboard>/w");
            Assert.AreEqual(0, s.Bindings.Count, "binding back to the default stores nothing");
            Controls.Set(s, Act.Drop, "<Keyboard>/g");
            Assert.AreEqual(1, s.Bindings.Count);
            Controls.ResetAll(s);
            Assert.AreEqual("<Keyboard>/q", Controls.PathOf(s, Act.Drop));
        }

        [Test]
        public void BindingsSurviveASaveAndAnOldFileGetsDefaults()
        {
            var s = new Settings();
            Controls.Set(s, Act.Interact, "<Keyboard>/f");
            var back = JsonUtility.FromJson<Settings>(JsonUtility.ToJson(s));
            Assert.AreEqual("<Keyboard>/f", Controls.PathOf(back, Act.Interact));
            Assert.AreEqual("<Keyboard>/e", Controls.PathOf(back, Act.Torch));
            // A round-1 settings file has no Bindings field at all.
            var old = JsonUtility.FromJson<Settings>("{\"MouseSensitivity\":1.5,\"Quality\":1}");
            foreach (Act a in Enum.GetValues(typeof(Act)))
                Assert.AreEqual(Controls.Defaults[a], Controls.PathOf(old, a));
        }
    }
}
