using UnityEngine;

namespace AfterHours
{
    /// <summary>Small helpers for building greybox geometry and grime surfaces in code.</summary>
    public static class Geo
    {
        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat, bool collider = true, int layer = Layers.Default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        public static GameObject Quad(Transform parent, string name, Vector3 center, Quaternion rot, Vector2 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rot;
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>
        /// Create a grime overlay. <paramref name="normal"/> is the outward surface normal and
        /// <paramref name="up"/> the in-plane direction of the surface's V axis.
        /// </summary>
        public static GrimeSurface Grime(Transform parent, string id, Vector3 center, Vector3 normal, Vector3 vAxis,
            Vector2 size, ToolKind tool, GrimeSpec spec, float ppm = 96f)
        {
            var go = new GameObject("GRIME_" + id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center + normal.normalized * 0.003f;
            go.transform.localRotation = Quaternion.LookRotation(vAxis, normal);
            var g = go.AddComponent<GrimeSurface>();
            g.Id = id;
            g.Tool = tool;
            g.Size = size;
            g.Spec = spec;
            g.MaskPpm = ppm;
            GrimeDefaults(g);
            g.Build();
            return g;
        }

        /// <summary>Per-tool look and feel defaults for a grime surface.</summary>
        public static void GrimeDefaults(GrimeSurface g)
        {
            switch (g.Tool)
            {
                case ToolKind.Vacuum:
                    g.NapStrength = 0.16f; g.DirtSmoothness = 0.05f; g.PatternPpm = 150f; g.Grain = 0.55f; g.AutoFinishAt = 0.9f;
                    break;
                case ToolKind.Mop:
                    g.WetStrength = 1f; g.DirtSmoothness = 0.15f; g.PatternPpm = 150f; g.Grain = 0.35f; g.AutoFinishAt = 0.9f;
                    break;
                case ToolKind.Squeegee:
                    g.FoamStrength = 1f; g.DirtSmoothness = 0.6f; g.PatternPpm = 160f; g.Grain = 0.25f;
                    g.Scatter = 1f; g.BackScatter = 0.9f; g.AutoFinishAt = 0.93f;
                    break;
                case ToolKind.Cloth:
                    g.FoamStrength = 0.55f; g.DirtSmoothness = 0.3f; g.PatternPpm = 240f; g.Grain = 0.4f; g.AutoFinishAt = 0.92f;
                    break;
            }
        }

        public static Light PointLight(Transform parent, Vector3 pos, Color color, float intensity, float range, bool shadows = false)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            return l;
        }

        public static Light SpotLight(Transform parent, Vector3 pos, Vector3 dir, Color color, float intensity, float range, float angle, bool shadows = false)
        {
            var go = new GameObject("Spot");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.LookRotation(dir);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.spotAngle = angle;
            l.innerSpotAngle = angle * 0.55f;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            return l;
        }
    }
}
