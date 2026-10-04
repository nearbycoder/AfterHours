using UnityEngine;

namespace AfterHours
{
    /// <summary>Deterministic hashing, value noise and fBm used by pattern generation.</summary>
    public static class Noise
    {
        public static uint Hash(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16;
            return x;
        }

        public static float Hash01(int x, int y, int seed)
        {
            uint h = Hash((uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u ^ (uint)seed * 0xcb1ab31fu);
            return (h & 0xffffff) / 16777216f;
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>Value noise in [0,1], optionally periodic (period &gt; 0) for tileable textures.</summary>
        public static float Value(float x, float y, int seed, int period = 0)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            int x0 = xi, x1 = xi + 1, y0 = yi, y1 = yi + 1;
            if (period > 0)
            {
                x0 = ((x0 % period) + period) % period; x1 = ((x1 % period) + period) % period;
                y0 = ((y0 % period) + period) % period; y1 = ((y1 % period) + period) % period;
            }
            float a = Hash01(x0, y0, seed), b = Hash01(x1, y0, seed);
            float c = Hash01(x0, y1, seed), d = Hash01(x1, y1, seed);
            float u = Smooth(xf), v = Smooth(yf);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        public static float Fbm(float x, float y, int seed, int octaves = 4, int period = 0)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(x, y, seed + i * 131, period) * amp;
                norm += amp;
                x *= 2f; y *= 2f; amp *= 0.5f;
                if (period > 0) period *= 2;
            }
            return sum / norm;
        }

        static float[] table;
        const int TableSize = 256;

        /// <summary>
        /// Fast tileable fBm: a precomputed 256² table (period = 8 noise cells) sampled
        /// bilinearly. One unit of x/y equals one noise cell. Values in [0,1].
        /// </summary>
        public static float Table(float x, float y)
        {
            if (table == null)
            {
                table = new float[TableSize * TableSize];
                for (int j = 0; j < TableSize; j++)
                for (int i = 0; i < TableSize; i++)
                    table[j * TableSize + i] = Fbm(i / 32f, j / 32f, 4242, 4, 8);
            }
            x *= 32f; y *= 32f;
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            int x0 = xi & (TableSize - 1), y0 = yi & (TableSize - 1);
            int x1 = (x0 + 1) & (TableSize - 1), y1 = (y0 + 1) & (TableSize - 1);
            float a = table[y0 * TableSize + x0], b = table[y0 * TableSize + x1];
            float c = table[y1 * TableSize + x0], d = table[y1 * TableSize + x1];
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static Texture2D tileable;

        /// <summary>A 256² tileable noise texture (R: fBm, G: fine fBm) used by shaders.</summary>
        public static Texture2D TileableTexture
        {
            get
            {
                if (tileable != null) return tileable;
                const int size = 256;
                tileable = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
                {
                    name = "AH_Noise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 4,
                };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float a = Fbm(x / 32f, y / 32f, 11, 4, 8);
                    float b = Fbm(x / 8f, y / 8f, 29, 3, 32);
                    a = Mathf.Clamp01((a - 0.5f) * 1.9f + 0.5f);
                    px[y * size + x] = new Color32((byte)(a * 255), (byte)(b * 255), 128, 255);
                }
                tileable.SetPixels32(px);
                tileable.Apply(true, true);
                return tileable;
            }
        }
    }

    /// <summary>Small deterministic RNG (xorshift) so night content is reproducible.</summary>
    public struct Rng
    {
        uint state;
        public Rng(int seed) { state = Noise.Hash((uint)seed * 2654435761u + 1013904223u) | 1u; }
        public uint Next() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state; }
        public float Value => (Next() & 0xffffff) / 16777216f;
        public float Range(float a, float b) => a + (b - a) * Value;
        public int Range(int a, int bExclusive) => a + (int)(Next() % (uint)Mathf.Max(1, bExclusive - a));
        public Vector2 InsideUnitCircle()
        {
            float ang = Value * Mathf.PI * 2f, r = Mathf.Sqrt(Value);
            return new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
        }
    }
}
