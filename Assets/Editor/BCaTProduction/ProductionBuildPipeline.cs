using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Batch-mode build entry points for the three supported production targets:
    /// Windows 11 x64, Apple Silicon macOS, and Meta Quest (Android).
    /// Invoked via -executeMethod from the command line; also usable from the menu.
    /// Builds Addressables content for the active target before building the player
    /// so the catalog and local bundles always match the player build.
    /// </summary>
    public static class ProductionBuildPipeline
    {
        const string OutputRoot = "Builds";
        const string ProductFileName = "BlackHomeplaces";

        static string[] EnabledScenes =>
            EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        static bool IsDevelopment =>
            !Environment.GetCommandLineArgs().Contains("-bcatRelease");

        static bool SkipAddressables =>
            Environment.GetCommandLineArgs().Contains("-bcatSkipAddressables");

        [MenuItem("BCaT/Production Builds/Build macOS (Apple Silicon)")]
        public static void BuildMacOS()
        {
            SetMacArchitectureArm64();
            var path = Path.Combine(OutputRoot, "macOS", ProductFileName + ".app");
            Run(BuildTarget.StandaloneOSX, path);
        }

        [MenuItem("BCaT/Production Builds/Build Windows x64")]
        public static void BuildWindows()
        {
            var path = Path.Combine(OutputRoot, "Windows64", ProductFileName, ProductFileName + ".exe");
            Run(BuildTarget.StandaloneWindows64, path);
        }

        [MenuItem("BCaT/Production Builds/Build Meta Quest APK")]
        public static void BuildQuest()
        {
            EditorUserBuildSettings.buildAppBundle = false;
            var path = Path.Combine(OutputRoot, "Quest", ProductFileName + "-Quest.apk");
            Run(BuildTarget.Android, path);
        }

        static void Run(BuildTarget target, string outputPath)
        {
            var summary = new StringBuilder();
            summary.AppendLine($"BCaT production build — {target}");
            summary.AppendLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            summary.AppendLine($"Development build: {IsDevelopment}");

            try
            {
                if (EditorUserBuildSettings.activeBuildTarget != target)
                    throw new InvalidOperationException(
                        $"Active build target is {EditorUserBuildSettings.activeBuildTarget}; " +
                        $"launch the editor with -buildTarget for {target} before building.");

                if (!SkipAddressables)
                    BuildAddressablesContent(summary);

                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));

                var options = new BuildPlayerOptions
                {
                    scenes = EnabledScenes,
                    locationPathName = outputPath,
                    target = target,
                    options = IsDevelopment ? BuildOptions.Development : BuildOptions.None,
                };

                summary.AppendLine("Scenes: " + string.Join(", ", options.scenes));

                BuildReport report = UnityEditor.BuildPipeline.BuildPlayer(options);
                AppendReport(summary, report);

                bool ok = report.summary.result == BuildResult.Succeeded;
                WriteSummary(target, summary);
                ExitIfBatch(ok ? 0 : 1);
            }
            catch (Exception e)
            {
                summary.AppendLine("BUILD EXCEPTION: " + e);
                WriteSummary(target, summary);
                Debug.LogError(e);
                ExitIfBatch(1);
                throw;
            }
        }

        static void BuildAddressablesContent(StringBuilder summary)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                summary.AppendLine("Addressables: no settings object found — skipped.");
                return;
            }

            summary.AppendLine($"Addressables: building content for profile " +
                $"'{settings.profileSettings.GetProfileName(settings.activeProfileId)}'.");
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrEmpty(result.Error))
                throw new Exception("Addressables build failed: " + result.Error);
            summary.AppendLine($"Addressables: OK ({result.Duration:F1}s), " +
                $"location count {result.LocationCount}.");
        }

        /// <summary>
        /// Restrict the macOS build to Apple Silicon. Uses reflection so the code
        /// tolerates enum/type moves of UserBuildSettings.architecture across versions.
        /// </summary>
        static void SetMacArchitectureArm64()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEditor.OSXStandalone.UserBuildSettings"))
                .FirstOrDefault(t => t != null);
            var prop = type?.GetProperty("architecture", BindingFlags.Public | BindingFlags.Static);
            if (prop == null)
            {
                Debug.LogWarning("[BCaT Build] Could not find OSXStandalone.UserBuildSettings.architecture; " +
                                 "macOS architecture left at editor default.");
                return;
            }
            var value = Enum.Parse(prop.PropertyType, "ARM64");
            prop.SetValue(null, value);
            Debug.Log("[BCaT Build] macOS architecture set to ARM64 (Apple Silicon).");
        }

        static void AppendReport(StringBuilder summary, BuildReport report)
        {
            var s = report.summary;
            summary.AppendLine($"Result: {s.result}");
            summary.AppendLine($"Output: {s.outputPath}");
            summary.AppendLine($"Total size: {s.totalSize / (1024.0 * 1024.0):F1} MB");
            summary.AppendLine($"Total time: {s.totalTime.TotalMinutes:F1} min");
            summary.AppendLine($"Errors: {s.totalErrors}, Warnings: {s.totalWarnings}");

            foreach (var step in report.steps)
            foreach (var msg in step.messages)
                if (msg.type == LogType.Error || msg.type == LogType.Exception)
                    summary.AppendLine($"  [{msg.type}] {step.name}: {msg.content}");
        }

        static void WriteSummary(BuildTarget target, StringBuilder summary)
        {
            Directory.CreateDirectory(OutputRoot);
            var file = Path.Combine(OutputRoot, $"BuildSummary_{target}.txt");
            File.WriteAllText(file, summary.ToString());
            Debug.Log(summary.ToString());
        }

        static void ExitIfBatch(int code)
        {
            if (Application.isBatchMode)
                EditorApplication.Exit(code);
        }
    }
}
