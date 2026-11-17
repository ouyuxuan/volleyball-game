using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Sandspike.EditorTools
{
    /// <summary>Menu "Sandspike > Build Windows": builds the scenes ticked in Build Settings to Build/Windows/Sandspike.exe.</summary>
    public static class BuildGame
    {
        const string WindowsExe = "Build/Windows/Sandspike.exe";

        [MenuItem("Sandspike/Build Windows")]
        public static void BuildWindows()
        {
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);

            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = WindowsExe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildSummary r = BuildPipeline.BuildPlayer(options).summary;
            Debug.Log($"[Build] {r.result}: {r.totalSize / (1024 * 1024)} MB, {r.totalTime.TotalSeconds:0} s, {r.totalErrors} errors, {r.totalWarnings} warnings -> {r.outputPath}");
        }
    }
}
