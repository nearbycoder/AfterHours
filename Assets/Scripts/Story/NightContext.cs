using System;
using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    /// <summary>What a night's script can touch. Handlers registered here are removed when the night ends.</summary>
    public class NightContext
    {
        public NightDirector Director;
        public OfficeBuilder Office => Director.Office;
        public StoryState State => Story.State;
        public Furnisher Furniture => Director.Furniture;
        readonly List<(GameEvent e, Action<string> h)> handlers = new();
        readonly List<Action<float>> ticks = new();
        public readonly Dictionary<string, GameObject> Spawned = new();

        public void On(GameEvent e, Action<string> h)
        {
            Events.On(e, h);
            handlers.Add((e, h));
        }

        public void Tick(Action<float> t) => ticks.Add(t);

        public void Delay(float seconds, Action a) => Tween.Delay(seconds, () => { if (Director.Running && Director.Ctx == this) a(); }, true);

        internal void Update(float dt)
        {
            for (int i = ticks.Count - 1; i >= 0; i--)
            {
                try { ticks[i](dt); }
                catch (Exception ex) { Debug.LogException(ex); ticks.RemoveAt(i); }
            }
        }

        public void Dispose()
        {
            foreach (var (e, h) in handlers) Events.Off(e, h);
            handlers.Clear();
            ticks.Clear();
        }

        public GameObject Get(string id) => Spawned.TryGetValue(id, out var g) ? g : null;
        public GrimeSurface Surface(string id) => Office.Surfaces.TryGetValue(id, out var s) ? s : null;
        public MonitorScreen Monitor(string desk) => Furniture.Monitors.TryGetValue(desk, out var m) ? m : null;
        public Room Room(string id) => Office.Rooms.TryGetValue(id, out var r) ? r : null;
        public void Secret(string id) => Director.FindSecret(id);
        public void Toast(string title, string sub = null, Color? c = null, float hold = 2.6f) => Hud.Instance?.Toast(title, sub, c, hold);
        public void Caption(string text, float hold = 3f) => Hud.Instance?.Caption(text, hold);

        /// <summary>Spawn a story prop at runtime (registered for cleanup).</summary>
        public GameObject Spawn(SpawnDef s) => Director.SpawnOne(s, this);
    }
}
