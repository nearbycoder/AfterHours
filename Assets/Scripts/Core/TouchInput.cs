using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AfterHours
{
    /// <summary>
    /// The browser's on-screen controls (phones and tablets). The page draws them over the canvas
    /// (Assets/WebGLTemplates/AfterHours/TemplateData/touch.js) and keeps their state; the game reads
    /// it once a frame (<see cref="WebPlatform.ReadTouch"/>) and folds it into <see cref="GameInput"/>.
    /// The game tells the page what the buttons should say and which soft keys a screen needs
    /// (<see cref="Describe"/>, through <see cref="WebPlatform.Report"/>).
    /// </summary>
    public static class TouchInput
    {
        // Buttons, as bits (the page's touch.js uses the same numbers).
        public const int Use = 1, Spray = 2, Interact = 4, Drop = 8, Torch = 16, Clipboard = 32, Sprint = 64, Crouch = 128,
            Pause = 256, Tool = 512, Up = 1 << 10, Down = 1 << 11, Left = 1 << 12, Right = 1 << 13,
            Confirm = 1 << 14, Back = 1 << 15, Keep = 1 << 16, Alt = 1 << 17;

        /// <summary>The on-screen controls are showing (a touch was the last input); prompts name their buttons.</summary>
        public static bool Active { get; private set; }
        /// <summary>The left stick, -1 to 1 (up is forward).</summary>
        public static Vector2 Move { get; private set; }
        /// <summary>Drag on the right of the screen since the last frame, in CSS pixels (up is positive).</summary>
        public static Vector2 LookPixels { get; private set; }
        static int held, pressed;
        static readonly float[] buffer = new float[8];

        public static bool Held(int bit) => (held & bit) != 0;
        public static bool Pressed(int bit) => (pressed & bit) != 0;

        /// <summary>A tap on the game itself (outside the on-screen buttons) began this frame: menus and "press to continue" screens take it as a click.</summary>
        public static bool Tapped
        {
            get
            {
                var ts = Touchscreen.current;
                return ts != null && ts.primaryTouch.press.wasPressedThisFrame;
            }
        }

        /// <summary>Once a frame, before devices are read (GameInput).</summary>
        public static void Read()
        {
            WebPlatform.ReadTouch(buffer);
            Active = buffer[0] > 0.5f;
            Move = Vector2.ClampMagnitude(new Vector2(buffer[1], buffer[2]), 1f);
            LookPixels = new Vector2(buffer[3], buffer[4]);
            held = (int)buffer[5];
            pressed = (int)buffer[6];
        }

        /// <summary>
        /// What a key in a prompt is called on the touch screen: the name on its on-screen button
        /// ("E" is the Use button). The main button says Throw while something is held.
        /// </summary>
        public static string Glyph(string key)
        {
            switch (key.ToUpperInvariant())
            {
                case "LMB": return GameRoot.Instance != null && GameRoot.Instance.Hands != null && GameRoot.Instance.Hands.Holding != null ? "Throw" : "Clean";
                case "RMB": return "Spray";
                case "E": return "Use";
                case "Q": return "Drop";
                case "F": return "UV";
                case "TAB": return "Sheet";
                case "SHIFT": return "Brisk";
                case "C": return "Crouch";
                case "X": return "Toss";
                case "ESC": return "Back";
                case "ENTER": case "SPACE": return "OK";
                case "W / S": return "▲ ▼";
                case "A / D": return "◀ ▶";
                default: return key;
            }
        }

        public static string Glyph(Act a) => a switch
        {
            Act.Use => Glyph("LMB"), Act.Spray => "Spray", Act.Interact => "Use", Act.Drop => "Drop", Act.Torch => "UV",
            Act.Clipboard => "Sheet", Act.Sprint => "Brisk", Act.Crouch => "Crouch", Act.Discard => "Toss",
            _ => "Stick",
        };

        /// <summary>A soft key: which button bit it sends, and what it says.</summary>
        public readonly struct Key
        {
            public readonly int Bit;
            public readonly string Label;
            public Key(int bit, string label) { Bit = bit; Label = label; }
        }

        /// <summary>
        /// The soft keys a screen needs on a touch screen: only where its own buttons can't be tapped
        /// (the clipboard and the document reader are read with keys). Menus with buttons need none,
        /// and screens that say "press to continue" take a tap anywhere.
        /// </summary>
        public static List<Key> SoftKeys()
        {
            var k = new List<Key>();
            if (InspectView.IsOpen)
            {
                switch (InspectView.Mode)
                {
                    case InspectMode.Evidence:
                        k.Add(new Key(Keep, "Keep")); k.Add(new Key(Alt, "Throw away")); k.Add(new Key(Back, "Close")); break;
                    case InspectMode.Screen:
                        k.Add(new Key(Alt, "Switch off")); k.Add(new Key(Back, "Close")); break;
                    default:
                        k.Add(new Key(Back, "Close")); break;
                }
            }
            else if (ShredPuzzle.IsOpen)
            {
                k.Add(new Key(Left, "◀")); k.Add(new Key(Right, "▶")); k.Add(new Key(Confirm, "Pick up · swap")); k.Add(new Key(Back, "Give up"));
            }
            else if (ChoiceMenu.IsOpen) { }
            else if (Clipboard_Open)
            {
                k.Add(new Key(Left, "◀ Page")); k.Add(new Key(Right, "Page ▶")); k.Add(new Key(Up, "▲")); k.Add(new Key(Down, "▼"));
                if (AfterHours.Clipboard.Instance.Page == AfterHours.Clipboard.CasePage) k.Add(new Key(Confirm, "Read"));
                k.Add(new Key(Back, "Close"));
            }
            else if (ChatInterlude.Showing)
            {
                k.Add(new Key(Up, "▲")); k.Add(new Key(Down, "▼"));
            }
            return k;
        }

        static bool Clipboard_Open => AfterHours.Clipboard.Instance != null && AfterHours.Clipboard.Instance.Open;

        /// <summary>
        /// JSON for the page: whether gameplay has the screen (the stick and buttons show), the
        /// prompt's labels by key (the buttons show them), whether the torch is in hand, and soft keys.
        /// </summary>
        public static string Describe(bool play, IReadOnlyList<(string key, string label)> prompt)
        {
            var sb = new StringBuilder(160);
            sb.Append("{\"play\":").Append(play ? "true" : "false");
            sb.Append(",\"holding\":").Append(GameRoot.Instance != null && GameRoot.Instance.Hands != null && GameRoot.Instance.Hands.Holding != null ? "true" : "false");
            sb.Append(",\"torch\":").Append(UvTorch.Instance != null && UvTorch.Instance.Available ? "true" : "false");
            sb.Append(",\"prompt\":{");
            bool first = true;
            if (play && prompt != null)
                foreach (var (key, label) in prompt)
                {
                    if (string.IsNullOrEmpty(key)) continue;
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('"').Append(WebPlatform.Escape(key.ToUpperInvariant())).Append("\":\"").Append(WebPlatform.Escape(StripTags(label))).Append('"');
                }
            sb.Append("},\"keys\":[");
            first = true;
            foreach (var key in SoftKeys())
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append("[").Append(key.Bit).Append(",\"").Append(WebPlatform.Escape(key.Label)).Append("\"]");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        static string StripTags(string s) => string.IsNullOrEmpty(s) ? "" : System.Text.RegularExpressions.Regex.Replace(s, "<[^>]*>", "");
    }
}
