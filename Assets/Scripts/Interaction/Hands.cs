using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// Carrying: the held object floats in front of the camera, LMB charges and throws (with an arc
    /// preview), E places on the surface you aim at (or snaps home), Q drops.
    /// </summary>
    public class Hands : MonoBehaviour
    {
        public static Hands Instance { get; private set; }
        public FirstPersonController Player;
        public CleaningController Cleaning;
        public Holdable Holding { get; private set; }
        public float Charge { get; private set; }
        public bool Charging { get; private set; }

        int heldLayer;
        Vector3 holdVel;
        LineRenderer arc;
        float grabTime;
        Resettable homeCandidate;

        void Awake()
        {
            Instance = this;
            var go = new GameObject("ThrowArc");
            arc = go.AddComponent<LineRenderer>();
            arc.positionCount = 0;
            arc.widthMultiplier = 0.02f;
            arc.material = new Material(Res.Material("AH_Fx"));
            arc.material.SetFloat("_Shape", 5);
            arc.material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            arc.material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            arc.textureMode = LineTextureMode.Tile;
            arc.numCapVertices = 2;
            arc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            arc.startColor = new Color(1f, 0.85f, 0.5f, 0.9f);
            arc.endColor = new Color(1f, 0.85f, 0.5f, 0.0f);
        }

        public void Grab(Holdable h)
        {
            if (Holding != null || h == null || h.Locked) return;
            Holding = h;
            h.Held = true;
            h.InFlight = false;
            heldLayer = h.gameObject.layer;
            SetLayer(h.gameObject, Layers.Viewmodel);
            h.Body.isKinematic = true;
            h.Body.interpolation = RigidbodyInterpolation.None;
            holdVel = Vector3.zero;
            grabTime = Time.time;
            Cleaning.Suspended = true;
            Cleaning.Rig.Hidden = true;
            Sfx.Play("pickup", h.transform.position, 0.6f);
            h.OnGrabbed();
            h.GetComponent<Resettable>()?.OnGrabbed();
        }

        static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayer(c.gameObject, layer);
        }

        Vector3 HoldPoint(Holdable h)
        {
            var cam = Player.Camera.transform;
            return cam.position + cam.forward * h.HoldDistance - cam.up * 0.16f + cam.right * 0.05f;
        }

        void Release(bool keepPhysics = true)
        {
            var h = Holding;
            if (h == null) return;
            Holding = null;
            h.Held = false;
            SetLayer(h.gameObject, heldLayer);
            h.Body.isKinematic = !keepPhysics;
            h.Body.interpolation = RigidbodyInterpolation.Interpolate;
            Cleaning.Suspended = false;
            Cleaning.Rig.Hidden = false;
            Charging = false;
            Charge = 0f;
            arc.positionCount = 0;
            HideGhost();
        }

        void Update()
        {
            var h = Holding;
            if (h == null) { arc.positionCount = 0; return; }
            if (h == null || !h.gameObject.activeInHierarchy) { Holding = null; Cleaning.Suspended = false; Cleaning.Rig.Hidden = false; return; }
            var input = GameInput.Frame;
            float dt = Time.deltaTime;
            var cam = Player.Camera.transform;

            // Follow the hold point (spring), keep the object upright and facing the player.
            var target = HoldPoint(h);
            if (Charging) target -= cam.forward * (0.08f * Charge) - cam.up * 0.04f * Charge;
            var pos = h.transform.position;
            holdVel += ((target - pos) * 420f - holdVel * 38f) * dt;
            pos += holdVel * dt;
            if ((target - pos).sqrMagnitude > 1f) pos = target;
            var rot = Quaternion.Slerp(h.transform.rotation, Quaternion.Euler(0, Player.Yaw + 180f, 0) * Quaternion.Euler(0, 20f, 0), 1f - Mathf.Exp(-dt * 12f));
            h.transform.SetPositionAndRotation(pos, rot);

            // Home spot ghost
            var res = h.GetComponent<Resettable>();
            homeCandidate = res != null && res.NearHome(PlaceTarget(out _, out _) ?? h.transform.position) ? res : null;
            if (homeCandidate != null) ShowGhost(homeCandidate); else HideGhost();

            bool justGrabbed = Time.time - grabTime < 0.15f;

            // Throw
            if (input.UseDown && !justGrabbed) { Charging = true; Charge = 0f; }
            if (Charging)
            {
                Charge = Mathf.Min(1f, Charge + dt / 0.7f);
                DrawArc(h);
                if (input.UseUp || !input.Use)
                {
                    Throw(h);
                    return;
                }
            }

            if (input.Interact && !justGrabbed)
            {
                if (homeCandidate != null)
                {
                    var r = homeCandidate;
                    Release(false);
                    r.SnapHome();
                    return;
                }
                var p = PlaceTarget(out var normal, out var hitCol);
                if (p.HasValue)
                {
                    Release(false);
                    h.transform.position = p.Value;
                    h.transform.rotation = Quaternion.Euler(0, h.transform.eulerAngles.y, 0);
                    h.Body.isKinematic = false;
                    Sfx.Play("drop_soft", p.Value, 0.5f);
                    h.OnReleased(false);
                }
                else Drop(h);
                return;
            }
            if (input.Drop) Drop(h);
        }

        void Drop(Holdable h)
        {
            Release();
            h.Body.linearVelocity = holdVel * 0.3f;
            h.OnReleased(false);
        }

        void Throw(Holdable h)
        {
            var cam = Player.Camera.transform;
            float c = Charge;
            Release();
            var v = cam.forward * Mathf.Lerp(3.2f, 10f, c * c) + Vector3.up * Mathf.Lerp(0.8f, 1.6f, c);
            h.Body.linearVelocity = v;
            h.Body.angularVelocity = Random.insideUnitSphere * 8f;
            h.ThrownFrom = cam.position;
            h.InFlight = true;
            Sfx.Play("toss", cam.position, 0.4f + c * 0.3f, 0.9f + c * 0.3f);
            Player.Kick(0.6f + c * 1.2f);
            h.OnReleased(true);
        }

        /// <summary>Where E would place the held object (top of a roughly horizontal surface).</summary>
        Vector3? PlaceTarget(out Vector3 normal, out Collider col)
        {
            normal = Vector3.up;
            col = null;
            var cam = Player.Camera.transform;
            if (Physics.Raycast(cam.position, cam.forward, out var hit, 2.4f, Layers.SolidMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y > 0.65f)
                {
                    normal = hit.normal;
                    col = hit.collider;
                    return hit.point + Vector3.up * 0.01f;
                }
            }
            return null;
        }

        void DrawArc(Holdable h)
        {
            var cam = Player.Camera.transform;
            float c = Charge;
            var v = cam.forward * Mathf.Lerp(3.2f, 10f, c * c) + Vector3.up * Mathf.Lerp(0.8f, 1.6f, c);
            var p = h.transform.position;
            const int n = 28;
            arc.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                arc.SetPosition(i, p);
                var next = p + v * 0.035f;
                v += Physics.gravity * 0.035f;
                if (Physics.Linecast(p, next, out var hit, Layers.SolidMask, QueryTriggerInteraction.Ignore))
                {
                    arc.positionCount = i + 1;
                    arc.SetPosition(i, hit.point);
                    break;
                }
                p = next;
            }
            arc.widthMultiplier = 0.012f + c * 0.01f;
        }

        // ---- ghost preview for home spots ----
        GameObject ghost;
        Resettable ghostFor;

        void ShowGhost(Resettable r)
        {
            if (ghostFor != r)
            {
                HideGhost();
                ghostFor = r;
                ghost = r.MakeGhost();
            }
            if (ghost)
            {
                float pulse = 0.25f + 0.15f * Mathf.Sin(Time.time * 6f);
                foreach (var rend in ghost.GetComponentsInChildren<Renderer>())
                    rend.sharedMaterial.SetColor("_BaseColor", new Color(0.7f, 0.95f, 1f, pulse));
            }
        }

        void HideGhost()
        {
            if (ghost) Destroy(ghost);
            ghost = null;
            ghostFor = null;
        }

        public string PromptText()
        {
            if (Holding == null) return null;
            return homeCandidate != null ? "Put back" : "Place";
        }

        public bool HasHomeCandidate => homeCandidate != null;
    }
}
