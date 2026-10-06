using System;
using UnityEngine;

namespace AfterHours
{
    public enum BrushShape { Round, Blade }

    /// <summary>How a tool removes grime. Radii and blade sizes are in metres.</summary>
    public struct Brush
    {
        public BrushShape Shape;
        public float Radius;         // Round: radius. Blade: half-length of the blade.
        public float Rate;           // dirt removed per second at full pressure
        public float ScrubRef;       // m/s of stroke speed that gives 1x; 0 = constant rate
        public float MinScrub;       // rate multiplier when holding still
        public bool Nap;             // vacuum: comb the carpet nap
        public float Wet;            // mop: wetness left behind
        public float DryFactor;      // squeegee: effectiveness without foam
        public bool ClearsFoam;      // squeegee removes foam as it goes
    }

    /// <summary>
    /// A cleanable overlay. The quad lies in the local XZ plane (normal +Y) and spans
    /// <see cref="Size"/> metres. Painting happens on the CPU at mask resolution and is uploaded
    /// to a small RGBA mask texture, which keeps completion exact and needs no GPU readback.
    /// </summary>
    [DisallowMultipleComponent]
    public class GrimeSurface : MonoBehaviour
    {
        public string Id;
        public string DisplayName;
        public ToolKind Tool = ToolKind.Cloth;
        public Vector2 Size = Vector2.one;
        public float MaskPpm = 96f;
        public float PatternPpm = 220f;
        public float Scatter;
        public float BackScatter;
        public float Grain = 0.4f;
        public float AutoFinishAt = 0.94f;
        public float NapStrength;
        public float WetStrength;
        public float FoamStrength;
        public float DirtSmoothness = 0.2f;
        public bool Required = true;
        public bool RevealOnly;          // no dirt: "cleaning" reveals the ghost layer (pencil rubbing)
        public string Verb;              // prompt override
        public GrimeSpec Spec = new();

        public event Action<GrimeSurface> Completed;
        public event Action<GrimeSurface> GhostRevealed;
        public event Action<GrimeSurface> GhostScrubbed;

        public float Completion { get; private set; }
        public bool Done { get; private set; }
        public bool HasGhost => ghostWeightTotal > 0f;
        public float GhostReveal { get; private set; }
        public float GhostRemaining { get; private set; } = 1f;
        public bool GhostWasRevealed { get; private set; }
        public bool GhostWasScrubbed { get; private set; }
        public int MaskWidth => mw;
        public int MaskHeight => mh;
        public BoxCollider Collider => box;

        // CPU state at mask resolution
        int mw, mh;
        float[] remain, weight, tough, ghostW;
        byte[] mask;               // RGBA32, uploaded to maskTex
        float totalWeight, remainingWeight, ghostWeightTotal;
        Texture2D maskTex, patternTex, ghostTex;
        Material material;
        MeshRenderer meshRenderer;
        BoxCollider box;
        bool maskDirty, built;
        int wetTexels;
        float finishT = -1f, gleamT = -1f;

        static readonly int PatternId = Shader.PropertyToID("_PatternTex");
        static readonly int MaskId = Shader.PropertyToID("_MaskTex");
        static readonly int GhostId = Shader.PropertyToID("_GhostTex");
        static readonly int NoiseId = Shader.PropertyToID("_NoiseTex");
        static readonly int SizeId = Shader.PropertyToID("_Size");
        static readonly int NapId = Shader.PropertyToID("_NapStrength");
        static readonly int WetId = Shader.PropertyToID("_WetStrength");
        static readonly int FoamId = Shader.PropertyToID("_FoamStrength");
        static readonly int GhostModeId = Shader.PropertyToID("_GhostMode");
        static readonly int GleamId = Shader.PropertyToID("_GleamT");
        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int ShimmerId = Shader.PropertyToID("_Shimmer");
        static readonly int SmoothId = Shader.PropertyToID("_DirtSmoothness");
        static readonly int ScatterId = Shader.PropertyToID("_Scatter");
        static readonly int BackScatterId = Shader.PropertyToID("_BackScatter");
        static readonly int GrainId = Shader.PropertyToID("_Grain");

