using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AfterHours
{
    /// <summary>
    /// Global look: ambient light, fog and the post-processing stack. Exposes a few knobs that
    /// gameplay animates (inspect depth of field, discovery pulse, power flicker).
    /// </summary>
    public class PostFx : MonoBehaviour
    {
        public static PostFx Instance { get; private set; }

        Volume volume;
        VolumeProfile profile;
        Bloom bloom;
        Vignette vignette;
        ColorAdjustments color;
        DepthOfField dof;
        ChromaticAberration chroma;
        FilmGrain grain;
        ShadowsMidtonesHighlights smh;

        float dofTarget, dofWeight;
        float pulse, flicker, exposureTarget, exposure;

        public static PostFx Create()
        {
            var go = new GameObject("PostFx");
            var p = go.AddComponent<PostFx>();
            p.Build();
            Instance = p;
            return p;
        }

        void Build()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.2f, 0.25f, 0.36f);
            RenderSettings.ambientEquatorColor = new Color(0.13f, 0.15f, 0.21f);
            RenderSettings.ambientGroundColor = new Color(0.07f, 0.07f, 0.09f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.004f;
            RenderSettings.fogColor = new Color(0.05f, 0.07f, 0.11f);
            RenderSettings.reflectionIntensity = 0.6f;

            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.62f);
            bloom.tint.Override(new Color(1f, 0.95f, 0.9f));
            bloom.highQualityFiltering.Override(true);

            color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.6f);
            color.contrast.Override(14f);
            color.saturation.Override(6f);

            smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.86f, 0.94f, 1.12f, -0.02f));
            smh.midtones.Override(new Vector4(1f, 0.99f, 0.98f, 0f));
            smh.highlights.Override(new Vector4(1.08f, 1.0f, 0.9f, 0f));

            vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.02f, 0.03f, 0.06f));

            grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.16f);
            grain.response.Override(0.75f);

            chroma = profile.Add<ChromaticAberration>(true);
            chroma.intensity.Override(0f);

            dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(0.45f);
            dof.focalLength.Override(70f);
            dof.aperture.Override(2.8f);
            dof.active = false;

            exposure = exposureTarget = 0.6f;
        }

        static readonly System.Collections.Generic.List<Camera> cameras = new();

        public static void ConfigureCamera(Camera cam)
        {
            if (!cameras.Contains(cam)) cameras.Add(cam);
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        /// <summary>Graphics preset (0 low … 2 high): bloom filtering and the camera's anti-aliasing.</summary>
        public void SetQuality(int q)
        {
            bloom.highQualityFiltering.Override(q == 2);
            cameras.RemoveAll(c => !c);
            foreach (var cam in cameras)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                data.antialiasing = q == 0 ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = q == 2 ? AntialiasingQuality.High : AntialiasingQuality.Medium;
            }
        }

        /// <summary>Blur the background (inspect mode).</summary>
        public void SetInspect(bool on) => dofTarget = on ? 1f : 0f;

        /// <summary>Brief vignette + exposure kick for discoveries.</summary>
        public void Pulse(float amount = 1f) => pulse = Mathf.Max(pulse, amount);

        /// <summary>Power flicker (storm night).</summary>
        public void Flicker(float amount) => flicker = Mathf.Max(flicker, amount);

        public void SetExposure(float ev) => exposureTarget = ev;

        void Update()
        {
            float dt = GameTime.UnscaledDelta;
            dofWeight = Mathf.MoveTowards(dofWeight, dofTarget, dt * 4f);
            dof.active = dofWeight > 0.01f;
            dof.aperture.Override(Mathf.Lerp(16f, 2.2f, dofWeight));
            pulse = Mathf.MoveTowards(pulse, 0f, dt * 0.9f);
            flicker = Mathf.MoveTowards(flicker, 0f, dt * 2.5f);
            exposure = Mathf.Lerp(exposure, exposureTarget, 1f - Mathf.Exp(-dt * 3f));
            bool reduce = Settings.Current.ReduceFlashing;
            vignette.intensity.Override(0.3f + pulse * 0.18f + dofWeight * 0.12f);
            chroma.intensity.Override(reduce ? 0f : flicker * 0.6f + pulse * 0.15f);
            float flick = reduce ? 0f : flicker * (Mathf.PerlinNoise(Time.time * 30f, 0.3f) - 0.5f) * 1.6f;
            color.postExposure.Override(exposure + pulse * 0.25f + flick);
        }
    }
}
