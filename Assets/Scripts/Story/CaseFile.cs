using System.Collections.Generic;
using System.Linq;

namespace AfterHours
{
    /// <summary>
    /// What the case file lists for a story: every document read (that still exists) and each
    /// morning's chat, grouped by night. Shared by the clipboard (during a night) and the title
    /// (between sessions and after the ending), which read different states.
    /// </summary>
    public static class CaseFile
    {
        /// <summary>Entries for a morning's chat are "chat:N", N the night before it.</summary>
        public const string ChatPrefix = "chat:";
        /// <summary>The ending's entry (a finished story only), listed first under its own heading.</summary>
        public const string EndingId = "ending";
        /// <summary>The "night" the ending's entry is grouped under: the morning after the last night.</summary>
        public static int EndingNight => NightDefs.Count + 1;

        /// <summary>Every document read so far that still exists, newest night first, in reading order within a night.</summary>
        public static List<ReadRecord> Reads(StoryState s) => (s?.Read ?? new List<ReadRecord>())
            .Where(r => Docs.Get(r.Id) != null)
            .Select((r, i) => (r, i)).OrderByDescending(x => x.r.Night).ThenBy(x => x.i).Select(x => x.r).ToList();

        /// <summary>The nights before the state's night whose morning chat was kept and had messages, newest first.</summary>
        public static List<int> Mornings(StoryState s) => (s?.Chats ?? new List<ChatRecord>())
            .Where(c => c.Night < s.Night && c.Lines != null && c.Lines.Count > 0 && NightDefs.Get(c.Night) != null)
            .Select(c => c.Night).Distinct().OrderByDescending(n => n).ToList();

        /// <summary>
        /// The list in order: the ending, once the story has one; then newest night first, each
        /// night's documents, then the next morning's chat.
        /// </summary>
        public static List<(int night, string id)> Entries(StoryState s)
        {
            var reads = Reads(s);
            var mornings = Mornings(s);
            var entries = new List<(int night, string id)>();
            if (!string.IsNullOrEmpty(s?.Ending)) entries.Add((EndingNight, EndingId));
            foreach (int n in reads.Select(r => r.Night).Concat(mornings).Distinct().OrderByDescending(n => n))
            {
                entries.AddRange(reads.Where(r => r.Night == n).Select(r => (n, r.Id)));
                if (mornings.Contains(n)) entries.Add((n, ChatPrefix + n));
            }
            return entries;
        }

        /// <summary>The night a chat entry belongs to, or -1 for a document.</summary>
        public static int ChatNight(string id) =>
            id != null && id.StartsWith(ChatPrefix) && int.TryParse(id.Substring(ChatPrefix.Length), out int n) ? n : -1;

        /// <summary>A past morning's messages, as they were shown.</summary>
        public static List<ChatLine> MorningLines(StoryState s, int night)
        {
            var def = NightDefs.Get(night);
            var rec = s?.ChatFor(night);
            if (def == null || rec == null) return new List<ChatLine>();
            return rec.Lines.Where(i => i >= 0 && i < def.Chat.Count).Select(i => def.Chat[i]).ToList();
        }

        public static string MorningLabel(int night) => $"{NightDefs.MorningAfter(night)} morning";

        /// <summary>An entry's title, and the small note after it (what became of a document, or "the office chat").</summary>
        public static (string title, string note) Describe(StoryState s, string id)
        {
            int night = ChatNight(id);
            if (night >= 0) return (MorningLabel(night) + " · #general", "the office chat");
            if (id == EndingId) return ($"{NightDefs.MorningAfter(NightDefs.Count)} morning · The Meridian Daily", "the ending: " + Endings.TitleOf(s?.Ending));
            var d = Docs.Get(id);
            return d == null ? (id, null) : (d.Title, s?.FateLabel(d.Id, d.Evidence));
        }

        /// <summary>"12 documents you've read, 3 mornings of chat".</summary>
        public static string Summary(StoryState s)
        {
            int docs = Reads(s).Count, mornings = Mornings(s).Count;
            string d = docs == 1 ? "One document" : docs + " documents";
            return $"{d} you've read{(mornings > 0 ? $", {(mornings == 1 ? "one morning" : mornings + " mornings")} of chat" : "")}";
        }
    }
}