        public static readonly System.Collections.Generic.List<GrimeSurface> All = new();

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void OnDestroy()
        {
            if (maskTex) Destroy(maskTex);
            if (patternTex) Destroy(patternTex);
            if (material) Destroy(material);
        }

        /// <summary>Create mesh, collider, textures and the dirt pattern. Safe to call again to regenerate.</summary>
        public void Build()
        {
            gameObject.layer = Layers.Grime;
            mw = Mathf.Clamp(Mathf.RoundToInt(Size.x * MaskPpm), 16, 1024);
            mh = Mathf.Clamp(Mathf.RoundToInt(Size.y * MaskPpm), 16, 1024);
            int pw = Mathf.Clamp(Mathf.RoundToInt(Size.x * PatternPpm), mw, 1600);
            int ph = Mathf.Clamp(Mathf.RoundToInt(Size.y * PatternPpm), mh, 1600);

            if (!built)
            {
                var mf = gameObject.AddComponent<MeshFilter>();
                mf.sharedMesh = QuadMesh(Size);
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = true;
                material = new Material(Res.Material("AH_Grime")) { name = "Grime_" + Id };
                meshRenderer.sharedMaterial = material;
                box = gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(Size.x, 0.012f, Size.y);
                box.center = new Vector3(0, 0.004f, 0);
                built = true;
            }

            var data = GrimePatternData.Generate(Spec, Size, pw, ph);
            if (patternTex) Destroy(patternTex);
            patternTex = new Texture2D(pw, ph, TextureFormat.RGBA32, true, false)
            {
                name = "Pattern_" + Id, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8,
            };
            patternTex.SetPixels32(data.Pixels);
            patternTex.Apply(true, true);

            int n = mw * mh;
            remain = new float[n]; weight = new float[n]; tough = new float[n];
            mask = new byte[n * 4];
            totalWeight = 0;
            for (int y = 0; y < mh; y++)
            for (int x = 0; x < mw; x++)
            {
                // Box-filter the pattern down to mask resolution.
                int px0 = x * pw / mw, px1 = Mathf.Max(px0 + 1, (x + 1) * pw / mw);
                int py0 = y * ph / mh, py1 = Mathf.Max(py0 + 1, (y + 1) * ph / mh);
                float cov = 0, t = 0; int cnt = 0;
                for (int py = py0; py < py1; py++)
                for (int px = px0; px < px1; px++)
                {
                    int pi = py * pw + px;
                    cov += data.Coverage[pi];
                    t += data.Toughness[pi] * data.Coverage[pi];
                    cnt++;
                }
                int i = y * mw + x;
                float c = cov / cnt;
                weight[i] = c < 0.03f ? 0f : c;
                tough[i] = cov > 0 ? Mathf.Max(0.25f, t / cov) : 1f;
                remain[i] = 1f;
                totalWeight += weight[i];
                mask[i * 4 + 0] = 255; mask[i * 4 + 1] = 128; mask[i * 4 + 2] = 0; mask[i * 4 + 3] = 255;
            }
            remainingWeight = totalWeight;

            if (maskTex) Destroy(maskTex);
            maskTex = new Texture2D(mw, mh, TextureFormat.RGBA32, false, true)
            {
                name = "Mask_" + Id, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            maskTex.SetPixelData(mask, 0);
            maskTex.Apply(false, false);

            // Optional secret layer.
            ghostWeightTotal = 0;
            ghostW = null;
            ghostTex = string.IsNullOrEmpty(Spec.GhostTexture) ? null : Resources.Load<Texture2D>(Spec.GhostTexture);
            if (ghostTex != null)
            {
                ghostW = new float[n];
                for (int y = 0; y < mh; y++)
                for (int x = 0; x < mw; x++)
                {
                    float a = ghostTex.GetPixelBilinear((x + 0.5f) / mw, (y + 0.5f) / mh).a;
                    ghostW[y * mw + x] = a > 0.1f ? a : 0f;
                    ghostWeightTotal += ghostW[y * mw + x];
                }
            }

            Done = !RevealOnly && totalWeight <= 0.0001f;
            Completion = Done ? 1f : 0f;
            GhostReveal = 0; GhostRemaining = 1; GhostWasRevealed = false; GhostWasScrubbed = false;
            finishT = -1; gleamT = -1; wetTexels = 0;

            material.SetTexture(PatternId, patternTex);
            material.SetTexture(MaskId, maskTex);
            material.SetTexture(GhostId, ghostTex != null ? ghostTex : Texture2D.blackTexture);
            material.SetTexture(NoiseId, Noise.TileableTexture);
            material.SetVector(SizeId, new Vector4(Size.x, Size.y, 0, 0));
            material.SetFloat(NapId, NapStrength);
            material.SetFloat(WetId, WetStrength);
            material.SetFloat(FoamId, FoamStrength);
            material.SetFloat(GhostModeId, Spec.GhostMode);
            material.SetFloat(GleamId, -1f);
            material.SetFloat(FadeId, 1f);
            material.SetFloat(SmoothId, DirtSmoothness);
            material.SetFloat(ScatterId, Scatter);
            material.SetFloat(BackScatterId, BackScatter);
            material.SetFloat(GrainId, Grain);
            meshRenderer.enabled = true;
        }

        public void SetShimmer(float v) { if (material) material.SetFloat(ShimmerId, v); }

        static Mesh QuadMesh(Vector2 size)
        {
            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            var m = new Mesh { name = "GrimeQuad" };
            m.vertices = new[] { new Vector3(-hx, 0, -hz), new Vector3(hx, 0, -hz), new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>World point → surface UV (0..1). False if outside the quad.</summary>
        public bool WorldToUv(Vector3 world, out Vector2 uv)
        {
            var l = transform.InverseTransformPoint(world);
            uv = new Vector2(l.x / Size.x + 0.5f, l.z / Size.y + 0.5f);
            return uv.x >= -0.02f && uv.x <= 1.02f && uv.y >= -0.02f && uv.y <= 1.02f;
        }

        public Vector3 UvToWorld(Vector2 uv) =>
            transform.TransformPoint(new Vector3((uv.x - 0.5f) * Size.x, 0, (uv.y - 0.5f) * Size.y));

        public Vector3 Normal => transform.up;

        /// <summary>Surface-space direction (metres) of a world-space vector.</summary>
        public Vector2 WorldDirToSurface(Vector3 dir)
        {
            var l = transform.InverseTransformDirection(dir);
            return new Vector2(l.x, l.z);
        }

        /// <summary>
        /// Sweep a brush from <paramref name="a"/> to <paramref name="b"/> (UV) over <paramref name="dt"/>.
        /// Returns the weighted amount of dirt removed (for audio / particles).
        /// </summary>
        public float Stroke(Vector2 a, Vector2 b, in Brush brush, float dt, float speed)
        {
            if (remain == null || finishT >= 0f) return 0f;
            var pa = new Vector2(a.x * Size.x, a.y * Size.y);
            var pb = new Vector2(b.x * Size.x, b.y * Size.y);
            var seg = pb - pa;
            float segLen = seg.magnitude;
            var dir = segLen > 1e-5f ? seg / segLen : Vector2.up;
            var perp = new Vector2(-dir.y, dir.x);

            float scrub = brush.ScrubRef > 0f
                ? Mathf.Max(brush.MinScrub, Mathf.Min(speed / brush.ScrubRef, 1.8f))
                : 1f;
            float reach = brush.Radius + 0.01f;
            Vector2 min = Vector2.Min(pa, pb) - Vector2.one * reach, max = Vector2.Max(pa, pb) + Vector2.one * reach;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(min.x / Size.x * mw)), x1 = Mathf.Min(mw - 1, Mathf.CeilToInt(max.x / Size.x * mw));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(min.y / Size.y * mh)), y1 = Mathf.Min(mh - 1, Mathf.CeilToInt(max.y / Size.y * mh));
            if (x0 > x1 || y0 > y1) return 0f;

            // Nap direction: forward along the surface's long axis combs light, backward dark.
            bool napLight = Mathf.Abs(dir.y) >= Mathf.Abs(dir.x) ? dir.y >= 0 : dir.x >= 0;
            byte napTarget = (byte)(napLight ? 205 : 50);

            float removed = 0f;
            float invW = Size.x / mw, invH = Size.y / mh;
            for (int y = y0; y <= y1; y++)
            {
                float qy = (y + 0.5f) * invH;
                for (int x = x0; x <= x1; x++)
                {
                    var q = new Vector2((x + 0.5f) * invW, qy);
                    float f;
                    if (brush.Shape == BrushShape.Round)
                    {
                        var rel = q - pa;
                        float t = segLen > 1e-5f ? Mathf.Clamp(Vector2.Dot(rel, dir), 0f, segLen) : 0f;
                        float dist = (rel - dir * t).magnitude / brush.Radius;
                        if (dist >= 1f) continue;
                        float k = 1f - dist * dist;
                        f = k * k;
                    }
                    else
                    {
                        // Blade swept along the stroke: full strength across, weak at the very ends,
                        // which leaves thin streak lines until the next overlapping pass.
                        var rel = q - pa;
                        float along = Vector2.Dot(rel, dir), across = Mathf.Abs(Vector2.Dot(rel, perp));
                        if (along < -0.025f || along > segLen + 0.025f || across > brush.Radius) continue;
                        float edge = across / brush.Radius;
                        f = edge > 0.9f ? 0.18f : 1f;
                    }

                    int i = y * mw + x;
                    int mi = i * 4;
                    float before = remain[i];
                    float rate = brush.Rate * dt * f * scrub;
                    if (brush.DryFactor > 0f)
                    {
                        float wet = mask[mi + 2] / 255f;
                        rate *= Mathf.Lerp(brush.DryFactor, 1f, Mathf.Clamp01(wet * 2.5f));
                    }
                    if (before > 0f && rate > 0f)
                    {
                        float after = Mathf.Max(0f, before - rate / tough[i]);
                        if (after < 0.02f) after = 0f;
                        remain[i] = after;
                        float dw = weight[i] * (before - after);
                        remainingWeight -= dw;
                        removed += dw;
                        mask[mi] = (byte)(after * 255f + 0.5f);
                    }
                    if (brush.Nap && f > 0.25f)
                        mask[mi + 1] = (byte)Mathf.Lerp(mask[mi + 1], napTarget, Mathf.Clamp01(f * dt * 14f));
                    if (brush.Wet > 0f)
                    {
                        byte w = (byte)Mathf.Max(mask[mi + 2], f * brush.Wet * 255f);
                        if (w > 0 && mask[mi + 2] == 0) wetTexels++;
                        mask[mi + 2] = w;
                    }
                    if (brush.ClearsFoam && mask[mi + 2] > 0)
                    {
                        mask[mi + 2] = (byte)(mask[mi + 2] * (1f - Mathf.Clamp01(f * 1.2f)));
                    }
                }
            }
            maskDirty = true;
            UpdateProgress();
            return removed;
        }

        /// <summary>Spray foam (squeegee) or cleaner (whiteboard ghost) in a soft disc.</summary>
        public void Spray(Vector2 uv, float radiusM, float amount)
        {
            if (mask == null) return;
            var c = new Vector2(uv.x * Size.x, uv.y * Size.y);
            int x0 = Mathf.Max(0, Mathf.FloorToInt((c.x - radiusM) / Size.x * mw)), x1 = Mathf.Min(mw - 1, Mathf.CeilToInt((c.x + radiusM) / Size.x * mw));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((c.y - radiusM) / Size.y * mh)), y1 = Mathf.Min(mh - 1, Mathf.CeilToInt((c.y + radiusM) / Size.y * mh));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var q = new Vector2((x + 0.5f) * Size.x / mw, (y + 0.5f) * Size.y / mh);
                float dist = (q - c).magnitude / radiusM;
                if (dist >= 1f) continue;
                // Speckled edge so the foam reads as a spray pattern, not a disc.
                float speckle = dist < 0.7f || Noise.Hash01(x, y, Time.frameCount) > (dist - 0.7f) * 3f ? 1f : 0.2f;
                float add = amount * (1f - dist * dist) * speckle * 255f;
                int mi = (y * mw + x) * 4 + 2;
                if (mask[mi] == 0 && add >= 1f) wetTexels++;
                mask[mi] = (byte)Mathf.Min(255f, mask[mi] + add);
            }
            maskDirty = true;
        }

