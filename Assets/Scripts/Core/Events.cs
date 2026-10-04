using System;
using System.Collections.Generic;

namespace AfterHours
{
    public enum GameEvent
    {
        LightsChanged,      // room id
        SurfaceCleaned,     // surface id
        TrashBinned,        // item id
        ItemReset,          // item id
        ChairTucked,        // chair id
        MonitorOff,         // monitor id
        EvidenceFound,      // evidence id
        EvidenceFate,       // evidence id
        SecretFound,        // secret id
        DoorOpened,         // door id
        RoomEntered,        // room id
        TaskDone,           // task id
        Discovery,          // big story beat id
        PhraseLearned,      // phrase id
        NoteLeft,           // recipient
    }

    /// <summary>Tiny global event bus. Story, tasks, audio and HUD listen; gameplay raises.</summary>
    public static class Events
    {
        static readonly Dictionary<GameEvent, Action<string>> handlers = new();

        public static void On(GameEvent e, Action<string> h)
        {
            handlers.TryGetValue(e, out var cur);
            handlers[e] = cur + h;
        }

        public static void Off(GameEvent e, Action<string> h)
        {
            if (handlers.TryGetValue(e, out var cur)) handlers[e] = cur - h;
        }

        public static void Raise(GameEvent e, string arg = null)
        {
            if (handlers.TryGetValue(e, out var h) && h != null)
            {
                try { h(arg); }
                catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
            }
        }

        public static void Clear() => handlers.Clear();
    }
}
