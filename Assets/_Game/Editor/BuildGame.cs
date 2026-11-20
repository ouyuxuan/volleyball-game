using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Sandspike.EditorTools
{
    /// <summary>
    /// Menu "Sandspike > Build ...": builds the scenes ticked in Build Settings into Build/&lt;platform&gt;.
    /// Building for another platform first switches the Editor to it, which re-imports assets (minutes, once per switch).
    /// </summary>
    public static class BuildGame
    {
        [MenuItem("Sandspike/Build Windows")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, "Build/Windows/Sandspike.exe");

        [MenuItem("Sandspike/Build macOS")]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, BuildTargetGroup.Standalone, "Build/macOS/Sandspike.app");

        [MenuItem("Sandspike/Build Web")]
        public static void BuildWeb()
        {
            ApplyWebSettings();
            Build(BuildTarget.WebGL, BuildTargetGroup.WebGL, "Build/Web");
        }

        // Release settings from Unity's "optimize your Web build" pages. Brotli keeps the download small; the decompression
        // fallback lets the build run on hosts that can't send Content-Encoding headers (itch.io, GitHub Pages) at a small cost.
        static void ApplyWebSettings()
        {
            var web = NamedBuildTarget.WebGL;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
            PlayerSettings.WebGL.wasm2023 = true;                                    // exceptions without the old overhead
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(web, ManagedStrippingLevel.High);
            PlayerSettings.SetIl2CppCodeGeneration(web, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetApiCompatibilityLevel(web, ApiCompatibilityLevel.NET_Standard);
            UnityEditor.WebGL.UserBuildSettings.codeOptimization = UnityEditor.WebGL.WasmCodeOptimization.DiskSizeLTO;   // stored per machine, so set on every build
            AssetDatabase.SaveAssets();
        }

        static void Build(BuildTarget target, BuildTargetGroup group, string path)
        {
            if (EditorUserBuildSettings.activeBuildTarget != target) EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);

            var scenes = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);

            var options = new BuildPlayerOptions { scenes = scenes.ToArray(), locationPathName = path, target = target, options = BuildOptions.None };
            BuildSummary r = BuildPipeline.BuildPlayer(options).summary;
            Debug.Log($"[Build] {target} {r.result}: {r.totalSize / (1024 * 1024)} MB, {r.totalTime.TotalSeconds:0} s, {r.totalErrors} errors, {r.totalWarnings} warnings -> {r.outputPath}");
        }
    }
}
