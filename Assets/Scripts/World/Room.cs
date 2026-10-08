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
        float stutterT = -1f, stutterFor;

        /// <summary>How far the stutter dims the lights with Reduce flashing on (a fraction of full).</summary>
        public const float CalmDip = 0.5f;
        const float CalmDown = 0.35f, CalmUp = 0.6f;

        /// <summary>What the lights give out now, 0 to about 1: the switch's level times any stutter.</summary>
        public float Output { get; private set; }
        public bool Stuttering => stutterT >= 0f;

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

        /// <summary>
        /// A power stutter (Night 7's thunder): the lights drop out for <paramref name="seconds"/>, or
        /// with Reduce flashing dim smoothly to about half and come back. The room stays switched on
        /// (<see cref="LightsOn"/> doesn't change), so tasks and switches don't see it.
        /// </summary>
        public void Stutter(float seconds)
        {
            if (!LightsOn) return;
            stutterT = 0f;
            stutterFor = seconds;
        }

        /// <summary>The stutter's gain on the lights <paramref name="t"/> seconds in (1 is untouched).</summary>
        public static float StutterGain(float t, float seconds, bool calm)
        {
            if (!calm) return t < seconds ? 0f : 1f;
            float hold = Mathf.Max(seconds, CalmDown);
            if (t < CalmDown) return Mathf.Lerp(1f, CalmDip, Mathf.SmoothStep(0f, 1f, t / CalmDown));
            if (t < hold) return CalmDip;
            return Mathf.Lerp(CalmDip, 1f, Mathf.SmoothStep(0f, 1f, (t - hold) / CalmUp));
        }

        /// <summary>How long a stutter of <paramref name="seconds"/> lasts in all.</summary>
        public static float StutterLength(float seconds, bool calm) => calm ? Mathf.Max(seconds, CalmDown) + CalmUp : seconds;

        void Update()
        {
            float dt = Time.deltaTime;
            float gain = 1f;
            bool stutter = stutterT >= 0f;
            if (stutter)
            {
                stutterT += dt;
                bool calm = Settings.Current.ReduceFlashing;
                if (stutterT >= StutterLength(stutterFor, calm)) stutterT = -1f;
                else gain = StutterGain(stutterT, stutterFor, calm);
            }
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
                Apply(level * gain);
                return;
            }
            if (!Mathf.Approximately(level, target))
            {
                level = Mathf.MoveTowards(level, target, dt * 6f);
                Apply(level * gain);
            }
            else if (stutter) Apply(level * gain); // the last stutter frame puts the lights back at full
        }

        void Apply(float v)
        {
            Output = v;
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
