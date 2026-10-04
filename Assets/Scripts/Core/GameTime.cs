using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// Unscaled time for UI, tweens and menus. Identical to Unity's unscaled time in normal play;
    /// while the showcase records on a fixed capture step it advances by exactly that step per frame,
    /// so typewriters, fades and holds last the same on video however fast the machine renders.
    /// </summary>
    public static class GameTime
    {
        static int lastFrame = -1;
        static float now;

        public static float UnscaledDelta => Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;

        public static float Unscaled
        {
            get
            {
                if (Time.captureDeltaTime <= 0f) { lastFrame = -1; return Time.unscaledTime; }
                int f = Time.frameCount;
                if (lastFrame < 0) now = Time.unscaledTime;
                else if (f != lastFrame) now += (f - lastFrame) * Time.captureDeltaTime;
                lastFrame = f;
                return now;
            }
        }
    }
}
