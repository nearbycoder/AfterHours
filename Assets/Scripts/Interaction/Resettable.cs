using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// An object with a home pose (desk items, mugs in the dish rack). Carry it near home and a
    /// ghost shows where it goes; E snaps it in with a click and a squash.
    /// </summary>
    public class Resettable : MonoBehaviour
    {
        public string Id;
        public string Group;               // shared slots (e.g. "dishrack")
        public Vector3 HomePos;
        public Quaternion HomeRot = Quaternion.identity;
        public bool AtHome { get; private set; }
        public float SnapRadius = 0.45f;
        public bool Required = true;

        public static readonly List<Resettable> All = new();
        static readonly Dictionary<string, List<(Vector3 pos, Quaternion rot)>> slots = new();
        static readonly Dictionary<string, Resettable[]> occupied = new();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static void ClearSlots() { slots.Clear(); occupied.Clear(); }

        /// <summary>Where this item belongs (first slot of its group, or its own home).</summary>
        public Vector3 HomeAnchor => !string.IsNullOrEmpty(Group) && slots.TryGetValue(Group, out var l) && l.Count > 0 ? l[0].pos : HomePos;

        public static void AddSlot(string group, Vector3 pos, Quaternion rot)
        {
            if (!slots.TryGetValue(group, out var list)) slots[group] = list = new List<(Vector3, Quaternion)>();
            list.Add((pos, rot));
            occupied[group] = new Resettable[list.Count];
        }

        public void SetHome(Vector3 pos, Quaternion rot)
        {
            HomePos = pos;
            HomeRot = rot;
        }

        public void OnGrabbed()
        {
            if (AtHome) { AtHome = false; FreeSlot(); }
        }

        int slotIndex = -1;

        void FreeSlot()
        {
            if (Group != null && slotIndex >= 0 && occupied.TryGetValue(Group, out var occ)) occ[slotIndex] = null;
            slotIndex = -1;
        }

        bool BestHome(Vector3 near, out Vector3 pos, out Quaternion rot, out int slot)
        {
            slot = -1;
            pos = HomePos;
            rot = HomeRot;
            if (string.IsNullOrEmpty(Group)) return (near - HomePos).sqrMagnitude < SnapRadius * SnapRadius;
            if (!slots.TryGetValue(Group, out var list)) return false;
            float best = SnapRadius * SnapRadius;
            var occ = occupied[Group];
            for (int i = 0; i < list.Count; i++)
            {
                if (occ[i] != null && occ[i] != this) continue;
                float d = (near - list[i].pos).sqrMagnitude;
                if (d < best) { best = d; slot = i; pos = list[i].pos; rot = list[i].rot; }
            }
            return slot >= 0;
        }

        public bool NearHome(Vector3 aimPoint) =>
            BestHome(aimPoint, out _, out _, out _) || BestHome(transform.position, out _, out _, out _);

        public GameObject MakeGhost()
        {
            var cam = Hands.Instance.Player.Camera.transform;
            if (!BestHome(AimPoint(cam), out var pos, out var rot, out _) && !BestHome(transform.position, out pos, out rot, out _)) return null;
            var g = new GameObject("Ghost");
            foreach (var mf in GetComponentsInChildren<MeshFilter>())
            {
                var c = new GameObject("g");
                c.transform.SetParent(g.transform, false);
                c.transform.localPosition = transform.InverseTransformPoint(mf.transform.position);
                c.transform.localRotation = Quaternion.Inverse(transform.rotation) * mf.transform.rotation;
                c.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var r = c.AddComponent<MeshRenderer>();
                r.sharedMaterial = new Material(Res.Material("AH_LitFade"));
                r.sharedMaterial.SetColor("_BaseColor", new Color(0.7f, 0.95f, 1f, 0.3f));
                r.sharedMaterial.SetColor("_EmissionColor", new Color(0.2f, 0.5f, 0.6f));
                r.sharedMaterial.EnableKeyword("_EMISSION");
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            g.transform.SetPositionAndRotation(pos, rot);
            return g;
        }

        static Vector3 AimPoint(Transform cam)
        {
            if (Physics.Raycast(cam.position, cam.forward, out var hit, 2.4f, Layers.SolidMask, QueryTriggerInteraction.Ignore)) return hit.point;
            return cam.position + cam.forward * 1.2f;
        }

        public void SnapHome()
        {
            var cam = Hands.Instance.Player.Camera.transform;
            if (!BestHome(AimPoint(cam), out var pos, out var rot, out int slot) && !BestHome(transform.position, out pos, out rot, out slot))
            {
                pos = HomePos; rot = HomeRot;
            }
            if (Group != null && slot >= 0) { occupied[Group][slot] = this; slotIndex = slot; }
            var rb = GetComponent<Rigidbody>();
            if (rb) rb.isKinematic = true;
            var start = transform.position;
            var startRot = transform.rotation;
            var scale = transform.localScale;
            Tween.Run(0.22f, k =>
            {
                transform.position = Vector3.Lerp(start, pos, k);
                transform.rotation = Quaternion.Slerp(startRot, rot, k);
            }, Ease.OutCubic, () =>
            {
                // Squash on landing.
                Tween.Run(0.25f, k =>
                {
                    float s = 1f + Mathf.Sin(k * Mathf.PI) * 0.12f * (1f - k);
                    transform.localScale = new Vector3(scale.x * s, scale.y / s, scale.z * s);
                }, Ease.Linear, () => transform.localScale = scale);
                Sfx.Play("snap_home", pos, 0.6f, 1f + Random.Range(-0.05f, 0.08f));
                if (Group == "dishrack" || Group == "glasses") Sfx.Play("ceramic_clink", pos, 0.5f);
                Fx.Burst(FxKind.Puff, pos, Vector3.up, 5, new Color(0.9f, 0.9f, 0.85f, 0.25f), 0.3f, 1f, 0.05f);
                Fx.Burst(FxKind.Glint, pos + Vector3.up * 0.05f, Vector3.up, 2, new Color(1f, 0.95f, 0.8f), 0.1f, 0.3f);
                bool was = AtHome;
                AtHome = true;
                if (!was) Events.Raise(GameEvent.ItemReset, Id);
            });
        }

        /// <summary>Place instantly at home (night setup).</summary>
        public void ForceHome()
        {
            transform.SetPositionAndRotation(HomePos, HomeRot);
            var rb = GetComponent<Rigidbody>();
            if (rb) rb.isKinematic = true;
            AtHome = true;
        }

        /// <summary>Displaced start pose for the night.</summary>
        public void Displace(Vector3 pos, Quaternion rot, bool physics = true)
        {
            transform.SetPositionAndRotation(pos, rot);
            var rb = GetComponent<Rigidbody>();
            if (rb) rb.isKinematic = !physics;
            AtHome = false;
        }
    }

    /// <summary>A chair you can tuck back under its desk with E.</summary>
    public class Chair : MonoBehaviour, IInteractable
    {
        public string Id;
        public Vector3 HomePos;
        public Quaternion HomeRot;
        public bool Tucked { get; private set; }
        public static readonly List<Chair> All = new();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public string Prompt(Interactor who) => Tucked ? null : "Tuck in chair";

        public void Interact(Interactor who) => Tuck();

        public void Tuck(bool instant = false)
        {
            if (Tucked) return;
            Tucked = true;
            if (instant) { transform.SetPositionAndRotation(HomePos, HomeRot); return; }
            var p0 = transform.position;
            var r0 = transform.rotation;
            Sfx.Play("chair_roll", transform.position, 0.6f);
            Tween.Run(0.55f, k =>
            {
                transform.position = Vector3.Lerp(p0, HomePos, k);
                transform.rotation = Quaternion.Slerp(r0, HomeRot, k);
            }, Ease.OutBack, () =>
            {
                Fx.Burst(FxKind.Glint, HomePos + Vector3.up * 0.9f, Vector3.up, 2, new Color(1f, 0.95f, 0.8f), 0.1f, 0.3f);
                Events.Raise(GameEvent.ChairTucked, Id);
            });
        }

        public void Untuck(Vector3 pos, Quaternion rot)
        {
            Tucked = false;
            transform.SetPositionAndRotation(pos, rot);
        }
    }
}
