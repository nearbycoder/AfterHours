using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// Walt's UV torch (F). Leftover grime fluoresces inside the cone, and Walt's invisible-ink
    /// marks show up. Shader globals: _AH_UvPos (xyz, w=on), _AH_UvDir (xyz, w=cos half angle).
    /// </summary>
    public class UvTorch : MonoBehaviour
    {
        public static UvTorch Instance { get; private set; }
        public FirstPersonController Player;
        public bool On { get; private set; }
        Light beam;
        Transform model;
        float level;
        const float HalfAngle = 22f, Range = 7f;

        static readonly int PosId = Shader.PropertyToID("_AH_UvPos");
        static readonly int DirId = Shader.PropertyToID("_AH_UvDir");
        static readonly int RangeId = Shader.PropertyToID("_AH_UvRange");

        public static UvTorch Create(FirstPersonController p)
        {
            var t = p.gameObject.AddComponent<UvTorch>();
            t.Player = p;
            Instance = t;
            var go = new GameObject("UvBeam");
            go.transform.SetParent(p.Camera.transform, false);
            go.transform.localPosition = new Vector3(-0.15f, -0.12f, 0.2f);
            t.beam = go.AddComponent<Light>();
            t.beam.type = LightType.Spot;
            t.beam.color = new Color(0.55f, 0.35f, 1f);
            t.beam.spotAngle = HalfAngle * 2f;
            t.beam.innerSpotAngle = HalfAngle;
            t.beam.range = Range;
            t.beam.intensity = 0f;
            t.beam.shadows = LightShadows.None;
            var prefab = Resources.Load<GameObject>("Models/Tools/uv_torch");
            if (prefab)
            {
                var m = Instantiate(prefab, p.Camera.transform);
                Materials.Apply(m);
                m.layer = Layers.Viewmodel;
                foreach (var c in m.GetComponentsInChildren<Collider>()) Destroy(c);
                m.transform.localPosition = new Vector3(-0.22f, -0.2f, 0.36f);
                m.transform.localRotation = Quaternion.Euler(4, 8, 0);
                m.SetActive(false);
                t.model = m.transform;
            }
            Shader.SetGlobalVector(PosId, Vector4.zero);
            return t;
        }

        public bool Available => Story.State.Has("has_uv_torch");

        void Update()
        {
            if (GameInput.Frame.Torch)
            {
                if (!Available)
                {
                    if (NightDirector.Instance && NightDirector.Instance.Running)
                        Hud.Instance?.Toast("No torch", "You don't have a UV torch… yet.", new Color(0.6f, 0.5f, 0.9f), 1.6f);
                }
                else
                {
                    On = !On;
                    Sfx.Play("light_switch", null, 0.5f, 1.6f);
                }
            }
            if (!Available) On = false;
            level = Mathf.MoveTowards(level, On ? 1f : 0f, Time.deltaTime * 6f);
            beam.intensity = level * 2.2f;
            beam.enabled = level > 0.01f;
            if (model) model.gameObject.SetActive(level > 0.05f);
            var cam = Player.Camera.transform;
            Shader.SetGlobalVector(PosId, new Vector4(cam.position.x, cam.position.y, cam.position.z, level));
            Shader.SetGlobalVector(DirId, new Vector4(cam.forward.x, cam.forward.y, cam.forward.z, Mathf.Cos(HalfAngle * Mathf.Deg2Rad)));
            Shader.SetGlobalFloat(RangeId, Range);
        }

        /// <summary>Is a world point lit by the torch right now?</summary>
        public bool Illuminates(Vector3 p, float maxDist = Range)
        {
            if (level < 0.5f) return false;
            var cam = Player.Camera.transform;
            var d = p - cam.position;
            if (d.magnitude > maxDist) return false;
            return Vector3.Dot(d.normalized, cam.forward) > Mathf.Cos(HalfAngle * 0.8f * Mathf.Deg2Rad)
                   && !Physics.Linecast(cam.position, p - d.normalized * 0.05f, Layers.SolidMask, QueryTriggerInteraction.Ignore);
        }
    }

    /// <summary>Invisible-ink mark (quad) that only glows under the UV torch. Spotting it can count as a secret.</summary>
    public class UvMark : MonoBehaviour
    {
        public string Secret;
        public string Caption;
        bool seen;
        public static readonly List<UvMark> All = new();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static UvMark Create(Transform parent, string texture, Vector3 pos, Quaternion rot, Vector2 size, string secret = null, string caption = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(go.GetComponent<Collider>());
            go.name = "UvMark_" + texture;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            var m = new Material(Res.Material("AH_UvMark"));
            m.SetTexture("_MainTex", Res.Texture("Textures/Uv/" + texture));
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var u = go.AddComponent<UvMark>();
            u.Secret = secret;
            u.Caption = caption;
            return u;
        }

        void Update()
        {
            if (seen || UvTorch.Instance == null) return;
            if (!UvTorch.Instance.Illuminates(transform.position, 4.5f)) return;
            seen = true;
            if (!string.IsNullOrEmpty(Caption)) Hud.Instance?.Caption(Caption, 3f);
            if (!string.IsNullOrEmpty(Secret)) NightDirector.Instance?.FindSecret(Secret);
        }
    }
}
