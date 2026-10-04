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

        bool lastUse;

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
                if (l.sqrMagnitude > 0.01f) f.Look += l * (160f * sens * Time.unscaledDeltaTime);
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
