using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AfterHours
{
    public enum Fate { Untouched, Seen, Kept, Returned, Shredded, Trashed, Delivered }

    [Serializable]
    public class EvidenceRecord
    {
        public string Id;
        public Fate Fate;
        public string To;      // person when Delivered
        public int Night;
    }

    [Serializable]
    public class NoteRecord
    {
        public string To;
        public string Phrase;
        public int Night;
    }

    [Serializable]
    public class NightResult
    {
        public int Night;
        public string Grade;
        public int Secrets;
        public int SecretsTotal;
        public int TasksDone;
        public int TasksTotal;
        public float Seconds;
        public bool Completed;
    }

    /// <summary>
    /// Everything the story remembers: evidence fates, flags, learned phrases, notes, suspicion
    /// and per-night results. Pure data; serialised to JSON.
    /// </summary>
    [Serializable]
    public class StoryState
    {
        public int Night = 1;
        public List<EvidenceRecord> Evidence = new();
        public List<string> Flags = new();
        public List<string> Phrases = new();
        public List<NoteRecord> Notes = new();
        public List<string> Inventory = new();     // kept doc ids (in your pocket / locker)
        public List<string> SecretsFound = new();
        public int Suspicion;
        public List<NightResult> Results = new();
        public string Ending;

        // ---- evidence ----------------------------------------------------------------------------

        public EvidenceRecord Record(string id)
        {
            var r = Evidence.FirstOrDefault(e => e.Id == id);
            if (r == null) { r = new EvidenceRecord { Id = id }; Evidence.Add(r); }
            return r;
        }

        public Fate FateOf(string id) => Evidence.FirstOrDefault(e => e.Id == id)?.Fate ?? Fate.Untouched;
        public string DeliveredTo(string id) { var r = Evidence.FirstOrDefault(e => e.Id == id); return r != null && r.Fate == Fate.Delivered ? r.To : null; }
        public bool IsDelivered(string id, string to) => DeliveredTo(id) == to;

        public void SetFate(string id, Fate f, string to = null)
        {
            var r = Record(id);
            r.Fate = f;
            r.To = to;
            r.Night = Night;
            if (f == Fate.Kept) { if (!Inventory.Contains(id)) Inventory.Add(id); }
            else Inventory.Remove(id);
        }

        // ---- flags & phrases ---------------------------------------------------------------------

        public bool Has(string flag) => Flags.Contains(flag);
        public void Set(string flag) { if (!Flags.Contains(flag)) Flags.Add(flag); }
        public void Clear(string flag) => Flags.Remove(flag);

        public bool Knows(string phrase) => Phrases.Contains(phrase);
        public bool Learn(string phrase)
        {
            if (string.IsNullOrEmpty(phrase) || Phrases.Contains(phrase)) return false;
            Phrases.Add(phrase);
            return true;
        }

        public bool NoteTo(string to, string phrase) => Notes.Any(n => n.To == to && (phrase == null || n.Phrase == phrase));
        public bool AnyNoteTo(string to) => Notes.Any(n => n.To == to);

        public NightResult ResultFor(int night) => Results.FirstOrDefault(r => r.Night == night);

        public StoryState Clone() => JsonUtility.FromJson<StoryState>(JsonUtility.ToJson(this));

        // ---- persistence -------------------------------------------------------------------------

        static string Dir => Application.persistentDataPath;
        static string SavePath => Path.Combine(Dir, "save.json");
        static string SnapPath(int night) => Path.Combine(Dir, $"night{night}_start.json");

        public void Save()
        {
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(this, true)); }
            catch (Exception e) { Debug.LogWarning("[Save] " + e.Message); }
        }

        /// <summary>Snapshot taken when a night starts, so Night Select can replay from there.</summary>
        public void SaveSnapshot()
        {
            try { File.WriteAllText(SnapPath(Night), JsonUtility.ToJson(this, true)); }
            catch (Exception e) { Debug.LogWarning("[Save] " + e.Message); }
        }

        public static StoryState Load()
        {
            try { if (File.Exists(SavePath)) return JsonUtility.FromJson<StoryState>(File.ReadAllText(SavePath)); }
            catch (Exception e) { Debug.LogWarning("[Save] " + e.Message); }
            return null;
        }

        public static StoryState LoadSnapshot(int night)
        {
            try { if (File.Exists(SnapPath(night))) return JsonUtility.FromJson<StoryState>(File.ReadAllText(SnapPath(night))); }
            catch (Exception e) { Debug.LogWarning("[Save] " + e.Message); }
            return null;
        }

        public static bool HasSnapshot(int night) => File.Exists(SnapPath(night));

        public static void DeleteAll()
        {
            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
                for (int n = 1; n <= 7; n++) if (File.Exists(SnapPath(n))) File.Delete(SnapPath(n));
            }
            catch (Exception e) { Debug.LogWarning("[Save] " + e.Message); }
        }
    }
}
