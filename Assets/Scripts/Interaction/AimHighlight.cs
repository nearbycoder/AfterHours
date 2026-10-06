using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace AfterHours
{
    /// <summary>
    /// A soft warm rim on whatever the reticle is on (the interactor's focus), so small things in
    /// dark rooms read as usable. Each of the object's mesh renderers gets a "shell": a child
    /// renderer sharing its mesh with the additive Highlight shader. Off in Settings.
    /// </summary>
    public class AimHighlight : MonoBehaviour
    {
        public static AimHighlight Instance { get; private set; }
        /// <summary>The object currently highlighted, if any (automation checks this).</summary>
        public Transform Target { get; private set; }
        public int ShellCount => shells.Count(s => s);

        /// <summary>Things bigger than this (a whole locker, a desk) aren't outlined; the prompt is enough.</summary>
        const float MaxSize = 2.4f;
        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int FillId = Shader.PropertyToID("_Fill");
        /// <summary>Rim strength and fill: enough to find a white paper ball on a lit carpet.</summary>
        const float Intensity = 2.4f, Fill = 0.2f;
        readonly List<MeshRenderer> shells = new();
        MaterialPropertyBlock block;
        Material material;
        float fade;

        void Awake()
        {
            Instance = this;
            block = new MaterialPropertyBlock();
            material = Res.Material("AH_Highlight");
        }

        void LateUpdate()
        {
            var ia = Interactor.Instance;
            var focus = Settings.Current.AimHighlight && ia != null && !ia.Locked ? ia.Focus : null;
            var t = focus != null ? focus.transform : null;
            if (t != Target)
            {
                Clear();
                Target = t;
                fade = 0f;
                if (t != null) Build(t);
            }
            if (shells.Count == 0) return;
            fade = Mathf.MoveTowards(fade, 1f, GameTime.UnscaledDelta * 7f);
            block.SetFloat(FadeId, fade);
            block.SetFloat(IntensityId, Intensity);
            block.SetFloat(FillId, Fill);
            foreach (var s in shells) if (s) s.SetPropertyBlock(block);
        }

        void Build(Transform t)
        {
            if (material == null) return;
            var rends = t.GetComponentsInChildren<MeshRenderer>()
                .Where(r => r.enabled && r.gameObject.activeInHierarchy && r.gameObject.layer != Layers.Grime && r.name != "Highlight")
                .ToList();
            if (rends.Count == 0) return;
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            if (b.size.magnitude > MaxSize) return;
            foreach (var r in rends)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var go = new GameObject("Highlight");
                go.layer = r.gameObject.layer;
                go.transform.SetParent(r.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, mf.sharedMesh.subMeshCount)).ToArray();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                shells.Add(mr);
            }
        }

        void Clear()
        {
            foreach (var s in shells) if (s) Destroy(s.gameObject);
            shells.Clear();
            Target = null;
        }
    }
}
