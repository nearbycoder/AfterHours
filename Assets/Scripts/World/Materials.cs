using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// Maps Blender material names to URP materials. Conventions (suffix after a dot is ignored):
    ///   col_RRGGBB[_sNN][_mNN]   lit colour, optional smoothness / metallic in percent
    ///   glow_RRGGBB[_iNN]        emissive (intensity NN/10)
    ///   glass_RRGGBB[_aNN]       transparent glass with alpha NN%
    ///   tex_NAME[_RRGGBB]        lit with Resources/Textures/NAME, optional tint
    ///   screen_*                 monitor screens (driven by MonitorScreen)
    /// </summary>
    public static class Materials
    {
        static readonly Dictionary<string, Material> cache = new();

        public static void Apply(GameObject root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var m = Resolve(mats[i].name);
                    if (m != null) { mats[i] = m; changed = true; }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        public static Material Resolve(string rawName)
        {
            var name = rawName.Replace(" (Instance)", "");
            int dot = name.IndexOf('.');
            if (dot >= 0) name = name.Substring(0, dot);
            if (cache.TryGetValue(name, out var m)) return m;
            m = Build(name);
            cache[name] = m;
            return m;
        }

        static Material Build(string name)
        {
            var parts = name.Split('_');
            if (parts.Length < 2) return null;
            Color c = Color.white;
            float smooth = 0.35f, metal = 0f, intensity = 2f, alpha = 0.15f;
            Texture2D tex = null;
            foreach (var p in parts)
            {
                if (p.Length == 6 && TryHex(p, out var col)) c = col;
                else if (p.Length > 1 && p[0] == 's' && int.TryParse(p.Substring(1), out int sv)) smooth = sv / 100f;
                else if (p.Length > 1 && p[0] == 'm' && int.TryParse(p.Substring(1), out int mv)) metal = mv / 100f;
                else if (p.Length > 1 && p[0] == 'i' && int.TryParse(p.Substring(1), out int iv)) intensity = iv / 10f;
                else if (p.Length > 1 && p[0] == 'a' && int.TryParse(p.Substring(1), out int av)) alpha = av / 100f;
            }
            switch (parts[0])
            {
                case "col": return Res.Lit(c, smooth, metal);
                case "glow": return Res.Emissive(c * 0.5f, c, intensity);
                case "glass": c.a = alpha; return Res.Glass(c);
                case "tex":
                    tex = Res.Texture("Textures/" + parts[1]);
                    var tint = parts.Length > 2 && TryHex(parts[2], out var t) ? t : Color.white;
                    return Res.Lit(tint, smooth, metal, tex);
                case "screen":
                    return Res.Emissive(Color.black, Palette.Monitor * 0.15f, 1f);
            }
            return null;
        }

        static bool TryHex(string s, out Color c)
        {
            c = Color.white;
            if (!int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)) return false;
            c = new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, 1f);
            return true;
        }
    }
}
