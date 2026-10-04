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
                f.Move = Vector2.zero; f.Look = Vector2.zero;
                f.Use = f.UseDown = f.Spray = f.Interact = f.Drop = f.Torch = false;
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
                m.Up = kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame;
                m.Down = kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame;
                m.Left = kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame;
                m.Right = kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame;
                m.Confirm = kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
                m.Back = m.Pause = kb.escapeKey.wasPressedThisFrame;
                m.Keep = m.Clipboard = kb.tabKey.wasPressedThisFrame;
                m.Alt = kb.xKey.wasPressedThisFrame;
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
                m.Clipboard |= pad.selectButton.wasPressedThisFrame;
                bool padActive = fire || pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame
                                 || pad.buttonWest.wasPressedThisFrame || pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame
                                 || pad.rightStick.ReadValue().sqrMagnitude > 0.1f || pad.leftStick.ReadValue().sqrMagnitude > 0.1f
                                 || pad.rightTrigger.wasPressedThisFrame || pad.leftTrigger.wasPressedThisFrame;
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
            if (!UsingPad) return key;
            return key.ToUpperInvariant() switch
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
                _ => key,
            };
        }

        static InputFrame ReadDevices()
        {
            var f = new InputFrame();
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;
            float sens = Settings.Current.MouseSensitivity;
            if (kb != null)
            {
                float x = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
                float y = (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0);
                f.Move = new Vector2(x, y);
                f.Sprint = kb.leftShiftKey.isPressed;
                f.Crouch = kb.cKey.isPressed || kb.leftCtrlKey.isPressed;
                f.Interact = kb.eKey.wasPressedThisFrame;
                f.Drop = kb.qKey.wasPressedThisFrame;
                f.Torch = kb.fKey.wasPressedThisFrame;
                f.Clipboard = kb.tabKey.wasPressedThisFrame;
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
                f.Use = mouse.leftButton.isPressed;
                f.Spray = mouse.rightButton.isPressed;
                f.Scroll = mouse.scroll.ReadValue().y;
                f.Pointer = mouse.position.ReadValue();
                f.Click = mouse.leftButton.wasPressedThisFrame;
            }
            if (pad != null)
            {
                var m = pad.leftStick.ReadValue();
                if (m.sqrMagnitude > 0.02f) f.Move = m;
                var l = pad.rightStick.ReadValue();
                if (l.sqrMagnitude > 0.01f) f.Look += l * (160f * sens * GameTime.UnscaledDelta);
                f.Use |= pad.rightTrigger.isPressed;
                f.Spray |= pad.leftTrigger.isPressed;
                f.Interact |= pad.buttonSouth.wasPressedThisFrame;
                f.Drop |= pad.buttonEast.wasPressedThisFrame;
                f.Torch |= pad.dpad.up.wasPressedThisFrame;
                f.Clipboard |= pad.selectButton.wasPressedThisFrame;
                f.Pause |= pad.startButton.wasPressedThisFrame;
                f.Crouch |= pad.leftStickButton.isPressed;
                f.Sprint |= pad.rightShoulder.isPressed;
            }
            if (Settings.Current.InvertY) f.Look.y = -f.Look.y;
            return f;
        }
    }
}
