using System;
using System.IO;
using UnityEngine;

namespace AfterHours
{
    /// <summary>Player preferences, stored as JSON next to the save file.</summary>
    [Serializable]
    public class Settings
    {
        public float MouseSensitivity = 1f;
        public bool InvertY;
        public float Fov = 72f;
        public bool HeadBob = true;
        public float MasterVolume = 0.9f;
        public float MusicVolume = 0.7f;
        public float SfxVolume = 0.9f;
        public float AmbienceVolume = 0.8f;
        public bool Fullscreen = true;
        public int Quality = 2;            // 0 low, 1 medium, 2 high
        public float RenderScale = 1f;
        public bool Captions = true;
        public bool ReduceFlashing;

        static Settings current;
        public static event Action Changed;

        static string FilePath => Path.Combine(StoryState.Dir, "settings.json");

        public static Settings Current
        {
            get
            {
                if (current != null) return current;
                try
                {
                    current = File.Exists(FilePath) ? JsonUtility.FromJson<Settings>(File.ReadAllText(FilePath)) : new Settings();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Settings] could not read settings: {e.Message}");
                    current = new Settings();
                }
                return current ??= new Settings();
            }
        }

        public static void Save()
        {
            try { File.WriteAllText(FilePath, JsonUtility.ToJson(Current, true)); }
            catch (Exception e) { Debug.LogWarning($"[Settings] could not save: {e.Message}"); }
            Changed?.Invoke();
        }

        public static void NotifyChanged() => Changed?.Invoke();

        /// <summary>Fullscreen and render scale.</summary>
        public static void ApplyGraphics()
        {
            var s = Current;
            if (Application.isEditor) return;
            var args = Environment.GetCommandLineArgs();
            bool automated = Array.IndexOf(args, "-ahCapture") >= 0 || Array.IndexOf(args, "-ahAutopilot") >= 0 || Array.IndexOf(args, "-ahShowcase") >= 0;
            if (!automated)
            {
                var mode = s.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                if (Screen.fullScreenMode != mode) Screen.fullScreenMode = mode;
            }
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)
                urp.renderScale = Mathf.Clamp(s.RenderScale, 0.5f, 1f);
        }
    }
}
