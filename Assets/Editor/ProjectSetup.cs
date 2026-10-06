using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Idempotent project wiring: layers, physics matrix, player settings, the (empty) Main scene,
    /// template materials that keep custom shaders and URP keyword variants in builds, and URP
    /// quality tweaks. Runs before every build and from the menu / batch mode.
    /// </summary>
    public static class ProjectSetup
    {
        public const string MainScene = "Assets/Scenes/Main.unity";
        const string MaterialDir = "Assets/Resources/Materials";

        // Layer indices are mirrored in AfterHours.Layers (runtime).
        static readonly (int index, string name)[] LayerNames =
        {
            (8, "Grime"), (9, "Player"), (10, "Prop"), (11, "Viewmodel"), (12, "Trigger"), (13, "Glass"), (14, "Hands"),
        };

        [MenuItem("After Hours/Apply Project Setup")]
        public static void Apply()
        {
            EnsureAll();
            Debug.Log("[ProjectSetup] done");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static void EnsureAll()
        {
            EnsureTmp();
            EnsureLayers();
            EnsurePhysics();
            EnsurePlayerSettings();
            EnsureTemplateMaterials();
            EnsureUrp();
            EnsureMainScene();
            AssetDatabase.SaveAssets();
        }

        /// <summary>TextMeshPro's shaders and default settings ship as a package inside uGUI.</summary>
        static void EnsureTmp()
        {
            if (Directory.Exists("Assets/TextMesh Pro")) return;
            var pkg = Directory.GetDirectories("Library/PackageCache", "com.unity.ugui@*").FirstOrDefault();
            if (pkg == null) { Debug.LogError("[ProjectSetup] ugui package not found"); return; }
            var path = Path.Combine(pkg, "Package Resources", "TMP Essential Resources.unitypackage");
            AssetDatabase.ImportPackage(path, false);
            AssetDatabase.Refresh();
            Debug.Log("[ProjectSetup] imported TMP Essential Resources");
        }

        static void EnsureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            foreach (var (index, name) in LayerNames)
                layers.GetArrayElementAtIndex(index).stringValue = name;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsurePhysics()
        {
            int grime = 8, player = 9, prop = 10, viewmodel = 11, trigger = 12;
            for (int i = 0; i < 32; i++)
            {
                Physics.IgnoreLayerCollision(grime, i, true);
                Physics.IgnoreLayerCollision(viewmodel, i, true);
                Physics.IgnoreLayerCollision(trigger, i, i != player && i != prop);
            }
            Physics.IgnoreLayerCollision(player, prop, true);
            Physics.IgnoreLayerCollision(trigger, player, false);
            Physics.IgnoreLayerCollision(trigger, prop, false);
        }

        static void EnsurePlayerSettings()
        {
            PlayerSettings.companyName = "After Hours Team";
            PlayerSettings.productName = "After Hours";
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SplashScreen.show = false;
            // Company and product names set the save folder, so they never change; the bundle id
            // names the macOS app.
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, BundleId);
            PlayerSettings.macOS.applicationCategoryType = "public.app-category.games";
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null) PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            else Debug.LogWarning("[ProjectSetup] no app icon at " + IconPath + " (run Tools/make_icon.py)");
        }

        public const string BundleId = "com.nearbycoder.afterhours";
        const string IconPath = "Assets/Icons/AppIcon.png";

        /// <summary>Material assets in Resources so shaders and their keyword variants ship.</summary>
        static void EnsureTemplateMaterials()
        {
            Directory.CreateDirectory(MaterialDir);
            var lit = Shader.Find("Universal Render Pipeline/Lit");

            Template("AH_Grime", Shader.Find("AfterHours/Grime"));
            Template("AH_UvMark", Shader.Find("AfterHours/UvMark"));
            Template("AH_Skyline", Shader.Find("AfterHours/Skyline"));
            Template("AH_Fx", Shader.Find("AfterHours/FxParticle"));

            var opaque = Template("AH_LitOpaque", lit);
            if (opaque != null) opaque.SetFloat("_Smoothness", 0.35f);

            var normalTmpl = Template("AH_LitNormal", lit);
            if (normalTmpl != null)
            {
                normalTmpl.EnableKeyword("_NORMALMAP");
                normalTmpl.SetFloat("_Smoothness", 0.3f);
                var anyNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Textures/Mat/tile_break_n.png");
                if (anyNormal != null) normalTmpl.SetTexture("_BumpMap", anyNormal);
            }

            var emissive = Template("AH_LitEmissive", lit);
            if (emissive != null)
            {
                emissive.EnableKeyword("_EMISSION");
                emissive.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                emissive.SetColor("_EmissionColor", Color.white);
                // Glowing surfaces (screens, LEDs, lamps) light themselves: no specular hotspot from
                // nearby lights and no sky reflection washing the picture out.
                emissive.SetFloat("_SpecularHighlights", 0f);
                emissive.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                emissive.SetFloat("_EnvironmentReflections", 0f);
                emissive.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                emissive.SetFloat("_Smoothness", 0.2f);
            }

            var glass = Template("AH_LitGlass", lit);
            if (glass != null)
            {
                glass.SetFloat("_Surface", 1f);
                glass.SetFloat("_Blend", 0f);
                glass.SetFloat("_AlphaClip", 0f);
                glass.SetFloat("_SrcBlend", (float)BlendMode.One);
                glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                glass.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                glass.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                glass.SetFloat("_ZWrite", 0f);
                glass.SetFloat("_Smoothness", 0.95f);
                glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                glass.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                glass.SetOverrideTag("RenderType", "Transparent");
                glass.renderQueue = (int)RenderQueue.Transparent;
                glass.SetColor("_BaseColor", new Color(0.55f, 0.7f, 0.75f, 0.12f));
            }

            var fade = Template("AH_LitFade", lit);
            if (fade != null)
            {
                fade.SetFloat("_Surface", 1f);
                fade.SetFloat("_Blend", 0f);
                fade.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                fade.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                fade.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                fade.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                fade.SetFloat("_ZWrite", 0f);
                fade.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                fade.SetOverrideTag("RenderType", "Transparent");
                fade.renderQueue = (int)RenderQueue.Transparent;
            }

            var unlit = Template("AH_Unlit", Shader.Find("Universal Render Pipeline/Unlit"));
            if (unlit != null) unlit.SetColor("_BaseColor", Color.white);
        }

        static Material Template(string name, Shader shader)
        {
            if (shader == null)
            {
                Debug.LogWarning($"[ProjectSetup] shader for {name} not found (yet)");
                return null;
            }
            var path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader) mat.shader = shader;
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void EnsureUrp()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (asset == null) return;
            var so = new SerializedObject(asset);
            void SetInt(string prop, int v) { var p = so.FindProperty(prop); if (p != null) p.intValue = v; }
            void SetFloat(string prop, float v) { var p = so.FindProperty(prop); if (p != null) p.floatValue = v; }
            void SetBool(string prop, bool v) { var p = so.FindProperty(prop); if (p != null) p.boolValue = v; }
            SetInt("m_MSAA", 4);
            SetBool("m_SupportsHDR", true);
            SetFloat("m_ShadowDistance", 28f);
            SetInt("m_ShadowCascadeCount", 2);
            SetBool("m_SoftShadowsSupported", true);
            SetBool("m_AdditionalLightShadowsSupported", true);
            SetInt("m_AdditionalLightsShadowmapResolution", 4096); // 8 shadowed room lights at full resolution
            SetBool("m_SupportsCameraDepthTexture", true);
            SetBool("m_SupportsCameraOpaqueTexture", true);
            so.ApplyModifiedPropertiesWithoutUndo();

            GraphicsSettings.defaultRenderPipeline = asset;
            int levels = QualitySettings.names.Length;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < levels; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
            EditorUtility.SetDirty(asset);
        }

        static void EnsureMainScene()
        {
            if (!File.Exists(MainScene))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MainScene)!);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, MainScene);
            }
            var scenes = new[] { new EditorBuildSettingsScene(MainScene, true) };
            if (!EditorBuildSettings.scenes.Select(s => s.path).SequenceEqual(scenes.Select(s => s.path)))
                EditorBuildSettings.scenes = scenes;
        }
    }
}
