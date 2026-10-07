using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AfterHours.Tests
{
    /// <summary>The case file: what was read, when, what became of it, and old saves.</summary>
    public class CaseFileTests
    {
        [Test]
        public void ReadsAreRecordedOnceWithTheNightTheyHappened()
        {
            var s = new StoryState { Night = 1 };
            s.NoteRead("dana_welcome");
            s.NoteRead("theo_note");
            s.Night = 2;
            s.NoteRead("theo_note");   // read again on night 2: still night 1's
            s.NoteRead("russ_slip");
            s.NoteRead(null);
            Assert.AreEqual(new[] { "dana_welcome", "theo_note", "russ_slip" }, s.Read.Select(r => r.Id).ToArray());
            Assert.AreEqual(new[] { 1, 1, 2 }, s.Read.Select(r => r.Night).ToArray());
        }

        [Test]
        public void FateLabelsSayWhatBecameOfEachDocument()
        {
            var s = new StoryState();
            s.SetFate("theo_note", Fate.Delivered, "priya");
            s.SetFate("russ_slip", Fate.Kept);
            s.SetFate("vpn_log", Fate.Shredded);
            s.SetFate("flight_note", Fate.Trashed);
            s.SetFate("walt_card", Fate.Seen);
            Assert.AreEqual("left for Priya", s.FateLabel("theo_note", true));
            Assert.AreEqual("kept", s.FateLabel("russ_slip", true));
            Assert.AreEqual("shredded", s.FateLabel("vpn_log", true));
            Assert.AreEqual("thrown away", s.FateLabel("flight_note", true));
            Assert.AreEqual("left where it was", s.FateLabel("walt_card", true));
            Assert.IsNull(s.FateLabel("dana_welcome", false), "plain notes and screens have no fate");
        }

        [Test]
        public void AnOldSaveBuildsItsCaseFileFromEvidence()
        {
            // A round 2 save: evidence records, no reading list.
            var old = new StoryState { Night = 3 };
            old.Night = 1; old.SetFate("theo_note", Fate.Kept);
            old.Night = 2; old.SetFate("russ_slip", Fate.Seen);
            old.Night = 3;
            old.Record("audit_agenda");   // untouched: never read
            var json = JsonUtility.ToJson(old).Replace("\"Read\":[],", "");
            Assert.IsFalse(json.Contains("\"Read\""), "the old file has no reading list");
            var loaded = StoryState.Upgrade(JsonUtility.FromJson<StoryState>(json));
            Assert.AreEqual(new[] { "theo_note", "russ_slip" }, loaded.Read.Select(r => r.Id).ToArray());
            Assert.AreEqual(new[] { 1, 2 }, loaded.Read.Select(r => r.Night).ToArray());
        }

        static int Line(NightDef def, string text) => def.Chat.FindIndex(c => c.Text.StartsWith(text));

        [Test]
        public void TheMorningChatFollowsWhatYouDid()
        {
            var def = NightDefs.Get(1);
            var toTheo = new StoryState { Night = 2 };
            toTheo.SetFate("theo_note", Fate.Delivered, "theo");
            var toDana = new StoryState { Night = 2 };
            toDana.SetFate("theo_note", Fate.Delivered, "dana");
            var a = def.MorningChat(toTheo);
            var b = def.MorningChat(toDana);
            Assert.Contains(Line(def, "Morning all!"), a, "unconditional lines always show");
            Assert.Contains(Line(def, "who left this on my desk"), a);
            Assert.IsFalse(a.Contains(Line(def, "Found a crumpled note")));
            Assert.Contains(Line(def, "Found a crumpled note"), b);
            Assert.IsFalse(b.Contains(Line(def, "who left this on my desk")));
            Assert.AreEqual(a.OrderBy(i => i).ToArray(), a.ToArray(), "in the order they're written");
        }

        [Test]
        public void ChatsAreKeptPerNightAndAReplayReplacesIt()
        {
            var s = new StoryState { Night = 3 };
            s.NoteChat(1, new[] { 0, 1 });
            s.NoteChat(2, new[] { 3 });
            s.NoteChat(1, new[] { 0, 2 });   // night 1 replayed from Night Select
            var loaded = JsonUtility.FromJson<StoryState>(JsonUtility.ToJson(s));
            Assert.AreEqual(2, loaded.Chats.Count);
            Assert.AreEqual(new[] { 0, 2 }, loaded.ChatFor(1).Lines.ToArray());
            Assert.AreEqual(new[] { 3 }, loaded.ChatFor(2).Lines.ToArray());
            Assert.IsNull(loaded.ChatFor(3));
        }

        [Test]
        public void AnOldSaveGetsItsChatsFromTheSnapshots()
        {
            // A round 5 save on night 4: three nights played, no chats kept.
            var old = new StoryState { Night = 4 };
            for (int n = 1; n <= 3; n++) old.Results.Add(new NightResult { Night = n, Grade = "A" });
            var json = JsonUtility.ToJson(old).Replace("\"Chats\":[],", "");
            Assert.IsFalse(json.Contains("\"Chats\""), "the old file has no chats");
            // Night 2's snapshot is the state night 1's chat was chosen from; night 4's is missing.
            var snap2 = new StoryState { Night = 2 };
            snap2.SetFate("theo_note", Fate.Delivered, "theo");
            var snap3 = new StoryState { Night = 3 };
            var snaps = new System.Collections.Generic.Dictionary<int, StoryState> { { 2, snap2 }, { 3, snap3 } };
            var loaded = StoryState.Upgrade(JsonUtility.FromJson<StoryState>(json), n => snaps.TryGetValue(n, out var x) ? x : null);
            Assert.AreEqual(NightDefs.Get(1).MorningChat(snap2), loaded.ChatFor(1).Lines);
            Assert.AreEqual(NightDefs.Get(2).MorningChat(snap3), loaded.ChatFor(2).Lines);
            Assert.IsNull(loaded.ChatFor(3), "no snapshot, no chat");
            // A save that kept its chats isn't seeded again.
            var kept = new StoryState { Night = 3 };
            kept.Results.Add(new NightResult { Night = 1 });
            kept.Results.Add(new NightResult { Night = 2 });
            kept.NoteChat(1, new[] { 0 });
            var again = StoryState.Upgrade(JsonUtility.FromJson<StoryState>(JsonUtility.ToJson(kept)), n => snaps.TryGetValue(n, out var x) ? x : null);
            Assert.AreEqual(new[] { 0 }, again.ChatFor(1).Lines.ToArray());
            Assert.IsNull(again.ChatFor(2));
        }

        [Test]
        public void ANewSaveKeepsItsOwnList()
        {
            var s = new StoryState { Night = 2 };
            s.NoteRead("fridge_note");
            s.SetFate("russ_slip", Fate.Seen);
            var loaded = StoryState.Upgrade(JsonUtility.FromJson<StoryState>(JsonUtility.ToJson(s)));
            Assert.AreEqual(new[] { "fridge_note" }, loaded.Read.Select(r => r.Id).ToArray(), "a save with a list isn't re-seeded");
        }
            // ---- the case file from the title (round 7) ----------------------------------------------

        [Test]
        public void EntriesListEachNightsDocumentsThenItsMorningNewestFirst()
        {
            var s = new StoryState { Night = 1 };
            s.NoteRead("dana_welcome");
            s.NoteRead("theo_note");
            s.Night = 2;
            s.NoteRead("russ_slip");
            s.NoteRead("no_such_document");   // a document that no longer exists isn't listed
            s.NoteChat(1, new[] { 0, 1 });
            s.Night = 3;
            s.NoteChat(2, new[] { 0 });
            s.NoteChat(3, new[] { 0 });       // tonight's chat (a replay's) isn't a past morning yet
            var e = CaseFile.Entries(s);
            Assert.AreEqual(new[] { "russ_slip", "chat:2", "dana_welcome", "theo_note", "chat:1" }, e.Select(x => x.id).ToArray());
            Assert.AreEqual(new[] { 2, 2, 1, 1, 1 }, e.Select(x => x.night).ToArray());
            Assert.AreEqual("3 documents you've read, 2 mornings of chat", CaseFile.Summary(s));
            Assert.AreEqual(("Tuesday morning · #general", "the office chat"), CaseFile.Describe(s, "chat:1"));
            Assert.AreEqual(1, CaseFile.ChatNight("chat:1"));
            Assert.AreEqual(-1, CaseFile.ChatNight("theo_note"));
        }

        [Test]
        public void TheWholeStoryIsListedAfterTheEnding()
        {
            // After night 7: the save is on night 8 with an ending; night 7 kept an empty chat.
            var s = new StoryState { Night = 7 };
            s.NoteRead("red_folder");
            s.SetFate("red_folder", Fate.Delivered, "auditor");
            s.Night = 6;
            s.NoteRead("payment_ledger");
            s.NoteChat(6, new[] { 0, 4 });
            s.Night = 8;
            s.NoteChat(7, NightDefs.Get(7).MorningChat(s));
            s.Ending = "audit";
            var loaded = JsonUtility.FromJson<StoryState>(JsonUtility.ToJson(s));
            var e = CaseFile.Entries(loaded);
            Assert.AreEqual(new[] { CaseFile.EndingId, "red_folder", "payment_ledger", "chat:6" }, e.Select(x => x.id).ToArray(), "the ending, then night 7's documents; night 7 has no morning chat");
            Assert.AreEqual(CaseFile.EndingNight, e[0].night);
            Assert.AreEqual(("Tuesday morning · The Meridian Daily", "the ending: The Audit"), CaseFile.Describe(loaded, CaseFile.EndingId));
            Assert.AreEqual(("The red folder", "left for the auditor"), CaseFile.Describe(loaded, "red_folder"));
            Assert.AreEqual("Monday morning", CaseFile.MorningLabel(6));
            Assert.AreEqual(2, CaseFile.MorningLines(loaded, 6).Count);
            Assert.IsTrue(CaseFilePanel.HasEntries(loaded));
        }

        [Test]
        public void ANewOrMissingSaveHasNoCaseFile()
        {
            Assert.IsFalse(CaseFilePanel.HasEntries(null));
            Assert.IsFalse(CaseFilePanel.HasEntries(new StoryState()));
            var chatOnlyEmpty = new StoryState { Night = 2 };
            chatOnlyEmpty.NoteChat(1, new int[0]);   // a morning with no messages isn't listed
            Assert.IsFalse(CaseFilePanel.HasEntries(chatOnlyEmpty));
            Assert.AreEqual(0, CaseFile.MorningLines(chatOnlyEmpty, 5).Count);
        }
    }
}
