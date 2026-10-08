using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// Opt-in playtest log (Settings, or <c>-playtest</c>): one JSON object per line, one file per
    /// session, in the save folder under <c>playtest/</c>. Nothing leaves the machine; testers send
    /// the file by hand. Records what pacing needs: nights started and finished, tasks ticked,
    /// surfaces cleaned, secrets, evidence choices, stuck moments (glints), recovered items,
    /// clipboard opens, pauses and quitting mid-night. Read it with Tools/playtest_report.py.
    /// </summary>
    public class PlaytestLog : MonoBehaviour
    {
        public static PlaytestLog Instance { get; private set; }
        public static bool Enabled => Settings.Current.PlaytestLog || GameRoot.HasArg("-playtest");
        /// <summary>The file being written this session, or null if nothing has been logged.</summary>
        public static string CurrentFile { get; private set; }
        public static int Lines { get; private set; }

        const long MaxBytes = 5 * 1024 * 1024;
        static StreamWriter writer;
        static float sessionStart;

        public static PlaytestLog Create(NightDirector director)
        {
            var go = new GameObject("PlaytestLog");
            Instance = go.AddComponent<PlaytestLog>();
            sessionStart = Time.realtimeSinceStartup;
            Events.On(GameEvent.TaskDone, id => Log("task_done", ("id", id), ("optional", director.Def?.Tasks.FirstOrDefault(t => t.Id == id)?.Optional ?? false)));
            Events.On(GameEvent.SurfaceCleaned, id => Log("surface_done", ("id", id)));
            Events.On(GameEvent.SecretFound, id => Log("secret", ("id", id)));
            Events.On(GameEvent.EvidenceFate, id => Log("evidence", ("id", id), ("fate", Story.State.FateOf(id).ToString()), ("to", Story.State.DeliveredTo(id))));
            Events.On(GameEvent.NoteLeft, to => Log("note", ("to", to)));
            return Instance;
        }

        // ---- the format (pure; tested in EditMode) --------------------------------------------------

        /// <summary>One line of JSON: the common fields, then the event's own.</summary>
        public static string Format(float t, int night, float nightT, string type, params (string key, object value)[] fields)
        {
            var sb = new StringBuilder(128);
            sb.Append("{\"t\":").Append(Num(t)).Append(",\"night\":").Append(night).Append(",\"nt\":").Append(Num(nightT))
              .Append(",\"type\":").Append(Str(type));
            foreach (var (k, v) in fields)
            {
                sb.Append(',').Append(Str(k)).Append(':');
                sb.Append(v switch
                {
                    null => "null",
                    bool b => b ? "true" : "false",
                    int i => i.ToString(CultureInfo.InvariantCulture),
                    float f => Num(f),
                    double d => Num((float)d),
                    string[] a => "[" + string.Join(",", a.Select(Str)) + "]",
                    _ => Str(v.ToString()),
                });
            }
            return sb.Append('}').ToString();
        }

        static string Num(float f) => f.ToString("0.###", CultureInfo.InvariantCulture);

        static string Str(string s)
        {
            var sb = new StringBuilder(s.Length + 2).Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        // ---- writing ------------------------------------------------------------------------------

        public static void Log(string type, params (string key, object value)[] fields)
        {
            if (!Enabled) return;
            try
            {
                if (writer == null) Open();
                if (writer == null || writer.BaseStream.Length > MaxBytes) return;
                var d = NightDirector.Instance;
                int night = d != null && d.Def != null ? d.Def.Number : 0;
                float nt = d != null && d.Running ? d.Elapsed : 0f;
                writer.WriteLine(Format(Time.realtimeSinceStartup - sessionStart, night, nt, type, fields));
                Lines++;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Playtest] " + e.Message);
                writer = null;
            }
        }

        static void Open()
        {
            var dir = Path.Combine(StoryState.Dir, "playtest");
            Directory.CreateDirectory(dir);
            CurrentFile = Path.Combine(dir, $"session-{DateTime.Now:yyyyMMdd-HHmmss}.jsonl");
            writer = new StreamWriter(CurrentFile, true, new UTF8Encoding(false)) { AutoFlush = true }; // a crash loses nothing
            var s = Settings.Current;
            writer.WriteLine(Format(Time.realtimeSinceStartup - sessionStart, 0, 0, "session_start",
                ("version", Application.version), ("platform", Application.platform.ToString()), ("os", SystemInfo.operatingSystem),
                ("gpu", SystemInfo.graphicsDeviceName), ("api", SystemInfo.graphicsDeviceType.ToString()),
                ("screen", $"{Screen.width}x{Screen.height}"), ("quality", GraphicsQuality.Names[GraphicsQuality.Level]),
                ("pad", UnityEngine.InputSystem.Gamepad.current?.displayName), ("captions", s.Captions), ("highlight", s.AimHighlight), ("mono", s.MonoAudio),
                ("rebound", s.Bindings?.Count ?? 0)));
            Lines++;
            Debug.Log("[Playtest] logging to " + CurrentFile);
        }

        /// <summary>Play begins (after the title card); a restart logs another start.</summary>
        public static void NightStart(NightDef def) =>
            Log("night_start", ("title", def.Title), ("tasks", def.Tasks.Count(t => !t.Optional)), ("bonus", def.Tasks.Count(t => t.Optional)), ("secrets", def.Secrets.Count));

        public static void NightEnd(NightResult r, string[] open) =>
            Log("night_end", ("grade", r.Grade), ("seconds", r.Seconds), ("tasks_done", r.TasksDone), ("tasks_total", r.TasksTotal),
                ("secrets", r.Secrets), ("secrets_total", r.SecretsTotal), ("open", open));

        /// <summary>The required tasks still open tonight (for quitting or clocking out early).</summary>
        public static string[] OpenTasks()
        {
            var d = NightDirector.Instance;
            return d != null && d.Def != null ? d.RequiredRemaining().Select(t => t.Id).ToArray() : new string[0];
        }

        void OnApplicationQuit()
        {
            var d = NightDirector.Instance;
            if (d != null && d.Running && !d.Paused) Log("quit_mid_night", ("open", OpenTasks()));
            Log("session_end");
            writer?.Dispose();
            writer = null;
        }
    }
}
