using UnityEngine;
using UnityEngine.InputSystem;

namespace AfterHours
{
    /// <summary>
    /// Short gamepad rumble pulses (throws, the vacuum's clunk, a surface coming clean, a made
    /// shot). Only while the pad is the active device and Vibration is on in Settings.
    /// </summary>
    public static class Rumble
    {
        /// <summary>Pulses asked for, and pulses actually sent to a pad (automation checks these).</summary>
        public static int Requested, Sent;
        static float until;

        public static void Pulse(float low, float high, float seconds)
        {
            Requested++;
            var pad = Gamepad.current;
            if (pad == null || !GameInput.UsingPad || !Settings.Current.Vibration) return;
            Sent++;
            pad.SetMotorSpeeds(Mathf.Clamp01(low), Mathf.Clamp01(high));
            until = Mathf.Max(until, GameTime.Unscaled + seconds);
            Tween.Delay(seconds, () => { if (GameTime.Unscaled >= until - 0.005f) Stop(); });
        }

        public static void Stop()
        {
            until = 0f;
            Gamepad.current?.ResetHaptics();
        }
    }
}
