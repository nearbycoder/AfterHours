using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    public enum ToolKind { None, Cloth, Vacuum, Squeegee, Mop, Hands }

    public enum StampKind
    {
        Dust, CoffeeRing, Spill, Footprints, Smudge, Haze, Crumbs, Scuff, Grease, Fingerprints, Image, Drips, Trail, Patch,
    }

    /// <summary>One authored mark of dirt. Positions are in surface UV (0..1), sizes in metres.</summary>
    public class GrimeStamp
    {
        public StampKind Kind;
        public Vector2 Pos = new(0.5f, 0.5f);
        public Vector2 To = new(0.5f, 0.5f);      // Footprints: end of the walk
        public float Size = 0.1f;                 // radius or stride, metres
        public float Amount = 1f;                 // opacity multiplier
        public float Density = 0.5f;              // Dust / Haze coverage
        public int Count = 12;                    // Crumbs / Fingerprints / Scuffs / Drips
        public float Toughness = 1f;              // >1 needs more scrubbing
        public Color Color = new(0.3f, 0.2f, 0.12f, 1f);
        public string Texture;                    // Image: Resources path
        public Rect Region = new(0, 0, 1, 1);     // area (uv) for scattered kinds
        public float EdgeBoost;                   // Dust: extra build-up near the surface edges
        public Vector2[] Path;                    // Trail: polyline in uv

        public static GrimeStamp Dust(float density = 0.55f, float amount = 0.55f, float edge = 0.6f) => new()
            { Kind = StampKind.Dust, Density = density, Amount = amount, EdgeBoost = edge, Color = new Color(0.6f, 0.57f, 0.5f), Toughness = 0.6f };
        public static GrimeStamp Trail(float width, float amount, params Vector2[] path) => new()
            { Kind = StampKind.Trail, Size = width, Amount = amount, Path = path, Color = new Color(0.36f, 0.32f, 0.27f), Toughness = 0.9f };
        public static GrimeStamp Patch(float u, float v, float radius, float amount = 0.6f) => new()
            { Kind = StampKind.Patch, Pos = new Vector2(u, v), Size = radius, Amount = amount, Color = new Color(0.45f, 0.41f, 0.35f), Toughness = 0.8f };
        public static GrimeStamp Ring(float u, float v, float radius = 0.045f) => new()
            { Kind = StampKind.CoffeeRing, Pos = new Vector2(u, v), Size = radius, Color = new Color(0.3f, 0.17f, 0.08f), Toughness = 2.2f };
        public static GrimeStamp Spill(float u, float v, float radius, Color c, float tough = 1.6f) => new()
            { Kind = StampKind.Spill, Pos = new Vector2(u, v), Size = radius, Color = c, Toughness = tough };
        public static GrimeStamp Steps(float u0, float v0, float u1, float v1, float amount = 0.8f) => new()
            { Kind = StampKind.Footprints, Pos = new Vector2(u0, v0), To = new Vector2(u1, v1), Size = 0.32f, Amount = amount, Color = new Color(0.16f, 0.13f, 0.1f), Toughness = 1.3f };
        public static GrimeStamp Crumbs(int count, Rect region) => new()
            { Kind = StampKind.Crumbs, Count = count, Region = region, Color = new Color(0.55f, 0.4f, 0.22f), Toughness = 0.5f };
        public static GrimeStamp Haze(float amount = 0.45f) => new()
            { Kind = StampKind.Haze, Amount = amount, Color = new Color(0.78f, 0.8f, 0.78f), Toughness = 1f };
        public static GrimeStamp Smudge(float u, float v, float size = 0.12f) => new()
            { Kind = StampKind.Smudge, Pos = new Vector2(u, v), Size = size, Color = new Color(0.8f, 0.82f, 0.84f), Toughness = 1.2f };
        public static GrimeStamp Prints(int count, Rect region) => new()
            { Kind = StampKind.Fingerprints, Count = count, Region = region, Color = new Color(0.82f, 0.83f, 0.85f), Toughness = 1f };
        public static GrimeStamp Scuffs(int count, Rect region) => new()
            { Kind = StampKind.Scuff, Count = count, Region = region, Color = new Color(0.12f, 0.11f, 0.1f), Toughness = 1.5f };
        public static GrimeStamp Grease(float u, float v, float radius) => new()
            { Kind = StampKind.Grease, Pos = new Vector2(u, v), Size = radius, Color = new Color(0.55f, 0.38f, 0.12f), Toughness = 2f };
        public static GrimeStamp Image(string path, float tough = 1f) => new()
            { Kind = StampKind.Image, Texture = path, Toughness = tough };
        public static GrimeStamp DripLines(int count, Rect region, Color c) => new()
            { Kind = StampKind.Drips, Count = count, Region = region, Color = c, Toughness = 1.4f };
    }

    /// <summary>Everything needed to generate one surface's dirt deterministically.</summary>
    public class GrimeSpec
    {
        public int Seed = 1;
        public readonly List<GrimeStamp> Stamps = new();
        public string GhostTexture;     // Resources path of the secret layer, optional
        public int GhostMode;           // 0 revealed where cleaned, 1 resists foam
        public bool GhostScrubbable;    // whiteboard: ghost can be scrubbed away after spraying

        public GrimeSpec Add(GrimeStamp s) { Stamps.Add(s); return this; }
        public GrimeSpec WithSeed(int seed) { Seed = seed; return this; }
    }

    /// <summary>
    /// Rasterises a <see cref="GrimeSpec"/> into colour + coverage (pattern resolution) and the
    /// per-texel toughness used by the CPU brush.
    /// </summary>
    public class GrimePatternData
    {
        public int Width, Height;
        public Color32[] Pixels;   // RGB colour, A coverage
        public float[] Coverage;   // 0..1
        public float[] Toughness;  // >= 0.2

        public static GrimePatternData Generate(GrimeSpec spec, Vector2 sizeM, int width, int height)
        {
            var d = new GrimePatternData { Width = width, Height = height };
            int n = width * height;
            var r = new float[n]; var g = new float[n]; var b = new float[n];
            d.Coverage = new float[n];
            d.Toughness = new float[n];
            for (int i = 0; i < n; i++) d.Toughness[i] = 1f;
            var rng = new Rng(spec.Seed);
            int stampIndex = 0;
            foreach (var s in spec.Stamps)
            {
                int seed = spec.Seed * 977 + stampIndex++ * 131;
                switch (s.Kind)
                {
                    case StampKind.Dust: Dust(d, r, g, b, s, sizeM, seed); break;
                    case StampKind.Haze: Haze(d, r, g, b, s, sizeM, seed); break;
                    case StampKind.CoffeeRing: Ring(d, r, g, b, s, sizeM, seed); break;
                    case StampKind.Spill: Spill(d, r, g, b, s, sizeM, seed); break;
                    case StampKind.Grease: Grease(d, r, g, b, s, sizeM, ref rng); break;
                    case StampKind.Footprints: Footprints(d, r, g, b, s, sizeM, seed); break;
                    case StampKind.Crumbs: Crumbs(d, r, g, b, s, sizeM, ref rng); break;
                    case StampKind.Smudge: Smudge(d, r, g, b, s, sizeM, seed); break;
                    case StampKind.Fingerprints: Fingerprints(d, r, g, b, s, sizeM, ref rng); break;
                    case StampKind.Scuff: Scuffs(d, r, g, b, s, sizeM, ref rng); break;
                    case StampKind.Image: Image(d, r, g, b, s); break;
                    case StampKind.Drips: Drips(d, r, g, b, s, sizeM, ref rng); break;
                    case StampKind.Trail: Trail(d, r, g, b, s, sizeM, seed); break;
                    case StampKind.Patch: Patch(d, r, g, b, s, sizeM, seed); break;
                }
            }
            d.Pixels = new Color32[n];
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Clamp01(d.Coverage[i]);
                d.Pixels[i] = new Color32(ToByte(r[i]), ToByte(g[i]), ToByte(b[i]), ToByte(a));
            }
            return d;
        }

        /// <summary>GLSL-style smoothstep (Mathf.SmoothStep is an interpolator, not this).</summary>
        public static float SS(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        static byte ToByte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        /// <summary>Composite one texel "over" what is already there.</summary>
        static void Put(GrimePatternData d, float[] r, float[] g, float[] b, int i, Color c, float a, float tough)
        {
            if (a <= 0.002f) return;
            a = Mathf.Clamp01(a);
            float old = d.Coverage[i];
            float outA = a + old * (1 - a);
            r[i] = (c.r * a + r[i] * old * (1 - a)) / outA;
            g[i] = (c.g * a + g[i] * old * (1 - a)) / outA;
            b[i] = (c.b * a + b[i] * old * (1 - a)) / outA;
            d.Toughness[i] = old <= 0.01f ? tough : (d.Toughness[i] * old * (1 - a) + tough * a) / outA;
            d.Coverage[i] = outA;
        }

        static void Bounds(GrimePatternData d, Vector2 sizeM, Vector2 centerUv, float radiusM,
            out int x0, out int y0, out int x1, out int y1)
        {
            float rx = radiusM / sizeM.x * d.Width, ry = radiusM / sizeM.y * d.Height;
            float cx = centerUv.x * d.Width, cy = centerUv.y * d.Height;
            x0 = Mathf.Max(0, Mathf.FloorToInt(cx - rx)); x1 = Mathf.Min(d.Width - 1, Mathf.CeilToInt(cx + rx));
            y0 = Mathf.Max(0, Mathf.FloorToInt(cy - ry)); y1 = Mathf.Min(d.Height - 1, Mathf.CeilToInt(cy + ry));
        }

        static Vector2 Metres(GrimePatternData d, Vector2 sizeM, int x, int y) =>
            new((x + 0.5f) / d.Width * sizeM.x, (y + 0.5f) / d.Height * sizeM.y);

        static Color Vary(Color c, float t) => new(c.r * (0.85f + 0.3f * t), c.g * (0.85f + 0.3f * t), c.b * (0.85f + 0.3f * t), 1f);

        static void Dust(GrimePatternData d, float[] r, float[] g, float[] b, GrimeStamp s, Vector2 sizeM, int seed)
        {
            float ox = seed * 0.37f, oy = seed * 0.71f;
            for (int y = 0; y < d.Height; y++)
            for (int x = 0; x < d.Width; x++)
            {
                var uv = new Vector2((x + 0.5f) / d.Width, (y + 0.5f) / d.Height);
                if (!s.Region.Contains(uv)) continue;
                var m = Metres(d, sizeM, x, y);
                float f = Noise.Table(m.x * 1.6f + ox, m.y * 1.6f + oy);
                float clump = Noise.Table(m.x * 7f + oy, m.y * 7f + ox);
                float speck = Noise.Hash01(x, y, seed);
                // Dust gathers along edges and in corners.
                float edgeDist = Mathf.Min(Mathf.Min(m.x, sizeM.x - m.x), Mathf.Min(m.y, sizeM.y - m.y));
                float edge = s.EdgeBoost * Mathf.Exp(-edgeDist / 0.06f);
                float cov = SS(1f - s.Density - 0.12f, 1f - s.Density + 0.3f, f + edge * 0.5f) * (0.55f + 0.45f * clump);
                cov = Mathf.Clamp01(cov + edge * 0.6f) * (0.7f + 0.3f * speck);
                Put(d, r, g, b, y * d.Width + x, Vary(s.Color, clump), cov * s.Amount, s.Toughness);
            }
        }

        static void Trail(GrimePatternData d, float[] r, float[] g, float[] b, GrimeStamp s, Vector2 sizeM, int seed)
        {
            if (s.Path == null || s.Path.Length < 2) return;
            var pts = new Vector2[s.Path.Length];
            for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(s.Path[i].x * sizeM.x, s.Path[i].y * sizeM.y);
            float half = s.Size * 0.5f;
            Vector2 min = pts[0], max = pts[0];
            foreach (var p in pts) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            min -= Vector2.one * s.Size; max += Vector2.one * s.Size;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(min.x / sizeM.x * d.Width)), x1 = Mathf.Min(d.Width - 1, Mathf.CeilToInt(max.x / sizeM.x * d.Width));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(min.y / sizeM.y * d.Height)), y1 = Mathf.Min(d.Height - 1, Mathf.CeilToInt(max.y / sizeM.y * d.Height));
            float ox = seed * 0.53f;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var m = Metres(d, sizeM, x, y);
                float best = float.MaxValue;
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    var a = pts[i]; var e = pts[i + 1] - a;
                    float t = Mathf.Clamp01(Vector2.Dot(m - a, e) / Mathf.Max(e.sqrMagnitude, 1e-6f));
                    best = Mathf.Min(best, (m - a - e * t).sqrMagnitude);
                }
                float dist = Mathf.Sqrt(best);
                float wob = Noise.Table(m.x * 2.2f + ox, m.y * 2.2f) * 0.5f + 0.75f;
                float k = 1f - dist / (half * wob);
                if (k <= 0f) continue;
                float scuffy = Noise.Table(m.x * 9f, m.y * 9f + ox);
                float speck = Noise.Hash01(x, y, seed);
                Put(d, r, g, b, y * d.Width + x, Vary(s.Color, scuffy), SS(0f, 0.7f, k) * (0.45f + 0.55f * scuffy) * (0.75f + 0.25f * speck) * s.Amount, s.Toughness);
            }
        }

        static void Patch(GrimePatternData d, float[] r, float[] g, float[] b, GrimeStamp s, Vector2 sizeM, int seed)
        {
            Bounds(d, sizeM, s.Pos, s.Size * 1.5f, out int x0, out int y0, out int x1, out int y1);
            var c = new Vector2(s.Pos.x * sizeM.x, s.Pos.y * sizeM.y);
            float ox = seed * 0.29f;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var m = Metres(d, sizeM, x, y);
                float dist = (m - c).magnitude / s.Size;
                float shape = Noise.Table(m.x * 2.5f + ox, m.y * 2.5f) * 0.8f + 0.6f;
                float k = 1f - dist / shape;
                if (k <= 0f) continue;
                float clump = Noise.Table(m.x * 10f, m.y * 10f + ox);
                Put(d, r, g, b, y * d.Width + x, Vary(s.Color, clump), SS(0f, 0.6f, k) * (0.5f + 0.5f * clump) * s.Amount, s.Toughness);
            }
        }

        static void Haze(GrimePatternData d, float[] r, float[] g, float[] b, GrimeStamp s, Vector2 sizeM, int seed)
        {
            for (int y = 0; y < d.Height; y++)
            for (int x = 0; x < d.Width; x++)
            {
                var m = Metres(d, sizeM, x, y);
                float f = Noise.Table(m.x * 1.4f + seed * 0.3f, m.y * 1.4f);
                float streak = Noise.Table(m.x * 0.7f, m.y * 6f + seed * 0.7f);
                float wipe = Mathf.Abs(Mathf.Sin((m.x * 0.8f + m.y * 2.6f + streak * 1.3f) * 3.1f));
                float cov = (0.35f + 0.65f * f) * (0.6f + 0.45f * streak) * (0.75f + 0.25f * wipe);
                Put(d, r, g, b, y * d.Width + x, s.Color, cov * s.Amount, s.Toughness);
            }
        }

        static void Ring(GrimePatternData d, float[] r, float[] g, float[] b, GrimeStamp s, Vector2 sizeM, int seed)
        {
            Bounds(d, sizeM, s.Pos, s.Size * 1.35f, out int x0, out int y0, out int x1, out int y1);
            var c = new Vector2(s.Pos.x * sizeM.x, s.Pos.y * sizeM.y);
            var dark = new Color(s.Color.r * 0.6f, s.Color.g * 0.6f, s.Color.b * 0.6f);
            float wobbleSeed = seed * 0.37f;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var m = Metres(d, sizeM, x, y) - c;
                float ang = Mathf.Atan2(m.y, m.x);
                float wob = 1f + 0.06f * (Noise.Value(ang * 2.2f + 10f, wobbleSeed, seed) - 0.5f) * 2f;
                float dist = m.magnitude / (s.Size * wob);
                float ring = Mathf.Exp(-Mathf.Pow((dist - 1f) / 0.07f, 2f));
                float gap = Noise.Value(ang * 1.4f + 3f, 1.7f, seed + 5);
                ring *= SS(0.05f, 0.45f, gap);
                float fill = dist < 1f ? 0.22f * Noise.Fbm(m.x * 40f, m.y * 40f, seed, 2) : 0f;
                float a = ring * 0.95f + fill;
                Put(d, r, g, b, y * d.Width + x, Color.Lerp(s.Color, dark, ring), a * s.Amount, s.Toughness);
            }
        }

        static void Spill(GrimePatternData d, float[] r, float[] g, float[] b, GrimeStamp s, Vector2 sizeM, int seed)
        {
            Bounds(d, sizeM, s.Pos, s.Size * 1.6f, out int x0, out int y0, out int x1, out int y1);
            var c = new Vector2(s.Pos.x * sizeM.x, s.Pos.y * sizeM.y);
            var dark = new Color(s.Color.r * 0.55f, s.Color.g * 0.55f, s.Color.b * 0.55f);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var m = Metres(d, sizeM, x, y) - c;
                float ang = Mathf.Atan2(m.y, m.x);
                float lobes = Noise.Fbm(Mathf.Cos(ang) * 1.5f + 5f, Mathf.Sin(ang) * 1.5f + 5f, seed, 3);
                float radius = s.Size * (0.7f + 0.75f * lobes);
                float dist = m.magnitude / radius;
                if (dist > 1.05f) continue;
                float body = SS(1.02f, 0.92f, dist);
                float rim = Mathf.Exp(-Mathf.Pow((dist - 0.95f) / 0.06f, 2f));
                float mottled = 0.75f + 0.25f * Noise.Fbm(m.x * 25f, m.y * 25f, seed + 9, 2);
                Put(d, r, g, b, y * d.Width + x, Color.Lerp(s.Color, dark, rim), (body * 0.72f * mottled + rim * 0.3f) * s.Amount, s.Toughness);
            }
        }

        static void Grease(GrimePatternData d, float[] r, float[] g, float[] b, GrimeStamp s, Vector2 sizeM, ref Rng rng)
        {
            int drops = 40 + s.Count;
            for (int k = 0; k < drops; k++)
            {
                var off = rng.InsideUnitCircle() * s.Size;
                float rad = Mathf.Lerp(0.004f, 0.018f, rng.Value * rng.Value);
                var pos = s.Pos + new Vector2(off.x / sizeM.x, off.y / sizeM.y);
                Disc(d, r, g, b, sizeM, pos, rad, s.Color, 0.75f * s.Amount, s.Toughness);
            }
        }

        static void Disc(GrimePatternData d, float[] r, float[] g, float[] b, Vector2 sizeM, Vector2 uv, float radiusM, Color c, float a, float tough)
        {
            Bounds(d, sizeM, uv, radiusM, out int x0, out int y0, out int x1, out int y1);
            var cm = new Vector2(uv.x * sizeM.x, uv.y * sizeM.y);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dist = (Metres(d, sizeM, x, y) - cm).magnitude / radiusM;
                if (dist > 1f) continue;
                Put(d, r, g, b, y * d.Width + x, c, a * SS(1f, 0.6f, dist), tough);
            }
        }

        static void Footprints(GrimePatternData d, float[] r, float[] g, float[] b, GrimeStamp s, Vector2 sizeM, int seed)
        {
            var a = new Vector2(s.Pos.x * sizeM.x, s.Pos.y * sizeM.y);
            var e = new Vector2(s.To.x * sizeM.x, s.To.y * sizeM.y);
            var dir = (e - a);
            float len = dir.magnitude;
            if (len < 0.01f) return;
            dir /= len;
            var side = new Vector2(-dir.y, dir.x);
            int steps = Mathf.Max(1, Mathf.FloorToInt(len / s.Size));
            for (int k = 0; k <= steps; k++)
            {
                float fade = Mathf.Lerp(1f, 0.35f, k / (float)Mathf.Max(1, steps));
                var foot = a + dir * (k * s.Size) + side * ((k % 2 == 0 ? 1 : -1) * 0.085f);
                Shoe(d, r, g, b, sizeM, foot, dir, s, fade * s.Amount, seed + k);
            }
        }

        static void Shoe(GrimePatternData d, float[] r, float[] g, float[] b, Vector2 sizeM, Vector2 footM, Vector2 dir, GrimeStamp s, float amount, int seed)
        {
            var uv = new Vector2(footM.x / sizeM.x, footM.y / sizeM.y);
            Bounds(d, sizeM, uv, 0.17f, out int x0, out int y0, out int x1, out int y1);
            var side = new Vector2(-dir.y, dir.x);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var m = Metres(d, sizeM, x, y) - footM;
                float along = Vector2.Dot(m, dir), across = Vector2.Dot(m, side);
                // forefoot ellipse + heel ellipse
                float fore = Mathf.Pow((along - 0.045f) / 0.075f, 2f) + Mathf.Pow(across / 0.047f, 2f);
                float heel = Mathf.Pow((along + 0.085f) / 0.042f, 2f) + Mathf.Pow(across / 0.04f, 2f);
                float shape = Mathf.Max(SS(1.05f, 0.75f, fore), SS(1.05f, 0.75f, heel));
                if (shape <= 0f) continue;
                float tread = 0.62f + 0.38f * SS(-0.4f, 0.4f, Mathf.Sin(along * 170f + across * 30f));
                float wear = Noise.Table(m.x * 12f + seed * 0.1f, m.y * 12f);
                Put(d, r, g, b, y * d.Width + x, s.Color, shape * tread * (0.45f + 0.55f * wear) * amount, s.Toughness);
            }
        }

        static void Crumbs(GrimePatternData d, float[] r, float[] g, float[] b, GrimeStamp s, Vector2 sizeM, ref Rng rng)
        {
            for (int k = 0; k < s.Count; k++)
            {
                var uv = new Vector2(Mathf.Lerp(s.Region.xMin, s.Region.xMax, rng.Value), Mathf.Lerp(s.Region.yMin, s.Region.yMax, rng.Value));
                float rad = Mathf.Lerp(0.0025f, 0.007f, rng.Value);
                Disc(d, r, g, b, sizeM, uv, rad, Vary(s.Color, rng.Value), 0.95f * s.Amount, s.Toughness);
            }
        }

        static void Smudge(GrimePatternData d, float[] r, float[] g, float[] b, GrimeStamp s, Vector2 sizeM, int seed)
        {
            Bounds(d, sizeM, s.Pos, s.Size * 1.4f, out int x0, out int y0, out int x1, out int y1);
            var c = new Vector2(s.Pos.x * sizeM.x, s.Pos.y * sizeM.y);
            float ang = Noise.Hash01(seed, 3, 7) * Mathf.PI;
            var ax = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            var ay = new Vector2(-ax.y, ax.x);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var m = Metres(d, sizeM, x, y) - c;
                float u = Vector2.Dot(m, ax) / s.Size, v = Vector2.Dot(m, ay) / (s.Size * 0.45f);
                float e = u * u + v * v;
                if (e > 1.2f) continue;
                float streaks = 0.6f + 0.4f * Noise.Fbm(Vector2.Dot(m, ax) * 6f, Vector2.Dot(m, ay) * 90f, seed, 2);
                Put(d, r, g, b, y * d.Width + x, s.Color, SS(1.1f, 0.2f, e) * 0.55f * streaks * s.Amount, s.Toughness);
            }
        }

        static void Fingerprints(GrimePatternData d, float[] r, float[] g, float[] b, GrimeStamp s, Vector2 sizeM, ref Rng rng)
        {
            for (int k = 0; k < s.Count; k++)
            {
                var uv = new Vector2(Mathf.Lerp(s.Region.xMin, s.Region.xMax, rng.Value), Mathf.Lerp(s.Region.yMin, s.Region.yMax, rng.Value));
                float ang = rng.Value * Mathf.PI;
                Bounds(d, sizeM, uv, 0.02f, out int x0, out int y0, out int x1, out int y1);
                var c = new Vector2(uv.x * sizeM.x, uv.y * sizeM.y);
                var ax = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                var ay = new Vector2(-ax.y, ax.x);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var m = Metres(d, sizeM, x, y) - c;
                    float u = Vector2.Dot(m, ax) / 0.011f, v = Vector2.Dot(m, ay) / 0.0085f;
                    float e = Mathf.Sqrt(u * u + v * v);
                    if (e > 1f) continue;
                    float ridges = 0.5f + 0.5f * Mathf.Sin(e * 26f);
                    Put(d, r, g, b, y * d.Width + x, s.Color, ridges * SS(1f, 0.5f, e) * 0.6f * s.Amount, s.Toughness);
                }
            }
        }

        static void Scuffs(GrimePatternData d, float[] r, float[] g, float[] b, GrimeStamp s, Vector2 sizeM, ref Rng rng)
        {
            for (int k = 0; k < s.Count; k++)
            {
                var uv = new Vector2(Mathf.Lerp(s.Region.xMin, s.Region.xMax, rng.Value), Mathf.Lerp(s.Region.yMin, s.Region.yMax, rng.Value));
                float ang = rng.Value * Mathf.PI * 2f;
                float len = rng.Range(0.05f, 0.22f), width = rng.Range(0.004f, 0.012f);
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                var c = new Vector2(uv.x * sizeM.x, uv.y * sizeM.y);
                Bounds(d, sizeM, uv, len, out int x0, out int y0, out int x1, out int y1);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var m = Metres(d, sizeM, x, y) - c;
                    float t = Mathf.Clamp(Vector2.Dot(m, dir), -len * 0.5f, len * 0.5f);
                    float dist = (m - dir * t).magnitude / width;
                    if (dist > 1f) continue;
                    float taper = 1f - Mathf.Abs(t) / (len * 0.5f);
                    Put(d, r, g, b, y * d.Width + x, s.Color, SS(1f, 0.3f, dist) * taper * 0.7f * s.Amount, s.Toughness);
                }
            }
        }

        static void Drips(GrimePatternData d, float[] r, float[] g, float[] b, GrimeStamp s, Vector2 sizeM, ref Rng rng)
        {
            for (int k = 0; k < s.Count; k++)
            {
                float u = Mathf.Lerp(s.Region.xMin, s.Region.xMax, rng.Value);
                float top = Mathf.Lerp(s.Region.yMin, s.Region.yMax, rng.Value);
                float lenM = rng.Range(0.04f, 0.25f), w = rng.Range(0.003f, 0.007f);
                var start = new Vector2(u, top);
                Bounds(d, sizeM, start, lenM, out int x0, out int y0, out int x1, out int y1);
                float cx = u * sizeM.x, cy = top * sizeM.y;
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var m = Metres(d, sizeM, x, y);
                    float down = cy - m.y;
                    if (down < 0 || down > lenM) continue;
                    float dist = Mathf.Abs(m.x - cx) / (w * (1f + 0.6f * down / lenM));
                    if (dist > 1f) continue;
                    Put(d, r, g, b, y * d.Width + x, s.Color, SS(1f, 0.4f, dist) * 0.6f * s.Amount, s.Toughness);
                }
            }
        }

        static void Image(GrimePatternData d, float[] r, float[] g, float[] b, GrimeStamp s)
        {
            var tex = Resources.Load<Texture2D>(s.Texture);
            if (tex == null) { Debug.LogWarning($"[Grime] missing image {s.Texture}"); return; }
            for (int y = 0; y < d.Height; y++)
            for (int x = 0; x < d.Width; x++)
            {
                var c = tex.GetPixelBilinear((x + 0.5f) / d.Width, (y + 0.5f) / d.Height);
                Put(d, r, g, b, y * d.Width + x, c, c.a * s.Amount, s.Toughness);
            }
        }
    }
}
