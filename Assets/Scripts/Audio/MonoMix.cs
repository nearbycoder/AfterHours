using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// Mono audio (Settings → Sound): sits beside the AudioListener, so it gets the game's final
    /// mix, and with the setting on folds every channel to their average, the same on both sides.
    /// Nothing panned to one side is lost to a missing ear or a single earbud.
    /// </summary>
    public class MonoMix : MonoBehaviour
    {
        public static MonoMix Instance { get; private set; }

        /// <summary>Energy in the mix, going in and coming out (automation reads this; summed while <see cref="Metering"/>).</summary>
        public struct Meter
        {
            public double InLeft, InRight, InSide, OutLeft, OutRight, OutSide;
            public long Frames;
        }

        public static bool Metering;
        static Meter meter;
        static readonly object meterLock = new();

        volatile bool on; // read on the audio thread

        void Awake() { Instance = this; on = Settings.Current.MonoAudio; }
        void Update() => on = Settings.Current.MonoAudio;

        /// <summary>Averages each frame's channels into all of them (2 or more channels; 1 is left alone).</summary>
        public static void Downmix(float[] data, int channels)
        {
            if (channels < 2) return;
            for (int i = 0; i + channels <= data.Length; i += channels)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++) sum += data[i + c];
                float m = sum / channels;
                for (int c = 0; c < channels; c++) data[i + c] = m;
            }
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            bool metering = Metering && channels >= 2;
            double inL = 0, inR = 0, inS = 0;
            if (metering) Measure(data, channels, out inL, out inR, out inS);
            if (on) Downmix(data, channels);
            if (!metering) return;
            Measure(data, channels, out double outL, out double outR, out double outS);
            lock (meterLock)
            {
                meter.InLeft += inL; meter.InRight += inR; meter.InSide += inS;
                meter.OutLeft += outL; meter.OutRight += outR; meter.OutSide += outS;
                meter.Frames += data.Length / channels;
            }
        }

        static void Measure(float[] data, int channels, out double left, out double right, out double side)
        {
            left = right = side = 0;
            for (int i = 0; i + channels <= data.Length; i += channels)
            {
                float l = data[i], r = data[i + 1];
                left += l * l; right += r * r; side += (l - r) * (l - r);
            }
        }

        public static void ResetMeter() { lock (meterLock) meter = default; }
        public static Meter ReadMeter() { lock (meterLock) return meter; }
    }
}
