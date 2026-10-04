using UnityEditor;
using UnityEngine;

namespace AfterHours.EditorTools
{
    /// <summary>Import rules for generated assets (textures, audio, Blender FBX).</summary>
    public class ImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/Textures/")) return;
            var ti = (TextureImporter)assetImporter;
            if (assetPath.Contains("/Textures/Grime/"))
            {
                // Grime layers are sampled on the CPU (pattern + secret coverage).
                ti.isReadable = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.mipmapEnabled = true;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.alphaIsTransparency = true;
            }
            else if (assetPath.Contains("/Textures/UI/") || assetPath.Contains("/Textures/Docs/"))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.wrapMode = TextureWrapMode.Clamp;
            }
            else
            {
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.mipmapEnabled = true;
                ti.anisoLevel = 8;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                if (assetPath.Contains("_n.png"))
                    ti.textureType = TextureImporterType.NormalMap;
            }
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.Contains("/Resources/Audio/")) return;
            var ai = (AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            bool music = assetPath.Contains("/Audio/music_") || assetPath.Contains("/Audio/amb_");
            s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = music ? 0.6f : 0.75f;
            s.preloadAudioData = !music;
            ai.defaultSampleSettings = s;
            ai.forceToMono = false;
            ai.loadInBackground = music;
        }

        void OnPreprocessModel()
        {
            if (!assetPath.Contains("/Resources/Models/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.importVisibility = false;
            mi.addCollider = false;
            mi.isReadable = true;  // runtime MeshColliders and bounds
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.CalculateMikk;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }
    }
}
