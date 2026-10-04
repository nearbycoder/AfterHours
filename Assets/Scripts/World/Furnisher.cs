using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// Dresses the office with the furniture-level props every night: desk kits, chairs, trays,
    /// bins, shredders, appliances, plants and the closet. Night scripts then vary what is on, out
    /// of place or dirty.
    /// </summary>
    public class Furnisher
    {
        public readonly Dictionary<string, MonitorScreen> Monitors = new();
        public readonly Dictionary<string, Chair> Chairs = new();
        public readonly Dictionary<string, InboxTray> Trays = new();
        public readonly Dictionary<string, Shredder> Shredders = new();
        public readonly List<Bin> Bins = new();
        public readonly Dictionary<string, GameObject> Named = new();
        public Transform Root;
        OfficeBuilder office;

        static readonly string[] Desks = { "dana", "theo", "priya", "russ", "walt", "marian" };

        public void Build(OfficeBuilder o, Transform parent)
        {
            office = o;
            Root = new GameObject("Furnishings").transform;
            Root.SetParent(parent, false);

            foreach (var who in Desks) DeskKit(who);
            Personal();
            foreach (var kv in o.Anchors)
            {
                var n = kv.Key;
                if (n.StartsWith("BIN_"))
                {
                    var parts = n.Split('_');
                    var b = Bin.Create(kv.Value, parts[1] == "recycle" ? Bin.BinKind.Recycle : Bin.BinKind.Trash, parts[2]);
                    b.transform.SetParent(Root, true);
                    Bins.Add(b);
                }
                else if (n.StartsWith("TRAY_"))
                {
                    var who = n.Substring(5);
                    if (who == "auditor") continue; // appears on night 6
                    AddTray(who, kv.Value);
                }
                else if (n.StartsWith("ANCHOR_chair_conf_") || n.StartsWith("ANCHOR_chair_break_"))
                {
                    var id = n.Substring(13);
                    AddChair(id, n.Contains("break") ? "chair_break" : "chair_office", kv.Value);
                }
                else if (n.StartsWith("ANCHOR_plant_"))
                {
                    var plant = n.Contains("reception") ? "plant_monstera" : n.Contains("conf") || n.Contains("office") ? "plant_snake" : (n.EndsWith("_2") ? "plant_monstera" : "plant_snake");
                    Place(plant, kv.Value, Vector3.zero, 0, n);
                }
            }
            foreach (var g in new[] { "guest_1", "guest_2" })
                if (o.Anchor("ANCHOR_" + g) is { } a) AddChair(g, "chair_guest", a);

            // Shredders
            foreach (var id in new[] { "bullpen", "office" })
            {
                var a = o.Anchor("ANCHOR_shredder_" + id);
                if (a == null) continue;
                var go = Place("shredder", a, Vector3.zero, 0, "shredder_" + id);
                var s = go.AddComponent<Shredder>();
                s.Id = id;
                Shredders[id] = s;
            }

            // Break room
            Place("microwave", o.Anchor("ANCHOR_microwave"), Vector3.zero, 0, "microwave");
            Place("coffee_machine", o.Anchor("ANCHOR_coffee_machine"), Vector3.zero, 0, "coffee_machine");
            var rack = Place("dish_rack", o.Anchor("ANCHOR_dishrack"), Vector3.zero, 0, "dish_rack");
            for (int i = 0; i < 6; i++)
                Resettable.AddSlot("dishrack", rack.transform.TransformPoint(new Vector3(-0.15f + i * 0.06f, 0.012f, 0)), rack.transform.rotation * Quaternion.Euler(0, 0, 90));
            WallClock(new Vector3(3.5f, 2.35f, 8.92f), 180f);
            WallClock(new Vector3(5.6f, 2.45f, 9.08f), 0f);

            // Closet
            var pc = Place("punch_clock", o.Anchor("ANCHOR_punchclock"), Vector3.zero, 0, "punch_clock");
            pc.AddComponent<PunchClock>();
            Place("janitor_cart", o.Anchor("ANCHOR_cart"), Vector3.zero, -90, "cart");
            Place("mop_bucket", o.Anchor("ANCHOR_cart"), new Vector3(0.75f, 0, -0.3f), 20, "mop_bucket");

            // Reception odds and ends
            Place("fire_extinguisher", null, new Vector3(13.4f, 0, 0.35f), 180, "extinguisher");
            Place("desk_phone", o.Anchor("ANCHOR_phone_reception"), Vector3.zero, 0, "phone_reception");
        }

        GameObject Place(string prop, Transform anchor, Vector3 offset, float yaw, string name, bool optional = false)
        {
            if (optional && !PropLibrary.Has(prop)) return null;
            Vector3 pos = anchor ? anchor.TransformPoint(offset) : offset;
            Quaternion rot = (anchor ? anchor.rotation : Quaternion.identity) * Quaternion.Euler(0, yaw, 0);
            var go = PropLibrary.Spawn(prop, pos, rot, Root);
            if (name != null) { go.name = name; Named[name] = go; }
            return go;
        }

        void DeskKit(string who)
        {
            var a = office.Anchor("ANCHOR_desk_" + who);
            if (a == null) return;
            bool exec = who == "marian";
            bool recep = who == "dana";
            // Desk-local: +Z is the sitter's side; the monitor sits toward the back.
            var mon = Place("monitor", a, new Vector3(recep ? -0.4f : exec ? -0.15f : 0f, 0, exec ? -0.15f : recep ? -0.12f : -0.2f), 0, "monitor_" + who);
            var ms = mon.AddComponent<MonitorScreen>();
            ms.Init(who, null, false);
            Monitors[who] = ms;
            Place("keyboard", a, new Vector3(recep ? -0.4f : exec ? -0.15f : 0f, 0, exec ? 0.18f : 0.13f), 0, "keyboard_" + who);
            Place("mouse", a, new Vector3(recep ? -0.12f : exec ? 0.15f : 0.3f, 0, exec ? 0.2f : 0.15f), 0, "mouse_" + who);
            var chairAnchor = office.Anchor("ANCHOR_chair_" + who);
            if (chairAnchor) AddChair(who, exec ? "chair_exec" : "chair_office", chairAnchor);
            if (recep || exec)
            {
                var lampAnchor = office.Anchor(recep ? "LIGHT_lamp_reception_1" : "LIGHT_lamp_office_1");
                if (lampAnchor) Place("desk_lamp", null, new Vector3(lampAnchor.position.x, a.position.y, lampAnchor.position.z), recep ? 200 : -110, "lamp_" + who);
            }
        }

        void Personal()
        {
            var o = office;
            // Dana: phone, pen cup, calendar
            Dress("dana", "pen_cup", new Vector3(0.45f, 0, 0.0f), 0);
            Dress("dana", "calendar_desk", new Vector3(0.75f, 0, -0.1f), -15);
            Dress("dana", "plant_succulent", new Vector3(-0.95f, 0, -0.15f), 0);
            // Theo: succulent, pen cup, binder
            Dress("theo", "plant_succulent", new Vector3(0.6f, 0, -0.25f), 0);
            Dress("theo", "pen_cup", new Vector3(-0.45f, 0, -0.22f), 0);
            Dress("theo", "binder", new Vector3(-0.7f, 0, -0.25f), 90);
            // Priya: energy cans as a little tower, laptop
            Dress("priya", "energy_can", new Vector3(0.62f, 0, -0.25f), 0);
            Dress("priya", "energy_can", new Vector3(0.68f, 0, -0.18f), 40);
            Dress("priya", "laptop", new Vector3(-0.5f, 0, 0.05f), 15);
            // Russ: photo frame, stress-y desk
            Dress("russ", "photo_frame", new Vector3(0.6f, 0, -0.24f), -20);
            Dress("russ", "mug_red", new Vector3(0.4f, 0, 0.0f), 30);
            // Walt's old hot desk: empty-ish
            Dress("walt", "paper_stack", new Vector3(-0.5f, 0, -0.1f), 5);
            // Marian: orchid, plaque, phone (notepad is a story object)
            Dress("marian", "orchid", new Vector3(-0.75f, 0, -0.3f), 0);
            Dress("marian", "award_plaque", new Vector3(0.75f, 0, -0.33f), -10);
            Dress("marian", "desk_phone", new Vector3(0.6f, 0, 0.0f), -15);
            Place("orchid", o.Anchor("ANCHOR_credenza_office"), new Vector3(0.4f, 0, 0), 0, "orchid_credenza");
            Place("award_plaque", o.Anchor("ANCHOR_credenza_office"), new Vector3(-0.3f, 0, 0), 0, null);
            // Bookshelf contents
            var shelf = o.Anchor("ANCHOR_shelf_office");
            if (shelf)
            {
                var rng = new Rng(77);
                for (int s = 0; s < 3; s++)
                for (int i = 0; i < 9; i++)
                {
                    if (rng.Value < 0.25f) continue;
                    float x = -1.5f + i * 0.33f + rng.Range(-0.03f, 0.03f);
                    float y = -0.5f + 0.1f + (s + 1) * (2.1f - 0.12f) / 5 + 0.013f;
                    Place(rng.Value < 0.75f ? "book" : "binder", shelf, new Vector3(x, y, 0.05f), 90 + rng.Range(-4f, 4f), null);
                }
            }
            var low = o.Anchor("ANCHOR_shelf_bullpen");
            if (low)
            {
                Place("binder", low, new Vector3(-0.4f, 0, 0), 90, null);
                Place("binder", low, new Vector3(-0.33f, 0, 0), 90, null);
                Place("paper_stack", low, new Vector3(0.25f, 0, 0), 10, null);
            }
            Place("water_glass", o.Anchor("ANCHOR_credenza_conf"), new Vector3(0.6f, 0, 0), 0, null);
            Place("plant_succulent", o.Anchor("ANCHOR_credenza_conf"), new Vector3(-0.5f, 0, 0), 0, null);
        }

        void Dress(string desk, string prop, Vector3 off, float yaw)
        {
            var a = office.Anchor("ANCHOR_desk_" + desk);
            if (a != null) Place(prop, a, off, yaw, null);
        }

        void AddChair(string id, string prop, Transform anchor)
        {
            var go = Place(prop, anchor, Vector3.zero, 0, "chair_" + id);
            var c = go.AddComponent<Chair>();
            c.Id = id;
            c.HomePos = go.transform.position;
            c.HomeRot = go.transform.rotation;
            c.Tuck(true);
            Chairs[id] = c;
        }

        void AddTray(string who, Transform anchor)
        {
            var go = Place("inbox_tray", anchor, Vector3.zero, 0, "tray_" + who);
            var t = go.AddComponent<InboxTray>();
            t.Person = who;
            Trays[who] = t;
        }

        public void AddAuditorTray()
        {
            if (Trays.ContainsKey("auditor")) return;
            var a = office.Anchor("TRAY_auditor");
            if (a) AddTray("auditor", a);
        }

        void WallClock(Vector3 pos, float yaw)
        {
            var go = PropLibrary.Spawn("wall_clock", pos, Quaternion.Euler(0, yaw, 0), Root);
            var h = PropLibrary.Spawn("clock_hand_h", pos, Quaternion.Euler(0, yaw, 0), go.transform);
            var m = PropLibrary.Spawn("clock_hand_m", pos, Quaternion.Euler(0, yaw, 0), go.transform);
            foreach (var c in h.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            foreach (var c in m.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            go.AddComponent<WallClockHands>().Init(h.transform, m.transform);
        }
    }

    /// <summary>Points the clock hands at the in-game time.</summary>
    public class WallClockHands : MonoBehaviour
    {
        Transform hour, minute;
        public void Init(Transform h, Transform m) { hour = h; minute = m; }
        void Update()
        {
            float mins = NightDirector.Instance != null ? NightDirector.Instance.ClockMinutes : 0f;
            float total = 22 * 60 + mins;
            float hh = (total / 60f) % 12f, mm = total % 60f;
            hour.localRotation = Quaternion.Euler(0, 0, -hh * 30f);
            minute.localRotation = Quaternion.Euler(0, 0, -mm * 6f);
        }
    }
}
