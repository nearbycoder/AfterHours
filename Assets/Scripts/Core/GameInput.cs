using UnityEngine;
using UnityEngine.InputSystem;

namespace AfterHours
{
    /// <summary>One frame of player intent. Filled from devices, or by the AutoPilot.</summary>
    public struct InputFrame
    {
        public Vector2 Move;
        public Vector2 Look;        // degrees this frame (already scaled)
        public bool Use, UseDown, UseUp;
        public bool Spray;
        public bool Interact;       // pressed this frame
        public bool Drop;
        public bool Sprint, Crouch;
        public bool Torch, Clipboard, Pause, Confirm, Back;
        public int ToolSlot;        // 0 = none, 1..4
        public int ToolCycle;       // pad d-pad: -1 previous, +1 next pinned tool
        public float Scroll;
        public Vector2 Pointer;     // screen position for menus
        public bool Click;
    }

    /// <summary>
    /// One frame of menu intent, from the keyboard or a gamepad. Unlike <see cref="InputFrame"/> it
    /// is never zeroed while gameplay is blocked, because that is exactly when menus need it.
    /// </summary>
    public struct MenuFrame
    {
        public bool Up, Down, Left, Right;
        public bool Confirm;    // E / Enter / Space, pad A
        public bool Back;       // Esc, pad B
        public bool Keep;       // Tab, pad Y
        public bool Alt;        // X (or Q on monitors), pad X
        public bool Pause;      // Esc, pad Start
        public bool Clipboard;  // Tab, pad Select
        public bool Start;      // pad Start only (Esc already means back)
    }

    /// <summary>
    /// A button that is either held, or pressed once to switch on and again to switch off
    /// (crouch and brisk walk). Toggled brisk walk also ends when the player stops moving.
    /// </summary>
    public class ToggleLatch
    {
        public bool On { get; private set; }
        bool was, moved;

        /// <summary>One frame: the raw button, the mode, and whether gameplay takes input right now.</summary>
        public bool Step(bool held, bool toggle, bool allowed = true)
        {
            bool pressed = held && !was;
            was = held;
            if (!toggle) { On = held; return On; }
            if (pressed && allowed) { On = !On; moved = false; }
            return On;
        }

        /// <summary>Brisk walk: once you've moved with it on, standing still switches it off.</summary>
        public bool StopWhenIdle(bool moving)
        {
            if (!On) return false;
            if (moving) moved = true;
            else if (moved) On = false;
            return On;
        }

        public void Reset() { On = false; moved = false; }
    }

    /// <summary>
    /// Clean and spray, held or toggled. Toggled, they're one switch with two positions: turning
    /// one on turns the other off, so a press always does what it says.
    /// </summary>
    public class UseLatches
    {
        public readonly ToggleLatch Use = new(), Spray = new();

        public (bool use, bool spray) Step(bool useHeld, bool sprayHeld, bool toggle, bool allowed = true)
        {
            bool useWas = Use.On, sprayWas = Spray.On;
            bool use = Use.Step(useHeld, toggle, allowed);
            bool spray = Spray.Step(sprayHeld, toggle, allowed);
            if (!toggle) return (use, spray);
            if (Use.On && !useWas) { Spray.Reset(); spray = false; }
            else if (Spray.On && !sprayWas) { Use.Reset(); use = false; }
            return (use, spray);
        }

        public void Reset() { Use.Reset(); Spray.Reset(); }
    }

    public interface IInputProvider
    {
        InputFrame Read(InputFrame devices);
    }

    /// <summary>Single place where devices are read. Gameplay code reads <see cref="Frame"/>.</summary>
    [DefaultExecutionOrder(-1000)]
    public class GameInput : MonoBehaviour
    {
        public static InputFrame Frame;
        public static IInputProvider Override;
        public static bool GameplayEnabled = true;
        public static MenuFrame Menu;
        /// <summary>True when the gamepad was the last device touched; prompts show pad buttons.</summary>
        public static bool UsingPad;
        public static event System.Action DeviceChanged;
        static readonly ToggleLatch crouchLatch = new(), sprintLatch = new();
        static readonly UseLatches useLatches = new();

        /// <summary>A new night starts standing and walking, whatever was toggled before.</summary>
        public static void ResetToggles() { crouchLatch.Reset(); sprintLatch.Reset(); ReleaseUse(); }

        /// <summary>Stop a toggled clean or spray (picking something up, putting it down, any menu).</summary>
        public static void ReleaseUse() => useLatches.Reset();

