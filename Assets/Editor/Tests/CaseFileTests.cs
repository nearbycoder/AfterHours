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

        [Test]
        public void ANewSaveKeepsItsOwnList()
        {
            var s = new StoryState { Night = 2 };
            s.NoteRead("fridge_note");
            s.SetFate("russ_slip", Fate.Seen);
            var loaded = StoryState.Upgrade(JsonUtility.FromJson<StoryState>(JsonUtility.ToJson(s)));
            Assert.AreEqual(new[] { "fridge_note" }, loaded.Read.Select(r => r.Id).ToArray(), "a save with a list isn't re-seeded");
        }
    }
}
