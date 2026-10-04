using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    /// <summary>
    /// A room: its volume, light panels and lights. Lights switch on with a fluorescent flicker;
    /// the room tracks whether the player is inside (for ambience, captions and tasks).
    /// </summary>
    public class Room : MonoBehaviour
    {
        public string Id;
        public string DisplayName;
        public Bounds Bounds;
        public readonly List<Light> Lights = new();
        public readonly List<Renderer> Panels = new();
        public readonly List<GrimeSurface> Surfaces = new();
        public bool LightsOn { get; private set; }
        public bool Locked;
        public LoopVoice Hum;

        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        readonly List<Material> panelMats = new();
        readonly List<float> lightIntensity = new();
        Color panelGlow = new Color(0.95f, 0.97f, 1f) * 2.4f;
        float level, target;
        float flickerT = -1f;

        public static readonly Dictionary<string, Room> All = new();

        void OnEnable() { if (!string.IsNullOrEmpty(Id)) All[Id] = this; }
        void OnDisable() { if (All.TryGetValue(Id ?? "", out var r) && r == this) All.Remove(Id); }

        public void Register()
        {
            All[Id] = this;
            foreach (var r in Panels)
                foreach (var m in r.materials)
                    if (m.IsKeywordEnabled("_EMISSION")) panelMats.Add(m);
            foreach (var l in Lights) lightIntensity.Add(l.intensity);
            Apply(0f);
        }

        public bool Contains(Vector3 p) => Bounds.Contains(p);

        public void SetLights(bool on, bool instant = false)
        {
            if (on == LightsOn && !instant) return;
            LightsOn = on;
            target = on ? 1f : 0f;
            if (instant) { level = target; flickerT = -1f; Apply(level); }
            else if (on) flickerT = 0f;
            if (Hum) Hum.TargetVolume = on ? 0.18f : 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (flickerT >= 0f)
            {
                // Classic tube start: two or three stutters, then settle with a slight overshoot.
                flickerT += dt;
                float t = flickerT;
                float v = t < 0.08f ? 0.6f : t < 0.14f ? 0.05f : t < 0.2f ? 0.8f : t < 0.3f ? 0.15f : t < 0.36f ? 0.9f : t < 0.42f ? 0.3f : Mathf.Min(1.08f, 0.55f + (t - 0.42f) * 2.5f);
                if (t > 0.65f) { v = Mathf.Lerp(1.08f, 1f, (t - 0.65f) * 4f); }
                if (t > 0.9f) { flickerT = -1f; v = 1f; }
                if (Settings.Current.ReduceFlashing) v = Mathf.Clamp01(t / 0.5f);
                level = v;
                Apply(level);
                return;
            }
            if (!Mathf.Approximately(level, target))
            {
                level = Mathf.MoveTowards(level, target, dt * 6f);
                Apply(level);
            }
        }

        void Apply(float v)
        {
            for (int i = 0; i < Lights.Count; i++)
            {
                Lights[i].intensity = lightIntensity[i] * v;
                Lights[i].enabled = v > 0.01f;
            }
            foreach (var m in panelMats) m.SetColor(EmissionColor, panelGlow * Mathf.Max(0.02f, v));
        }

        public static Room At(Vector3 p)
        {
            foreach (var r in All.Values) if (r.Contains(p)) return r;
            return null;
        }
    }
}
