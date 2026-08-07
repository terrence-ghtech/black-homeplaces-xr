using System;
using System.IO;
using System.IO.Compression;
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
        const string QuestApkFileName = "Black Homeplaces XR - Quest.apk";
        const string BlackKitchenAddress = "BlackKitchen_MemoryScene";
        const string BlackKitchenBundlePrefix = "assets/aa/Android/blackkitchen_remote_scenes_all_";

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
            var path = Path.Combine(OutputRoot, "Quest", QuestApkFileName);
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

                if (target == BuildTarget.Android && SkipAddressables)
                    throw new InvalidOperationException("Quest production builds must rebuild Android Addressables; remove -bcatSkipAddressables.");

                if (!SkipAddressables)
                    BuildAddressablesContent(summary);

                if (target == BuildTarget.Android)
                    ValidateAndroidAddressablesOutput(summary);

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
                if (ok && target == BuildTarget.Android)
                    ValidateQuestApkAddressables(outputPath, summary);

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

        static void ValidateAndroidAddressablesOutput(StringBuilder summary)
        {
            string root = Path.Combine("Library", "com.unity.addressables", "aa", "Android");
            string catalog = Path.Combine(root, "catalog.bin");
            string hash = Path.Combine(root, "catalog.hash");
            string settings = Path.Combine(root, "settings.json");
            string bundleDir = Path.Combine(root, "Android");

            RequireFile(catalog, "Android Addressables catalog");
            RequireFile(hash, "Android Addressables catalog hash");
            RequireFile(settings, "Android Addressables settings");
            if (!Directory.Exists(bundleDir))
                throw new FileNotFoundException("Android Addressables bundle directory is missing.", bundleDir);

            string catalogText = ReadBinaryAsText(catalog);
            if (!catalogText.Contains(BlackKitchenAddress))
                throw new InvalidOperationException($"Android Addressables catalog is missing key '{BlackKitchenAddress}'.");

            string blackKitchenBundle = Directory.GetFiles(bundleDir, "blackkitchen_remote_scenes_all_*.bundle")
                .FirstOrDefault();
            if (string.IsNullOrEmpty(blackKitchenBundle))
                throw new FileNotFoundException("Black Kitchen Android Addressables bundle is missing.", bundleDir);

            summary.AppendLine($"Addressables validation: OK, catalog contains '{BlackKitchenAddress}', bundle '{Path.GetFileName(blackKitchenBundle)}'.");
        }

        static void ValidateQuestApkAddressables(string apkPath, StringBuilder summary)
        {
            RequireFile(apkPath, "Quest APK");

            using ZipArchive archive = ZipFile.OpenRead(apkPath);
            bool hasCatalog = HasEntry(archive, "assets/aa/catalog.bin");
            bool hasHash = HasEntry(archive, "assets/aa/catalog.hash");
            bool hasSettings = HasEntry(archive, "assets/aa/settings.json");
            bool hasAndroidFolder = archive.Entries.Any(e => e.FullName.StartsWith("assets/aa/Android/", StringComparison.Ordinal));
            ZipArchiveEntry blackKitchenBundle = archive.Entries.FirstOrDefault(e =>
                e.FullName.StartsWith(BlackKitchenBundlePrefix, StringComparison.Ordinal) &&
                e.FullName.EndsWith(".bundle", StringComparison.Ordinal));

            if (!hasCatalog || !hasHash || !hasSettings || !hasAndroidFolder || blackKitchenBundle == null)
            {
                throw new InvalidOperationException(
                    "Quest APK Addressables validation failed: " +
                    $"catalog={hasCatalog}, hash={hasHash}, settings={hasSettings}, androidFolder={hasAndroidFolder}, " +
                    $"blackKitchenBundle={blackKitchenBundle != null}.");
            }

            using Stream catalogStream = archive.GetEntry("assets/aa/catalog.bin").Open();
            using MemoryStream catalogBytes = new MemoryStream();
            catalogStream.CopyTo(catalogBytes);
            string catalogText = Encoding.UTF8.GetString(catalogBytes.ToArray());
            if (!catalogText.Contains(BlackKitchenAddress))
                throw new InvalidOperationException($"Quest APK catalog is missing key '{BlackKitchenAddress}'.");

            FileInfo apk = new FileInfo(apkPath);
            summary.AppendLine($"Quest APK validation: OK, {apk.FullName}, {apk.Length} bytes.");
            summary.AppendLine($"Quest APK Addressables: catalog/hash/settings present, Android bundle present, key '{BlackKitchenAddress}' present.");
        }

        static void RequireFile(string path, string label)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(label + " is missing.", path);
        }

        static bool HasEntry(ZipArchive archive, string name) =>
            archive.GetEntry(name) != null;

        static string ReadBinaryAsText(string path) =>
            Encoding.UTF8.GetString(File.ReadAllBytes(path));

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
