using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// Instantiates the Blender office and turns its naming conventions into behaviour: colliders,
    /// rooms, lights, doors, switches, glass, grime surfaces and named anchors.
    /// </summary>
    public class OfficeBuilder : MonoBehaviour
    {
        public static OfficeBuilder Instance { get; private set; }

        public readonly Dictionary<string, Transform> Anchors = new();
        public readonly Dictionary<string, GrimeSurface> Surfaces = new();
        public readonly Dictionary<string, Door> Doors = new();
        public readonly Dictionary<string, Room> Rooms = new();
        public readonly Dictionary<string, LightSwitch> Switches = new();
        public readonly List<Transform> Bins = new();
        public readonly List<Transform> Trays = new();
        public Transform Root { get; private set; }
        public Light Moon { get; private set; }

        static readonly Dictionary<string, string> RoomNames = new()
        {
            { "reception", "Reception" }, { "closet", "Janitor's Closet" }, { "bullpen", "Bullpen" },
            { "conference", "Conference Room" }, { "breakroom", "Break Room" }, { "office", "Marian's Office" },
            { "lobby", "Lobby" },
        };

        public static OfficeBuilder Build()
        {
            var go = new GameObject("Office");
            var ob = go.AddComponent<OfficeBuilder>();
            Instance = ob;
            ob.Make();
            return ob;
        }

        public Transform Anchor(string id) => Anchors.TryGetValue(id, out var t) ? t : null;

        void Make()
        {
            var prefab = Resources.Load<GameObject>("Models/Office");
            if (prefab == null) { Debug.LogError("[Office] Models/Office missing"); return; }
            var inst = Instantiate(prefab, transform);
            inst.name = "OfficeModel";
            Root = inst.transform;
            Materials.Apply(inst);

            var panelsByRoom = new Dictionary<string, List<Renderer>>();
            var lightAnchors = new List<Transform>();
            var grimeMarkers = new List<Transform>();
            var all = inst.GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                if (t == Root) continue;
                string n = t.name;
                var mf = t.GetComponent<MeshFilter>();
                var mr = t.GetComponent<MeshRenderer>();

                if (n.StartsWith("ROOM_"))
                {
                    MakeRoom(t, n.Substring(5), mr);
                    continue;
                }
                if (n.StartsWith("GRIME_")) { grimeMarkers.Add(t); continue; }
                if (n.StartsWith("ANCHOR_") || n.StartsWith("TRAY_") || n.StartsWith("BIN_") || n.StartsWith("SWITCH_") || n.StartsWith("LIGHT_"))
                {
                    Anchors[n] = t;
                    if (n.StartsWith("BIN_")) Bins.Add(t);
                    if (n.StartsWith("TRAY_")) Trays.Add(t);
                    if (n.StartsWith("LIGHT_")) lightAnchors.Add(t);
                    continue;
                }
                if (mr == null || mf == null) continue;

                if (n.StartsWith("PANEL_"))
                {
                    var room = n.Substring(6, n.LastIndexOf('_') - 6);
                    if (!panelsByRoom.TryGetValue(room, out var list)) panelsByRoom[room] = list = new List<Renderer>();
                    list.Add(mr);
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    continue;
                }
                if (n.StartsWith("GLASS_"))
                {
                    t.gameObject.layer = Layers.Glass;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var bc = t.gameObject.AddComponent<BoxCollider>();
                    FitBox(bc, mf.sharedMesh);
                    continue;
                }
                if (n.StartsWith("DOOR_"))
                {
                    var bc = t.gameObject.AddComponent<BoxCollider>();
                    FitBox(bc, mf.sharedMesh);
                    var d = t.gameObject.AddComponent<Door>();
                    d.Id = n.Substring(5);
                    Doors[d.Id] = d;
                    continue;
                }
                if (n.StartsWith("FLOOR_"))
                {
                    var bc = t.gameObject.AddComponent<BoxCollider>();
                    FitBox(bc, mf.sharedMesh);
                    var kind = n.Split('_')[1];
                    t.gameObject.AddComponent<FloorSurface>().Kind = kind is "carpet" or "rug" ? FloorKind.Carpet : kind == "wood" ? FloorKind.Vinyl : FloorKind.Tile;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    continue;
                }
                if (n.StartsWith("NC_") || n.StartsWith("WALL_ceiling"))
                {
                    if (n.StartsWith("WALL_ceiling")) t.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                    continue;
                }
                // Static architecture and furniture: exact mesh colliders (rays pass under desks).
                var mc = t.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                t.gameObject.isStatic = true;
            }

            // Rooms get their panels and lights.
            foreach (var la in lightAnchors) MakeLight(la);
            foreach (var kv in panelsByRoom)
                if (Rooms.TryGetValue(kv.Key, out var room)) room.Panels.AddRange(kv.Value);
            foreach (var r in Rooms.Values) r.Register();

            // Grime surfaces (dormant until a night assigns dirt).
            foreach (var g in grimeMarkers) MakeGrime(g);

            // Switches
            foreach (var kv in Anchors)
            {
                if (!kv.Key.StartsWith("SWITCH_")) continue;
                var roomId = kv.Key.Substring(7);
                if (!Rooms.TryGetValue(roomId, out var room)) continue;
                Switches[roomId] = LightSwitch.Create(kv.Value, room);
            }

            BuildOutdoors();
        }

        static void FitBox(BoxCollider bc, Mesh mesh)
        {
            var b = mesh.bounds;
            bc.center = b.center;
            bc.size = new Vector3(Mathf.Max(b.size.x, 0.02f), Mathf.Max(b.size.y, 0.02f), Mathf.Max(b.size.z, 0.02f));
        }

        void MakeRoom(Transform t, string id, MeshRenderer mr)
        {
            var b = mr != null ? mr.bounds : new Bounds(t.position, Vector3.one);
            if (mr) Destroy(mr);
            var mf = t.GetComponent<MeshFilter>();
            if (mf) Destroy(mf);
            var room = t.gameObject.AddComponent<Room>();
            room.Id = id;
            room.DisplayName = RoomNames.TryGetValue(id, out var dn) ? dn : id;
            room.Bounds = b;
            Rooms[id] = room;
        }

        void MakeLight(Transform a)
        {
            // LIGHT_<kind>_<room>_<n>
            var parts = a.name.Split('_');
            if (parts.Length < 4) return;
            string kind = parts[1], roomId = parts[2];
            Rooms.TryGetValue(roomId, out var room);
            var go = new GameObject("Light_" + a.name.Substring(6));
            go.transform.SetParent(a, false);
            var l = go.AddComponent<Light>();
            if (kind == "panel")
            {
                if (roomId == "closet")
                {
                    l.type = LightType.Point;
                    l.color = new Color(1f, 0.82f, 0.6f);
                    l.intensity = 5f;
                    l.range = 5.5f;
                    l.shadows = LightShadows.Soft;
                }
                else
                {
                    go.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                    l.type = LightType.Spot;
                    l.color = new Color(0.93f, 0.96f, 1f);
                    l.intensity = roomId == "lobby" ? 5f : 16f;
                    l.range = 8.5f;
                    l.spotAngle = 155f;
                    l.innerSpotAngle = 75f;
                    l.shadows = LightShadows.Soft;
                    l.shadowStrength = 0.75f;
                }
            }
            else if (kind == "lamp")
            {
                l.type = LightType.Point;
                l.color = Palette.Tungsten;
                l.intensity = 1.6f;
                l.range = 3.2f;
                l.shadows = LightShadows.None;
                if (room != null) room = null; // lamps are controlled by their own switch
            }
            if (room != null) room.Lights.Add(l);
        }

        void MakeGrime(Transform marker)
        {
            string id = marker.name.Substring(6);
            var go = new GameObject("GRIME_" + id);
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(marker.position, marker.rotation);
            var g = go.AddComponent<GrimeSurface>();
            g.Id = id;
            var s = marker.lossyScale;
            g.Size = new Vector2(Mathf.Abs(s.x), Mathf.Abs(s.z));
            g.Tool = ToolFor(id);
            g.MaskPpm = g.Tool switch { ToolKind.Vacuum => 50f, ToolKind.Mop => 50f, ToolKind.Squeegee => 80f, _ => 120f };
            Geo.GrimeDefaults(g);
            g.DisplayName = NiceName(id);
            go.SetActive(false);
            Surfaces[id] = g;
            var room = Room.At(go.transform.position + go.transform.up * 0.05f);
            if (room != null) room.Surfaces.Add(g);
        }

        public static ToolKind ToolFor(string id)
        {
            if (id.StartsWith("floor_"))
            {
                // Carpeted rooms vacuum, hard floors mop.
                return id is "floor_bullpen" or "floor_conf" ? ToolKind.Vacuum : ToolKind.Mop;
            }
            if (id.StartsWith("rug_")) return ToolKind.Vacuum;
            if (id.StartsWith("win_") || id.StartsWith("glass")) return ToolKind.Squeegee;
            return ToolKind.Cloth;
        }

        static string NiceName(string id) => id switch
        {
            "floor_bullpen" => "Bullpen carpet",
            "floor_conf" => "Conference carpet",
            "floor_reception" => "Reception floor",
            "floor_break" => "Break room floor",
            "floor_office" => "Office floor",
            "rug_reception" => "Lobby rug",
            "glass_doors" => "Glass doors",
            "glass_entry_l" or "glass_entry_r" => "Entrance glass",
            "glasswall_conf" => "Glass wall",
            "desk_reception" => "Reception desk",
            "coffeetable_reception" => "Coffee table",
            "whiteboard_conf" => "Whiteboard",
            "table_conf" => "Conference table",
            "table_break" => "Break room table",
            "counter_break" => "Counter",
            "fridge_break" => "Fridge door",
            "printer_top" => "Copier",
            "desk_marian" => "Marian's desk",
            _ when id.StartsWith("desk_") => char.ToUpper(id[5]) + id.Substring(6) + "'s desk",
            _ when id.StartsWith("win_") => "Window",
            _ when id.StartsWith("shelf_") => "Bookshelf",
            _ => id,
        };

        void BuildOutdoors()
        {
            // The city on three sides, a cool moon through the north-west windows.
            Skyline.Build(transform, new Vector3(12.5f, 0f, 22f), 3, 160f, Vector3.forward);
            Skyline.Build(transform, new Vector3(-6f, 0f, 8f), 7, 120f, Vector3.left);
            Skyline.Build(transform, new Vector3(31f, 0f, 12f), 11, 120f, Vector3.right);

            var moonGo = new GameObject("Moon");
            moonGo.transform.SetParent(transform, false);
            moonGo.transform.rotation = Quaternion.Euler(28f, 150f, 0f);
            Moon = moonGo.AddComponent<Light>();
            Moon.type = LightType.Directional;
            Moon.color = new Color(0.55f, 0.68f, 1f);
            Moon.intensity = 0.65f;

            // Cool city spill just inside every window run keeps dark rooms readable.
            (Vector3 pos, float range)[] spills =
            {
                (new Vector3(12.5f, 1.9f, 15.0f), 7f), (new Vector3(3.5f, 1.9f, 15.0f), 5f), (new Vector3(1.0f, 1.9f, 12.5f), 5f),
                (new Vector3(1.0f, 1.9f, 6.0f), 5f), (new Vector3(21.5f, 1.6f, 15.0f), 5.5f), (new Vector3(24.0f, 1.6f, 12.5f), 5.5f),
                (new Vector3(11.2f, 1.6f, 0.8f), 4f),
            };
            foreach (var (pos, range) in spills)
            {
                var sg = new GameObject("CitySpill");
                sg.transform.SetParent(transform, false);
                sg.transform.position = pos;
                var sl = sg.AddComponent<Light>();
                sl.type = LightType.Point;
                sl.color = new Color(0.45f, 0.58f, 0.85f);
                sl.intensity = 1.1f;
                sl.range = range;
                sl.shadows = LightShadows.None;
            }
            Moon.shadows = LightShadows.Soft;
            Moon.shadowStrength = 0.9f;
            RenderSettings.sun = Moon;
        }
    }
}
