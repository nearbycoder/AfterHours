using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AfterHours.EditorTools
{
    /// <summary>Menu items and batch-mode entry points for building players.</summary>
    public static class BuildScript
    {
        static readonly string[] Scenes = { "Assets/Scenes/Main.unity" };

        [MenuItem("After Hours/Build Linux Player")]
        public static void BuildLinux() =>
            Build(BuildTarget.StandaloneLinux64, "Builds/Linux/AfterHours.x86_64");

        /// <summary>A universal (Intel + Apple Silicon) app, unsigned.</summary>
        [MenuItem("After Hours/Build macOS Player")]
        public static void BuildMac()
        {
            SetMacArchitecture("x64ARM64");
            Build(BuildTarget.StandaloneOSX, "Builds/macOS/After Hours.app");
        }

        /// <summary>Needs Unity's Windows Build Support module (not installed on the dev machine).</summary>
        [MenuItem("After Hours/Build Windows Player")]
        public static void BuildWindows() =>
            Build(BuildTarget.StandaloneWindows64, "Builds/Windows/AfterHours.exe");

        /// <summary>
        /// The browser build for GitHub Pages (Tools/build-pages.sh): a static site in Builds/Pages,
        /// served under /AfterHours/ without any server headers. Brotli with decompression fallback
        /// (the loader unpacks the files itself), single-threaded (no SharedArrayBuffer), the
        /// project's page template, and saves in IndexedDB.
        /// </summary>
        [MenuItem("After Hours/Build Web Player (GitHub Pages)")]
        public static void BuildWebGL()
        {
            PlayerSettings.WebGL.template = "PROJECT:AfterHours";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.showDiagnostics = false;
            PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
            PlayerSettings.WebGL.initialMemorySize = 256;
            PlayerSettings.WebGL.maximumMemorySize = 2048;
            PlayerSettings.stripEngineCode = true;
            SetWebCodeOptimization("DiskSize");
            Build(BuildTarget.WebGL, "Builds/Pages");
        }

        static void Build(BuildTarget target, string path)
        {
            var group = BuildPipeline.GetBuildTargetGroup(target);
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                Debug.LogError($"[AfterHours] {target} build support isn't installed. Add it in Unity Hub (Installs → 6000.6.2f1 → Add modules).");
                if (Application.isBatchMode) EditorApplication.Exit(2);
                return;
            }
            ProjectSetup.EnsureAll();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            Debug.Log($"[AfterHours] {target} build {summary.result}: {summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors -> {path}");
            if (Application.isBatchMode)
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>
        /// The web player's code optimization (smaller download over faster builds). It lives in the
        /// WebGL module's editor assembly, so it is set by reflection like the Mac architecture.
        /// </summary>
        static void SetWebCodeOptimization(string mode)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEditor.WebGL.UserBuildSettings", false))
                .FirstOrDefault(t => t != null);
            var prop = type?.GetProperty("codeOptimization");
            if (prop == null) { Debug.LogWarning("[AfterHours] can't set the web code optimization"); return; }
            prop.SetValue(null, Enum.Parse(prop.PropertyType, mode));
            Debug.Log($"[AfterHours] web code optimization: {prop.GetValue(null)}");
        }

        /// <summary>
        /// macOS player architecture. The setting lives in the Mac module's editor assembly, so it is
        /// set by reflection: the project still compiles on an editor without Mac support.
        /// </summary>
        static void SetMacArchitecture(string arch)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEditor.OSXStandalone.UserBuildSettings", false))
                .FirstOrDefault(t => t != null);
            var prop = type?.GetProperty("architecture");
            if (prop == null) { Debug.LogWarning("[AfterHours] can't set the macOS architecture (no Mac build support?)"); return; }
            prop.SetValue(null, Enum.Parse(prop.PropertyType, arch));
            Debug.Log($"[AfterHours] macOS architecture: {prop.GetValue(null)}");
        }
    }
}
