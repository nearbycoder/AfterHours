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

        /// <summary>Graphics fidelity for a first visit on the web (Medium; desktop starts on High).</summary>
        public const int DefaultQuality = 1;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int AH_PointerLocked();
        [DllImport("__Internal")] static extern void AH_SetLeaveWarning(int on);
        [DllImport("__Internal")] static extern void AH_Report(string json);
#else
        static int AH_PointerLocked() => 0;
        static void AH_SetLeaveWarning(int on) { }
        static void AH_Report(string json) { }
#endif

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
        public static void Report(string screen, int night, int quality, string selected, string blockers)
        {
            if (!IsWeb) return;
            string json = $"{{\"screen\":\"{screen}\",\"night\":{night},\"quality\":{quality},\"qualityName\":\"{GraphicsQuality.Names[quality]}\"," +
                          $"\"selected\":\"{Escape(selected)}\",\"blockers\":\"{Escape(blockers)}\"}}";
            if (json == reported) return;
            reported = json;
            AH_Report(json);
        }

        static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