        /// <summary>Clean is switched on by a toggle press rather than held down.</summary>
        public static bool UseLatched => Settings.Current.ToggleUse && useLatches.Use.On;

        bool lastUse;
        Vector2 heldDir;
        float repeatAt;

        void Update()
        {
            var f = ReadDevices();
            if (Override != null) f = Override.Read(f);
            f.UseDown = f.Use && !lastUse;
            f.UseUp = !f.Use && lastUse;
            lastUse = f.Use;
            if (!GameplayEnabled)
            {
                ReleaseUse();
                f.Move = Vector2.zero; f.Look = Vector2.zero;
                f.Use = f.UseDown = f.Spray = f.Interact = f.Drop = f.Torch = false;
                f.ToolCycle = 0; f.ToolSlot = 0; f.Scroll = 0;
            }
            Frame = f;
            Menu = ReadMenu();
        }

        MenuFrame ReadMenu()
        {
            var m = new MenuFrame();
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;
            bool wasPad = UsingPad;
            if (kb != null)
            {
                // Menus follow the movement and interact bindings as well as the arrows and Enter.
                bool Bound(Act a) => Controls.Control(a) is UnityEngine.InputSystem.Controls.KeyControl k && k.wasPressedThisFrame;
                m.Up = Bound(Act.Forward) || kb.upArrowKey.wasPressedThisFrame;
                m.Down = Bound(Act.Back) || kb.downArrowKey.wasPressedThisFrame;
                m.Left = Bound(Act.Left) || kb.leftArrowKey.wasPressedThisFrame;
                m.Right = Bound(Act.Right) || kb.rightArrowKey.wasPressedThisFrame;
                m.Confirm = Bound(Act.Interact) || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
                m.Back = m.Pause = kb.escapeKey.wasPressedThisFrame;
                m.Keep = m.Clipboard = Bound(Act.Clipboard);
                m.Alt = Bound(Act.Discard);
                if (kb.anyKey.wasPressedThisFrame) UsingPad = false;
            }
            if (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 9f || mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
                UsingPad = false;
            if (pad != null)
            {
                // D-pad or stick, with key-repeat when held.
                var dir = pad.dpad.ReadValue() + pad.leftStick.ReadValue();
                var snapped = Vector2.zero;
                if (dir.magnitude > 0.55f)
                    snapped = Mathf.Abs(dir.x) > Mathf.Abs(dir.y) ? new Vector2(Mathf.Sign(dir.x), 0) : new Vector2(0, Mathf.Sign(dir.y));
                bool fire = false;
                if (snapped != heldDir) { heldDir = snapped; fire = snapped != Vector2.zero; repeatAt = GameTime.Unscaled + 0.4f; }
                else if (snapped != Vector2.zero && GameTime.Unscaled >= repeatAt) { fire = true; repeatAt = GameTime.Unscaled + 0.12f; }
                if (fire)
                {
                    m.Up |= snapped.y > 0; m.Down |= snapped.y < 0;
                    m.Left |= snapped.x < 0; m.Right |= snapped.x > 0;
                }
                m.Confirm |= pad.buttonSouth.wasPressedThisFrame;
                m.Back |= pad.buttonEast.wasPressedThisFrame;
                m.Keep |= pad.buttonNorth.wasPressedThisFrame;
                m.Alt |= pad.buttonWest.wasPressedThisFrame;
                m.Pause |= pad.startButton.wasPressedThisFrame;
                m.Start = pad.startButton.wasPressedThisFrame;
                m.Clipboard |= Controls.PadPressed(Act.Clipboard);
                bool padActive = fire || pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame
                                 || pad.buttonWest.wasPressedThisFrame || pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame
                                 || pad.rightStick.ReadValue().sqrMagnitude > 0.1f || pad.leftStick.ReadValue().sqrMagnitude > 0.1f
                                 || pad.rightTrigger.wasPressedThisFrame || pad.leftTrigger.wasPressedThisFrame
                                 || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame
                                 || pad.leftStickButton.wasPressedThisFrame || pad.rightStickButton.wasPressedThisFrame;
                if (padActive) UsingPad = true;
            }
            if (UsingPad != wasPad) DeviceChanged?.Invoke();
            return m;
        }

        /// <summary>
        /// The button to show for a keyboard/mouse key name in prompts and hints: the key itself, or
        /// the matching pad button when the gamepad is in use.
        /// </summary>
        public static string Glyph(string key)
        {
            if (!UsingPad)
            {
                // Callers name the default key; show whatever it's bound to now.
                var act = ActFor(key);
                if (act.HasValue) return Controls.Display(act.Value);
                if (key.ToUpperInvariant() == "A / D") return $"{Controls.Display(Act.Left)} / {Controls.Display(Act.Right)}";
                if (key.ToUpperInvariant() == "W / S") return $"{Controls.Display(Act.Forward)} / {Controls.Display(Act.Back)}";
                return key;
            }
            // In play, actions follow the pad bindings; TAB and X name the reader's fixed Keep and Throw away.
            var padAct = ActFor(key);
            if (padAct.HasValue && Controls.IsPadAct(padAct.Value) && padAct.Value != Act.Clipboard) return ActGlyph(padAct.Value);
            return MenuGlyph(key);
        }

        /// <summary>
        /// A key in a menu or a document, where pad buttons are fixed: E and Enter are A, Esc is B,
        /// Tab is Y (keep), X is X. On the keyboard, the same as <see cref="Glyph"/>.
        /// </summary>
        public static string MenuGlyph(string key)
        {
            if (!UsingPad)
            {
                var act = ActFor(key);
                if (act.HasValue) return Controls.Display(act.Value);
                if (key.ToUpperInvariant() == "A / D") return $"{Controls.Display(Act.Left)} / {Controls.Display(Act.Right)}";
                if (key.ToUpperInvariant() == "W / S") return $"{Controls.Display(Act.Forward)} / {Controls.Display(Act.Back)}";
                return key;
            }
            return PadGlyph(key.ToUpperInvariant() switch
            {
                "E" or "ENTER" or "SPACE" => "A",
                "Q" or "ESC" => "B",
                "TAB" => "Y",
                "X" => "X",
                "LMB" => "RT",
                "RMB" => "LT",
                "F" => "D-PAD ↑",
                "SHIFT" => "RB",
                "A / D" => "◀ ▶",
                "W / S" => "▲ ▼",
                _ => key,
            });
        }

        public static string MenuKeyTag(string key) => $"<mark=#FFFFFF33 padding=\"12,12,6,6\"><b>{MenuGlyph(key)}</b></mark>";

        /// <summary>The key or pad button for an action, as prompts show it ("Tab", "View", "□").</summary>
        public static string ActGlyph(Act a)
        {
            if (!UsingPad) return Controls.Display(a);
            if (Controls.IsPadAct(a)) return PadGlyph(Controls.PadName(Controls.PadPathOf(Settings.Current, a)));
            return PadGlyph(a == Act.Discard ? "X" : "L-STICK");
        }

        /// <summary>
        /// Key tokens in document text, "{key:Clipboard}", become the bound key or pad button, so
        /// a note that says "(Tab)" stays right after rebinding or on a pad.
        /// </summary>
        public static string ExpandKeys(string text)
        {
            if (string.IsNullOrEmpty(text) || !text.Contains("{key:")) return text;
            return System.Text.RegularExpressions.Regex.Replace(text, @"\{key:(\w+)\}",
                m => System.Enum.TryParse<Act>(m.Groups[1].Value, out var a) ? ActGlyph(a) : m.Value);
        }

        /// <summary>A pad button, named the Xbox way ("A", "RT", "View"), as the active pad labels it.</summary>
        public static string PadGlyph(string xbox)
        {
            if (Gamepad.current is not UnityEngine.InputSystem.DualShock.DualShockGamepad) return xbox;
            bool dualSense = Gamepad.current is UnityEngine.InputSystem.DualShock.DualSenseGamepadHID;
            return xbox switch
            {
                "A" => "✕", "B" => "○", "X" => "□", "Y" => "△",
                "RT" => "R2", "LT" => "L2", "RB" => "R1", "LB" => "L1", "LS" => "L3", "RS" => "R3",
                "View" => dualSense ? "Create" : "Share", "Menu" => "Options",
                _ => xbox,
            };
        }

        // Never leave a pad buzzing when the window loses focus or the game quits.
        void OnApplicationFocus(bool focus) { if (!focus) Rumble.Stop(); }
        void OnApplicationQuit() => Rumble.Stop();

        /// <summary>The action behind a default key name used in prompts ("E", "LMB", "TAB"...).</summary>
        static Act? ActFor(string key) => key.ToUpperInvariant() switch
        {
            "E" => Act.Interact, "Q" => Act.Drop, "F" => Act.Torch, "TAB" => Act.Clipboard, "X" => Act.Discard,
            "LMB" => Act.Use, "RMB" => Act.Spray, "SHIFT" => Act.Sprint, "C" => Act.Crouch,
            _ => null,
        };

        /// <summary>A key cap inside running text ("Press [E] to…"), following bindings and the pad.</summary>
        public static string KeyTag(string key) => $"<mark=#FFFFFF33 padding=\"12,12,6,6\"><b>{Glyph(key)}</b></mark>";

        static InputFrame ReadDevices()
        {
            var f = new InputFrame();
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;
            float sens = Settings.Current.MouseSensitivity;
            // Bound actions (keys or mouse buttons; see Controls).
            float x = (Controls.Held(Act.Right) ? 1 : 0) - (Controls.Held(Act.Left) ? 1 : 0);
            float y = (Controls.Held(Act.Forward) ? 1 : 0) - (Controls.Held(Act.Back) ? 1 : 0);
            f.Move = new Vector2(x, y);
            f.Sprint = Controls.Held(Act.Sprint);
            f.Crouch = Controls.Held(Act.Crouch) || (kb != null && Controls.CtrlCrouch && kb.leftCtrlKey.isPressed);
            f.Interact = Controls.Pressed(Act.Interact);
            f.Drop = Controls.Pressed(Act.Drop);
            f.Torch = Controls.Pressed(Act.Torch);
            f.Clipboard = Controls.Pressed(Act.Clipboard);
            f.Use = Controls.Held(Act.Use);
            f.Spray = Controls.Held(Act.Spray);
            if (kb != null)
            {
                f.Pause = kb.escapeKey.wasPressedThisFrame;
                f.Back = kb.escapeKey.wasPressedThisFrame;
                f.Confirm = kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
                if (kb.digit1Key.wasPressedThisFrame) f.ToolSlot = 1;
                if (kb.digit2Key.wasPressedThisFrame) f.ToolSlot = 2;
                if (kb.digit3Key.wasPressedThisFrame) f.ToolSlot = 3;
                if (kb.digit4Key.wasPressedThisFrame) f.ToolSlot = 4;
            }
            if (mouse != null)
            {
                var d = mouse.delta.ReadValue();
                f.Look = d * (0.06f * sens);
                f.Scroll = mouse.scroll.ReadValue().y;
                f.Pointer = mouse.position.ReadValue();
                f.Click = mouse.leftButton.wasPressedThisFrame;
            }
            if (pad != null)
            {
                var m = pad.leftStick.ReadValue();
                if (m.sqrMagnitude > 0.02f) f.Move = m;
                var l = pad.rightStick.ReadValue();
                if (l.sqrMagnitude > 0.01f) f.Look += l * (160f * Settings.Current.StickSensitivity * GameTime.UnscaledDelta);
                // Bound actions (Controls.PadDefaults); the tools and pause stay fixed.
                f.Use |= Controls.PadHeld(Act.Use);
                f.Spray |= Controls.PadHeld(Act.Spray);
                f.Interact |= Controls.PadPressed(Act.Interact);
                f.Drop |= Controls.PadPressed(Act.Drop);
                f.Torch |= Controls.PadPressed(Act.Torch);
                f.Clipboard |= Controls.PadPressed(Act.Clipboard);
                f.Crouch |= Controls.PadHeld(Act.Crouch);
                f.Sprint |= Controls.PadHeld(Act.Sprint);
                if (pad.dpad.right.wasPressedThisFrame) f.ToolCycle = 1;
                if (pad.dpad.left.wasPressedThisFrame) f.ToolCycle = -1;
                f.Pause |= pad.startButton.wasPressedThisFrame;
            }
            var st = Settings.Current;
            f.Crouch = crouchLatch.Step(f.Crouch, st.ToggleCrouch, GameplayEnabled);
            f.Sprint = sprintLatch.Step(f.Sprint, st.ToggleSprint, GameplayEnabled);
            if (st.ToggleSprint && GameplayEnabled) f.Sprint = sprintLatch.StopWhenIdle(f.Move.sqrMagnitude > 0.04f);
            (f.Use, f.Spray) = useLatches.Step(f.Use, f.Spray, st.ToggleUse, GameplayEnabled);
            if (st.InvertY) f.Look.y = -f.Look.y;
            return f;
        }
    }
}
