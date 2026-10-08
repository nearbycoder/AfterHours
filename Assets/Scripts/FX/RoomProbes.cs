using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace AfterHours
{
    /// <summary>
    /// Ultra's reflections: a box-projected reflection probe filling every room, so glass, tile,
    /// the whiteboard and the desks reflect the room around them. Each one is rendered when its
    /// room's lights (panels and lamps) settle after a change, one room at a time, and never with
    /// the UV torch on (its ink would stay in the reflection) or during a storm stutter.
    /// </summary>
    public class RoomProbes : MonoBehaviour
    {
        static RoomProbes instance;
        /// <summary>The probes' resolution (0: Graphics Fidelity has them off).</summary>
        public static int Resolution { get; private set; }
        public static int Count => instance ? instance.entries.Count(e => e.Probe && e.Probe.enabled) : 0;
        /// <summary>Probe renders started this session (automation reads it).</summary>
        public static int Renders { get; private set; }

        class Entry
        {
            public Room Room;
            public ReflectionProbe Probe;
            public readonly List<Light> Lights = new();
            public float Seen = -1f, Rendered = -1f, ChangedAt;
            public int Id = -1;
        }

        readonly List<Entry> entries = new();
        /// <summary>
        /// A room is rendered again once its light has held still for <see cref="Settle"/> seconds
        /// and differs from what was rendered by more than <see cref="Change"/> (a lamp, a monitor
        /// or a panel switching), so a light that only shimmers never keeps a probe busy.
        /// </summary>
        const float Settle = 0.4f, Still = 0.02f, Change = 0.3f;

        public static void Apply(int resolution)
        {
            Resolution = resolution;
            if (resolution <= 0)
            {
                // A render in flight on a disabled probe never finishes; forget it.
                if (instance) foreach (var e in instance.entries) { if (e.Probe) e.Probe.enabled = false; e.Id = -1; e.Rendered = -1f; }
                return;
            }
            if (!instance) instance = new GameObject("RoomProbes").AddComponent<RoomProbes>();
            instance.Build();
        }

        /// <summary>A night has started: tonight's lamps and props, and every room rendered again.</summary>
        public static void Refresh()
        {
            if (instance && Resolution > 0) instance.Build();
        }

        void Build()
        {
            entries.RemoveAll(e => !e.Room);
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None)
                .Where(l => l.type != LightType.Directional && l.GetComponentInParent<FirstPersonController>() == null).ToList();
            int mask = ~((1 << Layers.Player) | (1 << Layers.Viewmodel) | (1 << Layers.Hands) | (1 << Layers.Trigger) | (1 << 5));
            foreach (var room in Room.All.Values)
            {
                var e = entries.FirstOrDefault(x => x.Room == room);
                if (e == null)
                {
                    var go = new GameObject("Probe_" + room.Id);
                    go.transform.SetParent(transform, false);
                    go.transform.position = room.Bounds.center;
                    var p = go.AddComponent<ReflectionProbe>();
                    p.mode = ReflectionProbeMode.Realtime;
                    p.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                    // All six faces in one frame (one room a frame): time-sliced renders didn't always report finishing.
                    p.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                    p.boxProjection = true;
                    p.size = room.Bounds.size;
                    p.center = Vector3.zero;
                    p.blendDistance = 0.5f;
                    p.hdr = true;
                    p.nearClipPlane = 0.05f;
                    p.farClipPlane = 40f;
                    p.shadowDistance = 12f;
                    p.clearFlags = ReflectionProbeClearFlags.SolidColor;
                    p.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
                    p.cullingMask = mask;
                    p.importance = 1;
                    e = new Entry { Room = room, Probe = p };
                    entries.Add(e);
                }
                e.Probe.resolution = Resolution;
                e.Probe.enabled = true;
                var b = room.Bounds;
                b.Expand(0.6f);
                e.Lights.Clear();
                e.Lights.AddRange(lights.Where(l => b.Contains(l.transform.position)));
                e.Rendered = -1f; // render again, from the start
                e.Id = -1;
                e.ChangedAt = -Settle;
            }
        }

        /// <summary>The room's light: what's switched on and how bright.</summary>
        static float Signature(Entry e)
        {
            float sum = 0f;
            foreach (var l in e.Lights)
                if (l && l.enabled && l.gameObject.activeInHierarchy) sum += l.intensity * l.color.maxColorComponent;
            return sum;
        }

        static bool Stale(Entry e) => e.Rendered < 0f || Mathf.Abs(e.Seen - e.Rendered) > Change;

        void Update()
        {
            if (Resolution <= 0) return;
            float now = Time.unscaledTime;
            bool hold = UvTorch.Instance && UvTorch.Instance.On;
            bool started = false;
            foreach (var e in entries)
            {
                if (!e.Room || !e.Probe) continue;
                float sig = Signature(e);
                if (Mathf.Abs(sig - e.Seen) > Still) e.ChangedAt = now;
                e.Seen = sig;
                if (started || hold || e.Room.Stuttering || !Stale(e)) continue;
                if (now - e.ChangedAt < Settle) continue;
                if (e.Id >= 0 && !e.Probe.IsFinishedRendering(e.Id)) continue;
                e.Id = e.Probe.RenderProbe();
                e.Rendered = e.Seen;
                Renders++;
                started = true; // one room starts per frame
            }
        }

        /// <summary>Every probe is rendered for the lights as they are now (automation waits on it).</summary>
        public static bool Settled => !instance || Resolution <= 0 || instance.entries.All(e =>
            !e.Room || !e.Probe || (!Stale(e) && (e.Id < 0 || e.Probe.IsFinishedRendering(e.Id))));

        /// <summary>Each room's probe: its light now, as rendered, and whether its last render finished (for the logs).</summary>
        public static string Status() => !instance ? "none" : string.Join(", ", instance.entries.Where(e => e.Room && e.Probe).Select(e =>
            $"{e.Room.Id} {e.Seen:F2}/{e.Rendered:F2}{(e.Id >= 0 && !e.Probe.IsFinishedRendering(e.Id) ? " rendering" : "")}"));
    }
}
