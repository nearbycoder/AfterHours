using NUnit.Framework;
using UnityEngine;

namespace AfterHours.Tests
{
    /// <summary>The playtest log's lines are valid JSON with the common fields first.</summary>
    public class PlaytestLogTests
    {
        [System.Serializable]
        class Line { public float t; public int night; public float nt; public string type; public string id; public bool optional; public string[] open; public string to; }

        [Test]
        public void LinesAreJsonWithTheCommonFields()
        {
            var s = PlaytestLog.Format(12.5f, 3, 4.25f, "task_done", ("id", "whiteboard"), ("optional", false));
            Assert.AreEqual("{\"t\":12.5,\"night\":3,\"nt\":4.25,\"type\":\"task_done\",\"id\":\"whiteboard\",\"optional\":false}", s);
            var l = JsonUtility.FromJson<Line>(s);
            Assert.AreEqual(3, l.night);
            Assert.AreEqual("whiteboard", l.id);
        }

        [Test]
        public void ArraysNullsAndAwkwardTextSurvive()
        {
            var s = PlaytestLog.Format(1f, 1, 0f, "quit_mid_night", ("open", new[] { "trash", "lights" }), ("to", null), ("note", "a \"quoted\"\nline \\ here"));
            var l = JsonUtility.FromJson<Line>(s);
            Assert.AreEqual(new[] { "trash", "lights" }, l.open);
            StringAssert.Contains("\"to\":null", s);
            StringAssert.Contains("\\\"quoted\\\"\\nline \\\\ here", s);
        }

        [Test]
        public void NumbersDontDependOnTheLocale()
        {
            var was = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                StringAssert.StartsWith("{\"t\":1.5,", PlaytestLog.Format(1.5f, 0, 0f, "x"));
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = was; }
        }
    }
}
