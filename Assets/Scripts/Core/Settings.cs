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

        static string FilePath => Path.Combine(Application.persistentDataPath, "settings.json");

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
    }
}
