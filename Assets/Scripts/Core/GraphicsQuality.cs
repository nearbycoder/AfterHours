using System.Collections.Generic;
using System.Reflection;
using Object = UnityEngine.Object;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AfterHours
{
    /// <summary>
    /// What one Graphics Fidelity step renders (see <see cref="GraphicsQuality.StepFor"/>). Pure data,
    /// so the table can be tested; sizes are in pixels.
    /// </summary>
    public readonly struct FidelityStep
    {
        public readonly int Msaa, MainShadowRes, ShadowAtlas, LightShadowTile, Cascades;
        public readonly float ShadowDistance;
        /// <summary>Room lights' shadows: 0 none, 1 hard, 2 as authored (soft).</summary>
        public readonly int RoomShadows;
        /// <summary>Desk lamps cast soft shadows (they cast none below Ultra).</summary>
        public readonly bool LampShadows;
        /// <summary>SSAO: 0 off, 1 as authored (8 samples), 2 the most samples (12) and normals.</summary>
        public readonly int Ssao;
        /// <summary>Anisotropic filtering: 0 off, -1 as the project sets it (forced on, at least 9×), else forced to at least this level.</summary>
        public readonly int Aniso;
        /// <summary>Mip levels dropped from textures (1 halves them).</summary>
        public readonly int TextureMipLimit;
        /// <summary>Per-room reflection probe resolution (0 none).</summary>
        public readonly int Reflections;
        /// <summary>How many particles a burst emits, against High.</summary>
        public readonly float Particles;
        public readonly bool BloomHighQuality, BloomQuarter, FilmGrain;
        public readonly AntialiasingMode PostAa;
        public readonly AntialiasingQuality PostAaQuality;

        public FidelityStep(int msaa, int mainShadowRes, int shadowAtlas, int lightShadowTile, int cascades, float shadowDistance,
            int roomShadows, bool lampShadows, int ssao, int aniso, int textureMipLimit, int reflections, float particles,
            bool bloomHighQuality, bool bloomQuarter, bool filmGrain, AntialiasingMode postAa, AntialiasingQuality postAaQuality)
        {
            Msaa = msaa; MainShadowRes = mainShadowRes; ShadowAtlas = shadowAtlas; LightShadowTile = lightShadowTile;
            Cascades = cascades; ShadowDistance = shadowDistance; RoomShadows = roomShadows; LampShadows = lampShadows;
            Ssao = ssao; Aniso = aniso; TextureMipLimit = textureMipLimit; Reflections = reflections; Particles = particles;
            BloomHighQuality = bloomHighQuality; BloomQuarter = bloomQuarter; FilmGrain = filmGrain;
            PostAa = postAa; PostAaQuality = postAaQuality;
        }
    }

    /// <summary>
    /// Graphics Fidelity (<see cref="Settings.Quality"/>): Low, Medium, High and Ultra, applied at
    /// runtime to the URP asset, the renderer's SSAO, every light's shadows, the cameras'
    /// anti-aliasing, texture filtering, the rooms' reflection probes and the particles. High is
    /// the look the game was built with and the default; Low keeps rooms lit and grime readable on
    /// weak GPUs; Ultra goes past High where the engine allows.
    /// </summary>
    public static class GraphicsQuality
    {
        public static readonly string[] Names = { "Low", "Medium", "High", "Ultra" };
        public const int Lowest = 0, Default = 2, Highest = 3;

        /// <summary>One line under the slider: what the step does.</summary>
        public static readonly string[] Blurbs =
        {
            "Fastest: no ambient occlusion or room-light shadows",
            "Hard shadows, ambient occlusion, 2× MSAA",
            "Soft shadows, ambient occlusion, 4× MSAA",
            "Room reflections, lamp shadows, sharper shadows and AO",
        };

        /// <summary>
        /// The table. The asset's authored values (what High has always used) are the defaults:
        /// 4× MSAA, a 2048 main-light shadow map, a 4096 atlas of 1024 tiles for the room lights,
        /// two cascades to 28 m.
        /// </summary>
        public static FidelityStep StepFor(int level, int msaa = 4, int mainRes = 2048, int atlas = 4096, int tile = 1024, int cascades = 2, float distance = 28f) =>
            Mathf.Clamp(level, Lowest, Highest) switch
            {
                0 => new FidelityStep(1, Mathf.Min(mainRes, 1024), Mathf.Min(atlas, 1024), Mathf.Min(tile, 512), 1, Mathf.Min(distance, 18f),
                    0, false, 0, 0, 1, 0, 0.5f, false, true, false, AntialiasingMode.FastApproximateAntialiasing, AntialiasingQuality.Medium),
                1 => new FidelityStep(Mathf.Min(msaa, 2), Mathf.Min(mainRes, 2048), Mathf.Min(atlas, 2048), tile, cascades, distance,
                    1, false, 1, -1, 0, 0, 0.75f, false, false, true, AntialiasingMode.SubpixelMorphologicalAntiAliasing, AntialiasingQuality.Medium),
                2 => new FidelityStep(msaa, mainRes, atlas, tile, cascades, distance,
                    2, false, 1, -1, 0, 0, 1f, true, false, true, AntialiasingMode.SubpixelMorphologicalAntiAliasing, AntialiasingQuality.High),
                _ => new FidelityStep(msaa, Mathf.Max(mainRes, 4096), Mathf.Max(atlas, 8192), Mathf.Max(tile, 2048), 4, Mathf.Max(distance, 40f),
                    2, true, 2, 16, 0, 256, 1.5f, true, false, true, AntialiasingMode.SubpixelMorphologicalAntiAliasing, AntialiasingQuality.High),
            };

        // The asset's authored values, captured before the first change.
        static bool captured;
        static int msaa, mainRes, addRes, tierHigh, cascades;
        static float shadowDistance;
        static AnisotropicFiltering aniso;
        /// <summary>The project's own anisotropic filtering (what High uses).</summary>
        public static AnisotropicFiltering AuthoredAniso => captured ? aniso : QualitySettings.anisotropicFiltering;
        static readonly Dictionary<Light, LightShadows> authored = new();

        public static int Level => Mathf.Clamp(Settings.Current.Quality, Lowest, Highest);

        /// <summary>The step in use, from the authored values.</summary>
        public static FidelityStep Current => StepFor(Level, msaa > 0 ? msaa : 4, mainRes > 0 ? mainRes : 2048, addRes > 0 ? addRes : 4096,
            tierHigh > 0 ? tierHigh : 1024, cascades > 0 ? cascades : 2, shadowDistance > 0 ? shadowDistance : 28f);

        static readonly FieldInfo TierHighField = typeof(UniversalRenderPipelineAsset).GetField("m_AdditionalLightsShadowResolutionTierHigh", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void Apply()
        {
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset urp) return;
            if (!captured)
            {
                captured = true;
                msaa = urp.msaaSampleCount;
                mainRes = urp.mainLightShadowmapResolution;
                addRes = urp.additionalLightsShadowmapResolution;
                tierHigh = urp.additionalLightsShadowResolutionTierHigh;
                cascades = urp.shadowCascadeCount;
                shadowDistance = urp.shadowDistance;
                aniso = QualitySettings.anisotropicFiltering;
            }
            var st = Current;
            urp.msaaSampleCount = st.Msaa;
            urp.mainLightShadowmapResolution = st.MainShadowRes;
            urp.additionalLightsShadowmapResolution = st.ShadowAtlas;
            TierHighField?.SetValue(urp, st.LightShadowTile); // the room lights use the High tier
            urp.shadowCascadeCount = st.Cascades;
            urp.shadowDistance = st.ShadowDistance;

            foreach (var data in urp.rendererDataList)
            {
                if (data == null) continue;
                foreach (var f in data.rendererFeatures)
                    if (f != null && f.name.Contains("AmbientOcclusion"))
                    {
                        f.SetActive(st.Ssao > 0);
                        SetSsao(f, st.Ssao >= 2);
                    }
            }

            QualitySettings.anisotropicFiltering = st.Aniso == 0 ? AnisotropicFiltering.Disable : st.Aniso > 0 ? AnisotropicFiltering.ForceEnable : aniso;
            // Unity's defaults (the project forces anisotropic filtering on: at least 9×, at most 16×).
            if (st.Aniso > 0) Texture.SetGlobalAnisotropicFilteringLimits(st.Aniso, 16);
            else Texture.SetGlobalAnisotropicFilteringLimits(9, 16);
            if (QualitySettings.globalTextureMipmapLimit != st.TextureMipLimit) QualitySettings.globalTextureMipmapLimit = st.TextureMipLimit;
            QualitySettings.realtimeReflectionProbes = st.Reflections > 0;

            Fx.Density = st.Particles;
            PostFx.Instance?.SetQuality(st);
            ApplyLights();
            RoomProbes.Apply(st.Reflections);
        }

        // SSAO's settings are internal to URP; the renderer feature reads them every frame.
        static FieldInfo ssaoSettingsField;
        static object ssaoAuthored;

        static void SetSsao(ScriptableRendererFeature f, bool most)
        {
            ssaoSettingsField ??= f.GetType().GetField("m_Settings", BindingFlags.NonPublic | BindingFlags.Instance);
            var settings = ssaoSettingsField?.GetValue(f);
            if (settings == null) return;
            var t = settings.GetType();
            var samples = t.GetField("Samples", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            var normals = t.GetField("NormalSamples", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (samples == null || normals == null) return;
            ssaoAuthored ??= (samples.GetValue(settings), normals.GetValue(settings));
            var (s0, n0) = ((object, object))ssaoAuthored;
            // AOSampleOption: High (12 samples) is 0. NormalQuality: High is 2.
            samples.SetValue(settings, most ? System.Enum.ToObject(samples.FieldType, 0) : s0);
            normals.SetValue(settings, most ? System.Enum.ToObject(normals.FieldType, 2) : n0);
        }

        /// <summary>SSAO's sample setting now (High, Medium or Low), or "off"; automation reads it back.</summary>
        public static string SsaoNow()
        {
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset urp) return "?";
            foreach (var data in urp.rendererDataList)
            {
                if (data == null) continue;
                foreach (var f in data.rendererFeatures)
                    if (f != null && f.name.Contains("AmbientOcclusion"))
                    {
                        if (!f.isActive) return "off";
                        var settings = f.GetType().GetField("m_Settings", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(f);
                        return settings?.GetType().GetField("Samples", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)?.GetValue(settings)?.ToString() ?? "?";
                    }
            }
            return "none";
        }

        /// <summary>A desk lamp (they're authored without shadows; Ultra gives them soft ones).</summary>
        static bool IsLamp(Light l) => l.type == LightType.Point && l.name.StartsWith("Light_lamp");

        /// <summary>Shadows per light: authored on High, plus the lamps on Ultra; hard on Medium; the moon only on Low.</summary>
        public static void ApplyLights()
        {
            var st = Current;
            var dead = new List<Light>();
            foreach (var k in authored.Keys) if (!k) dead.Add(k);
            foreach (var k in dead) authored.Remove(k);
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!authored.TryGetValue(l, out var orig)) authored[l] = orig = l.shadows;
                if (orig == LightShadows.None)
                {
                    if (IsLamp(l)) l.shadows = st.LampShadows ? LightShadows.Soft : LightShadows.None;
                    continue;
                }
                l.shadows = l.type == LightType.Directional ? (st.RoomShadows >= 2 ? orig : LightShadows.Hard)
                    : st.RoomShadows == 0 ? LightShadows.None : st.RoomShadows == 1 ? LightShadows.Hard : orig;
            }
        }

        /// <summary>What the engine reports now, for the logs and the AutoPilot.</summary>
        public static string Describe()
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) return "no URP asset";
            int shadowed = 0, soft = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.shadows != LightShadows.None) shadowed++;
                if (l.shadows == LightShadows.Soft) soft++;
            }
            return $"{Names[Level]}: MSAA {urp.msaaSampleCount}x, main shadows {urp.mainLightShadowmapResolution} x{urp.shadowCascadeCount} to {urp.shadowDistance:F0} m, " +
                   $"light shadows {urp.additionalLightsShadowResolutionTierHigh} in {urp.additionalLightsShadowmapResolution}, shadowed lights {shadowed} ({soft} soft), " +
                   $"SSAO {SsaoNow()}, aniso {QualitySettings.anisotropicFiltering}, texture mip limit {QualitySettings.globalTextureMipmapLimit}, " +
                   $"reflection probes {RoomProbes.Count} at {RoomProbes.Resolution}, particles {Fx.Density:0.##}x, {PostFx.Instance?.QualityNow ?? "no post"}";
        }
    }
}
