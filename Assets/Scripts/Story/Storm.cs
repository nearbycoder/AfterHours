using System.Linq;
using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// Night 7's thunder: a clap, the screen's flicker and the lights stuttering in every lit room
    /// (a smooth dip with Reduce flashing, see <see cref="Room.Stutter"/>).
    /// </summary>
    public static class Storm
    {
        /// <summary>Claps so far (automation checks this).</summary>
        public static int Strikes { get; private set; }

        /// <param name="stutter">How long the lights drop out (random, 0.08–0.3 s, unless given).</param>
        public static void Strike(OfficeBuilder office, float? stutter = null)
        {
            Strikes++;
            Sfx.Play("thunder", null, 0.8f, Random.Range(0.85f, 1.1f), 0f, AudioBus.Ambience);
            PostFx.Instance?.Flicker(1f);
            var lit = office.Rooms.Values.Where(r => r.LightsOn).ToList();
            foreach (var r in lit) r.Stutter(stutter ?? Random.Range(0.08f, 0.3f));
        }
    }
}
