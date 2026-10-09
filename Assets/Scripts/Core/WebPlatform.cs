using System.Runtime.InteropServices;
using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// The browser build (GitHub Pages): what differs from the desktop game, and the few calls into
    /// the page (Assets/Plugins/WebGL/AfterHoursWeb.jslib). Everything here does nothing on desktop.
    /// Saves and settings still go through <see cref="SaveIO"/>: the web player keeps
    /// persistentDataPath in IndexedDB, and the page syncs it after every write.
    /// </summary>
    public static class WebPlatform
    {
        /// <summary>Running in a browser. No Quit, no window mode or VSync, no local playtest log.</summary>
        public static bool IsWeb => Application.platform == RuntimePlatform.WebGLPlayer;

        /// <summary>
        /// Graphics fidelity for a first visit on the web: Medium (desktop starts on High), Low on a
        /// phone or tablet, where the browser tab has far less memory (see <see cref="Mobile"/>).
        /// </summary>
        public static int DefaultQuality => Mobile ? 0 : 1;

        /// <summary>
        /// Grime patterns' resolution against the desktop's: half on a phone or tablet (a quarter of
        /// the memory), where the screen is small and the tab's memory is tight.
        /// </summary>
        public static float PatternScale => Mobile ? 0.5f : 1f;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int AH_PointerLocked();
        [DllImport("__Internal")] static extern void AH_SetLeaveWarning(int on);
        [DllImport("__Internal")] static extern void AH_Report(string json);
        [DllImport("__Internal")] static extern int AH_IsMobile();
        [DllImport("__Internal")] static extern void AH_TouchRead(float[] into, int length);
        [DllImport("__Internal")] static extern void AH_ReportUi(string json);
        [DllImport("__Internal")] static extern int AH_CanLockPointer();
#else
        static int AH_PointerLocked() => 0;
        static void AH_SetLeaveWarning(int on) { }
        static void AH_Report(string json) { }
        static int AH_IsMobile() => 0;
        static void AH_TouchRead(float[] into, int length) { }
        static void AH_ReportUi(string json) { }
        static int AH_CanLockPointer() => 1;
#endif

        /// <summary>The browser can capture the mouse (pointer lock); iPhones can't.</summary>
        public static bool CanLockPointer => !IsWeb || AH_CanLockPointer() != 0;

        static int mobile = -1;

        /// <summary>
        /// A phone or tablet (the page decides: iOS, iPadOS or Android, or a touch screen with no
        /// mouse). Lighter defaults there; the on-screen controls follow <see cref="TouchInput.Active"/>.
        /// </summary>
        public static bool Mobile
        {
            get
            {
                if (mobile < 0) mobile = IsWeb && AH_IsMobile() != 0 ? 1 : 0;
                return mobile == 1;
            }
        }

        /// <summary>The page's on-screen controls, read once per frame (zeros on desktop and outside the browser).</summary>
        public static void ReadTouch(float[] into)
        {
            System.Array.Clear(into, 0, into.Length);
            if (IsWeb) AH_TouchRead(into, into.Length);
        }

        /// <summary>The page has the mouse captured (pointer lock).</summary>
        public static bool PointerLocked => IsWeb && AH_PointerLocked() != 0;

        static int leaveWarning = -1;

        /// <summary>The browser asks before leaving the page (the desktop game's "Quit the game?").</summary>
        public static void SetLeaveWarning(bool on)
        {
            if (!IsWeb || leaveWarning == (on ? 1 : 0)) return;
            leaveWarning = on ? 1 : 0;
            AH_SetLeaveWarning(leaveWarning);
        }

        static string reported;

        /// <summary>Publish what's on screen to the page as <c>window.afterHours</c> (only when it changes).</summary>
        /// <param name="selected">The menu row or button the keyboard or pad is on (its object's name), or empty.</param>
        /// <param name="blockers">What holds gameplay input now (GameRoot.BlockerList), or empty.</param>
        /// <param name="touch">For the on-screen controls (<see cref="TouchInput.Describe"/>): what the buttons say and which soft keys show.</param>
        /// <param name="test">Browser tests only (-ahWebTest): JSON with the player's position and what the input is doing.</param>
        public static void Report(string screen, int night, int quality, string selected, string blockers, string touch = "", string test = "")
        {
            if (!IsWeb) return;
            string json = $"{{\"screen\":\"{screen}\",\"night\":{night},\"quality\":{quality},\"qualityName\":\"{GraphicsQuality.Names[quality]}\"," +
                          $"\"selected\":\"{Escape(selected)}\",\"blockers\":\"{Escape(blockers)}\",\"mobile\":{(Mobile ? "true" : "false")}" +
                          $"{(string.IsNullOrEmpty(touch) ? "" : ",\"touch\":" + touch)}{(string.IsNullOrEmpty(test) ? "" : ",\"test\":" + test)}}}";
            if (json == reported) return;
            reported = json;
            AH_Report(json);
        }

        /// <summary>
        /// Browser tests (?arg=-ahWebTest): every button and row that can be clicked or tapped now,
        /// by name, as screen rectangles (window.afterHoursUi), so a test taps real buttons.
        /// </summary>
        public static void ReportUi()
        {
            if (!IsWeb) return;
            var sb = new System.Text.StringBuilder("{");
            var seen = new System.Collections.Generic.Dictionary<string, int>();
            var corners = new Vector3[4];
            foreach (var sel in Object.FindObjectsByType<UnityEngine.UI.Selectable>(FindObjectsSortMode.InstanceID))
            {
                if (!sel.isActiveAndEnabled || !sel.IsInteractable()) continue;
                bool shown = true;
                foreach (var g in sel.GetComponentsInParent<CanvasGroup>()) if (g.alpha < 0.05f || !g.blocksRaycasts) { shown = false; break; }
                if (!shown) continue;
                ((RectTransform)sel.transform).GetWorldCorners(corners); // screen pixels on the overlay canvas
                string n = sel.name;
                seen[n] = seen.TryGetValue(n, out var k) ? k + 1 : 1;
                if (seen[n] > 1) n += "#" + seen[n];
                if (sb.Length > 1) sb.Append(',');
                sb.Append('"').Append(Escape(n)).Append("\":[")
                  .Append(corners[0].x.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                  .Append(corners[0].y.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                  .Append((corners[2].x - corners[0].x).ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                  .Append((corners[2].y - corners[0].y).ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(']');
            }
            AH_ReportUi(sb.Append('}').ToString());
        }

        public static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
