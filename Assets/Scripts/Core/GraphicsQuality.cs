using System.Collections.Generic;
using Object = UnityEngine.Object;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AfterHours
{
    /// <summary>
    /// The Low / Medium / High preset (<see cref="Settings.Quality"/>), applied to the URP asset,
    /// the renderer's SSAO, the camera's anti-aliasing and every light's shadows at runtime. High is
    /// the look the game was built with; Low keeps rooms lit and grime readable on weak GPUs.
    /// </summary>
    public static class GraphicsQuality
    {
        public static readonly string[] Names = { "Low", "Medium", "High" };

        // The asset's authored values, captured before the first change.
        static bool captured;
        static int msaa, mainRes, addRes;
        static float shadowDistance;
        static readonly Dictionary<Light, LightShadows> authored = new();

        public static int Level => Mathf.Clamp(Settings.Current.Quality, 0, 2);

        public static void Apply()
        {
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset urp) return;
            if (!captured)
            {
                captured = true;
                msaa = urp.msaaSampleCount;
                mainRes = urp.mainLightShadowmapResolution;
                addRes = urp.additionalLightsShadowmapResolution;
                shadowDistance = urp.shadowDistance;
            }
            int q = Level;
            urp.msaaSampleCount = q == 2 ? msaa : q == 1 ? Mathf.Min(msaa, 2) : 1;
            urp.mainLightShadowmapResolution = q == 2 ? mainRes : q == 1 ? Mathf.Min(mainRes, 2048) : Mathf.Min(mainRes, 1024);
            urp.additionalLightsShadowmapResolution = q == 2 ? addRes : q == 1 ? Mathf.Min(addRes, 2048) : Mathf.Min(addRes, 1024);
            urp.shadowDistance = q == 0 ? Mathf.Min(shadowDistance, 18f) : shadowDistance;

            foreach (var data in urp.rendererDataList)
            {
                if (data == null) continue;
                foreach (var f in data.rendererFeatures)
                    if (f != null && f.name.Contains("AmbientOcclusion")) f.SetActive(q > 0);
            }
            PostFx.Instance?.SetQuality(q);
            ApplyLights();
        }

        /// <summary>Shadows per light: authored on High, hard on Medium, main light only on Low.</summary>
        public static void ApplyLights()
        {
            int q = Level;
            var dead = new List<Light>();
            foreach (var k in authored.Keys) if (!k) dead.Add(k);
            foreach (var k in dead) authored.Remove(k);
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!authored.TryGetValue(l, out var orig)) authored[l] = orig = l.shadows;
                if (orig == LightShadows.None) continue;
                l.shadows = q == 2 ? orig
                    : q == 1 ? LightShadows.Hard
                    : l.type == LightType.Directional ? LightShadows.Hard : LightShadows.None;
            }
        }
    }
}
