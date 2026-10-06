using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// Runs a night: rebuilds the office state from the night's data and the story so far, tracks
    /// tasks and secrets, runs the clock, and handles clocking out into the shift report.
    /// </summary>
    public class NightDirector : MonoBehaviour
    {
        public static NightDirector Instance { get; private set; }
        public OfficeBuilder Office;
        public Furnisher Furniture;
        public NightDef Def { get; private set; }
        public NightContext Ctx { get; private set; }
        public bool Running { get; private set; }
        public bool Paused { get; private set; }

        /// <summary>Automation: jump the shift clock (minutes after 10 PM).</summary>
        public void SetClock(float minutes) => ClockMinutes = minutes;

        /// <summary>Hold the clock and scripts (title backdrop, title card).</summary>
        public void Pause(bool p) => Paused = p;
        public float ClockMinutes { get; private set; }
        public float Elapsed { get; private set; }

        Transform nightRoot;
        /// <summary>Parent for anything a night creates; destroyed when the night ends.</summary>
        public Transform NightRoot => nightRoot;
        readonly List<(TrashItem item, string room)> trash = new();
        readonly HashSet<string> secrets = new();
        readonly HashSet<string> doneTasks = new();
        bool announcedComplete;
        float taskTimer;

        public static readonly string[] AllRooms = { "reception", "bullpen", "breakroom", "conference", "office", "closet" };

        public event Action<int> NightStarted;

        public static NightDirector Create(OfficeBuilder office)
        {
            var go = new GameObject("NightDirector");
            var d = go.AddComponent<NightDirector>();
            d.Office = office;
            Instance = d;
            return d;
        }

        // =========================================================================================
        // Setup
        // =========================================================================================

        public void Begin(int night)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            Def = NightDefs.Get(night);
            if (Def == null) { Debug.LogError("[Night] no night " + night); return; }
            Cleanup();
            Story.State.Night = night;
            Story.State.SaveSnapshot();
            Story.State.Save();

            nightRoot = new GameObject("Night" + night).transform;
            nightRoot.SetParent(transform, false);
            Resettable.ClearSlots();
            Furniture = new Furnisher();
            Furniture.Build(Office, nightRoot);

            // Rooms: lights off (closet on), doors closed, locks for rooms not on tonight's sheet.
            foreach (var r in Office.Rooms.Values) r.SetLights(false, true);
            if (Office.Rooms.TryGetValue("closet", out var closet)) closet.SetLights(true, true);
            foreach (var sw in Office.Switches.Values) sw.Sync();
            foreach (var kv in Office.Doors)
            {
                var d = kv.Value;
                d.SetOpen(false, null, true);
                string room = kv.Key;
                d.Locked = kv.Key.StartsWith("entry") || (room != "closet" && !Def.Rooms.Contains(room));
                d.LockedMessage = kv.Key.StartsWith("entry") ? "Locked for the night" : room == "office" ? "Locked · Marian's office" : "Locked · not on tonight's sheet";
            }

            // Dirt
            foreach (var s in Office.Surfaces.Values) s.gameObject.SetActive(false);
            foreach (var (id, spec, req) in Def.Dirt)
            {
                if (!Office.Surfaces.TryGetValue(id, out var g)) { Debug.LogWarning("[Night] no surface " + id); continue; }
                g.Spec = spec;
                g.Required = req;
                g.gameObject.SetActive(true);
                g.Build();
                g.SetShimmer(night == 1 ? 0.6f : 0f);
            }
            GameRoot.Instance.Cleaning.HookAll();

            // Monitors and chairs
            foreach (var kv in Furniture.Monitors)
            {
                Def.Monitors.TryGetValue(kv.Key, out var screen);
                kv.Value.SetScreen(screen);
                kv.Value.SetOn(screen != null, true);
                kv.Value.CountsForTask = screen != null;
                kv.Value.StoryOwned = false;
                kv.Value.ScreenDoc = Def.MonitorDocs.TryGetValue(kv.Key, out var doc) ? doc : null;
            }
            var rng = new Rng(night * 31 + 7);
            foreach (var kv in Furniture.Chairs)
            {
                if (!Def.ChairsOut.Contains(kv.Key)) continue;
                var c = kv.Value;
                var back = -(c.HomeRot * Vector3.forward);   // chairs face their desk; pull out away from it
                var pos = c.HomePos + back * rng.Range(0.5f, 0.9f) + c.HomeRot * Vector3.right * rng.Range(-0.35f, 0.35f);
                c.Untuck(pos, c.HomeRot * Quaternion.Euler(0, rng.Range(-60f, 60f), 0));
            }

            // Spawns
            trash.Clear();
            Ctx = new NightContext { Director = this };
            foreach (var s in Def.Spawns)
            {
                if (s.When != null && !s.When(Story.State)) continue;
                SpawnOne(s, Ctx);
            }

            secrets.Clear();
            foreach (var id in Story.State.SecretsFound)
                if (id.StartsWith(night + ":")) secrets.Add(id.Substring(id.IndexOf(':') + 1));
            secrets.Clear(); // replays start fresh
            doneTasks.Clear();
            announcedComplete = false;
            Def.Script?.Invoke(Ctx);

            // Player
            var spawn = Office.Anchor("ANCHOR_spawn");
            GameRoot.Instance.Player.Teleport(spawn.position, spawn.eulerAngles.y, 4f);
            ClockMinutes = 0;
            Elapsed = 0;
            Running = true;
            AudioDirector.Instance?.SetNight(Def);
            if (RoomPhotos.Instance)
            {
                RoomPhotos.Instance.Clear();
                RoomPhotos.Instance.Snap(Def.Rooms, true);
            }
            GraphicsQuality.ApplyLights(); // tonight's lamps follow the graphics preset
            NightStarted?.Invoke(night);
            Debug.Log($"[Night] began night {night}: {Def.Dirt.Count} surfaces, {Def.Spawns.Count} spawns, {Def.Tasks.Count} tasks ({timer.ElapsedMilliseconds} ms)");
        }

        void Cleanup()
        {
            Ctx?.Dispose();
            Ctx = null;
            Running = false;
            // Night scripts hang story behaviour on permanent office furniture (Walt's locker, the
            // FC-2 cabinet); strip it so the next night starts from a clean office. Immediate,
            // because the next night's script runs in this same frame.
            foreach (var c in FindObjectsByType<Readable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!nightRoot || !c.transform.IsChildOf(nightRoot)) DestroyImmediate(c);
            foreach (var c in FindObjectsByType<ScriptedUse>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!nightRoot || !c.transform.IsChildOf(nightRoot)) DestroyImmediate(c);
            if (nightRoot) Destroy(nightRoot.gameObject);
            trash.Clear();
            if (Hands.Instance && Hands.Instance.Holding) Destroy(Hands.Instance.Holding.gameObject);
        }

        public GameObject SpawnOne(SpawnDef s, NightContext ctx)
        {
            var anchor = s.Anchor != null ? Office.Anchor(s.Anchor) : null;
            Vector3 pos = anchor ? anchor.TransformPoint(s.Pos) : s.Pos;
            Quaternion rot = (anchor ? anchor.rotation : Quaternion.identity) * Quaternion.Euler(s.Pitch, s.Yaw, s.Roll);
            bool physics = s.Kind is SpawnKind.Trash or SpawnKind.Loose or SpawnKind.Reset or SpawnKind.Evidence;
            var go = PropLibrary.Spawn(s.Prop, pos, rot, nightRoot, physics);
            if (s.Id != null) { go.name = s.Id; ctx.Spawned[s.Id] = go; }
            switch (s.Kind)
            {
                case SpawnKind.Trash:
                {
                    var t = go.AddComponent<TrashItem>();
                    t.Kind = s.Trash;
                    t.DisplayName = s.Name ?? s.Prop;
                    t.Id = s.Id ?? s.Prop;
                    var room = Room.At(pos + Vector3.up * 0.2f);
                    trash.Add((t, room != null ? room.Id : "?"));
                    break;
                }
                case SpawnKind.Evidence:
                {
                    var e = go.AddComponent<EvidenceItem>();
                    e.Doc = s.Doc;
                    e.Id = s.Doc;
                    e.DisplayName = Docs.Get(s.Doc)?.Title;
                    e.Body.isKinematic = true;
                    break;
                }
                case SpawnKind.Readable:
                {
                    var r = go.AddComponent<Readable>();
                    r.Doc = s.Doc;
                    r.Verb = s.Verb ?? "Read";
                    r.Label = s.Label;
                    break;
                }
                case SpawnKind.Reset:
                {
                    var h = go.AddComponent<Holdable>();
                    h.Id = s.Id;
                    h.DisplayName = s.Name ?? s.Prop;
                    var r = go.AddComponent<Resettable>();
                    r.Id = s.Id;
                    r.Group = s.Group;
                    var hp = anchor ? anchor.TransformPoint(s.Home) : s.Home;
                    r.SetHome(hp, (anchor ? anchor.rotation : Quaternion.identity) * Quaternion.Euler(0, s.HomeYaw, 0));
                    r.Displace(pos, rot, true);
                    break;
                }
                case SpawnKind.Loose:
                {
                    var h = go.AddComponent<Holdable>();
                    h.Id = s.Id ?? s.Prop;
                    h.DisplayName = s.Name ?? s.Prop;
                    break;
                }
            }
            return go;
        }

        // =========================================================================================
        // Running
        // =========================================================================================

        void Update()
        {
            if (!Running || Paused) return;
            float dt = Time.deltaTime;
            Elapsed += dt;
            ClockMinutes = Mathf.Min(465f, ClockMinutes + dt * (480f / 720f));
            Shader.SetGlobalFloat("_AH_Dawn", Mathf.Clamp01((ClockMinutes - 380f) / 85f) * 0.35f);
            if (Hud.Instance) Hud.Instance.SetClock(ClockText(), ClockSub());
            Ctx?.Update(dt);
            taskTimer -= dt;
            if (taskTimer <= 0f)
            {
                taskTimer = 0.25f;
                CheckTasks();
            }
        }

        public string ClockText()
        {
            int total = 22 * 60 + Mathf.FloorToInt(ClockMinutes);
            int h = (total / 60) % 12;
            if (h == 0) h = 12;
            return $"{h}:{total % 60:00}";
        }

        public string ClockSub()
        {
            int total = 22 * 60 + Mathf.FloorToInt(ClockMinutes);
            string ampm = (total / 60) % 24 >= 12 ? "PM" : "AM";
            return $"{ampm}   {Def.Day.Substring(0, 3).ToUpperInvariant()}";
        }

        public (int done, int total) Progress(TaskDef t)
        {
            switch (t.Kind)
            {
                case TaskKind.Clean:
                {
                    int done = 0, total = 0;
                    foreach (var id in t.Targets)
                    {
                        if (!Office.Surfaces.TryGetValue(id, out var g) || !g.gameObject.activeSelf) continue;
                        total++;
                        if (g.Done) done++;
                    }
                    return (done, total);
                }
                case TaskKind.Trash:
                {
                    var items = trash.Where(x => x.item != null && t.Targets.Contains(x.room)).ToList();
                    return (items.Count(x => x.item.Binned), items.Count);
                }
                case TaskKind.Reset:
                {
                    var items = Resettable.All.Where(r => r.Required && (t.Targets.Length == 0 || t.Targets.Contains(RoomOf(r.HomeAnchor)) || t.Targets.Contains(RoomOf(r.transform.position)) || t.Targets.Contains(r.Id))).ToList();
                    return (items.Count(r => r.AtHome), items.Count);
                }
                case TaskKind.Chairs:
                {
                    var items = Furniture.Chairs.Values.Where(c => t.Targets.Contains(RoomOf(c.HomePos))).ToList();
                    return (items.Count(c => c.Tucked), items.Count);
                }
                case TaskKind.Monitors:
                {
                    var items = Furniture.Monitors.Values.Where(m => t.Targets.Contains(RoomOf(m.transform.position)) && m.CountsForTask).ToList();
                    return (items.Count(m => !m.On), items.Count);
                }
                case TaskKind.Lights:
                {
                    // Locking up only counts once everything else on the sheet is done.
                    if (Def.Tasks.Any(o => !o.Optional && o.Kind != TaskKind.Lights && !IsDone(o))) return (0, t.Targets.Length);
                    int off = t.Targets.Count(r => Office.Rooms.TryGetValue(r, out var room) && !room.LightsOn);
                    return (off, t.Targets.Length);
                }
                case TaskKind.Flag:
                    return (t.Targets.All(f => Story.State.Has(f)) ? 1 : 0, 1);
            }
            return (0, 0);
        }

        public bool IsDone(TaskDef t)
        {
            var (d, n) = Progress(t);
            return n == 0 || d >= n;
        }

        /// <summary>
        /// Where a task's unfinished work is: one point per thing still to do (a few per dirty
        /// surface, at its dirtiest spots), with the room it's in. Empty when the task is done or,
        /// for lights, while anything else on the sheet is still open.
        /// </summary>
        public List<(Vector3 pos, string room)> Remaining(TaskDef t)
        {
            var list = new List<(Vector3, string)>();
            if (IsDone(t)) return list;
            void Add(Vector3 p) => list.Add((p, RoomOf(p)));
            switch (t.Kind)
            {
                case TaskKind.Clean:
                    foreach (var id in t.Targets)
                        if (Office.Surfaces.TryGetValue(id, out var g) && g.gameObject.activeSelf && !g.Done)
                            foreach (var p in g.DirtiestSpots(3)) Add(p);
                    break;
                case TaskKind.Trash:
                    foreach (var (item, room) in trash)
                        if (item != null && !item.Binned && item.gameObject.activeInHierarchy && t.Targets.Contains(room)) Add(item.transform.position);
                    break;
                case TaskKind.Reset:
                    foreach (var r in Resettable.All)
                        if (r.Required && !r.AtHome && (t.Targets.Length == 0 || t.Targets.Contains(RoomOf(r.HomeAnchor)) || t.Targets.Contains(RoomOf(r.transform.position)) || t.Targets.Contains(r.Id)))
                            Add(r.transform.position);
                    break;
                case TaskKind.Chairs:
                    foreach (var c in Furniture.Chairs.Values)
                        if (!c.Tucked && t.Targets.Contains(RoomOf(c.HomePos))) Add(c.transform.position + Vector3.up * 0.55f);
                    break;
                case TaskKind.Monitors:
                    foreach (var m in Furniture.Monitors.Values)
                        if (m.On && m.CountsForTask && t.Targets.Contains(RoomOf(m.transform.position))) Add(m.transform.position);
                    break;
                case TaskKind.Lights:
                    if (Def.Tasks.Any(o => !o.Optional && o.Kind != TaskKind.Lights && !IsDone(o))) break;
                    foreach (var r in t.Targets)
                        if (Office.Rooms.TryGetValue(r, out var room) && room.LightsOn && Office.Switches.TryGetValue(r, out var sw))
                            list.Add((sw.transform.position, r));
                    break;
            }
            return list;
        }

        static string RoomOf(Vector3 p) => Room.At(p + Vector3.up * 0.2f)?.Id ?? "?";

        void CheckTasks()
        {
            foreach (var t in Def.Tasks)
            {
                if (t.Kind == TaskKind.Lights) continue; // judged at clock-out
                bool done = IsDone(t);
                if (done && doneTasks.Add(t.Id))
                {
                    Hud.Instance?.Toast("✓ " + t.Label, t.Optional ? "Bonus" : null, Ui.Good, 2.4f);
                    Sfx.Play("room_complete", null, 0.45f);
                    Events.Raise(GameEvent.TaskDone, t.Id);
                }
                else if (!done) doneTasks.Remove(t.Id);
            }
            if (!announcedComplete && RequiredRemaining().All(t => t.Kind == TaskKind.Lights))
            {
                announcedComplete = true;
                Hud.Instance?.Toast("Shift sheet done", "Lights off, then clock out in the closet", Ui.Accent, 4f);
                Sfx.Play("discover", null, 0.35f);
            }
        }

        public IEnumerable<TaskDef> RequiredRemaining() => Def.Tasks.Where(t => !t.Optional && !IsDone(t));

        public void FindSecret(string id)
        {
            if (Def == null || !Running) return;
            var s = Def.Secrets.FirstOrDefault(x => x.Id == id);
            if (s == null || !secrets.Add(id)) return;
            string key = Def.Number + ":" + id;
            if (!Story.State.SecretsFound.Contains(key)) Story.State.SecretsFound.Add(key);
            Hud.Instance?.Toast($"Secret {secrets.Count}/{Def.Secrets.Count}", s.Label, Palette.Hex("C58CFF"), 3f);
            Sfx.Play("discover", null, 0.6f);
            PostFx.Instance?.Pulse(0.8f);
            Events.Raise(GameEvent.SecretFound, id);
        }

        public int SecretsFoundCount => secrets.Count;
        public bool HasSecret(string id) => secrets.Contains(id);

        // =========================================================================================
        // Clock out
        // =========================================================================================

        public void RequestClockOut()
        {
            if (!Running) return;
            var remaining = RequiredRemaining().ToList();
            if (remaining.Count == 0) { EndNight(); return; }
            if (Def.Number == 1)
            {
                Hud.Instance?.Toast("Not yet", "Finish your shift sheet first: " + remaining[0].Label, Palette.EvidenceRed, 3f);
                Sfx.Play("wrong_bin", null, 0.4f);
                return;
            }
            ChoiceMenu.Show("Clock out early?", $"{remaining.Count} task{(remaining.Count == 1 ? "" : "s")} unfinished. The agency will notice.", new List<ChoiceMenu.Option>
            {
                new("Clock out anyway", remaining[0].Label + (remaining.Count > 1 ? $" (+{remaining.Count - 1} more)" : ""), EndNight),
            });
        }

        public void EndNight()
        {
            if (!Running) return;
            RoomPhotos.Instance?.Snap(Def.Rooms, false);
            Running = false;
            Sfx.Play("punch_clock", null, 0.8f);
            int reqTotal = Def.Tasks.Count(t => !t.Optional), reqDone = Def.Tasks.Count(t => !t.Optional && IsDone(t));
            int optTotal = Def.Tasks.Count(t => t.Optional), optDone = Def.Tasks.Count(t => t.Optional && IsDone(t));
            var active = Office.Surfaces.Values.Where(s => s.gameObject.activeSelf).ToList();
            float clean = active.Count > 0 ? active.Average(s => s.Completion) : 1f;
            float score = (reqTotal > 0 ? reqDone / (float)reqTotal : 1f) * 0.6f + (optTotal > 0 ? optDone / (float)optTotal : 1f) * 0.15f + clean * 0.25f;
            string grade = reqDone == reqTotal && score >= 0.97f ? "S" : score >= 0.9f ? "A" : score >= 0.75f ? "B" : "C";
            var result = new NightResult
            {
                Night = Def.Number, Grade = grade, Secrets = secrets.Count, SecretsTotal = Def.Secrets.Count,
                TasksDone = reqDone + optDone, TasksTotal = reqTotal + optTotal, Seconds = Elapsed, Completed = true,
            };
            var prev = Story.State.ResultFor(Def.Number);
            if (prev != null) Story.State.Results.Remove(prev);
            Story.State.Results.Add(result);
            Records.Note(result);

            // Night-specific consequences (suspicion etc.)
            Def.End?.Invoke(Ctx);
            Ctx?.Dispose();

            int finished = Def.Number;
            Story.State.Night = finished + 1;
            Story.State.Save();
            Debug.Log($"[Night] ended night {finished}: grade {grade}, secrets {result.Secrets}/{result.SecretsTotal}, tasks {result.TasksDone}/{result.TasksTotal}, {Elapsed:F0}s");
            GameRoot.Instance.OnNightEnded(Def, result);
        }
    }
}
