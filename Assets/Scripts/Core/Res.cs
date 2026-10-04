using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    /// <summary>Cached access to template materials, runtime material variants, clips and textures.</summary>
    public static class Res
    {
        static readonly Dictionary<string, Material> templates = new();
        static readonly Dictionary<string, Material> variants = new();
        static readonly Dictionary<string, AudioClip> clips = new();
        static readonly Dictionary<string, Texture2D> textures = new();

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        static readonly int Metallic = Shader.PropertyToID("_Metallic");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        public static Material Material(string name)
        {
            if (templates.TryGetValue(name, out var m) && m != null) return m;
            m = Resources.Load<Material>("Materials/" + name);
            if (m == null) Debug.LogError($"[Res] missing template material {name}");
            templates[name] = m;
            return m;
        }

        /// <summary>Real paint and plastic top out around 80% reflectance; brighter albedo blows out under the panel lights.</summary>
        static Color CapAlbedo(Color c)
        {
            const float max = 0.8f;
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (m <= max) return c;
            float k = max / m;
            return new Color(c.r * k, c.g * k, c.b * k, c.a);
        }

        public static Material Lit(Color c, float smooth = 0.35f, float metallic = 0f, Texture2D tex = null, Vector2? tiling = null)
        {
            c = CapAlbedo(c);
            string key = $"lit_{ColorUtility.ToHtmlStringRGBA(c)}_{smooth:F2}_{metallic:F2}_{(tex ? tex.name : "")}_{tiling}";
            if (variants.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Material("AH_LitOpaque")) { name = key };
            m.SetColor(BaseColor, c);
            m.SetFloat(Smoothness, smooth);
            m.SetFloat(Metallic, metallic);
            if (tex != null)
            {
                m.SetTexture(BaseMap, tex);
                m.SetTextureScale(BaseMap, tiling ?? Vector2.one);
            }
            variants[key] = m;
            return m;
        }

        static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
        static readonly int BumpScale = Shader.PropertyToID("_BumpScale");

        /// <summary>Lit material with base map, optional normal map and uniform tiling (repeats per metre).</summary>
        public static Material LitTextured(Color tint, float smooth, float metallic, Texture2D tex, Texture2D normal, float tiling)
        {
            tint = CapAlbedo(tint * 0.92f);
            string key = $"litT_{ColorUtility.ToHtmlStringRGB(tint)}_{smooth:F2}_{metallic:F2}_{(tex ? tex.name : "")}_{(normal ? 1 : 0)}_{tiling:F2}";
            if (variants.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Material(normal != null ? "AH_LitNormal" : "AH_LitOpaque")) { name = key };
            m.SetColor(BaseColor, tint);
            m.SetFloat(Smoothness, smooth);
            m.SetFloat(Metallic, metallic);
            if (tex != null)
            {
                m.SetTexture(BaseMap, tex);
                m.SetTextureScale(BaseMap, Vector2.one * tiling);
            }
            if (normal != null)
            {
                m.SetTexture(BumpMap, normal);
                m.SetFloat(BumpScale, 1f);
                m.EnableKeyword("_NORMALMAP");
            }
            variants[key] = m;
            return m;
        }

        public static Material Emissive(Color baseColor, Color emission, float intensity)
        {
            string key = $"emi_{ColorUtility.ToHtmlStringRGB(baseColor)}_{ColorUtility.ToHtmlStringRGB(emission)}_{intensity:F2}";
            if (variants.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Material("AH_LitEmissive")) { name = key };
            m.SetColor(BaseColor, baseColor);
            m.SetColor(EmissionColor, emission * intensity);
            m.EnableKeyword("_EMISSION");
            variants[key] = m;
            return m;
        }

        /// <summary>A fresh (uncached) emissive material whose emission can be animated.</summary>
        public static Material EmissiveInstance(Color baseColor, Color emission, float intensity)
        {
            var m = new Material(Material("AH_LitEmissive"));
            m.SetColor(BaseColor, baseColor);
            m.SetColor(EmissionColor, emission * intensity);
            m.EnableKeyword("_EMISSION");
            return m;
        }

        public static Material Glass(Color tint)
        {
            string key = $"glass_{ColorUtility.ToHtmlStringRGBA(tint)}";
            if (variants.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Material("AH_LitGlass")) { name = key };
            m.SetColor(BaseColor, tint);
            variants[key] = m;
            return m;
        }

        public static Material Fade(Color c)
        {
            string key = $"fade_{ColorUtility.ToHtmlStringRGBA(c)}";
            if (variants.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Material("AH_LitFade")) { name = key };
            m.SetColor(BaseColor, c);
            variants[key] = m;
            return m;
        }

        public static Material Unlit(Color c, Texture2D tex = null)
        {
            string key = $"unlit_{ColorUtility.ToHtmlStringRGBA(c)}_{(tex ? tex.name : "")}";
            if (variants.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Material("AH_Unlit")) { name = key };
            m.SetColor(BaseColor, c);
            if (tex) m.SetTexture(BaseMap, tex);
            variants[key] = m;
            return m;
        }

        public static AudioClip Clip(string name)
        {
            if (clips.TryGetValue(name, out var c)) return c;
            c = Resources.Load<AudioClip>("Audio/" + name);
            clips[name] = c;
            return c;
        }

        public static Texture2D Texture(string path)
        {
            if (textures.TryGetValue(path, out var t)) return t;
            t = Resources.Load<Texture2D>(path);
            textures[path] = t;
            return t;
        }
    }
}
