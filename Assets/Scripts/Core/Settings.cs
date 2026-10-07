using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AfterHours
{
    /// <summary>Player preferences, stored as JSON next to the save file.</summary>
    [Serializable]
    public class Settings
    {
        public float MouseSensitivity = 1f;
        public float StickSensitivity = 1f;
        public bool InvertY;
        public bool Vibration = true;
        public float Fov = 72f;
        public bool HeadBob = true;
        public float MasterVolume = 0.9f;
        public float MusicVolume = 0.7f;
        public float SfxVolume = 0.9f;
        public float AmbienceVolume = 0.8f;
        public bool Fullscreen = true;
        public int Quality = 2;            // 0 low, 1 medium, 2 high (GraphicsQuality)
        public float RenderScale = 1f;
        public bool VSync = true;
        public int FrameCap;               // 0 = no cap, else frames per second
        public bool Captions = true;
        public bool ReduceFlashing;
        public bool AimHighlight = true;
        /// <summary>Image brightness, 0 to 1; 0.5 is the game as designed (see <see cref="PostFx.GammaFor"/>).</summary>
        public float Brightness = 0.5f;
        /// <summary>The brightness page offered on first launch has been seen and closed.</summary>
        public bool BrightnessChecked;
        /// <summary>Crouch and brisk walk: false holds the button, true presses once to switch on and again to switch off.</summary>
        public bool ToggleCrouch;
        public bool ToggleSprint;
        /// <summary>Clean and spray: false holds the button, true presses once to start and again to stop.</summary>
        public bool ToggleUse;
        /// <summary>Write a local playtest log (see PlaytestLog and docs/PLAYTEST.md).</summary>
        public bool PlaytestLog;
        /// <summary>Keyboard and mouse bindings that differ from the defaults (see <see cref="Controls"/>).</summary>
        public List<Binding> Bindings = new();
        /// <summary>Gamepad bindings that differ from the defaults (see <see cref="Controls.PadDefaults"/>).</summary>
        public List<Binding> PadBindings = new();

        static Settings current;
        public static event Action Changed;

        static string FilePath => Path.Combine(StoryState.Dir, "settings.json");

        public static Settings Current
        {
            get
            {
                if (current != null) return current;
                current = SaveIO.Load<Settings>(FilePath) ?? new Settings();
                return current;
            }
        }

        public static void Save()
        {
            try { SaveIO.WriteAtomic(FilePath, JsonUtility.ToJson(Current, true)); }
            catch (Exception e) { Debug.LogWarning($"[Settings] could not save: {e.Message}"); }
            Changed?.Invoke();
        }

        public static void NotifyChanged() => Changed?.Invoke();

        public static readonly int[] FrameCaps = { 0, 30, 60, 120, 144 };

        /// <summary>Fullscreen, VSync and frame cap, quality preset and render scale.</summary>
        public static void ApplyGraphics()
        {
            var s = Current;
            if (Application.isEditor) return;
            var args = Environment.GetCommandLineArgs();
            bool automated = Array.IndexOf(args, "-ahCapture") >= 0 || Array.IndexOf(args, "-ahAutopilot") >= 0 || Array.IndexOf(args, "-ahShowcase") >= 0 || Array.IndexOf(args, "-ahTrailer") >= 0;
            if (!automated)
            {
                // Automation runs uncapped (or on a fixed clock) and keeps its own timing.
                var mode = s.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                if (Screen.fullScreenMode != mode) Screen.fullScreenMode = mode;
                QualitySettings.vSyncCount = s.VSync ? 1 : 0;
                Application.targetFrameRate = s.FrameCap > 0 ? s.FrameCap : -1;
            }
            GraphicsQuality.Apply();
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)
                urp.renderScale = Mathf.Clamp(s.RenderScale, 0.5f, 1f);
        }
    }
}
