using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    public enum FxKind { Foam, Dust, Sparkle, Drops, Splash, Confetti, Bubbles, Puff, Paper, Glint }

    /// <summary>Pooled particle systems for every kind of feedback burst.</summary>
    public class Fx : MonoBehaviour
    {
        static Fx instance;
        readonly Dictionary<FxKind, ParticleSystem> systems = new();

        static Fx Instance
        {
            get
            {
                if (instance) return instance;
                var go = new GameObject("Fx");
                instance = go.AddComponent<Fx>();
                instance.Build();
                return instance;
            }
        }

        static Material Mat(float shape, bool additive, float intensity = 1f)
        {
            var m = new Material(Res.Material("AH_Fx"));
            m.SetFloat("_Shape", shape);
            m.SetFloat("_Intensity", intensity);
            m.SetFloat("_SrcBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            return m;
        }

        void Build()
        {
            Make(FxKind.Foam, Mat(0, false), size: (0.008f, 0.022f), life: (0.35f, 0.7f), gravity: 0.6f, drag: 2.5f, max: 600);
            Make(FxKind.Dust, Mat(0, false), size: (0.03f, 0.08f), life: (0.5f, 1.1f), gravity: -0.05f, drag: 3f, max: 400, grow: 2.2f);
            Make(FxKind.Sparkle, Mat(1, true, 2.4f), size: (0.025f, 0.07f), life: (0.45f, 0.9f), gravity: -0.15f, drag: 2f, max: 300, twinkle: true);
            Make(FxKind.Drops, Mat(0, false), size: (0.006f, 0.014f), life: (0.5f, 1.0f), gravity: 1.4f, drag: 0.4f, max: 400);
            Make(FxKind.Splash, Mat(0, false), size: (0.012f, 0.03f), life: (0.3f, 0.6f), gravity: 1.6f, drag: 1f, max: 300);
            Make(FxKind.Confetti, Mat(2, false), size: (0.01f, 0.018f), life: (0.6f, 1.2f), gravity: 0.5f, drag: 2.5f, max: 300, spin: true);
            Make(FxKind.Bubbles, Mat(4, false), size: (0.01f, 0.03f), life: (0.6f, 1.4f), gravity: -0.03f, drag: 3f, max: 300);
            Make(FxKind.Puff, Mat(0, false), size: (0.05f, 0.12f), life: (0.4f, 0.8f), gravity: -0.02f, drag: 4f, max: 200, grow: 1.8f);
            Make(FxKind.Paper, Mat(2, false), size: (0.012f, 0.028f), life: (0.5f, 1.0f), gravity: 0.9f, drag: 1.5f, max: 200, spin: true);
            Make(FxKind.Glint, Mat(1, true, 3f), size: (0.05f, 0.12f), life: (0.25f, 0.45f), gravity: 0f, drag: 0f, max: 100, twinkle: true);
        }

        void Make(FxKind kind, Material mat, (float, float) size, (float, float) life, float gravity, float drag, int max,
            float grow = 1f, bool twinkle = false, bool spin = false)
        {
            var go = new GameObject("fx_" + kind);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.Item1, life.Item2);
            main.startSize = new ParticleSystem.MinMaxCurve(size.Item1, size.Item2);
            main.startSpeed = 0f;
            main.gravityModifier = gravity;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = drag > 0f;
            limit.drag = drag;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = true;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, grow > 1f
                ? AnimationCurve.EaseInOut(0, 0.6f, 1, grow)
                : new AnimationCurve(new Keyframe(0, 0.4f), new Keyframe(0.15f, 1f), new Keyframe(1, 0.2f)));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                twinkle
                    ? new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.12f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0, 1) }
                    : new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.85f, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = g;
            if (spin)
            {
                var rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = -10;
            ps.Play();
            systems[kind] = ps;
        }

        /// <summary>Emit <paramref name="count"/> particles at <paramref name="pos"/>, flung along <paramref name="dir"/>.</summary>
        public static void Burst(FxKind kind, Vector3 pos, Vector3 dir, int count, Color color, float speed = 1f, float spread = 0.6f, float radius = 0f)
        {
            if (count <= 0) return;
            var ps = Instance.systems[kind];
            var ep = new ParticleSystem.EmitParams();
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.up;
            for (int i = 0; i < count; i++)
            {
                var v = (dir + Random.insideUnitSphere * spread).normalized * speed * Random.Range(0.4f, 1f);
                ep.position = pos + (radius > 0f ? Random.insideUnitSphere * radius : Vector3.zero);
                ep.velocity = v;
                var c = color;
                if (kind == FxKind.Confetti) c = Palette.Confetti[Random.Range(0, Palette.Confetti.Length)];
                ep.startColor = c;
                ps.Emit(ep, 1);
            }
        }

        /// <summary>Sparkles scattered across a rectangle (surface completion).</summary>
        public static void SparkleRect(Vector3 center, Vector3 right, Vector3 forward, Vector2 size, Vector3 normal, int count)
        {
            var ps = Instance.systems[FxKind.Sparkle];
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                var p = center + right * (Random.Range(-0.5f, 0.5f) * size.x) + forward * (Random.Range(-0.5f, 0.5f) * size.y) + normal * 0.02f;
                ep.position = p;
                ep.velocity = normal * Random.Range(0.05f, 0.35f) + Random.insideUnitSphere * 0.08f;
                ep.startColor = Color.Lerp(new Color(1f, 0.95f, 0.8f), new Color(0.75f, 0.9f, 1f), Random.value);
                ep.startLifetime = Random.Range(0.5f, 1.1f);
                ps.Emit(ep, 1);
            }
        }
    }

    public static class Palette
    {
        public static readonly Color InkBlue = Hex("141C2B");
        public static readonly Color Night = Hex("1E2B40");
        public static readonly Color NightLight = Hex("2C4366");
        public static readonly Color CityTeal = Hex("3FA7B5");
        public static readonly Color Tungsten = Hex("FFB45E");
        public static readonly Color Fluoro = Hex("E4F2FF");
        public static readonly Color Monitor = Hex("5FE3FF");
        public static readonly Color EvidenceRed = Hex("D9483B");
        public static readonly Color Sticky = Hex("FFD54A");
        public static readonly Color Carpet = Hex("3B4A5E");
        public static readonly Color Oak = Hex("A0703F");
        public static readonly Color Coffee = Hex("4A2F1D");
        public static readonly Color[] Confetti =
        {
            Hex("FF5A6E"), Hex("FFC845"), Hex("4ED6B8"), Hex("5AA7FF"), Hex("C58CFF"), Hex("FF8C42"),
        };

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
