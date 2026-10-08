using NUnit.Framework;
using UnityEngine;

namespace AfterHours.Tests
{
    /// <summary>Night 7's thunder stutters the lights: out and back, or a smooth dip with Reduce flashing.</summary>
    public class StormTests
    {
        [Test]
        public void WithoutReduceFlashingTheLightsDropOutAndComeBack()
        {
            Assert.AreEqual(0f, Room.StutterGain(0f, 0.2f, false));
            Assert.AreEqual(0f, Room.StutterGain(0.19f, 0.2f, false));
            Assert.AreEqual(1f, Room.StutterGain(0.2f, 0.2f, false));
            Assert.AreEqual(0.2f, Room.StutterLength(0.2f, false));
        }

        [Test]
        public void WithReduceFlashingTheyDimSmoothlyAndNeverGoDark()
        {
            foreach (float seconds in new[] { 0.08f, 0.2f, 0.3f })
            {
                float len = Room.StutterLength(seconds, true);
                float last = 1f, min = 1f, steepest = 0f;
                const float step = 1f / 240f;
                for (float t = 0; t <= len; t += step)
                {
                    float g = Room.StutterGain(t, seconds, true);
                    min = Mathf.Min(min, g);
                    steepest = Mathf.Max(steepest, Mathf.Abs(g - last) / step);
                    last = g;
                }
                Assert.AreEqual(Room.CalmDip, min, 0.001f, $"{seconds}s: dims to the dip and no further");
                Assert.LessOrEqual(steepest, 2.5f, $"{seconds}s: no faster than 2.5 a second");
                Assert.AreEqual(1f, Room.StutterGain(len, seconds, true), 0.001f, $"{seconds}s: back to full at the end");
                Assert.AreEqual(1f, Room.StutterGain(0f, seconds, true), 0.001f, $"{seconds}s: starts from full");
            }
        }
    }
}
