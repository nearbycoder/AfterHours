using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// The tools you see. The cloth and spray bottle sit in the view; the vacuum, squeegee and mop
    /// are long-handled, so their heads touch the surface you aim at and the shaft runs back to
    /// your hands. Models come from Blender (Resources/Models/Tools) with primitive fallbacks.
    /// </summary>
    public class ToolRig : MonoBehaviour
    {
        Transform viewRoot, hands;
        Transform cloth, bottle, vacGrip, vacHead, vacWand, sqGrip, sqHead, sqPole, mopGrip, mopHead, mopPole;
        Transform ring;
        Material ringMat;
        ToolKind shown = ToolKind.None, pending = ToolKind.Cloth;
        float swap = 1f;               // 0 = lowered, 1 = raised
        float scrubSpeed, scrubPhase, sprayTimer, sprayKick;
        Vector3 scrubDirView;
        Vector3 headPos, headVel;
        Quaternion headRot = Quaternion.identity;
        Vector3 bladeDir = Vector3.right;
        float idleT;
        Vector3 swayPos;
        Quaternion swayRot = Quaternion.identity;

        public float ScrubSpeed => scrubSpeed;

        public static ToolRig Create(Camera cam)
        {
            var go = new GameObject("ToolRig");
            var rig = go.AddComponent<ToolRig>();
            rig.Build(cam);
            return rig;
        }

        void Build(Camera cam)
        {
            viewRoot = new GameObject("ViewRoot").transform;
            viewRoot.SetParent(cam.transform, false);
            hands = new GameObject("Hands").transform;
            hands.SetParent(viewRoot, false);

            var plastic = Res.Lit(Palette.Hex("2F7FD8"), 0.55f);
            var white = Res.Lit(Palette.Hex("E9EEF2"), 0.4f);
            var steel = Res.Lit(Palette.Hex("B9C2CC"), 0.75f, 0.85f);
            var rubber = Res.Lit(Palette.Hex("1E2226"), 0.25f);
            var orange = Res.Lit(Palette.Hex("F08A24"), 0.45f);
            var clothMat = Res.Lit(Palette.Hex("F2C14E"), 0.05f);
            var mopMat = Res.Lit(Palette.Hex("D8D2C4"), 0.05f);
            var yellow = Res.Lit(Palette.Hex("F4C430"), 0.5f);

            // Cloth + spray bottle (view space).
            bottle = Model("spray_bottle", hands, new Vector3(-0.02f, -0.07f, 0.06f), Quaternion.Euler(0, 15, 0), () =>
            {
                var b = new GameObject("bottle").transform;
                Prim(PrimitiveType.Cylinder, b, new Vector3(0, 0, 0), new Vector3(0.07f, 0.09f, 0.07f), white);
                Prim(PrimitiveType.Cube, b, new Vector3(0, 0.11f, 0.015f), new Vector3(0.035f, 0.05f, 0.06f), plastic);
                Prim(PrimitiveType.Cube, b, new Vector3(0, 0.12f, 0.06f), new Vector3(0.012f, 0.012f, 0.035f), plastic);
                Prim(PrimitiveType.Cube, b, new Vector3(0, 0.07f, 0.05f), new Vector3(0.012f, 0.05f, 0.012f), plastic);
                return b;
            });
            cloth = Model("cloth", hands, new Vector3(0.14f, -0.07f, 0.12f), Quaternion.Euler(10, -10, 0), () =>
            {
                var c = new GameObject("cloth").transform;
                Prim(PrimitiveType.Cube, c, Vector3.zero, new Vector3(0.12f, 0.03f, 0.1f), clothMat);
                Prim(PrimitiveType.Cube, c, new Vector3(0.02f, 0.02f, -0.01f), new Vector3(0.09f, 0.025f, 0.07f), clothMat);
                return c;
            });

            // Long-handled tools live in world space.
            vacGrip = Model("vacuum_grip", hands, new Vector3(0.12f, -0.12f, 0.1f), Quaternion.identity, () =>
            {
                var g = new GameObject("grip").transform;
                Prim(PrimitiveType.Cube, g, Vector3.zero, new Vector3(0.05f, 0.05f, 0.16f), orange);
                return g;
            });
            vacWand = Shaft("vac_wand", steel, 0.017f);
            vacHead = Model("vacuum_head", null, Vector3.zero, Quaternion.identity, () =>
            {
                var h = new GameObject("vac_head").transform;
                Prim(PrimitiveType.Cube, h, new Vector3(0, 0.025f, 0), new Vector3(0.34f, 0.045f, 0.1f), rubber);
                Prim(PrimitiveType.Cube, h, new Vector3(0, 0.055f, -0.02f), new Vector3(0.1f, 0.04f, 0.07f), orange);
                Prim(PrimitiveType.Cube, h, new Vector3(0, 0.005f, 0.045f), new Vector3(0.33f, 0.01f, 0.012f), Res.Emissive(Palette.Hex("FFB45E"), Palette.Hex("FFB45E"), 1.5f));
                return h;
            });

            sqGrip = Model("squeegee_grip", hands, new Vector3(0.1f, -0.12f, 0.12f), Quaternion.identity, () =>
            {
                var g = new GameObject("grip").transform;
                Prim(PrimitiveType.Cube, g, Vector3.zero, new Vector3(0.04f, 0.04f, 0.14f), yellow);
                return g;
            });
            sqPole = Shaft("sq_pole", steel, 0.012f);
            sqHead = Model("squeegee_head", null, Vector3.zero, Quaternion.identity, () =>
            {
                var h = new GameObject("sq_head").transform;
                Prim(PrimitiveType.Cube, h, new Vector3(0, 0.015f, 0), new Vector3(0.38f, 0.03f, 0.03f), steel);
                Prim(PrimitiveType.Cube, h, new Vector3(0, 0.004f, 0), new Vector3(0.37f, 0.012f, 0.008f), rubber);
                Prim(PrimitiveType.Cube, h, new Vector3(0, 0.045f, 0), new Vector3(0.05f, 0.04f, 0.03f), yellow);
                return h;
            });

            mopGrip = Model("mop_grip", hands, new Vector3(0.1f, -0.14f, 0.1f), Quaternion.identity, () =>
            {
                var g = new GameObject("grip").transform;
                Prim(PrimitiveType.Cube, g, Vector3.zero, new Vector3(0.04f, 0.04f, 0.12f), plastic);
                return g;
            });
            mopPole = Shaft("mop_pole", Res.Lit(Palette.Hex("4E79B6"), 0.4f), 0.014f);
            mopHead = Model("mop_head", null, Vector3.zero, Quaternion.identity, () =>
            {
                var h = new GameObject("mop_head").transform;
                Prim(PrimitiveType.Cube, h, new Vector3(0, 0.03f, 0), new Vector3(0.44f, 0.03f, 0.1f), plastic);
                for (int i = 0; i < 9; i++)
                    Prim(PrimitiveType.Cube, h, new Vector3(-0.2f + i * 0.05f, 0.01f, 0), new Vector3(0.045f, 0.02f, 0.13f), mopMat);
                return h;
            });

            // Contact ring
            ring = new GameObject("ContactRing").transform;
            ring.gameObject.layer = Layers.Viewmodel;
            var mf = ring.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = QuadXZ();
            var mr = ring.gameObject.AddComponent<MeshRenderer>();
            ringMat = new Material(Res.Material("AH_Fx"));
            ringMat.SetFloat("_Shape", 3);
            ringMat.SetFloat("_Intensity", 1f);
            ringMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            ringMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mr.sharedMaterial = ringMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Everything carried in the hands draws on the overlay camera; heads and shafts that
            // touch the world stay depth-tested against it.
            SetLayer(viewRoot, Layers.Hands);
            SetActive(ToolKind.None);
            Show(ToolKind.Cloth);
        }

        static Mesh QuadXZ()
        {
            var m = new Mesh { name = "RingQuad" };
            m.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            return m;
        }

        Transform Model(string name, Transform parent, Vector3 pos, Quaternion rot, System.Func<Transform> fallback)
        {
            Transform t = null;
            var prefab = Resources.Load<GameObject>("Models/Tools/" + name);
            if (prefab != null)
            {
                var go = Instantiate(prefab);
                go.name = name;
                Materials.Apply(go);
                t = go.transform;
            }
            else t = fallback();
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = rot;
            SetLayer(t, Layers.Viewmodel);
            foreach (var c in t.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var r in t.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }

        Transform Shaft(string name, Material mat, float radius)
        {
            var t = Prim(PrimitiveType.Cylinder, null, Vector3.zero, new Vector3(radius * 2, 0.5f, radius * 2), mat).transform;
            t.name = name;
            t.gameObject.layer = Layers.Viewmodel;
            t.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }

        static GameObject Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            if (parent) go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform c in t) SetLayer(c, layer);
        }

        void SetActive(ToolKind k)
        {
            cloth.gameObject.SetActive(k == ToolKind.Cloth);
            bottle.gameObject.SetActive(k is ToolKind.Cloth or ToolKind.Squeegee);
            vacGrip.gameObject.SetActive(k == ToolKind.Vacuum);
            vacWand.gameObject.SetActive(k == ToolKind.Vacuum);
            vacHead.gameObject.SetActive(k == ToolKind.Vacuum);
            sqGrip.gameObject.SetActive(k == ToolKind.Squeegee);
            sqPole.gameObject.SetActive(k == ToolKind.Squeegee);
            sqHead.gameObject.SetActive(k == ToolKind.Squeegee);
            mopGrip.gameObject.SetActive(k == ToolKind.Mop);
            mopPole.gameObject.SetActive(k == ToolKind.Mop);
            mopHead.gameObject.SetActive(k == ToolKind.Mop);
            shown = k;
        }

        public void Show(ToolKind k)
        {
            pending = k;
        }

        /// <summary>Hide every tool (hands busy).</summary>
        public bool Hidden { get; set; }

        public void Scrub(float speed, Vector3 worldDir)
        {
            scrubSpeed = Mathf.Lerp(scrubSpeed, speed, 0.3f);
            if (viewRoot && worldDir.sqrMagnitude > 1e-6f)
                scrubDirView = Vector3.Lerp(scrubDirView, viewRoot.parent.InverseTransformDirection(worldDir), 0.3f);
            if (worldDir.sqrMagnitude > 1e-6f) bladeDir = Vector3.Slerp(bladeDir, Vector3.Cross(worldDir, headRot * Vector3.up).normalized, 0.15f);
        }

        /// <summary>Animate the bottle trigger; returns true when a mist particle burst is due.</summary>
        public bool SprayPuff(float dt)
        {
            sprayKick = 1f;
            sprayTimer -= dt;
            if (sprayTimer > 0f) return false;
            sprayTimer = 0.045f;
            return true;
        }

        public void Tick(CleaningController cc, float dt)
        {
            // Swap: lower, switch, raise.
            bool busy = GameRoot.Instance != null && GameRoot.Instance.Blocked;
            var want = Hidden || busy ? ToolKind.None : pending;
            if (want != shown)
            {
                swap = Mathf.MoveTowards(swap, 0f, dt * 7f);
                if (swap <= 0f) SetActive(want);
            }
            else swap = Mathf.MoveTowards(swap, 1f, dt * 5f);
            float raise = 1f - Mathf.Pow(1f - swap, 3f);

            // View sway from look and movement.
            var look = GameInput.Frame.Look;
            var targetSway = new Vector3(-look.x * 0.0018f, -look.y * 0.0018f, 0f);
            swayPos = Vector3.Lerp(swayPos, Vector3.ClampMagnitude(targetSway, 0.05f), 1f - Mathf.Exp(-dt * 10f));
            swayRot = Quaternion.Slerp(swayRot, Quaternion.Euler(look.y * 0.4f, -look.x * 0.4f, -look.x * 0.6f), 1f - Mathf.Exp(-dt * 8f));
            idleT += dt;
            var breathe = new Vector3(0, Mathf.Sin(idleT * 1.4f) * 0.004f, 0);

            // Scrubbing motion: follow the stroke plus a little circular wobble.
            if (!cc.Cleaning) scrubSpeed = Mathf.Lerp(scrubSpeed, 0f, 1f - Mathf.Exp(-dt * 8f));
            scrubPhase += dt * (6f + scrubSpeed * 10f);
            float s = Mathf.Clamp01(scrubSpeed * 1.4f) * (cc.Cleaning ? 1f : 0f);
            var scrub = new Vector3(Mathf.Sin(scrubPhase) * 0.02f, Mathf.Cos(scrubPhase * 2f) * 0.012f, 0) * s
                        + Vector3.ClampMagnitude(new Vector3(scrubDirView.x, scrubDirView.y, 0), 1f) * 0.03f * s;
            sprayKick = Mathf.MoveTowards(sprayKick, 0f, dt * 6f);

            viewRoot.localPosition = new Vector3(0.22f, -0.29f, 0.44f) + swayPos + breathe + Vector3.down * (1f - raise) * 0.45f;
            viewRoot.localRotation = swayRot;
            cloth.localPosition = new Vector3(0.14f, -0.07f, 0.12f) + scrub + (cc.Cleaning && shown == ToolKind.Cloth ? Vector3.forward * 0.06f : Vector3.zero);
            bottle.localRotation = Quaternion.Euler(-sprayKick * 12f, 15f, 0f);

            // Long-handled heads.
            Transform head = null, shaft = null, grip = null;
            switch (shown)
            {
                case ToolKind.Vacuum: head = vacHead; shaft = vacWand; grip = vacGrip; break;
                case ToolKind.Squeegee: head = sqHead; shaft = sqPole; grip = sqGrip; break;
                case ToolKind.Mop: head = mopHead; shaft = mopPole; grip = mopGrip; break;
            }
            if (head != null)
            {
                bool contact = cc.Target != null && cc.Target.Tool == shown && cc.InReach;
                Vector3 targetPos;
                Quaternion targetRot;
                var cam = cc.Player.Camera.transform;
                if (contact)
                {
                    targetPos = cc.ContactPoint + cc.ContactNormal * 0.004f;
                    var fwd = Vector3.ProjectOnPlane(cam.forward, cc.ContactNormal);
                    if (shown == ToolKind.Squeegee)
                    {
                        // Blade stays perpendicular to the stroke.
                        var side = Vector3.ProjectOnPlane(bladeDir, cc.ContactNormal);
                        if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(cc.ContactNormal, fwd);
                        targetRot = Quaternion.LookRotation(Vector3.Cross(side.normalized, cc.ContactNormal), cc.ContactNormal);
                    }
                    else targetRot = Quaternion.LookRotation(fwd.sqrMagnitude > 1e-4f ? fwd : cam.up, cc.ContactNormal);
                }
                else
                {
                    // Rest pose: head floats ahead and below the view.
                    var flatFwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                    if (flatFwd.sqrMagnitude < 1e-4f) flatFwd = cc.Player.transform.forward;
                    targetPos = cc.Player.transform.position + flatFwd * 0.9f + Vector3.up * (shown == ToolKind.Squeegee ? 1.0f : 0.02f);
                    targetRot = shown == ToolKind.Squeegee
                        ? Quaternion.LookRotation(Vector3.up, -flatFwd)
                        : Quaternion.LookRotation(flatFwd, Vector3.up);
                }
                targetPos += Vector3.down * (1f - raise) * 0.6f;
                // Critically damped follow so the head glides instead of teleporting.
                float k = contact ? 60f : 18f;
                headVel += ((targetPos - headPos) * k * k - headVel * 2f * k) * dt;
                if ((targetPos - headPos).sqrMagnitude > 4f) { headPos = targetPos; headVel = Vector3.zero; }
                headPos += headVel * dt;
                headRot = Quaternion.Slerp(headRot, targetRot, 1f - Mathf.Exp(-dt * 20f));
                head.SetPositionAndRotation(headPos, headRot);

                var a = grip.position + grip.forward * 0.06f;
                var b = head.position + head.up * (shown == ToolKind.Squeegee ? 0.05f : 0.06f);
                var mid = (a + b) * 0.5f;
                var d = b - a;
                shaft.position = mid;
                shaft.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
                var sc = shaft.localScale;
                shaft.localScale = new Vector3(sc.x, d.magnitude * 0.5f, sc.z);
                grip.rotation = Quaternion.LookRotation(d.normalized, cam.up);
            }

            // Contact ring: shows brush size and where you'll clean.
            bool showRing = cc.Target != null && cc.InReach && !cc.Suspended && !Hidden;
            float alpha = showRing ? (cc.Target.Tool == shown ? 0.55f : 0.22f) : 0f;
            ringAlpha = Mathf.Lerp(ringAlpha, alpha, 1f - Mathf.Exp(-dt * 14f));
            ring.gameObject.SetActive(ringAlpha > 0.01f);
            if (ring.gameObject.activeSelf)
            {
                var brush = ToolDefs.For(cc.Target != null ? cc.Target.Tool : shown);
                float size = (brush.Shape == BrushShape.Blade ? brush.Radius * 2f : brush.Radius * 2f) * (1f + Mathf.Sin(idleT * 5f) * 0.03f);
                ring.position = cc.ContactPoint + cc.ContactNormal * 0.006f;
                ring.rotation = Quaternion.FromToRotation(Vector3.up, cc.ContactNormal);
                ring.localScale = Vector3.one * size;
                var acc = ToolDefs.Accent(cc.Target != null ? cc.Target.Tool : shown);
                ringMat.SetFloat("_Intensity", ringAlpha * 1.4f);
                ring.GetComponent<MeshFilter>().sharedMesh.colors = new[] { acc, acc, acc, acc };
            }
        }

        float ringAlpha;
    }
}
