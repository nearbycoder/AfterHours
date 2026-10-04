using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AfterHours.Tests
{
    /// <summary>
    /// Story and content validation: every ending is reachable, and every night only references
    /// surfaces, anchors, props, documents and people that actually exist.
    /// </summary>
    public class StoryTests
    {
        static StoryState Fresh() => new StoryState();

        [Test]
        public void SpotlessWhenNothingIsTouched()
        {
            var s = Fresh();
            s.SetFate("theo_note", Fate.Seen);
            Assert.AreEqual("spotless", Endings.ResolveId(s));
        }

        [Test]
        public void SpotlessWhenOnlyFollowingOrders()
        {
            // Marian asks for her shredder bag to go down the chute; obeying isn't meddling.
            var s = Fresh();
            s.SetFate("shred_bag", Fate.Trashed);
            s.SetFate("red_folder", Fate.Seen);
            Assert.AreEqual("spotless", Endings.ResolveId(s));
            s.SetFate("shred_bag", Fate.Kept);
            Assert.AreNotEqual("spotless", Endings.ResolveId(s));
        }

        [Test]
        public void AuditWithRedFolderAndTwoMore()
        {
            var s = Fresh();
            s.SetFate("red_folder", Fate.Delivered, "auditor");
            s.SetFate("vpn_log", Fate.Delivered, "auditor");
            s.SetFate("notepad_rubbing", Fate.Delivered, "auditor");
            Assert.AreEqual("audit", Endings.ResolveId(s));
        }

        [Test]
        public void AuditThroughPriyaAndNotes()
        {
            var s = Fresh();
            s.SetFate("red_folder", Fate.Delivered, "auditor");
            s.SetFate("theo_note", Fate.Delivered, "priya");
            s.Notes.Add(new NoteRecord { To = "auditor", Phrase = "wire" });
            Assert.AreEqual(3, Endings.AuditScore(s));
            Assert.AreEqual("audit", Endings.ResolveId(s));
        }

        [Test]
        public void CleanBooksWhenTheFolderIsShredded()
        {
            var s = Fresh();
            s.SetFate("vpn_log", Fate.Delivered, "auditor");
            s.SetFate("red_folder", Fate.Shredded);
            Assert.AreEqual("cleanbooks", Endings.ResolveId(s));
        }

        [Test]
        public void CleanBooksWhenTheoIsSoldOut()
        {
            var s = Fresh();
            s.SetFate("theo_note", Fate.Delivered, "marian");
            Assert.AreEqual("cleanbooks", Endings.ResolveId(s));
        }

        [Test]
        public void LooseThreadsWhenTheFolderIsKept()
        {
            var s = Fresh();
            s.SetFate("red_folder", Fate.Kept);
            s.SetFate("payment_ledger", Fate.Delivered, "auditor");
            Assert.AreEqual("loose", Endings.ResolveId(s));
        }

        [Test]
        public void ChoiceSpaceReachesEveryEnding()
        {
            // Sweep the fates of four key documents across every destination: all four endings
            // must appear, and resolving never throws.
            string[] docs = { "theo_note", "vpn_log", "notepad_rubbing", "red_folder" };
            var fates = new (Fate f, string to)[]
            {
                (Fate.Untouched, null), (Fate.Seen, null), (Fate.Kept, null), (Fate.Shredded, null),
                (Fate.Delivered, "auditor"), (Fate.Delivered, "priya"), (Fate.Delivered, "marian"), (Fate.Delivered, "theo"),
            };
            var seen = new HashSet<string>();
            int combos = 0;
            void Recurse(int i, StoryState s)
            {
                if (i == docs.Length)
                {
                    combos++;
                    var e = Endings.Resolve(s.Clone());
                    Assert.IsNotNull(e);
                    Assert.IsNotEmpty(e.Lines);
                    seen.Add(e.Id);
                    return;
                }
                foreach (var (f, to) in fates)
                {
                    var c = s.Clone();
                    if (f != Fate.Untouched) c.SetFate(docs[i], f, to);
                    Recurse(i + 1, c);
                }
            }
            Recurse(0, Fresh());
            Debug.Log($"[StoryTests] {combos} combinations, endings reached: {string.Join(", ", seen)}");
            CollectionAssert.IsSubsetOf(new[] { "audit", "cleanbooks", "loose", "spotless" }, seen.ToList());
        }

        [Test]
        public void DocsAreConsistent()
        {
            foreach (var d in Docs.All.Values)
            {
                Assert.IsFalse(string.IsNullOrEmpty(d.Title), d.Id + " title");
                Assert.IsFalse(string.IsNullOrEmpty(d.Body), d.Id + " body");
                if (!string.IsNullOrEmpty(d.Phrase)) Assert.IsTrue(Phrases.Text.ContainsKey(d.Phrase), d.Id + " phrase " + d.Phrase);
            }
            foreach (var k in Endings.KeyDocs) Assert.IsTrue(Docs.All.ContainsKey(k) && Docs.All[k].Evidence, "key doc " + k);
        }

        [Test]
        public void NightsReferenceRealContent()
        {
            var office = Resources.Load<GameObject>("Models/Office");
            var props = Resources.Load<GameObject>("Models/Props");
            Assert.IsNotNull(office, "Office model");
            Assert.IsNotNull(props, "Props model");
            var officeNames = new HashSet<string>(office.GetComponentsInChildren<Transform>(true).Select(t => t.name));
            var propNames = new HashSet<string>(props.GetComponentsInChildren<Transform>(true).Select(t => t.name.Replace("PROP_", "")));
            int surfaces = 0, spawns = 0, tasks = 0, lines = 0;
            for (int n = 1; n <= NightDefs.Count; n++)
            {
                var def = NightDefs.Get(n);
                Assert.IsNotNull(def, "night " + n);
                Assert.AreEqual(n, def.Number);
                var dirty = new HashSet<string>(def.Dirt.Select(d => d.surface));
                foreach (var (id, spec, _) in def.Dirt)
                {
                    Assert.IsTrue(officeNames.Contains("GRIME_" + id), $"night {n}: surface {id} missing from office");
                    foreach (var st in spec.Stamps.Where(s => s.Kind == StampKind.Image))
                        Assert.IsNotNull(Resources.Load<Texture2D>(st.Texture), $"night {n}: texture {st.Texture}");
                    if (!string.IsNullOrEmpty(spec.GhostTexture))
                        Assert.IsNotNull(Resources.Load<Texture2D>(spec.GhostTexture), $"night {n}: ghost {spec.GhostTexture}");
                    surfaces++;
                }
                foreach (var s in def.Spawns)
                {
                    Assert.IsTrue(propNames.Contains(s.Prop), $"night {n}: prop {s.Prop}");
                    if (s.Anchor != null) Assert.IsTrue(officeNames.Contains(s.Anchor), $"night {n}: anchor {s.Anchor}");
                    if (s.Doc != null) Assert.IsNotNull(Docs.Get(s.Doc), $"night {n}: doc {s.Doc}");
                    spawns++;
                }
                foreach (var t in def.Tasks)
                {
                    if (t.Kind == TaskKind.Clean)
                        foreach (var id in t.Targets) Assert.IsTrue(dirty.Contains(id), $"night {n}: task {t.Id} cleans {id}, which isn't dirty tonight");
                    tasks++;
                }
                Assert.IsTrue(def.Tasks.Any(t => t.Kind == TaskKind.Lights), $"night {n}: lights task");
                foreach (var c in def.Chat)
                {
                    Assert.IsTrue(People.Name.ContainsKey(c.Who), $"night {n}: chat speaker {c.Who}");
                    lines++;
                }
                foreach (var kv in def.Monitors)
                    if (kv.Value != null) Assert.IsNotNull(Resources.Load<Texture2D>(kv.Value), $"night {n}: screen {kv.Value}");
                foreach (var kv in def.MonitorDocs) Assert.IsNotNull(Docs.Get(kv.Value), $"night {n}: monitor doc {kv.Value}");
                Assert.Greater(def.Secrets.Count, 0, $"night {n}: secrets");
            }
            Debug.Log($"[StoryTests] nights ok: {surfaces} dirty surfaces, {spawns} spawns, {tasks} tasks, {lines} chat lines");
        }

        [Test]
        public void GrimePatternsGenerate()
        {
            var spec = new GrimeSpec().WithSeed(3).Add(GrimeStamp.Dust()).Add(GrimeStamp.Ring(0.5f, 0.5f)).Add(GrimeStamp.Steps(0.1f, 0.1f, 0.9f, 0.9f))
                .Add(GrimeStamp.Confetti(100, new Rect(0, 0, 1, 1))).Add(GrimeStamp.Trail(0.3f, 0.5f, new Vector2(0, 0), new Vector2(1, 1)));
            var d = GrimePatternData.Generate(spec, new Vector2(2, 1), 200, 100);
            float cov = d.Coverage.Average();
            Assert.Greater(cov, 0.05f);
            Assert.Less(cov, 0.95f);
        }
    }
}
