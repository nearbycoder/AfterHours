using UnityEngine;

namespace AfterHours
{
    /// <summary>Input provider used by automation: replaces device input with scripted intent.</summary>
    public class ScriptedInput : IInputProvider
    {
        public Vector2 Move;
        public bool Use, Spray, Sprint, Crouch;
        public bool InteractOnce, DropOnce, TorchOnce, ClipboardOnce, PauseOnce, ConfirmOnce, ClickOnce;
        public int ToolSlotOnce;
        public Vector2 Pointer;

        public InputFrame Read(InputFrame devices)
        {
            var f = new InputFrame
            {
                Move = Move, Use = Use, Spray = Spray, Sprint = Sprint, Crouch = Crouch,
                Interact = InteractOnce, Drop = DropOnce, Torch = TorchOnce, Clipboard = ClipboardOnce,
                Pause = PauseOnce, Confirm = ConfirmOnce, ToolSlot = ToolSlotOnce, Pointer = Pointer, Click = ClickOnce,
            };
            InteractOnce = DropOnce = TorchOnce = ClipboardOnce = PauseOnce = ConfirmOnce = ClickOnce = false;
            ToolSlotOnce = 0;
            return f;
        }
    }
}
