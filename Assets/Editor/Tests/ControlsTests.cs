using System;
using System.Linq;
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
    
        // ---- the controller page ------------------------------------------------------------------

        [Test]
        public void EveryPadActionHasADefaultALabelAndAnAllowedButton()
        {
            var s = new Settings();
            foreach (var (act, label) in Controls.PadLabels)
            {
                Assert.IsTrue(Controls.PadDefaults.ContainsKey(act), act.ToString());
                Assert.IsFalse(string.IsNullOrEmpty(label));
                CollectionAssert.Contains(Controls.PadButtons, Controls.PadPathOf(s, act));
                Assert.AreNotEqual("?", Controls.PadName(Controls.PadPathOf(s, act)));
            }
            Assert.AreEqual(Controls.PadDefaults.Count, Controls.PadDefaults.Values.Distinct().Count(), "no two defaults share a button");
            Assert.AreEqual("A", Controls.PadName(Controls.PadPathOf(s, Act.Interact)));
            Assert.IsNull(Controls.PadPathOf(s, Act.Forward), "moving is the stick, not a button");
        }

        [Test]
        public void TakingAPadButtonInUseSwaps()
        {
            var s = new Settings();
            Assert.IsTrue(Controls.SetPad(s, Act.Interact, "<Gamepad>/buttonEast"));
            Assert.AreEqual("<Gamepad>/buttonEast", Controls.PadPathOf(s, Act.Interact));
            Assert.AreEqual("<Gamepad>/buttonSouth", Controls.PadPathOf(s, Act.Drop), "drop swaps onto A");
            // A free button: nothing else moves.
            Assert.IsTrue(Controls.SetPad(s, Act.Torch, "<Gamepad>/buttonWest"));
            Assert.AreEqual("X", Controls.PadName(Controls.PadPathOf(s, Act.Torch)));
            Assert.AreEqual(Act.Torch, Controls.PadActionOn(s, "<Gamepad>/buttonWest"));
            Assert.IsNull(Controls.PadActionOn(s, "<Gamepad>/dpad/up"), "d-pad up is free now");
        }

        [Test]
        public void FixedPadButtonsAreRefused()
        {
            var s = new Settings();
            foreach (var path in new[] { "<Gamepad>/start", "<Gamepad>/dpad/left", "<Gamepad>/dpad/right", "<Gamepad>/leftStick/up", "<Keyboard>/e" })
                Assert.IsFalse(Controls.SetPad(s, Act.Interact, path), path);
            Assert.IsFalse(Controls.SetPad(s, Act.Forward, "<Gamepad>/buttonWest"), "moving can't go on a button");
            Assert.AreEqual(0, s.PadBindings.Count);
        }

        [Test]
        public void PadBindingsSurviveASaveResetAndAnOldFileGetsDefaults()
        {
            var s = new Settings();
            Controls.SetPad(s, Act.Interact, "<Gamepad>/buttonWest");
            Controls.Set(s, Act.Interact, "<Keyboard>/f");
            Assert.AreEqual(1, s.PadBindings.Count, "only changes are stored");
            var back = JsonUtility.FromJson<Settings>(JsonUtility.ToJson(s));
            Assert.AreEqual("<Gamepad>/buttonWest", Controls.PadPathOf(back, Act.Interact));
            Assert.AreEqual("<Keyboard>/f", Controls.PathOf(back, Act.Interact), "keys and pad are separate");
            Controls.ResetPad(back);
            Assert.AreEqual("<Gamepad>/buttonSouth", Controls.PadPathOf(back, Act.Interact));
            Assert.AreEqual("<Keyboard>/f", Controls.PathOf(back, Act.Interact), "resetting the pad leaves the keys");
            // A round-2 settings file has keyboard bindings but no pad bindings.
            var old = JsonUtility.FromJson<Settings>("{\"Bindings\":[{\"Action\":\"Torch\",\"Path\":\"<Keyboard>/g\"}]}");
            foreach (var a in Controls.PadDefaults.Keys)
                Assert.AreEqual(Controls.PadDefaults[a], Controls.PadPathOf(old, a));
        }
    }
}
