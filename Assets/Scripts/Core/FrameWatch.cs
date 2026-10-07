using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// Frame times while a night is being played, and whether they say the game is running slowly:
    /// after <see cref="Warmup"/> seconds, the median frame over the last <see cref="Window"/>
    /// seconds of play is slower than <see cref="SlowMs"/> (under about 28 frames a second; VSync
    /// halving a 60 Hz screen to 30 doesn't count). Pure, so it can be tested.
    /// </summary>
    public class SlowFrames
    {
        public const float Warmup = 8f, Window = 12f, SlowMs = 36f;
        /// <summary>Single frames longer than this (a load, a hitch) aren't counted.</summary>
        public const float Hitch = 1f;

        readonly Queue<float> times = new();
        float warm, total;

        public void Reset() { times.Clear(); warm = 0f; total = 0f; }

        /// <summary>Count one frame of play, in seconds.</summary>
        public void Add(float dt)
        {
            if (dt <= 0f || dt > Hitch) return;
            if (warm < Warmup) { warm += dt; return; }
            times.Enqueue(dt);
            total += dt;
            while (times.Count > 1 && total - times.Peek() >= Window) total -= times.Dequeue();
        }

        /// <summary>A whole window has been measured since the warm-up.</summary>
        public bool Full => total >= Window * 0.999f;

        public float MedianMs
        {
            get
            {
                if (times.Count == 0) return 0f;
                var a = times.ToArray();
                System.Array.Sort(a);
                return a[a.Length / 2] * 1000f;
            }
        }

        public float Fps => MedianMs > 0f ? 1000f / MedianMs : 0f;
        public bool Slow => Full && MedianMs > SlowMs;

        /// <summary>The next lower setting: the preset one step down, then the render scale to 75% and 50%; null when nothing is lower.</summary>
        public static (int quality, float scale)? NextStep(int quality, float scale)
        {
            if (quality > 0) return (quality - 1, scale);
            if (scale > 0.76f) return (0, 0.75f);
            if (scale > 0.51f) return (0, 0.5f);
            return null;
        }

        /// <summary>
        /// Worth watching: not after "don't ask again", not with a frame-rate limit of 40 or under
        /// (the player chose slow frames), and only while there's something lower to offer.
        /// </summary>
        public static bool Worth(Settings s) =>
            !s.SlowFramesDeclined && (s.FrameCap <= 0 || s.FrameCap > 40) && NextStep(s.Quality, s.RenderScale) != null;
    }

    /// <summary>
    /// When a night runs slowly, offers the next lower graphics setting (round 9): a first-time
    /// player on a weak GPU or a large screen otherwise has no way to know Settings has a preset.
    /// Lowering it can be offered again, a step further, if it's still slow; "Keep" means never
    /// ask again; Never mind means not again this session. The offer goes in the playtest log.
    /// </summary>
    public class FrameWatch : MonoBehaviour
    {
        /// <summary>Off in automated runs (their frame times are the machine's load); the AutoPilot turns it on to test it.</summary>
        public static bool Watching = !GameRoot.Automated;
        public static FrameWatch Instance { get; private set; }
        public readonly SlowFrames Frames = new();
        /// <summary>How many times it has asked this session.</summary>
        public int Offers { get; private set; }
        /// <summary>The frame rate the last offer named.</summary>
        public int OfferedFps { get; private set; }
        bool notNow;

        void Awake() => Instance = this;

        void Update()
        {
            var root = GameRoot.Instance;
            var s = Settings.Current;
            bool playing = Watching && !notNow && root != null && root.InNight && !root.Director.Paused
                && GameInput.GameplayEnabled && Time.timeScale > 0f && (Application.isFocused || GameRoot.Automated)
                && SlowFrames.Worth(s);
            if (!playing) { Frames.Reset(); return; }
            Frames.Add(Time.unscaledDeltaTime);
            if (Frames.Slow) Offer(s);
        }

        void Offer(Settings s)
        {
            var next = SlowFrames.NextStep(s.Quality, s.RenderScale);
            int fps = OfferedFps = Mathf.RoundToInt(Frames.Fps);
            Frames.Reset();
            if (next == null) return;
            var (q, scale) = next.Value;
            Offers++;
            notNow = true; // Never mind: not again this session (lowering clears it)
            bool preset = q != s.Quality;
            PlaytestLog.Log("slow_frames", ("fps", fps), ("quality", s.Quality), ("scale", s.RenderScale));
            ChoiceMenu.Show("Running slowly", $"About {fps} frames a second. A lower setting may help.", new List<ChoiceMenu.Option>
            {
                new(preset ? $"Lower the graphics to {GraphicsQuality.Names[q]}" : $"Lower the render scale to {Mathf.RoundToInt(scale * 100f)}%",
                    "Settings → Display can change it back.", () => Lower(q, scale)),
                new(preset ? $"Keep {GraphicsQuality.Names[s.Quality]}" : "Keep these settings", "And don't ask again.", () =>
                {
                    s.SlowFramesDeclined = true;
                    Settings.Save();
                    PlaytestLog.Log("slow_frames_choice", ("choice", "keep"));
                }),
            });
        }

        void Lower(int quality, float scale)
        {
            var s = Settings.Current;
            bool preset = quality != s.Quality;
            s.Quality = quality;
            s.RenderScale = scale;
            Settings.ApplyGraphics();
            Settings.Save();
            notNow = false;
            Frames.Reset();
            PlaytestLog.Log("slow_frames_choice", ("choice", "lower"), ("quality", quality), ("scale", scale));
            Hud.Instance?.Toast(preset ? $"Graphics: {GraphicsQuality.Names[quality]}" : $"Render scale: {Mathf.RoundToInt(scale * 100f)}%",
                "Settings → Display", Ui.Accent, 2.6f);
        }
    }
}
