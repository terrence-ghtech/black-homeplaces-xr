using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Static regression audit for the production architecture. Verifies that
    /// no production script polls the keyboard for world interaction outside
    /// the sanctioned files, that the quality tiers exist with the expected
    /// names, and that the Black Kitchen Addressables group points at local
    /// paths. Writes Docs/Production/VALIDATION_AUDIT.txt.
    /// </summary>
    public static class ProductionValidationAudit
    {
        static readonly string[] ProductionDirs =
        {
            "Assets/Scripts",
            "Assets/BCaT",
            "Assets/BCaT_assets",
        };

        // Files allowed to touch Keyboard.current: the central input providers
        // and the kiosk controller's activity/admin-chord tracking.
        static readonly string[] SanctionedKeyboardFiles =
        {
            "InteractionInput.cs",
            "KioskController.cs",
        };

        [MenuItem("BCaT/Production Setup/Validation Audit")]
        public static void Run()
        {
            var report = new StringBuilder();
            report.AppendLine($"BCaT production validation audit — {System.DateTime.Now:yyyy-MM-dd HH:mm}");
            bool ok = true;

            // 1. Interaction input ownership.
            report.AppendLine("\n== Keyboard polling audit ==");
            var offenders = new List<string>();
            foreach (string dir in ProductionDirs)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    if (file.Replace('\\', '/').Contains("/Editor/")) continue;
                    string name = Path.GetFileName(file);
                    if (SanctionedKeyboardFiles.Contains(name)) continue;

                    string text = File.ReadAllText(file);
                    // FocusedUiInput usage is fine; direct Keyboard.current is not.
                    foreach (Match m in Regex.Matches(text, @"Keyboard\.current|Input\.GetKey"))
                    {
                        offenders.Add($"{file}: {m.Value}");
                    }
                }
            }
            if (offenders.Count == 0)
            {
                report.AppendLine("PASS: no production script polls Keyboard.current/Input.GetKey directly.");
            }
            else
            {
                ok = false;
                report.AppendLine($"FAIL: {offenders.Count} direct polls remain:");
                foreach (string o in offenders.Distinct())
                    report.AppendLine("  " + o);
            }

            // 2. Quality tiers.
            report.AppendLine("\n== Quality tiers ==");
            string[] expected = { "Desktop Low", "Desktop Standard", "Desktop High", "Quest" };
            var names = QualitySettings.names;
            foreach (string tier in expected)
            {
                bool found = names.Contains(tier);
                report.AppendLine($"{(found ? "PASS" : "FAIL")}: tier '{tier}'" +
                                  (found ? "" : " missing"));
                ok &= found;
            }
            report.AppendLine("Tiers present: " + string.Join(", ", names));

            // 3. Black Kitchen group paths.
            report.AppendLine("\n== Black Kitchen Addressables group ==");
            string schemaPath =
                "Assets/AddressableAssetsData/AssetGroups/Schemas/BlackKitchen_Remote_BundledAssetGroupSchema.asset";
            if (File.Exists(schemaPath))
            {
                string schema = File.ReadAllText(schemaPath);
                // Local path profile variable ids from the Default profile.
                bool localBuild = schema.Contains("a5602186f69a14e258888b786aaf5f5a");
                bool localLoad = schema.Contains("10ec9f28dc9944d96bdda97f4e1d0b6d");
                report.AppendLine($"{(localBuild ? "PASS" : "FAIL")}: build path is Local.BuildPath");
                report.AppendLine($"{(localLoad ? "PASS" : "FAIL")}: load path is Local.LoadPath");
                ok &= localBuild && localLoad;
            }
            else
            {
                ok = false;
                report.AppendLine("FAIL: schema asset not found.");
            }

            // 4. Application identifier.
            report.AppendLine("\n== Player metadata ==");
            string id = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
            bool idOk = !id.Contains("UnityTechnologies") && !string.IsNullOrEmpty(id);
            report.AppendLine($"{(idOk ? "PASS" : "FAIL")}: Android identifier = '{id}'");
            ok &= idOk;

            report.AppendLine($"\nOVERALL: {(ok ? "PASS" : "FAIL")}");

            Directory.CreateDirectory("Docs/Production");
            File.WriteAllText("Docs/Production/VALIDATION_AUDIT.txt", report.ToString());
            Debug.Log(report.ToString());

            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
