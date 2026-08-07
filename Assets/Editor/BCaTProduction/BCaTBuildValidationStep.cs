using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Runs the architecture validator before every player build and aborts on
    /// any Error-severity finding.
    ///
    /// This is what makes "no manual hierarchy change should ever be required
    /// before building" true rather than aspirational: a scene that would need
    /// hand-fixing fails the build with a named location instead of shipping.
    /// Before this existed, the pipeline validated Addressables output and the
    /// APK contents thoroughly but never checked the scenes, so a Quest build
    /// would happily ship a scene whose desktop rig awoke and whose EventSystem
    /// had no input module.
    ///
    /// Skipped when -bcatSkipArchitectureValidation is passed, for the rare case
    /// of needing a diagnostic build from a known-broken tree.
    /// </summary>
    public sealed class BCaTBuildValidationStep : IPreprocessBuildWithReport
    {
        public const string SkipArgument = "-bcatSkipArchitectureValidation";

        // Run before other preprocessors so a failure costs no build time.
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (System.Environment.GetCommandLineArgs().Contains(SkipArgument))
            {
                Debug.LogWarning($"[BCaTBuildValidation] Skipped: {SkipArgument} was passed. " +
                                 "The resulting build is not architecture-validated.");
                return;
            }

            Debug.Log("[BCaTBuildValidation] Validating the platform architecture before building…");
            List<ValidationFinding> findings = BCaTArchitectureValidator.Collect();

            var errors = findings.Where(f => f.Severity == RuleSeverity.Error).ToList();
            int warnings = findings.Count(f => f.Severity == RuleSeverity.Warning);

            foreach (ValidationFinding warning in findings.Where(f => f.Severity == RuleSeverity.Warning))
                Debug.LogWarning($"[BCaTBuildValidation] {warning.RuleId} {warning.Location}: {warning.Message}");

            if (errors.Count == 0)
            {
                Debug.Log($"[BCaTBuildValidation] PASS — 0 errors, {warnings} warning(s).");
                return;
            }

            foreach (ValidationFinding error in errors)
                Debug.LogError($"[BCaTBuildValidation] {error.RuleId} {error.Location}: {error.Message}");

            throw new BuildFailedException(
                $"[BCaTBuildValidation] {errors.Count} architecture error(s) — build aborted. " +
                "See the errors above and Docs/Production/ARCHITECTURE_VALIDATION.md. " +
                $"Pass {SkipArgument} only for a deliberate diagnostic build.");
        }
    }
}
