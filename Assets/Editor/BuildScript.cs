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

        static void Build(BuildTarget target, string path)
        {
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
    }
}
