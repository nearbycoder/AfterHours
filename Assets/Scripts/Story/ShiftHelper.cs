using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// Keeps a night from stalling on the last can. When the shift has made no progress for a
    /// minute (or the player checks the clipboard with only a few things left), everything still
    /// to do glints with a soft 3D ping. Anything the player lets go of that ends up out of reach
    /// (out of the world, or where no standing or crouching spot can see it) comes back.
    /// </summary>
    public class ShiftHelper : MonoBehaviour
    {
        public static ShiftHelper Instance { get; private set; }

        /// <summary>Seconds of unblocked play without progress before the leftovers glint.</summary>
        public float StuckAfter = 60f;
        public float RepeatEvery = 10f;
        /// <summary>The points that glinted last time (automation checks against these).</summary>
        public readonly List<Vector3> LastGlint = new();
        public int GlintCount { get; private set; }
        public int Recovered { get; private set; }
        /// <summary>Seconds of unblocked play since the last progress.</summary>
        public float Idle => idle;
        /// <summary><see cref="Idle"/> when the last glint fired.</summary>
        public float LastGlintIdle { get; private set; }
        /// <summary>The caption the last glint showed ("[A chime from the bullpen]"), or null if it showed none.</summary>
        public string LastWhere { get; private set; }

        // Said where the chimes come from in this stretch without progress.
        bool saidWhere;

        NightDirector dir;
        float idle, nextGlint, sampleT, lastScore = -1f;
        bool clipboardWasOpen;
        readonly Dictionary<Holdable, float> watched = new();

        public static ShiftHelper Create(NightDirector director)
        {
            var h = director.gameObject.AddComponent<ShiftHelper>();
            h.dir = director;
            Instance = h;
            Holdable.Released += h.Watch;
            director.NightStarted += _ => h.ResetNight();
            return h;
        }

        void OnDestroy() => Holdable.Released -= Watch;

        void ResetNight()
        {
            idle = 0; lastScore = -1f; nextGlint = 0; saidWhere = false;
            LastWhere = null;
            watched.Clear();
            LastGlint.Clear();
        }

        /// <summary>Follow a released item until it settles, then make sure it can be reached.</summary>
        public void Watch(Holdable h)
        {
            if (h != null) watched[h] = 0f;
        }

        void Update()
        {
            if (dir == null || !dir.Running || dir.Paused) return;
            var root = GameRoot.Instance;
            bool blocked = root == null || root.Blocked;
            float dt = Time.deltaTime;

            UpdateWatched(dt);

            // Progress: anything ticked off, any surface a little cleaner, any secret found.
            sampleT -= dt;
            if (sampleT <= 0f)
            {
                sampleT = 0.5f;
                float score = Score();
                if (score > lastScore + 0.001f) { lastScore = score; idle = 0; nextGlint = 0; saidWhere = false; }
            }
            if (!blocked) idle += dt;
            if (!blocked && idle >= StuckAfter && idle >= nextGlint)
            {
                nextGlint = idle + RepeatEvery;
                Glint();
            }

            // Closing the shift sheet near the end of a night points at the last few things.
            bool open = Clipboard.Instance != null && Clipboard.Instance.Open;
            if (clipboardWasOpen && !open && dir.Elapsed > 90f)
            {
                var left = Targets();
                if (left.Count > 0 && left.Count <= 4) Glint();
            }
            clipboardWasOpen = open;
        }

        /// <summary>How many of the nearest glints also chime.</summary>
        public const int Chimes = 3;

        /// <summary>
        /// "[A chime from the bullpen]", "[Chimes from reception and the break room]": the rooms of
        /// the chimes, nearest first, as the shift sheet names them.
        /// </summary>
        public static string WhereCaption(int chimes, IList<string> roomNames)
        {
            var rooms = roomNames.Select(n => string.IsNullOrEmpty(n) ? null : InRoom(n)).Where(n => n != null).Distinct().ToList();
            string where = rooms.Count switch
            {
                0 => "somewhere close",
                1 => "from " + rooms[0],
                _ => "from " + string.Join(", ", rooms.Take(rooms.Count - 1)) + " and " + rooms[^1],
            };
            return chimes == 1 ? $"[A chime {where}]" : $"[Chimes {where}]";
        }

        /// <summary>"Bullpen" → "the bullpen"; names that read as places of their own (Reception, Marian's Office) keep no article.</summary>
        static string InRoom(string displayName)
        {
            string n = displayName.ToLowerInvariant();
            if (n == "reception") return n;
            if (n.StartsWith("marian")) return "Marian's office";
            return "the " + n;
        }

        float Score()
        {
            float s = dir.SecretsFoundCount * 10f;
            foreach (var t in dir.Def.Tasks) s += dir.Progress(t).done * 10f;
            foreach (var g in dir.Office.Surfaces.Values)
                if (g.gameObject.activeSelf) s += Mathf.Floor(g.Completion * 50f);
            return s;
        }

        /// <summary>Everything still to do on the required tasks, or the punch clock when the sheet is done.</summary>
        public List<Vector3> Targets()
        {
            var pts = new List<Vector3>();
            foreach (var t in dir.RequiredRemaining())
                pts.AddRange(dir.Remaining(t).Select(r => r.pos));
            if (pts.Count == 0 && !dir.RequiredRemaining().Any())
            {
                var clock = FindAnyObjectByType<PunchClock>();
                if (clock) pts.Add(clock.transform.position);
            }
            return pts;
        }

        public void Glint()
        {
            var pts = Targets();
            LastGlint.Clear();
            LastGlint.AddRange(pts);
            if (pts.Count == 0) return;
            GlintCount++;
            LastGlintIdle = idle;
            PlaytestLog.Log("stuck_glint", ("points", pts.Count), ("idle", idle), ("open", PlaytestLog.OpenTasks()));
            bool calm = Settings.Current.ReduceFlashing;
            var player = GameRoot.Instance.Player.transform.position;
            var warm = new Color(1f, 0.86f, 0.55f);
            var order = pts.OrderBy(p => (p - player).sqrMagnitude).ToList();
            // Where the chimes come from, once for each stretch without progress: the glints may all
            // be in another room, and a player who can't hear the chime has nothing else to go on.
            LastWhere = null;
            if (!saidWhere)
            {
                saidWhere = true;
                var chimes = order.Take(Chimes).ToList();
                LastWhere = WhereCaption(chimes.Count, chimes.Select(p => Room.At(p + Vector3.up * 0.2f)?.DisplayName).ToList());
                Hud.Instance?.Caption(LastWhere, 4f);
            }
            int i = 0;
            foreach (var p in order)
            {
                var at = p + Vector3.up * 0.08f;
                bool ping = i < Chimes; // the nearest few also make a sound, so off-screen ones can be found by ear
                float delay = Mathf.Min(i * 0.04f, 0.8f);
                for (int pulse = 0; pulse < 3; pulse++)
                {
                    bool first = pulse == 0;
                    Tween.Delay(delay + pulse * 0.45f, () =>
                    {
                        Fx.Burst(FxKind.Beacon, at, Vector3.up, calm ? 1 : 3, warm, 0.12f, 0.8f, 0.04f);
                        if (!calm) Fx.Burst(FxKind.Sparkle, at, Vector3.up, 5, warm, 0.35f, 1f, 0.05f);
                        if (first && ping) Sfx.Play("sparkle", at, 0.28f, 1.15f, 0.06f);
                    });
                }
                i++;
            }
        }

        // ---- lost items ------------------------------------------------------------------------

        void UpdateWatched(float dt)
        {
            if (watched.Count == 0) return;
            foreach (var h in watched.Keys.ToList())
            {
                if (h == null || !h.gameObject.activeInHierarchy || h.Held || h.Locked || (h is TrashItem t && t.Binned))
                {
                    watched.Remove(h);
                    continue;
                }
                var res = h.GetComponent<Resettable>();
                if (res != null && res.AtHome) { watched.Remove(h); continue; }
                var p = h.transform.position;
                if (p.y < -0.6f || p.y > 4f || (Mathf.Abs(p.x) > 60f || Mathf.Abs(p.z) > 60f))
                {
                    watched.Remove(h);
                    Recover(h);
                    continue;
                }
                bool resting = h.Body.isKinematic || h.Body.IsSleeping() || h.Body.linearVelocity.sqrMagnitude < 0.0025f;
                watched[h] = resting ? watched[h] + dt : 0f;
                if (watched[h] < 1.2f) continue;
                watched.Remove(h);
                if (!Reachable(h)) Recover(h);
            }
        }

        /// <summary>
        /// Can the player see the item from somewhere they can stand (or crouch) within arm's
        /// reach? Samples a ring of spots around it, the same way the interaction ray would.
        /// </summary>
        public static bool Reachable(Holdable h)
        {
            var cols = h.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).ToArray();
            if (cols.Length == 0) return true;
            var b = cols[0].bounds;
            foreach (var c in cols) b.Encapsulate(c.bounds);
            var target = b.center;
            if (target.y - b.extents.y > 2.2f) return false;
            if (Room.At(target + Vector3.up * 0.1f) == null && Room.At(target) == null) return false;
            float reach = Interactor.Instance != null ? Interactor.Instance.Reach : 2.3f;
            foreach (float r in new[] { 0.6f, 1.0f, 1.5f })
            for (int a = 0; a < 16; a++)
            {
                float ang = a * 22.5f * Mathf.Deg2Rad;
                var feet = new Vector3(target.x + Mathf.Sin(ang) * r, 0f, target.z + Mathf.Cos(ang) * r);
                if (Room.At(feet + Vector3.up * 0.5f) == null) continue;
                if (!Physics.Raycast(feet + Vector3.up * 0.5f, Vector3.down, 0.7f, Layers.WalkMask, QueryTriggerInteraction.Ignore)) continue;
                foreach (float eyeH in new[] { FirstPersonController.StandEye, FirstPersonController.CrouchEye })
                {
                    float top = eyeH + 0.15f;
                    if (Physics.CheckCapsule(feet + Vector3.up * 0.35f, feet + Vector3.up * Mathf.Max(0.36f, top - 0.3f), 0.28f, Layers.WalkMask, QueryTriggerInteraction.Ignore)) continue;
                    var eye = feet + Vector3.up * eyeH;
                    var d = target - eye;
                    if (d.magnitude > reach) continue;
                    if (Physics.Raycast(eye, d.normalized, out var hit, reach, Layers.SolidMask, QueryTriggerInteraction.Ignore) && hit.collider.transform.IsChildOf(h.transform))
                        return true;
                    if (Physics.SphereCast(eye, 0.07f, d.normalized, out var sh, reach, Layers.SolidMask, QueryTriggerInteraction.Ignore) && sh.collider.transform.IsChildOf(h.transform))
                        return true;
                }
            }
            return false;
        }

        /// <summary>Put an out-of-reach item down on the floor just in front of the player.</summary>
        public void Recover(Holdable h)
        {
            var pl = GameRoot.Instance.Player;
            var fwd = Quaternion.Euler(0, pl.Yaw, 0) * Vector3.forward;
            var spot = pl.transform.position;
            foreach (float d in new[] { 0.9f, 0.6f, 0.3f })
            {
                var c = pl.transform.position + fwd * d;
                if (!Physics.CheckSphere(c + Vector3.up * 0.15f, 0.12f, Layers.SolidMask, QueryTriggerInteraction.Ignore)) { spot = c; break; }
            }
            h.Body.isKinematic = false;
            h.Body.linearVelocity = Vector3.zero;
            h.Body.angularVelocity = Vector3.zero;
            var pos = spot + Vector3.up * 0.15f;
            h.Body.position = pos;
            h.transform.position = pos;
            h.InFlight = false;
            Recovered++;
            PlaytestLog.Log("item_recovered", ("id", h.Id));
            Sfx.Play("drop_soft", pos, 0.5f);
            Fx.Burst(FxKind.Puff, pos, Vector3.up, 5, new Color(0.9f, 0.9f, 0.85f, 0.25f), 0.3f, 1f, 0.05f);
            Hud.Instance?.Caption($"The {h.DisplayName.ToLowerInvariant()} turns up at your feet", 2.5f);
            Debug.Log($"[ShiftHelper] recovered {h.Id} to {pos}");
        }
    }
}
