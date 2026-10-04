using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// Spawns props from the Blender prop library (Resources/Models/Props.fbx, one child per prop
    /// named PROP_&lt;id&gt;). Adds colliders and, for loose items, physics.
    /// </summary>
    public static class PropLibrary
    {
        static GameObject lib;
        static readonly Dictionary<string, GameObject> byId = new();

        /// <summary>Physical feel per prop: mass and impact sound.</summary>
        public static readonly Dictionary<string, (float mass, string sound)> Physical = new()
        {
            { "paper_ball", (0.02f, "drop_soft") }, { "paper_ball_yellow", (0.01f, "drop_soft") },
            { "paper_sheet", (0.01f, "drop_soft") }, { "paper_stack", (0.1f, "drop_soft") },
            { "folder_manila", (0.1f, "drop_soft") }, { "folder_red", (0.2f, "drop_soft") },
            { "envelope", (0.03f, "drop_soft") }, { "sticky_note", (0.005f, "drop_soft") },
            { "notepad", (0.1f, "drop_soft") }, { "key", (0.02f, "can_clank") },
            { "soda_can", (0.03f, "can_clank") }, { "energy_can", (0.03f, "can_clank") },
            { "water_bottle", (0.05f, "drop_soft") }, { "paper_cup", (0.03f, "drop_soft") },
            { "takeout_box", (0.08f, "drop_soft") }, { "pizza_box", (0.3f, "drop_soft") },
            { "party_plate", (0.02f, "drop_soft") }, { "banana_peel", (0.05f, "drop_soft") },
            { "party_hat", (0.02f, "drop_soft") },
            { "mug_white", (0.3f, "ceramic_clink") }, { "mug_red", (0.3f, "ceramic_clink") },
            { "mug_teal", (0.3f, "ceramic_clink") }, { "mug_yellow", (0.3f, "ceramic_clink") },
            { "water_glass", (0.2f, "ceramic_clink") }, { "stapler", (0.3f, "drop_hard") },
            { "pen_cup", (0.2f, "drop_hard") }, { "photo_frame", (0.3f, "drop_hard") },
            { "binder", (0.6f, "drop_hard") }, { "book", (0.4f, "drop_hard") },
            { "sticky_pad", (0.05f, "drop_soft") }, { "keyboard", (0.6f, "drop_hard") },
            { "mouse", (0.1f, "drop_hard") }, { "plant_succulent", (0.4f, "drop_hard") },
            { "calendar_desk", (0.1f, "drop_soft") }, { "award_plaque", (0.5f, "drop_hard") },
            { "archive_box", (2f, "drop_hard") }, { "card_box", (1.5f, "drop_hard") },
            { "desk_phone", (0.8f, "drop_hard") }, { "laptop", (1.2f, "drop_hard") },
        };

        static void Load()
        {
            if (lib != null) return;
            lib = Resources.Load<GameObject>("Models/Props");
            if (lib == null) { Debug.LogError("[Props] Models/Props missing"); return; }
            foreach (Transform c in lib.transform)
            {
                var id = c.name.StartsWith("PROP_") ? c.name.Substring(5) : c.name;
                byId[id] = c.gameObject;
            }
        }

        public static bool Has(string id) { Load(); return byId.ContainsKey(id); }

        public static IEnumerable<string> Ids { get { Load(); return byId.Keys; } }

        /// <summary>Instantiate a prop with a fitted collider. Loose props get a Rigidbody.</summary>
        public static GameObject Spawn(string id, Vector3 pos, Quaternion rot, Transform parent = null, bool physics = false)
        {
            Load();
            if (!byId.TryGetValue(id, out var src))
            {
                Debug.LogWarning("[Props] unknown prop " + id);
                var fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fallback.transform.localScale = Vector3.one * 0.1f;
                fallback.transform.SetPositionAndRotation(pos, rot);
                return fallback;
            }
            var go = Object.Instantiate(src, pos, rot, parent);
            go.name = id;
            go.transform.localScale = Vector3.one;
            Materials.Apply(go);
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var b = mf.sharedMesh.bounds;
                var bc = go.AddComponent<BoxCollider>();
                bc.center = b.center;
                bc.size = new Vector3(Mathf.Max(b.size.x, 0.012f), Mathf.Max(b.size.y, 0.012f), Mathf.Max(b.size.z, 0.012f));
            }
            go.layer = physics ? Layers.Prop : Layers.Default;
            if (physics)
            {
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = Physical.TryGetValue(id, out var p) ? p.mass : 0.3f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.linearDamping = 0.05f;
                rb.angularDamping = 0.2f;
                go.AddComponent<ImpactSound>().Sound = Physical.TryGetValue(id, out var ps) ? ps.sound : "drop_hard";
            }
            return go;
        }
    }

    /// <summary>Plays a pitched impact sound on collisions above a speed threshold.</summary>
    public class ImpactSound : MonoBehaviour
    {
        public string Sound = "drop_hard";
        float last;

        void OnCollisionEnter(Collision c)
        {
            float v = c.relativeVelocity.magnitude;
            if (v < 0.6f || Time.time - last < 0.08f) return;
            last = Time.time;
            Sfx.Play(Sound, transform.position, Mathf.Clamp01(v / 5f) * 0.7f, 1f, 0.12f);
        }
    }
}
