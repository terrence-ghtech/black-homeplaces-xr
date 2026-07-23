// BCAT public-WebGL optimization — Addressables configuration (Stage 5).
// Creates the remote Black Kitchen group, points it at the Remote build/load
// profile paths, and removes the scene from the built-in scene list (the
// LoadingSceneController falls back to SceneManager whenever the scene is
// present in the build, so Quest/desktop builds can simply re-enable it).
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BCATAddressablesSetup
{
    private const string BlackKitchenScenePath =
        "Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity";
    private const string GroupName = "BlackKitchen_Remote";
    private const string LocalTestLoadPath = "http://127.0.0.1:8090/addressables/[BuildTarget]";

    public static void Setup()
    {
        try
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var ps = settings.profileSettings;
            string profileId = settings.activeProfileId;

            string remoteBuildVar = ps.GetVariableNames()
                .FirstOrDefault(n => n.Contains("Remote") && n.Contains("Build")) ?? "Remote.BuildPath";
            string remoteLoadVar = ps.GetVariableNames()
                .FirstOrDefault(n => n.Contains("Remote") && n.Contains("Load")) ?? "Remote.LoadPath";
            ps.SetValue(profileId, remoteBuildVar, "ServerData/[BuildTarget]");
            ps.SetValue(profileId, remoteLoadVar, LocalTestLoadPath);

            var group = settings.FindGroup(GroupName) ?? settings.CreateGroup(
                GroupName, false, false, false, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(settings, remoteBuildVar);
            schema.LoadPath.SetVariableByName(settings, remoteLoadVar);
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            schema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            schema.UseAssetBundleCache = true;

            string guid = AssetDatabase.AssetPathToGUID(BlackKitchenScenePath);
            if (string.IsNullOrEmpty(guid))
                throw new Exception("Black Kitchen scene not found at " + BlackKitchenScenePath);
            var entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = "BlackKitchen_MemoryScene";

            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Select(s => s.path == BlackKitchenScenePath
                    ? new EditorBuildSettingsScene(s.path, false) : s)
                .ToArray();

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BCATAddr] Setup complete. Remote vars: {remoteBuildVar}, {remoteLoadVar}. " +
                      "Black Kitchen scene disabled in Build Settings and added to remote group.");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("[BCATAddr] Setup failed: " + e);
            EditorApplication.Exit(1);
        }
    }

    // Stage 7: Addressables content build + fresh player build into webgl-public-optimized/.
    public static void BuildOptimizedPlayer()
    {
        string reportDir = "webgl-public-optimization-reports/build";
        Directory.CreateDirectory(reportDir);
        try
        {
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult addrResult);
            if (!string.IsNullOrEmpty(addrResult.Error))
            {
                File.WriteAllText(Path.Combine(reportDir, "addressables_build_error.txt"), addrResult.Error);
                Debug.LogError("[BCATAddr] Addressables build failed: " + addrResult.Error);
                EditorApplication.Exit(1);
                return;
            }
            string bundleList = addrResult.AssetBundleBuildResults != null
                ? string.Join("\n", addrResult.AssetBundleBuildResults.Select(b => $"  {b.FilePath} <- {b.SourceAssetGroup?.Name}"))
                : "(none reported)";
            File.WriteAllText(Path.Combine(reportDir, "addressables_build.txt"),
                $"outputPath: {addrResult.OutputPath}\nduration: {addrResult.Duration}\nbundles:\n{bundleList}\n");

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "webgl-public-optimized",
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.DetailedBuildReport,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BCATWebGLAuditTool.WriteBuildReport(report, reportDir);

            // Make the remote bundles servable next to the build for local validation.
            string src = "ServerData/WebGL";
            string dst = "webgl-public-optimized/addressables/WebGL";
            if (Directory.Exists(src))
            {
                Directory.CreateDirectory(dst);
                foreach (string f in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
                {
                    string rel = f.Substring(src.Length + 1);
                    string target = Path.Combine(dst, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(f, target, true);
                }
            }

            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log("[BCATAddr] Player build result: " + report.summary.result);
            EditorApplication.Exit(ok ? 0 : 1);
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(reportDir, "build_exception.txt"), e.ToString());
            Debug.LogError("[BCATAddr] Build exception: " + e);
            EditorApplication.Exit(1);
        }
    }
}
