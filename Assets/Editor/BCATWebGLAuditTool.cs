// BCAT WebGL audit tooling — added 2026-07-23 by the WebGL baseline audit.
// Invoked via Unity batch mode with -executeMethod. Writes reports to the
// directory passed with -bcatReportDir (outside Assets).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BCATWebGLAuditTool
{
    private const string BuildOutputDir = "webgl-temp-audit";

    private static string GetArg(string name, string fallback)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return fallback;
    }

    private static string ReportDir()
    {
        string dir = GetArg("-bcatReportDir", "webgl-temp-audit-reports/baseline");
        Directory.CreateDirectory(dir);
        return dir;
    }

    // ---------------------------------------------------------------- build
    public static void BuildToAuditFolder()
    {
        string reportDir = ReportDir();
        try
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = BuildOutputDir,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.DetailedBuildReport,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            WriteBuildReport(report, reportDir);

            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log("[BCATAudit] Build result: " + report.summary.result);
            EditorApplication.Exit(ok ? 0 : 1);
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(reportDir, "build_exception.txt"), e.ToString());
            Debug.LogError("[BCATAudit] Build exception: " + e);
            EditorApplication.Exit(1);
        }
    }

    private static void WriteBuildReport(BuildReport report, string dir)
    {
        var s = report.summary;
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"result\": \"" + s.result + "\",");
        sb.AppendLine("  \"platform\": \"" + s.platform + "\",");
        sb.AppendLine("  \"outputPath\": " + Json(s.outputPath) + ",");
        sb.AppendLine("  \"totalSizeBytes\": " + s.totalSize + ",");
        sb.AppendLine("  \"totalTimeSeconds\": " + s.totalTime.TotalSeconds.ToString("F1") + ",");
        sb.AppendLine("  \"totalErrors\": " + s.totalErrors + ",");
        sb.AppendLine("  \"totalWarnings\": " + s.totalWarnings + ",");
        sb.AppendLine("  \"buildStartedAt\": \"" + s.buildStartedAt.ToString("o") + "\",");
        sb.AppendLine("  \"buildEndedAt\": \"" + s.buildEndedAt.ToString("o") + "\",");
        sb.AppendLine("  \"guid\": \"" + s.guid + "\"");
        sb.AppendLine("}");
        File.WriteAllText(Path.Combine(dir, "build_summary.json"), sb.ToString());

        // Build output files
        var files = new StringBuilder("path\trole\tsizeBytes\n");
        foreach (var f in report.GetFiles())
            files.Append(f.path).Append('\t').Append(f.role).Append('\t').Append(f.size).Append('\n');
        File.WriteAllText(Path.Combine(dir, "build_files.tsv"), files.ToString());

        // Build steps
        var steps = new StringBuilder();
        foreach (var step in report.steps)
        {
            steps.Append(new string(' ', step.depth * 2))
                 .Append(step.name).Append(" — ").Append(step.duration.TotalSeconds.ToString("F1")).Append("s\n");
            foreach (var m in step.messages)
                if (m.type == LogType.Error || m.type == LogType.Exception || m.type == LogType.Warning)
                    steps.Append(new string(' ', step.depth * 2 + 2))
                         .Append('[').Append(m.type).Append("] ").Append(m.content).Append('\n');
        }
        File.WriteAllText(Path.Combine(dir, "build_steps.txt"), steps.ToString());

        // Packed assets (what is inside the .data payload)
        var packed = new StringBuilder("packFile\tsourceAssetPath\tguid\ttype\tpackedSizeBytes\n");
        foreach (var pack in report.packedAssets)
            foreach (var info in pack.contents)
                packed.Append(pack.shortPath).Append('\t')
                      .Append(info.sourceAssetPath).Append('\t')
                      .Append(info.sourceAssetGUID).Append('\t')
                      .Append(info.type != null ? info.type.Name : "?").Append('\t')
                      .Append(info.packedSize).Append('\n');
        File.WriteAllText(Path.Combine(dir, "packed_assets.tsv"), packed.ToString());

        // Stripping info
        var strip = new StringBuilder();
        if (report.strippingInfo != null)
        {
            foreach (string module in report.strippingInfo.includedModules)
            {
                strip.Append(module).Append('\n');
                foreach (string reason in report.strippingInfo.GetReasonsForIncluding(module))
                    strip.Append("  <- ").Append(reason).Append('\n');
            }
        }
        else strip.Append("(no stripping info)\n");
        File.WriteAllText(Path.Combine(dir, "stripping_info.txt"), strip.ToString());

        // Scenes using assets (DetailedBuildReport only)
        var sua = new StringBuilder("assetPath\tscenePaths\n");
        if (report.scenesUsingAssets != null)
            foreach (var group in report.scenesUsingAssets)
                foreach (var entry in group.list)
                    sua.Append(entry.assetPath).Append('\t')
                       .Append(string.Join(";", entry.scenePaths)).Append('\n');
        File.WriteAllText(Path.Combine(dir, "scenes_using_assets.tsv"), sua.ToString());
    }

    private static string Json(string v) =>
        "\"" + (v ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    // ---------------------------------------------- dependency graph dump
    public static void DumpDependencyGraph()
    {
        string reportDir = ReportDir();
        try
        {
            var rootGroups = new Dictionary<string, List<string>>();

            List<string> enabledScenes = EditorBuildSettings.scenes
                .Where(s => s.enabled).Select(s => s.path).ToList();
            for (int i = 0; i < enabledScenes.Count; i++)
                rootGroups["scene:" + Path.GetFileNameWithoutExtension(enabledScenes[i])] =
                    new List<string> { enabledScenes[i] };

            rootGroups["renderPipeline"] = new List<string>
            {
                "Assets/Settings/PC_RPAsset.asset",
                "Assets/Settings/Mobile_RPAsset.asset",
            }.Where(p => AssetDatabase.GetMainAssetTypeAtPath(p) != null).ToList();

            rootGroups["inputActions"] = new List<string>
            {
                "Assets/StarterAssets/InputSystem/StarterAssets.inputactions",
            }.Where(p => AssetDatabase.GetMainAssetTypeAtPath(p) != null).ToList();

            rootGroups["xrSettings"] = AssetDatabase.FindAssets("t:Object", new[] { "Assets/XR" })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList();

            // Every asset inside any Resources folder is always packed.
            rootGroups["resources"] = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/") && !AssetDatabase.IsValidFolder(p)
                            && (p.Contains("/Resources/")))
                .ToList();

            rootGroups["streamingAssets"] = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/StreamingAssets/") && !AssetDatabase.IsValidFolder(p))
                .ToList();

            // Preloaded assets (empty per discovery, but capture defensively)
            rootGroups["preloaded"] = PlayerSettings.GetPreloadedAssets()
                .Where(a => a != null).Select(AssetDatabase.GetAssetPath)
                .Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();

            // Write the roots list
            var rootsOut = new StringBuilder();
            foreach (var kv in rootGroups)
            {
                rootsOut.Append("[").Append(kv.Key).Append("]\n");
                foreach (string p in kv.Value) rootsOut.Append("  ").Append(p).Append('\n');
            }
            File.WriteAllText(Path.Combine(reportDir, "dependency_roots.txt"), rootsOut.ToString());

            // Dependency closure per group
            var tagsByAsset = new Dictionary<string, HashSet<string>>();
            foreach (var kv in rootGroups)
            {
                if (kv.Value.Count == 0) continue;
                string[] deps = AssetDatabase.GetDependencies(kv.Value.ToArray(), true);
                foreach (string dep in deps)
                {
                    if (!dep.StartsWith("Assets/")) continue;
                    if (!tagsByAsset.TryGetValue(dep, out var set))
                        tagsByAsset[dep] = set = new HashSet<string>();
                    set.Add(kv.Key);
                }
            }

            // Full inventory of the Assets folder
            var inv = new StringBuilder("path\tguid\ttype\tsizeBytes\ttags\n");
            foreach (string path in AssetDatabase.GetAllAssetPaths()
                         .Where(p => p.StartsWith("Assets/"))
                         .OrderBy(p => p, StringComparer.Ordinal))
            {
                if (AssetDatabase.IsValidFolder(path)) continue;
                long size = 0;
                try { size = new FileInfo(path).Length; } catch { }
                string type = AssetDatabase.GetMainAssetTypeAtPath(path)?.Name ?? "?";
                string guid = AssetDatabase.AssetPathToGUID(path);
                string tags = tagsByAsset.TryGetValue(path, out var set)
                    ? string.Join(",", set.OrderBy(t => t)) : "-";
                inv.Append(path).Append('\t').Append(guid).Append('\t')
                   .Append(type).Append('\t').Append(size).Append('\t').Append(tags).Append('\n');
            }
            File.WriteAllText(Path.Combine(reportDir, "asset_inventory.tsv"), inv.ToString());

            Debug.Log("[BCATAudit] Dependency graph dumped to " + reportDir);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(reportDir, "dependency_exception.txt"), e.ToString());
            Debug.LogError("[BCATAudit] Dependency dump exception: " + e);
            EditorApplication.Exit(1);
        }
    }

    // ------------------------------------------------------- validation
    public static void ValidateProject()
    {
        string reportDir = ReportDir();
        var outSb = new StringBuilder();
        bool failed = false;
        try
        {
            foreach (var sceneEntry in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager
                    .OpenScene(sceneEntry.path, UnityEditor.SceneManagement.OpenSceneMode.Single);
                int missingScripts = 0;
                var stack = new Stack<GameObject>(scene.GetRootGameObjects());
                int goCount = 0;
                while (stack.Count > 0)
                {
                    GameObject go = stack.Pop();
                    goCount++;
                    missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                    foreach (Transform child in go.transform) stack.Push(child.gameObject);
                }
                outSb.Append(sceneEntry.path)
                     .Append(" — GameObjects: ").Append(goCount)
                     .Append(", missing scripts: ").Append(missingScripts).Append('\n');
                if (missingScripts > 0) failed = true;
            }
            File.WriteAllText(Path.Combine(reportDir, "scene_validation.txt"), outSb.ToString());
            Debug.Log("[BCATAudit] Validation done. failed=" + failed);
            EditorApplication.Exit(0); // missing scripts are reported, not fatal
        }
        catch (Exception e)
        {
            outSb.Append("EXCEPTION: ").Append(e).Append('\n');
            File.WriteAllText(Path.Combine(reportDir, "scene_validation.txt"), outSb.ToString());
            EditorApplication.Exit(1);
        }
    }
}
