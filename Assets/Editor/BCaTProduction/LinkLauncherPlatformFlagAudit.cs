using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Focused audit/repair for InteractableLinkLauncher's platform gate
    /// (allowDesktop/allowQuest, which drive IsAvailable per platform).
    ///
    /// Those fields were added to the component after several link exhibits were
    /// authored, so their prefab YAML has no keys for them. Verified 2026-08-04:
    /// Unity applies the C# field initializers in that case, NOT default(T) —
    /// every such exhibit reads true/true and is correctly enabled on both
    /// platforms. Absent keys are therefore not a defect on their own; only an
    /// explicitly serialized `allowDesktop: 0` / `allowQuest: 0` disables one.
    /// This tool exists to keep that verifiable rather than assumed.
    ///
    /// Audit reports the values Unity actually deserializes. Repair sets only
    /// those two booleans to true; every other field, reference and transform is
    /// left untouched, and the launcher script itself is never modified.
    ///
    ///   Unity -executeMethod BCaT.EditorTools.LinkLauncherPlatformFlagAudit.Audit
    ///   Unity -executeMethod BCaT.EditorTools.LinkLauncherPlatformFlagAudit.Repair
    /// </summary>
    public static class LinkLauncherPlatformFlagAudit
    {
        private const string MainScenePath = "Assets/BH_XR_MainScene.unity";
        private static readonly string ReportPath =
            Path.Combine("Docs", "Production", "LINK_LAUNCHER_PLATFORM_AUDIT.txt");

        // Recovery snapshots are historical backups and are not in build
        // settings, so they are reported but never modified.
        private const string ExcludedPathFragment = "/_Recovery/";

        [MenuItem("BCaT/Production Setup/Link Launcher Platform Flag Audit")]
        public static void Audit() => Execute(false);

        [MenuItem("BCaT/Production Setup/Link Launcher Platform Flag Repair")]
        public static void Repair() => Execute(true);

        private static void Execute(bool repair)
        {
            var report = new StringBuilder();
            report.AppendLine($"InteractableLinkLauncher platform-flag {(repair ? "repair" : "audit")} — " +
                              $"{System.DateTime.Now:yyyy-MM-dd HH:mm}");

            var repaired = new List<string>();
            var alreadyOk = new List<string>();

            // ---- Prefab assets ----
            report.AppendLine("\n== Prefab assets ==");
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Replace('\\', '/').Contains(ExcludedPathFragment))
                    continue;

                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null)
                    continue;

                var launchers = asset.GetComponentsInChildren<InteractableLinkLauncher>(true);
                if (launchers.Length == 0)
                    continue;

                bool needsWrite = false;
                var lines = new List<string>();

                foreach (InteractableLinkLauncher launcher in launchers)
                {
                    var so = new SerializedObject(launcher);
                    SerializedProperty desktop = so.FindProperty("allowDesktop");
                    SerializedProperty quest = so.FindProperty("allowQuest");
                    string url = so.FindProperty("targetUrl").stringValue;

                    lines.Add($"    '{launcher.gameObject.name}' allowDesktop={desktop.boolValue} " +
                              $"allowQuest={quest.boolValue} IsAvailable={launcher.IsAvailable} " +
                              $"url='{Trim(url)}'");

                    if (desktop.boolValue && quest.boolValue)
                        continue;

                    if (repair)
                    {
                        desktop.boolValue = true;
                        quest.boolValue = true;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        needsWrite = true;
                    }

                    string label = $"{path} :: {launcher.gameObject.name}";
                    if (!repaired.Contains(label))
                        repaired.Add(label);
                }

                report.AppendLine($"  {path}");
                foreach (string line in lines)
                    report.AppendLine(line);

                if (needsWrite)
                {
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssetIfDirty(asset);
                    report.AppendLine("    -> REPAIRED (allowDesktop/allowQuest set true, prefab saved)");
                }
                else if (lines.Count > 0 && !repaired.Any(r => r.StartsWith(path)))
                {
                    foreach (InteractableLinkLauncher launcher in launchers)
                        alreadyOk.Add($"{path} :: {launcher.gameObject.name}");
                }
            }

            // ---- Main scene objects ----
            report.AppendLine("\n== Main scene objects ==");
            Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            bool sceneDirty = false;

            foreach (InteractableLinkLauncher launcher in
                     Object.FindObjectsByType<InteractableLinkLauncher>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(launcher);
                SerializedProperty desktop = so.FindProperty("allowDesktop");
                SerializedProperty quest = so.FindProperty("allowQuest");
                string url = so.FindProperty("targetUrl").stringValue;
                bool fromPrefab = PrefabUtility.IsPartOfPrefabInstance(launcher);

                report.AppendLine($"  '{Path2(launcher.transform)}' allowDesktop={desktop.boolValue} " +
                                  $"allowQuest={quest.boolValue} IsAvailable={launcher.IsAvailable} " +
                                  $"prefabInstance={fromPrefab} url='{Trim(url)}'");

                if (desktop.boolValue && quest.boolValue)
                {
                    alreadyOk.Add($"{MainScenePath} :: {Path2(launcher.transform)}");
                    continue;
                }

                if (repair)
                {
                    desktop.boolValue = true;
                    quest.boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    sceneDirty = true;
                    report.AppendLine("    -> REPAIRED (scene object)");
                }

                repaired.Add($"{MainScenePath} :: {Path2(launcher.transform)}");
            }

            if (repair && sceneDirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene);
                report.AppendLine($"  scene saved: {saved}");
            }
            else if (repair)
            {
                report.AppendLine("  scene unchanged (no scene-level launcher needed repair)");
            }

            if (repair)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            report.AppendLine($"\n== Summary ==");
            report.AppendLine($"already correct: {alreadyOk.Count}");
            foreach (string a in alreadyOk.Distinct())
                report.AppendLine("  OK   " + a);
            report.AppendLine($"{(repair ? "repaired" : "needing repair")}: {repaired.Distinct().Count()}");
            foreach (string r in repaired.Distinct())
                report.AppendLine("  FIX  " + r);

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log("LINK_LAUNCHER_AUDIT\n" + report);
        }

        private static string Trim(string url) =>
            string.IsNullOrEmpty(url) ? "(empty)" : (url.Length > 60 ? url.Substring(0, 60) + "…" : url);

        private static string Path2(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
