using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace AfterHours.Tests
{
    /// <summary>Saves survive interrupted writes; records only ever improve; old settings files still load.</summary>
    public class SaveTests
    {
        string dir;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "ah-savetests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        static string Json(int night) => JsonUtility.ToJson(new StoryState { Night = night });

        [Test]
        public void AtomicWriteReplacesAndKeepsABackup()
        {
            var path = Path.Combine(dir, "save.json");
            SaveIO.WriteAtomic(path, Json(2));
            SaveIO.WriteAtomic(path, Json(3));
            Assert.AreEqual(3, SaveIO.Load<StoryState>(path).Night);
            Assert.IsTrue(File.Exists(path + ".bak"), "the previous save is kept as a backup");
            Assert.AreEqual(2, JsonUtility.FromJson<StoryState>(File.ReadAllText(path + ".bak")).Night);
            Assert.IsFalse(File.Exists(path + ".tmp"), "no temp file left behind");
        }

        [Test]
        public void ACrashMidWriteLeavesThePreviousSave()
        {
            var path = Path.Combine(dir, "save.json");
            SaveIO.WriteAtomic(path, Json(4));
            // A crash while writing leaves a half-written temp file; the real save is untouched.
            File.WriteAllText(path + ".tmp", Json(5).Substring(0, 12));
            Assert.AreEqual(4, SaveIO.Load<StoryState>(path).Night);
            // And the next save still goes through.
            SaveIO.WriteAtomic(path, Json(5));
            Assert.AreEqual(5, SaveIO.Load<StoryState>(path).Night);
        }

        [Test]
        public void ATruncatedSaveFallsBackToTheBackup()
        {
            var path = Path.Combine(dir, "save.json");
            SaveIO.WriteAtomic(path, Json(6));
            SaveIO.WriteAtomic(path, Json(7));
            // Simulate the old non-atomic failure: the main file cut off mid-write.
            File.WriteAllText(path, Json(7).Substring(0, 9));
            var s = SaveIO.Load<StoryState>(path);
            Assert.IsNotNull(s, "a damaged save doesn't lose the game");
            Assert.AreEqual(6, s.Night);
        }

        [Test]
        public void MissingFilesLoadAsNull()
        {
            Assert.IsNull(SaveIO.Load<StoryState>(Path.Combine(dir, "nothing.json")));
        }

        [Test]
        public void RecordsKeepTheBestOfEveryRun()
        {
            var r = new Records();
            Assert.IsTrue(r.Merge(new NightResult { Night = 3, Grade = "A", Secrets = 2, SecretsTotal = 5, Completed = true }));
            Assert.IsFalse(r.Merge(new NightResult { Night = 3, Grade = "C", Secrets = 1, SecretsTotal = 5, Completed = true }), "a worse replay changes nothing");
            Assert.AreEqual("A", r.Best(3).Grade);
            Assert.AreEqual(2, r.Best(3).Secrets);
            Assert.IsTrue(r.Merge(new NightResult { Night = 3, Grade = "B", Secrets = 4, SecretsTotal = 5, Completed = true }));
            Assert.AreEqual("A", r.Best(3).Grade, "grade and secrets are kept separately");
            Assert.AreEqual(4, r.Best(3).Secrets);
            Assert.IsTrue(r.Merge(new NightResult { Night = 3, Grade = "S", Secrets = 3, SecretsTotal = 5, Completed = true }));
            Assert.AreEqual("S", r.Best(3).Grade);
            Assert.AreEqual(4, r.Best(3).Secrets);
            Assert.IsFalse(r.Merge(new NightResult { Night = 4, Grade = "S", Completed = false }), "an unfinished night isn't a record");
        }

        [Test]
        public void EndingsAreCountedOnce()
        {
            var r = new Records();
            Assert.IsTrue(r.AddEnding("audit"));
            Assert.IsFalse(r.AddEnding("audit"));
            Assert.IsTrue(r.AddEnding("spotless"));
            Assert.AreEqual(2, r.Endings.Count);
            foreach (var id in r.Endings) CollectionAssert.Contains(Endings.Ids, id);
        }

        [Test]
        public void RecordsSeedFromAnOlderSave()
        {
            var s = new StoryState { Ending = "loose" };
            s.Results.Add(new NightResult { Night = 1, Grade = "B", Secrets = 3, SecretsTotal = 5, Completed = true });
            s.Results.Add(new NightResult { Night = 2, Grade = "S", Secrets = 5, SecretsTotal = 5, Completed = true });
            var r = Records.From(s);
            Assert.AreEqual("B", r.Best(1).Grade);
            Assert.AreEqual("S", r.Best(2).Grade);
            Assert.IsTrue(r.HasEnding("loose"));
        }

        [Test]
        public void OldSettingsFilesKeepTheirValuesAndGetNewDefaults()
        {
            // A v0.1.0 settings file: no fields added since.
            var old = "{\"MouseSensitivity\":1.7,\"InvertY\":true,\"Fov\":80.0,\"MusicVolume\":0.2}";
            var path = Path.Combine(dir, "settings.json");
            File.WriteAllText(path, old);
            var s = SaveIO.Load<Settings>(path);
            Assert.AreEqual(1.7f, s.MouseSensitivity, 1e-4f);
            Assert.IsTrue(s.InvertY);
            Assert.AreEqual(80f, s.Fov, 1e-4f);
            Assert.AreEqual(0.2f, s.MusicVolume, 1e-4f);
            var fresh = new Settings();
            Assert.AreEqual(fresh.SfxVolume, s.SfxVolume, 1e-4f, "fields missing from the file keep their defaults");
            Assert.AreEqual(fresh.HeadBob, s.HeadBob);
        }
    }
}
