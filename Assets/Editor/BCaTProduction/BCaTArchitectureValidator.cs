using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BCaT.Production.Interaction;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace BCaT.EditorTools
{
    public enum RuleSeverity
    {
        Info,
        Warning,
        Error,
    }

    /// <summary>One rule violation, located precisely enough to act on.</summary>
    public sealed class ValidationFinding
    {
        public string RuleId;
        public RuleSeverity Severity;
        public string Scene;
        public string ObjectPath;
        public string Message;

        public string Location =>
            string.IsNullOrEmpty(ObjectPath) ? Scene : Scene + " → " + ObjectPath;
    }

    /// <summary>
    /// The platform architecture rule catalogue, implemented as an automated
    /// check over every production scene, prefab, and production script.
    /// This is the mechanism that makes the architecture in
    /// Docs/Production/16_PLATFORM_ARCHITECTURE_REVIEW.md enforceable instead of
    /// aspirational: each rule has an id, a severity, and a location, and the
    /// build refuses to proceed when an Error-severity rule fails (see
    /// BCaTBuildValidationStep).
    ///
    /// Severity is data, not policy: rules ship as Warning while the migration
    /// they describe is in flight and are promoted to Error once the codebase
    /// satisfies them. RuleSeverities below is the single place that changes.
    ///
    ///   Unity -executeMethod BCaT.EditorTools.BCaTArchitectureValidator.RunBatch
    ///   Unity -executeMethod BCaT.EditorTools.BCaTArchitectureValidator.RunBatchStrict
    ///
    /// Reports: Docs/Production/ARCHITECTURE_VALIDATION.md (+ .json).
    /// Exit codes: 0 pass, 1 error-severity failure, 2 warnings only.
    /// </summary>
    public static class BCaTArchitectureValidator
    {
        // ---- Configuration ------------------------------------------------

        public const string PlatformGroupName = "Platform";
        public const string LegacyPlatformGroupName = "BuildProfiles";
        public const string DesktopBranchName = "Desktop";
        public const string QuestBranchName = "Quest";
        public const string LegacyDesktopBranchName = "Web";
        public const string LegacyQuestBranchName = "XR";
        public const string DevOnlyGroupName = "DevOnly";
        public const string SceneServicesGroupName = "SceneServices";

        static readonly string ReportPath = Path.Combine("Docs", "Production", "ARCHITECTURE_VALIDATION.md");
        static readonly string JsonPath = Path.Combine("Docs", "Production", "architecture_validation.json");

        /// <summary>
        /// Scenes that host a player rig and therefore carry the full platform
        /// contract.
        /// </summary>
        public static readonly string[] InhabitedScenes =
        {
            "Assets/BH_XR_MainScene.unity",
            "Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity",
        };

        /// <summary>
        /// Scenes that present but are not inhabited: no locomotion, no
        /// interaction, but they still need an XR-safe (head-tracked) camera.
        /// </summary>
        public static readonly string[] PresentationScenes =
        {
            "Assets/BCaT/ProductionCore/Scenes/MainMenuScene.unity",
            "Assets/BCaT/SceneTransitions/Scenes/LoadingScene.unity",
        };

        static readonly string[] ProductionScriptDirs =
        {
            "Assets/Scripts",
            "Assets/BCaT",
            "Assets/BCaT_assets",
        };

        // Files allowed to poll the keyboard directly (BCAT-L006).
        static readonly string[] SanctionedKeyboardFiles =
        {
            "InteractionInput.cs",
            "KioskController.cs",
        };

        // Files allowed to reach the raw platform APIs (BCAT-L005). Everything
        // else must ask BCaTPlatform.
        static readonly string[] SanctionedPlatformApiFiles =
        {
            "BCaTPlatform.cs",
            "BCaTPlatformProfile.cs",
            "PlatformCapabilities.cs",
            "PlatformInteractionPrompt.cs",
            "ApplicationModeService.cs",
            "RuntimeMediaPaths.cs",
            "BlackKitchenQuestTransitionDiagnostics.cs",
        };

        /// <summary>
        /// Rule severities. Promote a rule from Warning to Error only once the
        /// project satisfies it, so the build gate never blocks on known debt.
        /// </summary>
        static readonly Dictionary<string, RuleSeverity> RuleSeverities =
            new Dictionary<string, RuleSeverity>
            {
                // Hierarchy structure
                { "BCAT-P001", RuleSeverity.Warning }, // one ScenePlatformBinding per inhabited scene
                { "BCAT-P002", RuleSeverity.Warning }, // platform branches authored inactive
                { "BCAT-P003", RuleSeverity.Warning }, // one root Platform group, children ⊆ {Desktop,Quest}
                { "BCAT-P004", RuleSeverity.Warning }, // one EventSystem, exactly one input module
                { "BCAT-P005", RuleSeverity.Warning }, // one rig per kind, both under Platform/
                { "BCAT-P006", RuleSeverity.Warning }, // one XRInteractionManager under Platform/Quest

                // Platform leaks
                { "BCAT-L001", RuleSeverity.Warning }, // Quest components outside Platform/Quest
                { "BCAT-L002", RuleSeverity.Warning }, // Desktop components outside Platform/Desktop
                { "BCAT-L003", RuleSeverity.Warning }, // content inside Platform/
                { "BCAT-L004", RuleSeverity.Warning }, // DevOnly contents are editor-only
                { "BCAT-L005", RuleSeverity.Warning }, // raw platform API use outside sanctioned files
                { "BCAT-L006", RuleSeverity.Error   }, // keyboard polling (already satisfied)

                // Duplicates and orphans
                { "BCAT-D003", RuleSeverity.Warning }, // orphaned XRSimpleInteractable
                { "BCAT-D004", RuleSeverity.Warning }, // interactable unreachable by XRI casters
                { "BCAT-D005", RuleSeverity.Warning }, // missing script reference
                { "BCAT-D006", RuleSeverity.Warning }, // duplicate AudioListener

                // Interactable contract
                { "BCAT-Q001", RuleSeverity.Warning }, // trigger-only target needs an XR select surface
                { "BCAT-Q002", RuleSeverity.Warning }, // both platform prompts valid

                // Scene configuration
                { "BCAT-S001", RuleSeverity.Error   }, // transition scenes resolvable
                { "BCAT-S002", RuleSeverity.Error   }, // spawn ids resolvable
                { "BCAT-S003", RuleSeverity.Warning }, // MainCamera reachable per branch
                { "BCAT-S004", RuleSeverity.Warning }, // presentation scenes head-tracked on Quest
                { "BCAT-S005", RuleSeverity.Error   }, // quality tiers (already satisfied)
                { "BCAT-S006", RuleSeverity.Error   }, // BK Addressables local paths (already satisfied)
                { "BCAT-S007", RuleSeverity.Error   }, // Android identifier (already satisfied)
            };

        static readonly Dictionary<string, string> RuleTitles = new Dictionary<string, string>
        {
            { "BCAT-P001", "Exactly one ScenePlatformBinding per inhabited scene" },
            { "BCAT-P002", "Platform branches are authored inactive" },
            { "BCAT-P003", "One root Platform group with Desktop/Quest children only" },
            { "BCAT-P004", "One EventSystem per scene with exactly one input module" },
            { "BCAT-P005", "One rig per kind, both under Platform/" },
            { "BCAT-P006", "One XRInteractionManager, under Platform/Quest" },
            { "BCAT-L001", "No Quest-only components outside Platform/Quest" },
            { "BCAT-L002", "No Desktop-only components outside Platform/Desktop" },
            { "BCAT-L003", "Platform/ contains only rigs and platform services" },
            { "BCAT-L004", "DevOnly subtrees are editor-only" },
            { "BCAT-L005", "Raw platform APIs used only in sanctioned files" },
            { "BCAT-L006", "World-interaction keyboard polling is centralized" },
            { "BCAT-D003", "Every XRSimpleInteractable resolves to an interaction target" },
            { "BCAT-D004", "Every XRSimpleInteractable is reachable by XRI casters" },
            { "BCAT-D005", "No missing script references" },
            { "BCAT-D006", "At most one AudioListener per platform branch" },
            { "BCAT-Q001", "Trigger-only interaction targets carry an XR select surface" },
            { "BCAT-Q002", "Both desktop and XR prompts are valid" },
            { "BCAT-S001", "Transition destination scenes are loadable" },
            { "BCAT-S002", "Transition spawn ids resolve" },
            { "BCAT-S003", "Each platform branch has a MainCamera" },
            { "BCAT-S004", "Presentation scenes are head-tracked on Quest" },
            { "BCAT-S005", "Quality tiers exist with the expected names" },
            { "BCAT-S006", "Black Kitchen Addressables group uses local paths" },
            { "BCAT-S007", "Android application identifier is project-owned" },
        };

        // ---- Entry points -------------------------------------------------

        [MenuItem("BCaT/Architecture/Validate Architecture")]
        public static void ValidateFromMenu() => Run(false);

        [MenuItem("BCaT/Architecture/Validate Architecture (strict)")]
        public static void ValidateStrictFromMenu() => Run(true);

        public static void RunBatch() => Run(false);

        public static void RunBatchStrict() => Run(true);

        /// <summary>
        /// Collects findings without writing a report or exiting. Used by the
        /// pre-build validation step.
        /// </summary>
        public static List<ValidationFinding> Collect()
        {
            var findings = new List<ValidationFinding>();
            string originalScene = SceneManager.GetActiveScene().path;

            try
            {
                CheckProjectSettings(findings);
                CheckProductionScripts(findings);
                CheckSceneRegistration(findings);

                foreach (string path in InhabitedScenes)
                    ValidateScene(path, inhabited: true, findings);

                foreach (string path in PresentationScenes)
                    ValidateScene(path, inhabited: false, findings);
            }
            finally
            {
                if (!string.IsNullOrEmpty(originalScene) &&
                    SceneManager.GetActiveScene().path != originalScene &&
                    File.Exists(originalScene))
                {
                    EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
                }
            }

            return findings;
        }

        static void Run(bool strict)
        {
            List<ValidationFinding> findings = Collect();

            if (strict)
                foreach (var finding in findings)
                    if (finding.Severity == RuleSeverity.Warning)
                        finding.Severity = RuleSeverity.Error;

            int errors = findings.Count(f => f.Severity == RuleSeverity.Error);
            int warnings = findings.Count(f => f.Severity == RuleSeverity.Warning);

            WriteReports(findings, strict);

            string summary = $"[BCaTArchitectureValidator] {errors} error(s), {warnings} warning(s), " +
                             $"{findings.Count(f => f.Severity == RuleSeverity.Info)} info. Report: {ReportPath}";
            if (errors > 0) Debug.LogError(summary);
            else if (warnings > 0) Debug.LogWarning(summary);
            else Debug.Log(summary);

            if (Application.isBatchMode)
                EditorApplication.Exit(errors > 0 ? 1 : (warnings > 0 ? 2 : 0));
        }

        // ---- Scene validation ---------------------------------------------

        static void ValidateScene(string scenePath, bool inhabited, List<ValidationFinding> findings)
        {
            if (!File.Exists(scenePath))
            {
                Add(findings, "BCAT-S001", scenePath, "", "Scene file does not exist.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            string sceneName = Path.GetFileNameWithoutExtension(scenePath);
            GameObject[] roots = scene.GetRootGameObjects();
            var all = new List<GameObject>();
            foreach (GameObject root in roots)
                CollectRecursive(root, all);

            Transform platformGroup = FindPlatformGroup(roots, out bool legacyGroupName);
            Transform desktopBranch = FindBranch(platformGroup, DesktopBranchName, LegacyDesktopBranchName);
            Transform questBranch = FindBranch(platformGroup, QuestBranchName, LegacyQuestBranchName);

            CheckPlatformGroup(findings, sceneName, roots, platformGroup, legacyGroupName,
                desktopBranch, questBranch);
            bool hasBinding = all.Any(go => go.GetComponents<MonoBehaviour>()
                .Any(c => c != null && c.GetType().Name == "ScenePlatformBinding"));

            CheckBinding(findings, sceneName, all, inhabited);
            CheckEventSystems(findings, sceneName, all, hasBinding);
            CheckRigs(findings, sceneName, all, platformGroup, inhabited);
            CheckInteractionManagers(findings, sceneName, all, questBranch);
            CheckPlatformLeaks(findings, sceneName, all, desktopBranch, questBranch);
            CheckPlatformGroupContents(findings, sceneName, platformGroup, desktopBranch, questBranch);
            CheckDevOnly(findings, sceneName, all);
            CheckInteractables(findings, sceneName, all);
            CheckMissingScripts(findings, sceneName, all);
            CheckAudioListeners(findings, sceneName, all, desktopBranch, questBranch);
            CheckCameras(findings, sceneName, roots, desktopBranch, questBranch, inhabited);
            CheckSpawnPoints(findings, sceneName, all);
        }

        static void CollectRecursive(GameObject go, List<GameObject> into)
        {
            into.Add(go);
            foreach (Transform child in go.transform)
                CollectRecursive(child.gameObject, into);
        }

        static Transform FindPlatformGroup(GameObject[] roots, out bool legacyName)
        {
            legacyName = false;
            foreach (GameObject root in roots)
                if (root.name == PlatformGroupName)
                    return root.transform;

            foreach (GameObject root in roots)
            {
                if (root.name == LegacyPlatformGroupName)
                {
                    legacyName = true;
                    return root.transform;
                }
            }

            return null;
        }

        static Transform FindBranch(Transform group, string name, string legacyName)
        {
            if (group == null) return null;
            foreach (Transform child in group)
                if (child.name == name) return child;
            foreach (Transform child in group)
                if (child.name == legacyName) return child;
            return null;
        }

        // ---- BCAT-P003 -----------------------------------------------------

        static void CheckPlatformGroup(List<ValidationFinding> findings, string scene,
            GameObject[] roots, Transform group, bool legacyName,
            Transform desktopBranch, Transform questBranch)
        {
            int groupCount = roots.Count(r => r.name == PlatformGroupName || r.name == LegacyPlatformGroupName);

            if (group == null)
            {
                Add(findings, "BCAT-P003", scene, "",
                    $"No root GameObject named '{PlatformGroupName}'. Platform rigs and " +
                    "platform services must live in one root platform group.");
                return;
            }

            if (groupCount > 1)
                Add(findings, "BCAT-P003", scene, group.name,
                    $"{groupCount} root platform groups found; there must be exactly one.");

            if (legacyName)
                Add(findings, "BCAT-P003", scene, group.name,
                    $"Platform group is still named '{LegacyPlatformGroupName}'; rename to " +
                    $"'{PlatformGroupName}'. The name is load-bearing for legacy branch selection.");

            foreach (Transform child in group)
            {
                if (child.name == DesktopBranchName || child.name == QuestBranchName)
                    continue;

                if (child.name == LegacyDesktopBranchName || child.name == LegacyQuestBranchName)
                {
                    Add(findings, "BCAT-P003", scene, HierarchyPath(child),
                        $"Legacy platform branch name '{child.name}'; rename to " +
                        $"'{(child.name == LegacyDesktopBranchName ? DesktopBranchName : QuestBranchName)}'.");
                    continue;
                }

                if (child.name == "Shared")
                {
                    Add(findings, "BCAT-P003", scene, HierarchyPath(child),
                        "A 'Shared' branch is forbidden: shared content is the default and " +
                        "lives outside the platform group entirely.");
                    continue;
                }

                Add(findings, "BCAT-P003", scene, HierarchyPath(child),
                    $"Unexpected platform branch '{child.name}'; only " +
                    $"'{DesktopBranchName}' and '{QuestBranchName}' are allowed.");
            }

            // BCAT-P002: both branches must be authored inactive so no
            // wrong-platform component ever runs Awake.
            foreach (Transform branch in new[] { desktopBranch, questBranch })
            {
                if (branch == null) continue;
                if (branch.gameObject.activeSelf)
                    Add(findings, "BCAT-P002", scene, HierarchyPath(branch),
                        "Platform branch is authored ACTIVE. Both branches must be authored " +
                        "inactive; ScenePlatformBinding activates exactly one in Awake. An " +
                        "authored-active branch runs its Awake/OnEnable on the wrong platform.");
            }

            if (desktopBranch == null)
                Add(findings, "BCAT-P003", scene, group.name,
                    $"Platform group has no '{DesktopBranchName}' branch.");
            if (questBranch == null)
                Add(findings, "BCAT-P003", scene, group.name,
                    $"Platform group has no '{QuestBranchName}' branch.");
        }

        // ---- BCAT-P001 -----------------------------------------------------

        static void CheckBinding(List<ValidationFinding> findings, string scene,
            List<GameObject> all, bool inhabited)
        {
            var bindings = all
                .SelectMany(go => go.GetComponents<MonoBehaviour>().Where(c => c != null))
                .Where(c => c.GetType().Name == "ScenePlatformBinding")
                .ToList();

            if (bindings.Count == 0)
            {
                Add(findings, "BCAT-P001", scene, "",
                    "No ScenePlatformBinding. Every scene with a platform group must have " +
                    "exactly one binding, on an always-active object, to apply the resolved platform.");
                return;
            }

            if (bindings.Count > 1)
                Add(findings, "BCAT-P001", scene, HierarchyPath(bindings[1].transform),
                    $"{bindings.Count} ScenePlatformBinding components found; there must be exactly one.");

            foreach (var binding in bindings)
            {
                if (!binding.gameObject.activeInHierarchy)
                    Add(findings, "BCAT-P001", scene, HierarchyPath(binding.transform),
                        "ScenePlatformBinding is on an inactive object; it would never run.");
            }

            // Legacy selector must not coexist once a binding is present.
            var legacySelectors = all
                .SelectMany(go => go.GetComponents<MonoBehaviour>().Where(c => c != null))
                .Where(c => c.GetType().Name == "ScenePlatformRigSelector")
                .ToList();
            foreach (var selector in legacySelectors)
                Add(findings, "BCAT-P001", scene, HierarchyPath(selector.transform),
                    "ScenePlatformRigSelector coexists with ScenePlatformBinding: two platform " +
                    "authorities for one decision. Remove the legacy selector.");

        }

        // ---- BCAT-P004 -----------------------------------------------------

        static void CheckEventSystems(List<ValidationFinding> findings, string scene,
            List<GameObject> all, bool hasBinding)
        {
            var eventSystems = all.Select(go => go.GetComponent<EventSystem>())
                .Where(es => es != null).ToList();

            if (eventSystems.Count == 0)
                return; // scenes with no UI need no EventSystem

            if (eventSystems.Count > 1)
            {
                foreach (var es in eventSystems.Skip(1))
                    Add(findings, "BCAT-P004", scene, HierarchyPath(es.transform),
                        $"{eventSystems.Count} EventSystems in this scene. There must be exactly one, " +
                        "under SceneServices/UI, whose input module is chosen at runtime by " +
                        "ScenePlatformBinding.");
            }

            foreach (var es in eventSystems)
            {
                var modules = es.GetComponents<BaseInputModule>();
                if (modules.Length == 0)
                {
                    // With a binding present, zero authored modules is the
                    // intended state: ScenePlatformBinding assigns the profile's
                    // module in Awake, so there is exactly one owner.
                    if (!hasBinding)
                        Add(findings, "BCAT-P004", scene, HierarchyPath(es.transform),
                            "EventSystem has no BaseInputModule and the scene has no " +
                            "ScenePlatformBinding to assign one: UI pointer events are dead.");
                }
                else if (modules.Length >= 1 && hasBinding)
                {
                    Add(findings, "BCAT-P004", scene, HierarchyPath(es.transform),
                        $"EventSystem has {modules.Length} authored input module(s) " +
                        $"({string.Join(", ", modules.Select(m => m.GetType().Name))}) while a " +
                        "ScenePlatformBinding owns module assignment. Author no module so the " +
                        "platform chooses.");
                }
                else if (modules.Length > 1)
                {
                    Add(findings, "BCAT-P004", scene, HierarchyPath(es.transform),
                        $"EventSystem has {modules.Length} input modules " +
                        $"({string.Join(", ", modules.Select(m => m.GetType().Name))}). " +
                        "Exactly one module must be active.");
                }
            }
        }

        // ---- BCAT-P005 -----------------------------------------------------

        static void CheckRigs(List<ValidationFinding> findings, string scene,
            List<GameObject> all, Transform platformGroup, bool inhabited)
        {
            var rigs = all.Select(go => go.GetComponent<ScenePlayerRig>())
                .Where(r => r != null).ToList();

            if (!inhabited)
            {
                foreach (var rig in rigs)
                    Add(findings, "BCAT-P005", scene, HierarchyPath(rig.transform),
                        "Presentation scene contains a player rig; presentation scenes carry a " +
                        "head-tracked camera only.");
                return;
            }

            foreach (ScenePlayerRig.RigKind kind in Enum.GetValues(typeof(ScenePlayerRig.RigKind)))
            {
                int count = rigs.Count(r => r.Kind == kind);
                if (count == 0)
                    Add(findings, "BCAT-P005", scene, "",
                        $"No ScenePlayerRig of kind {kind}. Inhabited scenes must carry both rigs.");
                else if (count > 1)
                    Add(findings, "BCAT-P005", scene, "",
                        $"{count} ScenePlayerRig components of kind {kind}; there must be exactly one.");
            }

            foreach (var rig in rigs)
            {
                if (platformGroup == null || !rig.transform.IsChildOf(platformGroup))
                    Add(findings, "BCAT-P005", scene, HierarchyPath(rig.transform),
                        $"Rig (kind={rig.Kind}) is outside the platform group; rigs must live under " +
                        $"{PlatformGroupName}/{DesktopBranchName} or {PlatformGroupName}/{QuestBranchName}.");
            }
        }

        // ---- BCAT-P006 -----------------------------------------------------

        static void CheckInteractionManagers(List<ValidationFinding> findings, string scene,
            List<GameObject> all, Transform questBranch)
        {
            var managers = all.Select(go => go.GetComponent<XRInteractionManager>())
                .Where(m => m != null).ToList();

            if (managers.Count > 1)
                Add(findings, "BCAT-P006", scene, HierarchyPath(managers[1].transform),
                    $"{managers.Count} XRInteractionManagers; there must be exactly one.");

            foreach (var manager in managers)
            {
                if (questBranch == null || !manager.transform.IsChildOf(questBranch))
                    Add(findings, "BCAT-P006", scene, HierarchyPath(manager.transform),
                        $"XRInteractionManager is outside {PlatformGroupName}/{QuestBranchName}.");
            }
        }

        // ---- BCAT-L001 / BCAT-L002 -----------------------------------------

        static readonly Type[] QuestOnlyTypes =
        {
            typeof(XROrigin),
            typeof(XRInteractionManager),
            typeof(XRUIInputModule),
            typeof(NearFarInteractor),
            typeof(XRRayInteractor),
            typeof(XRPokeInteractor),
        };

        static readonly string[] QuestOnlyTypeNames =
        {
            "XRDeviceSimulator",
            "XRInputModalityManager",
            "TrackedPoseDriver",
        };

        // CharacterController is deliberately absent: the XRI rig carries one
        // for locomotion collision, so it is not a desktop marker.
        static readonly Type[] DesktopOnlyTypes =
        {
            typeof(StarterAssets.FirstPersonController),
            typeof(StarterAssets.StarterAssetsInputs),
        };

        static void CheckPlatformLeaks(List<ValidationFinding> findings, string scene,
            List<GameObject> all, Transform desktopBranch, Transform questBranch)
        {
            foreach (GameObject go in all)
            {
                bool inQuest = questBranch != null && go.transform.IsChildOf(questBranch);
                bool inDesktop = desktopBranch != null && go.transform.IsChildOf(desktopBranch);

                foreach (Component component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    Type type = component.GetType();

                    bool questOnly = QuestOnlyTypes.Any(t => t.IsAssignableFrom(type)) ||
                                     QuestOnlyTypeNames.Contains(type.Name);
                    if (questOnly && !inQuest)
                    {
                        // The single shared EventSystem's module is runtime-assigned;
                        // an authored XRUIInputModule outside the Quest branch is
                        // exactly the leak this rule exists to catch, so no exemption.
                        Add(findings, "BCAT-L001", scene, HierarchyPath(go.transform),
                            $"Quest-only component '{type.Name}' is outside " +
                            $"{PlatformGroupName}/{QuestBranchName}.");
                    }

                    bool desktopOnly = DesktopOnlyTypes.Any(t => t.IsAssignableFrom(type));
                    if (desktopOnly && !inDesktop)
                        Add(findings, "BCAT-L002", scene, HierarchyPath(go.transform),
                            $"Desktop-only component '{type.Name}' is outside " +
                            $"{PlatformGroupName}/{DesktopBranchName}.");

                    if (type == typeof(InputSystemUIInputModule) && (inQuest || inDesktop))
                        Add(findings, "BCAT-L002", scene, HierarchyPath(go.transform),
                            "InputSystemUIInputModule is inside a platform branch; the scene's " +
                            "single EventSystem lives under SceneServices/UI and its module is " +
                            "assigned at runtime.");
                }
            }
        }

        // ---- BCAT-L003 -----------------------------------------------------

        static readonly Type[] ForbiddenInsidePlatform =
        {
            typeof(MeshRenderer),
            typeof(SkinnedMeshRenderer),
            typeof(Terrain),
            typeof(AudioSource),
            typeof(UnityEngine.Video.VideoPlayer),
            typeof(Light),
        };

        static void CheckPlatformGroupContents(List<ValidationFinding> findings, string scene,
            Transform platformGroup, Transform desktopBranch, Transform questBranch)
        {
            if (platformGroup == null) return;

            foreach (Transform t in platformGroup.GetComponentsInChildren<Transform>(true))
            {
                if (t == platformGroup) continue;

                // Dev aids and rig-internal visuals are exempt: rigs legitimately
                // contain renderers (controller models, reticles) and audio.
                bool rigInternal = IsUnderRig(t);
                bool devOnly = IsUnderNamed(t, DevOnlyGroupName);
                if (rigInternal || devOnly) continue;

                foreach (Component component in t.GetComponents<Component>())
                {
                    if (component == null) continue;
                    if (ForbiddenInsidePlatform.Any(f => f.IsAssignableFrom(component.GetType())))
                        Add(findings, "BCAT-L003", scene, HierarchyPath(t),
                            $"Content component '{component.GetType().Name}' inside the platform " +
                            "group. Platform branches hold rigs and platform services only.");
                }
            }
        }

        /// <summary>
        /// Rig-internal: rigs legitimately contain renderers (controller models,
        /// reticles) and an audio listener. Both the player rigs and the
        /// presentation rigs count — the latter carry no ScenePlayerRig marker
        /// because they are not inhabited, so XROrigin is checked too.
        /// </summary>
        static bool IsUnderRig(Transform t)
        {
            Transform cursor = t;
            while (cursor != null)
            {
                if (cursor.GetComponent<ScenePlayerRig>() != null) return true;
                if (cursor.GetComponent<XROrigin>() != null) return true;
                cursor = cursor.parent;
            }
            return false;
        }

        static bool IsUnderNamed(Transform t, string name)
        {
            Transform cursor = t;
            while (cursor != null)
            {
                if (cursor.name == name) return true;
                cursor = cursor.parent;
            }
            return false;
        }

        // ---- BCAT-L004 -----------------------------------------------------

        static void CheckDevOnly(List<ValidationFinding> findings, string scene, List<GameObject> all)
        {
            foreach (GameObject go in all)
            {
                if (go.name != DevOnlyGroupName) continue;

                // The stripper destroys the whole marked GameObject, so a marker
                // on the group (or on any ancestor inside it) covers everything
                // beneath it. Only an unmarked subtree is a finding.
                bool groupMarked = HasMarkerAtOrAbove(go.transform, go.transform);
                if (groupMarked)
                    continue;

                foreach (Transform child in go.transform)
                {
                    if (!HasMarkerAtOrAbove(child, go.transform))
                        Add(findings, "BCAT-L004", scene, HierarchyPath(child),
                            "Object inside a DevOnly group is covered by no EditorOnlyObject marker " +
                            "(on itself, the group, or an ancestor inside the group); it would ship " +
                            "in player builds.");
                }
            }
        }

        static bool HasMarkerAtOrAbove(Transform t, Transform stopAtInclusive)
        {
            Transform cursor = t;
            while (cursor != null)
            {
                if (cursor.GetComponents<MonoBehaviour>()
                    .Any(c => c != null && c.GetType().Name == "EditorOnlyObject"))
                    return true;
                if (cursor == stopAtInclusive)
                    return false;
                cursor = cursor.parent;
            }
            return false;
        }

        // ---- BCAT-D003 / BCAT-D004 / BCAT-Q001 / BCAT-Q002 -----------------

        static void CheckInteractables(List<ValidationFinding> findings, string scene, List<GameObject> all)
        {
            foreach (GameObject go in all)
            {
                var interactable = go.GetComponent<XRSimpleInteractable>();
                if (interactable != null)
                {
                    if (!HasDispatchPath(interactable))
                        Add(findings, "BCAT-D003", scene, HierarchyPath(go.transform),
                            "XRSimpleInteractable has no dispatch path: no IInteractionTarget on " +
                            "itself or an ancestor, no select relay with a receiver, no XrSelectSurface, " +
                            "and no persistent selectEntered listener. Selecting it does nothing.");

                    if (!HasCasterReachableCollider(interactable))
                        Add(findings, "BCAT-D004", scene, HierarchyPath(go.transform),
                            "XRSimpleInteractable has no non-trigger collider reachable by the XRI " +
                            "casters (both ignore triggers), so it is invisible in headset: no " +
                            "hover, no prompt, no select.");
                }

                foreach (MonoBehaviour behaviour in go.GetComponents<MonoBehaviour>())
                {
                    if (behaviour is not IInteractionTarget target) continue;

                    CheckTargetPrompts(findings, scene, go, target);
                    CheckTargetXrSurface(findings, scene, go, behaviour);
                }
            }
        }

        static void CheckTargetPrompts(List<ValidationFinding> findings, string scene,
            GameObject go, IInteractionTarget target)
        {
            string desktop;
            string xr;
            try
            {
                desktop = target.GetPrompt(false);
                xr = target.GetPrompt(true);
            }
            catch (Exception e)
            {
                Add(findings, "BCAT-Q002", scene, HierarchyPath(go.transform),
                    $"GetPrompt threw: {e.GetType().Name}: {e.Message}");
                return;
            }

            // An empty shared-HUD prompt is correct for the two sanctioned
            // world-space prompt systems (Black Kitchen entrance, Front Home
            // Privacy Zones hologram): the floating prompt is the only
            // affordance there, and an empty HUD string is what prevents a
            // duplicate. Those targets expose the wording through
            // WorldPromptText(bool) instead, so accept an empty prompt when that
            // returns text for the same platform.
            if (string.IsNullOrWhiteSpace(desktop) && !HasWorldPromptText(target, false))
                Add(findings, "BCAT-Q002", scene, HierarchyPath(go.transform),
                    "Desktop prompt is empty and no world-space prompt supplies wording.");
            if (string.IsNullOrWhiteSpace(xr) && !HasWorldPromptText(target, true))
                Add(findings, "BCAT-Q002", scene, HierarchyPath(go.transform),
                    "XR prompt is empty and no world-space prompt supplies wording.");

            if (!string.IsNullOrWhiteSpace(xr))
            {
                foreach (string forbidden in new[] { "Press ", "press ", "key", "click", "Click" })
                {
                    if (xr.Contains(forbidden))
                    {
                        Add(findings, "BCAT-Q002", scene, HierarchyPath(go.transform),
                            $"XR prompt '{xr}' contains keyboard/mouse wording ('{forbidden}'); " +
                            "Quest has neither.");
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// The project's convention for the two sanctioned world-space prompts:
        /// the target exposes the wording through WorldPromptText(bool) and
        /// deliberately returns an empty shared-HUD prompt on that platform.
        /// </summary>
        static bool HasWorldPromptText(IInteractionTarget target, bool xr)
        {
            var method = target.GetType().GetMethod("WorldPromptText",
                new[] { typeof(bool) });
            if (method == null || method.ReturnType != typeof(string))
                return false;

            try
            {
                return !string.IsNullOrWhiteSpace(method.Invoke(target, new object[] { xr }) as string);
            }
            catch
            {
                return false;
            }
        }

        static void CheckTargetXrSurface(List<ValidationFinding> findings, string scene,
            GameObject go, MonoBehaviour behaviour)
        {
            Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0) return;

            bool hasNonTrigger = colliders.Any(c => c != null && !c.isTrigger);
            if (hasNonTrigger) return;

            bool hasSurface = go.GetComponentsInChildren<MonoBehaviour>(true)
                .Any(c => c != null &&
                          (c.GetType().Name == "XrSelectSurface" ||
                           c.GetType().Name == "QuestXrSelectCollider"));

            if (!hasSurface)
                Add(findings, "BCAT-Q001", scene, HierarchyPath(go.transform),
                    $"Interaction target '{behaviour.GetType().Name}' has only trigger colliders " +
                    "and no XR select surface, so it is unreachable by the XRI casters on Quest. " +
                    "Add an XrSelectSurface component.");
        }

        static bool HasCasterReachableCollider(XRSimpleInteractable interactable)
        {
            // A QuestXrSelectCollider / XrSelectSurface makes its colliders
            // non-trigger at Awake, so authored trigger state is not the answer.
            if (interactable.GetComponents<MonoBehaviour>().Any(c => c != null &&
                    (c.GetType().Name == "QuestXrSelectCollider" || c.GetType().Name == "XrSelectSurface")))
                return true;

            if (interactable.GetComponentsInChildren<MonoBehaviour>(true).Any(c => c != null &&
                    (c.GetType().Name == "QuestXrSelectCollider" || c.GetType().Name == "XrSelectSurface")))
                return true;

            IReadOnlyList<Collider> declared = interactable.colliders;
            if (declared != null && declared.Count > 0)
                return declared.Any(c => c != null && !c.isTrigger);

            return interactable.GetComponentsInChildren<Collider>(true)
                .Any(c => c != null && !c.isTrigger);
        }

        /// <summary>
        /// Whether selecting this interactable can reach anything. Deliberately
        /// permissive about *how*: a router target, an exclusive-zone station
        /// reached through a select relay, an XrSelectSurface, or a persistent
        /// UnityEvent listener are all valid dispatch paths in this project.
        /// </summary>
        static bool HasDispatchPath(XRSimpleInteractable interactable)
        {
            foreach (MonoBehaviour behaviour in interactable.GetComponentsInParent<MonoBehaviour>(true))
                if (behaviour is IInteractionTarget) return true;

            foreach (MonoBehaviour behaviour in interactable.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null) continue;
                string typeName = behaviour.GetType().Name;
                if (typeName != "BlackKitchenXrSelectRelay" && typeName != "XrSelectSurface")
                    continue;

                if (typeName == "XrSelectSurface") return true;

                // A relay dispatches through its receiver (router target,
                // exclusive-zone station, or SendMessage), so any receiver counts.
                var field = behaviour.GetType().GetField("receiver",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public);
                if (field?.GetValue(behaviour) as UnityEngine.Object != null) return true;
            }

            // A surface on a child (the legacy *_QuestXRSelect twin) forwards up.
            if (interactable.GetComponentsInChildren<MonoBehaviour>(true).Any(c => c != null &&
                    (c.GetType().Name == "XrSelectSurface" ||
                     c.GetType().Name == "BlackKitchenXrSelectRelay")))
                return true;

            return interactable.selectEntered.GetPersistentEventCount() > 0;
        }

        // ---- BCAT-D005 / BCAT-D006 -----------------------------------------

        static void CheckMissingScripts(List<ValidationFinding> findings, string scene, List<GameObject> all)
        {
            foreach (GameObject go in all)
            {
                Component[] components = go.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] == null)
                        Add(findings, "BCAT-D005", scene, HierarchyPath(go.transform),
                            $"Component slot {i} is a missing script reference.");
                }
            }
        }

        static void CheckAudioListeners(List<ValidationFinding> findings, string scene,
            List<GameObject> all, Transform desktopBranch, Transform questBranch)
        {
            var listeners = all.Select(go => go.GetComponent<AudioListener>())
                .Where(l => l != null).ToList();

            // One listener per platform branch is correct — only one branch is
            // ever active. The defect is two listeners that could be active
            // simultaneously, i.e. two outside every branch, or one outside plus
            // any branch-owned listener.
            var free = listeners.Where(l =>
                !IsUnderRig(l.transform) &&
                (desktopBranch == null || !l.transform.IsChildOf(desktopBranch)) &&
                (questBranch == null || !l.transform.IsChildOf(questBranch))).ToList();

            if (free.Count > 1)
            {
                Add(findings, "BCAT-D006", scene, HierarchyPath(free[1].transform),
                    $"{free.Count} AudioListeners outside any player rig; at most one listener may " +
                    "be active at a time.");
            }
            else if (free.Count == 1 && listeners.Count > free.Count)
            {
                Add(findings, "BCAT-D006", scene, HierarchyPath(free[0].transform),
                    "An AudioListener outside the player rigs coexists with rig-owned listeners; " +
                    "the activated rig would produce a second active listener.");
            }
        }

        // ---- BCAT-S003 / BCAT-S004 -----------------------------------------

        static void CheckCameras(List<ValidationFinding> findings, string scene,
            GameObject[] roots, Transform desktopBranch, Transform questBranch, bool inhabited)
        {
            foreach ((Transform branch, string label) in new[]
                     {
                         (desktopBranch, DesktopBranchName),
                         (questBranch, QuestBranchName),
                     })
            {
                if (branch == null) continue;

                Camera[] cameras = branch.GetComponentsInChildren<Camera>(true);
                if (!cameras.Any(c => c != null && c.CompareTag("MainCamera")))
                    Add(findings, "BCAT-S003", scene, HierarchyPath(branch),
                        $"Platform branch '{label}' has no camera tagged MainCamera; " +
                        "Camera.main would be null or resolve to another branch.");
            }

            if (inhabited)
                return;

            // Presentation scenes: a head-locked camera in a headset is a
            // comfort hazard, so the Quest branch camera must be head-tracked.
            if (questBranch == null)
            {
                bool anyCamera = roots.Any(r => r.GetComponentsInChildren<Camera>(true).Length > 0);
                if (anyCamera)
                    Add(findings, "BCAT-S004", scene, "",
                        "Presentation scene has a camera but no Quest presentation branch: on " +
                        "Quest the view would be head-locked (no tracked pose) for the whole scene.");
                return;
            }

            bool tracked = questBranch.GetComponentsInChildren<Component>(true)
                .Any(c => c != null && c.GetType().Name == "TrackedPoseDriver");
            if (!tracked)
                Add(findings, "BCAT-S004", scene, HierarchyPath(questBranch),
                    "Quest presentation branch has no TrackedPoseDriver; the view would be " +
                    "head-locked in headset.");
        }

        // ---- BCAT-S002 -----------------------------------------------------

        static void CheckSpawnPoints(List<ValidationFinding> findings, string scene, List<GameObject> all)
        {
            var byId = new Dictionary<string, string>();
            foreach (GameObject go in all)
            {
                var spawn = go.GetComponent<SceneSpawnPoint>();
                if (spawn == null) continue;

                if (string.IsNullOrWhiteSpace(spawn.SpawnId))
                {
                    Add(findings, "BCAT-S002", scene, HierarchyPath(go.transform),
                        "SceneSpawnPoint has an empty spawn id.");
                    continue;
                }

                if (byId.TryGetValue(spawn.SpawnId, out string existing))
                    Add(findings, "BCAT-S002", scene, HierarchyPath(go.transform),
                        $"Duplicate spawn id '{spawn.SpawnId}' (also on '{existing}').");
                else
                    byId[spawn.SpawnId] = HierarchyPath(go.transform);
            }

            string expected = scene switch
            {
                "BlackKitchen_MemoryScene" => SceneTransitionState.BlackKitchenEntrySpawnId,
                "BH_XR_MainScene" => SceneTransitionState.MainHouseKitchenReturnSpawnId,
                _ => null,
            };

            if (expected != null && !byId.ContainsKey(expected))
                Add(findings, "BCAT-S002", scene, "",
                    $"Scene has no SceneSpawnPoint with the transition spawn id '{expected}'.");
        }

        // ---- BCAT-S001 -----------------------------------------------------

        static void CheckSceneRegistration(List<ValidationFinding> findings)
        {
            var enabled = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => Path.GetFileNameWithoutExtension(s.path))
                .ToHashSet();

            foreach (string required in new[]
                     {
                         SceneTransitionState.MainHouseSceneName,
                         SceneTransitionState.LoadingSceneName,
                     })
            {
                if (!enabled.Contains(required))
                    Add(findings, "BCAT-S001", "EditorBuildSettings", "",
                        $"Scene '{required}' is referenced by SceneTransitionState but is not an " +
                        "enabled build scene.");
            }

            // The Black Kitchen must remain Addressables-loaded: enabling it in
            // build settings silently switches LoadingSceneController to the
            // built-in path and dormantizes the bundle release logic.
            if (enabled.Contains(SceneTransitionState.BlackKitchenSceneName))
                Add(findings, "BCAT-S001", "EditorBuildSettings", "",
                    $"Scene '{SceneTransitionState.BlackKitchenSceneName}' is an ENABLED build scene. " +
                    "It must stay Addressables-loaded; otherwise " +
                    "Application.CanStreamedLevelBeLoaded routes it down the built-in path and the " +
                    "Addressables bundle release logic never runs.");
        }

        // ---- BCAT-S005 / S006 / S007 ---------------------------------------

        static void CheckProjectSettings(List<ValidationFinding> findings)
        {
            string[] expectedTiers = { "Desktop Low", "Desktop Standard", "Desktop High", "Quest" };
            string[] names = QualitySettings.names;
            foreach (string tier in expectedTiers)
                if (!names.Contains(tier))
                    Add(findings, "BCAT-S005", "QualitySettings", "",
                        $"Quality tier '{tier}' is missing. Present: {string.Join(", ", names)}");

            const string schemaPath =
                "Assets/AddressableAssetsData/AssetGroups/Schemas/BlackKitchen_Remote_BundledAssetGroupSchema.asset";
            if (!File.Exists(schemaPath))
            {
                Add(findings, "BCAT-S006", "Addressables", "",
                    "Black Kitchen bundled asset group schema not found.");
            }
            else
            {
                string schema = File.ReadAllText(schemaPath);
                if (!schema.Contains("a5602186f69a14e258888b786aaf5f5a"))
                    Add(findings, "BCAT-S006", "Addressables", "",
                        "Black Kitchen group build path is not Local.BuildPath.");
                if (!schema.Contains("10ec9f28dc9944d96bdda97f4e1d0b6d"))
                    Add(findings, "BCAT-S006", "Addressables", "",
                        "Black Kitchen group load path is not Local.LoadPath.");
            }

            string id = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
            if (string.IsNullOrEmpty(id) || id.Contains("UnityTechnologies") || id.Contains("DefaultCompany"))
                Add(findings, "BCAT-S007", "PlayerSettings", "",
                    $"Android application identifier is not project-owned: '{id}'.");
        }

        // ---- BCAT-L005 / BCAT-L006 -----------------------------------------

        static void CheckProductionScripts(List<ValidationFinding> findings)
        {
            var keyboardPattern = new Regex(@"Keyboard\.current|Input\.GetKey");
            // Platform *decision* APIs only. UNITY_WEBGL is deliberately absent:
            // WebGL is an out-of-scope target and its remaining media branches
            // are dead-code cleanup, not a second platform authority.
            var platformApiPattern = new Regex(
                @"XRSettings\.|XRGeneralSettings|RuntimePlatform\.|UNITY_ANDROID|UNITY_STANDALONE");

            foreach (string dir in ProductionScriptDirs)
            {
                if (!Directory.Exists(dir)) continue;

                foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    string normalized = file.Replace('\\', '/');
                    if (normalized.Contains("/Editor/")) continue;

                    string name = Path.GetFileName(file);
                    string text = File.ReadAllText(file);

                    if (!SanctionedKeyboardFiles.Contains(name))
                    {
                        foreach (Match match in keyboardPattern.Matches(text))
                            Add(findings, "BCAT-L006", normalized, "",
                                $"Direct keyboard poll '{match.Value}'. World interaction input must " +
                                "go through the InteractionRouter's input providers.");
                    }

                    if (!SanctionedPlatformApiFiles.Contains(name))
                    {
                        var seen = new HashSet<string>();
                        foreach (Match match in platformApiPattern.Matches(text))
                        {
                            if (!seen.Add(match.Value)) continue;
                            Add(findings, "BCAT-L005", normalized, "",
                                $"Raw platform API/define '{match.Value}' outside a sanctioned file. " +
                                "Ask BCaTPlatform instead so there is one platform authority.");
                        }
                    }
                }
            }
        }

        // ---- Reporting -----------------------------------------------------

        static void Add(List<ValidationFinding> findings, string ruleId, string scene,
            string objectPath, string message)
        {
            findings.Add(new ValidationFinding
            {
                RuleId = ruleId,
                Severity = RuleSeverities.TryGetValue(ruleId, out RuleSeverity s) ? s : RuleSeverity.Warning,
                Scene = scene,
                ObjectPath = objectPath,
                Message = message,
            });
        }

        static string HierarchyPath(Transform t)
        {
            if (t == null) return "(null)";
            var parts = new List<string>();
            Transform cursor = t;
            while (cursor != null)
            {
                parts.Add(cursor.name);
                cursor = cursor.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        static void WriteReports(List<ValidationFinding> findings, bool strict)
        {
            Directory.CreateDirectory(System.IO.Path.Combine("Docs", "Production"));

            var md = new StringBuilder();
            md.AppendLine("# BCaT Architecture Validation");
            md.AppendLine();
            md.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm} · mode: {(strict ? "strict" : "report")}");
            md.AppendLine();

            int errors = findings.Count(f => f.Severity == RuleSeverity.Error);
            int warnings = findings.Count(f => f.Severity == RuleSeverity.Warning);
            md.AppendLine($"**{errors} error(s), {warnings} warning(s).** " +
                          (errors == 0 ? "No error-severity rule is failing; the build gate passes." : "The build gate blocks."));
            md.AppendLine();

            md.AppendLine("## Summary by rule");
            md.AppendLine();
            md.AppendLine("| Rule | Severity | Failures | Title |");
            md.AppendLine("|---|---|---|---|");
            foreach (var pair in RuleSeverities.OrderBy(p => p.Key))
            {
                int count = findings.Count(f => f.RuleId == pair.Key);
                RuleSeverity effective = strict && pair.Value == RuleSeverity.Warning
                    ? RuleSeverity.Error : pair.Value;
                string mark = count == 0 ? "PASS" : count.ToString();
                md.AppendLine($"| {pair.Key} | {effective} | {mark} | " +
                              $"{(RuleTitles.TryGetValue(pair.Key, out string t) ? t : "")} |");
            }
            md.AppendLine();

            if (findings.Count > 0)
            {
                md.AppendLine("## Findings");
                foreach (var group in findings.GroupBy(f => f.RuleId).OrderBy(g => g.Key))
                {
                    md.AppendLine();
                    md.AppendLine($"### {group.Key} — {(RuleTitles.TryGetValue(group.Key, out string t) ? t : "")}");
                    md.AppendLine();
                    foreach (var finding in group.OrderBy(f => f.Location))
                        md.AppendLine($"- `{finding.Location}` — {finding.Message}");
                }
            }

            File.WriteAllText(ReportPath, md.ToString());

            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine($"  \"generated\": \"{DateTime.Now:o}\",");
            json.AppendLine($"  \"strict\": {(strict ? "true" : "false")},");
            json.AppendLine($"  \"errors\": {errors},");
            json.AppendLine($"  \"warnings\": {warnings},");
            json.AppendLine("  \"findings\": [");
            for (int i = 0; i < findings.Count; i++)
            {
                ValidationFinding f = findings[i];
                json.AppendLine("    {" +
                    $"\"rule\": \"{f.RuleId}\", " +
                    $"\"severity\": \"{f.Severity}\", " +
                    $"\"scene\": \"{Escape(f.Scene)}\", " +
                    $"\"object\": \"{Escape(f.ObjectPath)}\", " +
                    $"\"message\": \"{Escape(f.Message)}\"" +
                    "}" + (i < findings.Count - 1 ? "," : ""));
            }
            json.AppendLine("  ]");
            json.AppendLine("}");
            File.WriteAllText(JsonPath, json.ToString());
        }

        static string Escape(string value) =>
            string.IsNullOrEmpty(value) ? "" : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
