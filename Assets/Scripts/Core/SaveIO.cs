using System;
using System.IO;
using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// Crash-safe JSON files. Writes go to a temp file that then replaces the old one (keeping it as
    /// <c>.bak</c>), so a crash or power cut mid-write never leaves a half-written save. Reads fall
    /// back to the backup when the main file is missing or unreadable.
    /// </summary>
    public static class SaveIO
    {
        public static void WriteAtomic(string path, string text)
        {
            string tmp = path + ".tmp", bak = path + ".bak";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var w = new StreamWriter(fs))
            {
                w.Write(text);
                w.Flush();
                fs.Flush(true);
            }
            if (File.Exists(path))
            {
                try
                {
                    File.Replace(tmp, path, bak);
                    return;
                }
                catch (Exception e) when (e is PlatformNotSupportedException or IOException or UnauthorizedAccessException)
                {
                    // Fallback: keep the old file as the backup, then move the new one in.
                    File.Copy(path, bak, true);
                    File.Delete(path);
                }
            }
            File.Move(tmp, path);
        }

        /// <summary>The object in <paramref name="path"/>, or in its backup if that fails; null if neither reads.</summary>
        public static T Load<T>(string path) where T : class
        {
            foreach (var p in new[] { path, path + ".bak" })
            {
                try
                {
                    if (!File.Exists(p)) continue;
                    var obj = JsonUtility.FromJson<T>(File.ReadAllText(p));
                    if (obj != null) return obj;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Save] could not read {Path.GetFileName(p)}: {e.Message}");
                }
            }
            return null;
        }

        public static bool Exists(string path) => File.Exists(path) || File.Exists(path + ".bak");

        public static void Delete(string path)
        {
            foreach (var p in new[] { path, path + ".bak", path + ".tmp" })
                if (File.Exists(p)) File.Delete(p);
        }
    }
}
