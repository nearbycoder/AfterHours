using System;
using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    public enum SpawnKind { Decor, Loose, Trash, Reset, Evidence, Readable }

    /// <summary>One prop placed for a night. Positions are world space unless Anchor is set.</summary>
    public class SpawnDef
    {
        public string Prop, Anchor, Id, Doc, Name, Group, Verb;
        public Vector3 Pos;
        public float Yaw, Pitch, Roll;
        public string Label;            // custom prompt for readables
        public SpawnKind Kind;
        public TrashKind Trash;
        public Vector3 Home;
        public float HomeYaw;
        public bool HasHome;
        public Func<StoryState, bool> When;
    }

    public enum TaskKind { Clean, Trash, Reset, Chairs, Monitors, Lights, Flag }

    public class TaskDef
    {
        public string Id, Label;
        public TaskKind Kind;
        public string[] Targets;
        public bool Optional;
        public string Room;      // for grouping on the clipboard
    }

    public class SecretDef
    {
        public string Id, Label;
    }

    public class ChatLine
    {
        public string Time, Who, Text;
        public Func<StoryState, bool> When;
        public string React;    // optional emoji reaction
    }

    public class NightDef
    {
        public int Number;
        public string Day, Title, Tagline;
        public string[] Rooms;                     // unlocked rooms
        public readonly List<(string surface, GrimeSpec spec, bool required)> Dirt = new();
        public readonly List<SpawnDef> Spawns = new();
        public readonly List<TaskDef> Tasks = new();
        public readonly List<SecretDef> Secrets = new();
        public readonly List<ChatLine> Chat = new();
        public Action<NightContext> Script;
        public Action<NightContext> End;
        public Dictionary<string, string> Monitors = new();     // desk -> screen texture (null = off)
        public Dictionary<string, string> MonitorDocs = new();  // desk -> readable doc
        public HashSet<string> ChairsOut = new();               // chair ids that start untucked
        public bool Storm;
        public int MusicIntensity;                              // 0 calm, 1 tense

        // ---- authoring helpers -----------------------------------------------------------------
        public NightDef Grime(string surface, GrimeSpec spec, bool required = true) { Dirt.Add((surface, spec, required)); return this; }

        public SpawnDef Trash(string prop, float x, float y, float z, TrashKind kind, string name, float yaw = 0, Func<StoryState, bool> when = null)
        {
            var s = new SpawnDef { Prop = prop, Pos = new Vector3(x, y, z), Yaw = yaw, Kind = SpawnKind.Trash, Trash = kind, Name = name, When = when };
            Spawns.Add(s);
            return s;
        }

        public SpawnDef Evidence(string doc, float x, float y, float z, float yaw = 0, Func<StoryState, bool> when = null)
        {
            var d = Docs.Get(doc);
            var s = new SpawnDef { Prop = d?.Prop ?? "paper_sheet", Pos = new Vector3(x, y, z), Yaw = yaw, Kind = SpawnKind.Evidence, Doc = doc, Id = doc, When = when };
            Spawns.Add(s);
            return s;
        }

        public SpawnDef Readable(string doc, string prop, float x, float y, float z, float yaw = 0, string verb = "Read", Func<StoryState, bool> when = null)
        {
            var s = new SpawnDef { Prop = prop, Pos = new Vector3(x, y, z), Yaw = yaw, Kind = SpawnKind.Readable, Doc = doc, Id = doc, Verb = verb, When = when };
            Spawns.Add(s);
            return s;
        }

        public SpawnDef Decor(string prop, float x, float y, float z, float yaw = 0, Func<StoryState, bool> when = null, string id = null)
        {
            var s = new SpawnDef { Prop = prop, Pos = new Vector3(x, y, z), Yaw = yaw, Kind = SpawnKind.Decor, When = when, Id = id };
            Spawns.Add(s);
            return s;
        }

        /// <summary>An item displaced from its home: put it back for credit.</summary>
        public SpawnDef Reset(string id, string prop, string name, Vector3 home, float homeYaw, Vector3 displaced, float displacedYaw, string group = null, string anchor = null)
        {
            var s = new SpawnDef { Id = id, Prop = prop, Name = name, Home = home, HomeYaw = homeYaw, HasHome = true, Pos = displaced, Yaw = displacedYaw, Kind = SpawnKind.Reset, Group = group, Anchor = anchor };
            Spawns.Add(s);
            return s;
        }

        public NightDef Task(string id, string label, TaskKind kind, string room, params string[] targets)
        {
            Tasks.Add(new TaskDef { Id = id, Label = label, Kind = kind, Room = room, Targets = targets });
            return this;
        }

        public NightDef Optional(string id, string label, TaskKind kind, string room, params string[] targets)
        {
            Tasks.Add(new TaskDef { Id = id, Label = label, Kind = kind, Room = room, Targets = targets, Optional = true });
            return this;
        }

        public NightDef Secret(string id, string label) { Secrets.Add(new SecretDef { Id = id, Label = label }); return this; }

        public NightDef Say(string time, string who, string text, Func<StoryState, bool> when = null, string react = null)
        {
            Chat.Add(new ChatLine { Time = time, Who = who, Text = text, When = when, React = react });
            return this;
        }
    }
}
