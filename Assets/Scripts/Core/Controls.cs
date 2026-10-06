using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AfterHours
{
    /// <summary>Gameplay actions the player can rebind (keyboard keys or mouse buttons).</summary>
    public enum Act { Forward, Back, Left, Right, Sprint, Crouch, Use, Spray, Interact, Drop, Torch, Clipboard, Discard }

    [Serializable]
    public class Binding
    {
        public string Action;   // Act name
        public string Path;     // "<Keyboard>/e", "<Mouse>/leftButton"
    }

    /// <summary>
    /// Keyboard and mouse bindings, stored in <see cref="Settings.Bindings"/> (only the ones that
    /// differ from the defaults). Esc, Enter, the arrow keys and 1-4 stay fixed; pad buttons too.
    /// </summary>
    public static class Controls
    {
        public static readonly Dictionary<Act, string> Defaults = new()
        {
            { Act.Forward, "<Keyboard>/w" }, { Act.Back, "<Keyboard>/s" }, { Act.Left, "<Keyboard>/a" }, { Act.Right, "<Keyboard>/d" },
            { Act.Sprint, "<Keyboard>/leftShift" }, { Act.Crouch, "<Keyboard>/c" },
            { Act.Use, "<Mouse>/leftButton" }, { Act.Spray, "<Mouse>/rightButton" },
            { Act.Interact, "<Keyboard>/e" }, { Act.Drop, "<Keyboard>/q" }, { Act.Torch, "<Keyboard>/f" },
            { Act.Clipboard, "<Keyboard>/tab" }, { Act.Discard, "<Keyboard>/x" },
        };

        public static readonly (Act act, string label)[] Labels =
        {
            (Act.Forward, "Move forward"), (Act.Back, "Move back"), (Act.Left, "Move left"), (Act.Right, "Move right"),
            (Act.Sprint, "Brisk walk"), (Act.Crouch, "Crouch"), (Act.Use, "Clean · throw (hold)"),
            (Act.Spray, "Spray"), (Act.Interact, "Interact · pick up"), (Act.Drop, "Drop · monitor off"),
            (Act.Torch, "UV torch"), (Act.Clipboard, "Clipboard · keep document"), (Act.Discard, "Throw a document away"),
        };

        /// <summary>Keys that can't be bound: they're fixed for menus (Esc, Enter) and tool pins (1-4).</summary>
        public static readonly string[] Reserved = { "<Keyboard>/escape", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Keyboard>/1", "<Keyboard>/2", "<Keyboard>/3", "<Keyboard>/4" };

        // ---- data (pure; tested in EditMode) -------------------------------------------------------

        public static string PathOf(Settings s, Act a)
        {
            var b = s.Bindings?.FirstOrDefault(x => x.Action == a.ToString());
            return b != null && !string.IsNullOrEmpty(b.Path) ? b.Path : Defaults[a];
        }

        /// <summary>The action bound to <paramref name="path"/>, if any.</summary>
        public static Act? ActionOn(Settings s, string path)
        {
            foreach (Act a in Enum.GetValues(typeof(Act)))
                if (string.Equals(PathOf(s, a), path, StringComparison.OrdinalIgnoreCase)) return a;
            return null;
        }

        /// <summary>
        /// Bind <paramref name="a"/> to <paramref name="path"/>. If another action already has that
        /// key, the two swap, so nothing is ever left unbound. False for reserved keys.
        /// </summary>
        public static bool Set(Settings s, Act a, string path)
        {
            if (string.IsNullOrEmpty(path) || Reserved.Any(r => string.Equals(r, path, StringComparison.OrdinalIgnoreCase))) return false;
            string old = PathOf(s, a);
            var other = ActionOn(s, path);
            Put(s, a, path);
            if (other.HasValue && other.Value != a) Put(s, other.Value, old);
            return true;
        }

        static void Put(Settings s, Act a, string path)
        {
            s.Bindings ??= new List<Binding>();
            s.Bindings.RemoveAll(x => x.Action == a.ToString());
            if (!string.Equals(path, Defaults[a], StringComparison.OrdinalIgnoreCase))
                s.Bindings.Add(new Binding { Action = a.ToString(), Path = path });
        }

        public static void ResetAll(Settings s) => s.Bindings?.Clear();

        // ---- devices -------------------------------------------------------------------------------

        static ButtonControl Control(string path)
        {
            if (path.StartsWith("<Keyboard>/")) return Keyboard.current?.TryGetChildControl<ButtonControl>(path.Substring(11));
            if (path.StartsWith("<Mouse>/")) return Mouse.current?.TryGetChildControl<ButtonControl>(path.Substring(8));
            return null;
        }

        public static ButtonControl Control(Act a) => Control(PathOf(Settings.Current, a));
        public static bool Held(Act a) => Control(a)?.isPressed ?? false;
        public static bool Pressed(Act a) => Control(a)?.wasPressedThisFrame ?? false;

        /// <summary>Left Ctrl crouches too, unless the player has given it another job.</summary>
        public static bool CtrlCrouch => ActionOn(Settings.Current, "<Keyboard>/leftCtrl") == null;

        /// <summary>How a binding reads on a key cap: the key's name on the current keyboard layout.</summary>
        public static string Display(string path)
        {
            switch (path)
            {
                case "<Mouse>/leftButton": return "LMB";
                case "<Mouse>/rightButton": return "RMB";
                case "<Mouse>/middleButton": return "MMB";
                case "<Mouse>/backButton": return "Mouse 4";
                case "<Mouse>/forwardButton": return "Mouse 5";
                case "<Keyboard>/leftShift": case "<Keyboard>/rightShift": return "Shift";
                case "<Keyboard>/leftCtrl": case "<Keyboard>/rightCtrl": return "Ctrl";
                case "<Keyboard>/leftAlt": case "<Keyboard>/rightAlt": return "Alt";
                case "<Keyboard>/tab": return "Tab";
                case "<Keyboard>/space": return "Space";
                case "<Keyboard>/backspace": return "Backspace";
            }
            var c = Control(path);
            string name = c != null && !string.IsNullOrWhiteSpace(c.displayName) ? c.displayName : path.Substring(path.LastIndexOf('/') + 1);
            return name.Length == 1 ? name.ToUpperInvariant() : name;
        }

        public static string Display(Act a) => Display(PathOf(Settings.Current, a));
    }
}