        /// <summary>Scrub the ghost layer (only where sprayed and the dirt above it is gone).</summary>
        public float ScrubGhost(Vector2 uv, float radiusM, float rate)
        {
            if (ghostW == null || !Spec.GhostScrubbable) return 0f;
            var c = new Vector2(uv.x * Size.x, uv.y * Size.y);
            int x0 = Mathf.Max(0, Mathf.FloorToInt((c.x - radiusM) / Size.x * mw)), x1 = Mathf.Min(mw - 1, Mathf.CeilToInt((c.x + radiusM) / Size.x * mw));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((c.y - radiusM) / Size.y * mh)), y1 = Mathf.Min(mh - 1, Mathf.CeilToInt((c.y + radiusM) / Size.y * mh));
            float removed = 0;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var q = new Vector2((x + 0.5f) * Size.x / mw, (y + 0.5f) * Size.y / mh);
                float dist = (q - c).magnitude / radiusM;
                if (dist >= 1f) continue;
                int i = y * mw + x, mi = i * 4;
                if (remain[i] > 0.1f || mask[mi + 2] < 20) continue;
                float before = mask[mi + 3] / 255f;
                float after = Mathf.Max(0f, before - rate * (1f - dist * dist));
                mask[mi + 3] = (byte)(after * 255f);
                removed += ghostW[i] * (before - after);
            }
            maskDirty = true;
            return removed;
        }

        void UpdateProgress()
        {
            Completion = RevealOnly ? GhostReveal : totalWeight > 0f ? Mathf.Clamp01(1f - remainingWeight / totalWeight) : 1f;
            if (!Done && Completion >= AutoFinishAt)
            {
                Done = true;
                finishT = 0f;
                Completed?.Invoke(this);
            }
            if (ghostW != null)
            {
                float rev = 0, rem = 0;
                for (int i = 0; i < ghostW.Length; i++)
                {
                    if (ghostW[i] <= 0f) continue;
                    rev += ghostW[i] * (1f - remain[i]);
                    rem += ghostW[i] * (mask[i * 4 + 3] / 255f);
                }
                GhostReveal = rev / ghostWeightTotal;
                GhostRemaining = rem / ghostWeightTotal;
                if (RevealOnly)
                {
                    Completion = Mathf.Clamp01(GhostReveal / 0.7f);
                    if (!Done && GhostReveal >= 0.7f) { Done = true; Completed?.Invoke(this); }
                }
                if (!GhostWasRevealed && GhostReveal > 0.45f)
                {
                    GhostWasRevealed = true;
                    GhostRevealed?.Invoke(this);
                }
                if (!GhostWasScrubbed && GhostRemaining < 0.12f)
                {
                    GhostWasScrubbed = true;
                    for (int i = 0; i < ghostW.Length; i++) mask[i * 4 + 3] = 0;
                    maskDirty = true;
                    GhostScrubbed?.Invoke(this);
                }
            }
        }

        /// <summary>Dirt remaining (0..1) at a UV, from the CPU mask.</summary>
        public float RemainingAt(Vector2 uv)
        {
            if (remain == null) return 1f;
            int x = Mathf.Clamp((int)(uv.x * mw), 0, mw - 1), y = Mathf.Clamp((int)(uv.y * mh), 0, mh - 1);
            return remain[y * mw + x];
        }

        /// <summary>
        /// World points at the centres of the dirtiest regions still left (up to
        /// <paramref name="max"/>, dirtiest first), from a coarse grid over the CPU mask.
        /// </summary>
        public System.Collections.Generic.List<Vector3> DirtiestSpots(int max)
        {
            var spots = new System.Collections.Generic.List<Vector3>();
            if (remain == null || weight == null) { spots.Add(transform.position); return spots; }
            int gx = Mathf.Clamp(Mathf.RoundToInt(Size.x / 0.6f), 1, 12), gy = Mathf.Clamp(Mathf.RoundToInt(Size.y / 0.6f), 1, 12);
            var cells = new float[gx * gy];
            float total = 0;
            for (int y = 0; y < mh; y++)
            for (int x = 0; x < mw; x++)
            {
                int i = y * mw + x;
                float v = remain[i] * weight[i];
                cells[Mathf.Min(gy - 1, y * gy / mh) * gx + Mathf.Min(gx - 1, x * gx / mw)] += v;
                total += v;
            }
            var order = System.Linq.Enumerable.ToList(System.Linq.Enumerable.OrderByDescending(System.Linq.Enumerable.Range(0, cells.Length), i => cells[i]));
            foreach (int c in order)
            {
                // Skip cells holding only a sliver of what's left.
                if (spots.Count >= max || cells[c] <= 0f || (spots.Count > 0 && cells[c] < total * 0.08f)) break;
                spots.Add(UvToWorld(new Vector2((c % gx + 0.5f) / gx, (c / gx + 0.5f) / gy)) + Normal * 0.03f);
            }
            if (spots.Count == 0) spots.Add(transform.position);
            return spots;
        }

        /// <summary>Fraction of the (optional) ghost texture's area that is currently foamed.</summary>
        public float GhostFoamCoverage()
        {
            if (ghostW == null) return 0f;
            float s = 0;
            for (int i = 0; i < ghostW.Length; i++) if (ghostW[i] > 0) s += ghostW[i] * (mask[i * 4 + 2] / 255f);
            return s / ghostWeightTotal;
        }

        /// <summary>Instantly clean (debug / autopilot fallback).</summary>
        public void ForceComplete()
        {
            if (remain == null) return;
            for (int i = 0; i < remain.Length; i++) { remain[i] = 0; mask[i * 4] = 0; }
            remainingWeight = 0;
            maskDirty = true;
            UpdateProgress();
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (finishT >= 0f)
            {
                finishT += dt;
                float k = Mathf.Clamp01(finishT / 0.35f);
                material.SetFloat(FadeId, 1f - k * k);
                if (k >= 1f)
                {
                    for (int i = 0; i < remain.Length; i++) { remain[i] = 0; mask[i * 4] = 0; }
                    remainingWeight = 0;
                    Completion = 1f;
                    material.SetFloat(FadeId, 1f);
                    maskDirty = true;
                    finishT = -1f;
                    gleamT = 0f;
                    UpdateProgress();
                }
            }
            if (gleamT >= 0f)
            {
                gleamT += dt / 0.8f;
                material.SetFloat(GleamId, gleamT);
                if (gleamT > 1.25f) { gleamT = -1f; material.SetFloat(GleamId, -1f); }
            }
            if (wetTexels > 0)
            {
                // Wetness and foam dry out (mop ~6 s, foam on glass ~14 s).
                float speed = Tool == ToolKind.Squeegee ? 18f : 42f;
                int dec = Mathf.Max(1, Mathf.RoundToInt(speed * dt));
                int count = 0;
                for (int i = 2; i < mask.Length; i += 4)
                {
                    byte v = mask[i];
                    if (v == 0) continue;
                    v = (byte)Mathf.Max(0, v - dec);
                    mask[i] = v;
                    if (v > 0) count++;
                }
                wetTexels = count;
                maskDirty = true;
            }
            if (maskDirty)
            {
                maskTex.SetPixelData(mask, 0);
                maskTex.Apply(false, false);
                maskDirty = false;
            }
        }
    }
}
