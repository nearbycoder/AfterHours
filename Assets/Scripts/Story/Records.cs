using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AfterHours
{
    [Serializable]
    public class NightBest
    {
        public int Night;
        public string Grade;
        public int Secrets;
        public int SecretsTotal;
    }

    /// <summary>
    /// The player's records across every run: best grade and most secrets per night, and the
    /// endings seen. Unlike the story save they are never rolled back (Night Select replays start
    /// from a snapshot) or wiped by New Game.
    /// </summary>
    [Serializable]
    public class Records
    {
        public List<NightBest> Nights = new();
        public List<string> Endings = new();

        public static int GradeRank(string g) => g switch { "S" => 4, "A" => 3, "B" => 2, "C" => 1, _ => 0 };

        public NightBest Best(int night) => Nights.FirstOrDefault(n => n.Night == night);

        /// <summary>Fold a night's result in. True if anything improved.</summary>
        public bool Merge(NightResult r)
        {
            if (r == null || !r.Completed) return false;
            var b = Best(r.Night);
            if (b == null)
            {
                Nights.Add(new NightBest { Night = r.Night, Grade = r.Grade, Secrets = r.Secrets, SecretsTotal = r.SecretsTotal });
                Nights.Sort((x, y) => x.Night.CompareTo(y.Night));
                return true;
            }
            bool better = false;
            if (GradeRank(r.Grade) > GradeRank(b.Grade)) { b.Grade = r.Grade; better = true; }
            if (r.Secrets > b.Secrets) { b.Secrets = r.Secrets; better = true; }
            if (r.SecretsTotal > b.SecretsTotal) b.SecretsTotal = r.SecretsTotal;
            return better;
        }

        public bool AddEnding(string id)
        {
            if (string.IsNullOrEmpty(id) || Endings.Contains(id)) return false;
            Endings.Add(id);
            return true;
        }

        public bool HasEnding(string id) => Endings.Contains(id);

        /// <summary>Records seeded from an existing save (players who started before records existed).</summary>
        public static Records From(StoryState s)
        {
            var r = new Records();
            if (s == null) return r;
            foreach (var res in s.Results) r.Merge(res);
            r.AddEnding(s.Ending);
            return r;
        }

        // ---- persistence -------------------------------------------------------------------------

        static Records current;
        static string FilePath => Path.Combine(StoryState.Dir, "records.json");

        public static Records Current
        {
            get
            {
                if (current != null) return current;
                current = SaveIO.Load<Records>(FilePath);
                if (current == null)
                {
                    current = From(StoryState.Load());
                    Save();
                }
                return current;
            }
        }

        public static void Save()
        {
            try { SaveIO.WriteAtomic(FilePath, JsonUtility.ToJson(Current, true)); }
            catch (Exception e) { Debug.LogWarning("[Records] could not save: " + e.Message); }
        }

        /// <summary>Note a finished night (called at clock-out).</summary>
        public static void Note(NightResult r)
        {
            if (Current.Merge(r)) Save();
        }

        /// <summary>Note an ending reached.</summary>
        public static void NoteEnding(string id)
        {
            if (Current.AddEnding(id)) Save();
        }
    }
}
