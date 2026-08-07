using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BCaT.Production;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Play Mode contract test for the platform layer: asserts the RESOLVED
    /// runtime state, not the authored state. This is the harness that proves a
    /// scene migration is correct, so it is deliberately written before the
    /// scenes are touched.
    ///
    /// It runs the same scene twice — once forced Desktop, once forced Quest
    /// (simulated) — and checks, per platform:
    ///   R001  exactly one active player rig, of the resolved platform's kind
    ///   R002  no duplicate rigs (one XROrigin, one desktop controller)
    ///   R003  exactly one active EventSystem with exactly one enabled input
    ///         module, of the profile's kind
    ///   R006  Camera.main exists and belongs to the active rig
    ///   R007  no ACTIVE component belonging to the other platform
    ///   R008  the shared prompt wording matches the platform
    ///
    /// Follows the project's established Play Mode harness mechanism
    /// (SessionState flag + domain-reload resume + EditorApplication.update).
    ///
    ///   Unity -executeMethod BCaT.EditorTools.BCaTPlatformPlayModeValidation.RunMainScene
    ///   Unity -executeMethod BCaT.EditorTools.BCaTPlatformPlayModeValidation.RunBlackKitchen
    ///
    /// Results: Library/BCaTPlatformValidation.log, exit 0 pass / 1 fail.
    /// </summary>
    public static class BCaTPlatformPlayModeValidation
    {
        const string PendingKey = "BCaTPlatformValidation.Pending";
        const string ScenePathKey = "BCaTPlatformValidation.ScenePath";
        const string PhaseKey = "BCaTPlatformValidation.Phase";
        const string ReportKey = "BCaTPlatformValidation.Report";

        const string MainScenePath = "Assets/BH_XR_MainScene.unity";
        const string BlackKitchenScenePath =
            "Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity";

        static readonly string ResultPath = Path.Combine("Library", "BCaTPlatformValidation.log");

        static readonly StringBuilder Report = new StringBuilder();
        static readonly List<string> Failures = new List<string>();
        static float phaseStart = -1f;
        static bool finished;

        // Phases: 0 = desktop settle, 1 = desktop assert, 2 = quest settle, 3 = quest assert.
        const int PhaseDesktop = 0;
        const int PhaseQuest = 2;

        [MenuItem("BCaT/Architecture/Validate Platform in Play Mode (Main Scene)")]
        public static void RunMainScene() => Begin(MainScenePath);

        [MenuItem("BCaT/Architecture/Validate Platform in Play Mode (Black Kitchen)")]
        public static void RunBlackKitchen() => Begin(BlackKitchenScenePath);

        static void Begin(string scenePath)
        {
            Directory.CreateDirectory("Library");
            File.WriteAllText(ResultPath, "STARTED\n");

            SessionState.SetString(ScenePathKey, scenePath);
            SessionState.SetInt(PhaseKey, PhaseDesktop);
            SessionState.SetString(ReportKey, string.Empty);
            SessionState.SetBool(PendingKey, true);

            StartPhase(PhaseDesktop, scenePath);
        }

        static void StartPhase(int phase, string scenePath)
        {
            string mode = phase < PhaseQuest
                ? BCaTPlatformTestMode.Desktop
                : BCaTPlatformTestMode.QuestSimulated;
            SessionState.SetString(BCaTPlatform.EditorOverrideKey, mode);
            SessionState.SetInt(PhaseKey, phase);

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        static void ResumeAfterDomainReload()
        {
            if (!SessionState.GetBool(PendingKey, false))
                return;

            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Report.Clear();
            Report.Append(SessionState.GetString(ReportKey, string.Empty));
            Failures.Clear();
            phaseStart = -1f;
            finished = false;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (finished || !EditorApplication.isPlaying)
                return;

            if (phaseStart < 0f)
                phaseStart = Time.realtimeSinceStartup;

            // Let the bootstrap, binding, rig activation and first scene loads settle.
            if (Time.realtimeSinceStartup - phaseStart < 3.0f)
                return;

            finished = true;
            EditorApplication.update -= Tick;

            int phase = SessionState.GetInt(PhaseKey, PhaseDesktop);
            BCaTPlatformId expected = phase < PhaseQuest ? BCaTPlatformId.Desktop : BCaTPlatformId.Quest;

            try
            {
                Assert(expected);
            }
            catch (Exception e)
            {
                Fail($"validation threw: {e}");
            }

            SessionState.SetString(ReportKey, Report.ToString());
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => Advance(phase);
        }

        static void Advance(int phase)
        {
            string scenePath = SessionState.GetString(ScenePathKey, MainScenePath);

            if (phase < PhaseQuest)
            {
                EditorApplication.delayCall += () => StartPhase(PhaseQuest, scenePath);
                return;
            }

            Finish();
        }

        static void Finish()
        {
            SessionState.SetBool(PendingKey, false);
            SessionState.SetString(BCaTPlatform.EditorOverrideKey, BCaTPlatformTestMode.Auto);

            string report = SessionState.GetString(ReportKey, string.Empty);
            bool failed = report.Contains("FAIL:");
            report += failed ? "\nRESULT: FAIL\n" : "\nRESULT: PASS\n";
            File.WriteAllText(ResultPath, report);
            Debug.Log("[BCaTPlatformValidation] " + (failed ? "FAIL" : "PASS") + " — " + ResultPath);

            if (Application.isBatchMode)
                EditorApplication.Exit(failed ? 1 : 0);
        }

        // ---- Assertions -----------------------------------------------------

        static void Assert(BCaTPlatformId expected)
        {
            Scene scene = SceneManager.GetActiveScene();
            Line($"\n== {expected} · scene '{scene.name}' ==");
            Line($"resolver: {BCaTPlatform.Describe()}");

            Check(BCaTPlatform.Current == expected,
                $"resolved platform is {expected} (got {BCaTPlatform.Current}, " +
                $"source {BCaTPlatform.Source})");

            BCaTPlatformProfile profile = BCaTPlatform.Profile;
            Check(profile != null && profile.platformId == expected,
                $"active profile is the {expected} profile (got '{profile?.displayName}')");

            AssertRigs(expected, profile);
            AssertEventSystem(profile);
            AssertCamera();
            AssertNoWrongPlatformComponents(expected);
            AssertPromptWording(profile);
        }

        static void AssertRigs(BCaTPlatformId expected, BCaTPlatformProfile profile)
        {
            var active = UnityEngine.Object
                .FindObjectsByType<ScenePlayerRig>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(r => r != null && r.gameObject.activeInHierarchy)
                .ToList();

            Check(active.Count == 1,
                $"exactly one active ScenePlayerRig (got {active.Count}: " +
                $"{string.Join(", ", active.Select(r => r.name))})");

            if (active.Count >= 1)
                Check(active[0].Kind == profile.rigKind,
                    $"active rig kind is {profile.rigKind} (got {active[0].Kind} on '{active[0].name}')");

            int origins = UnityEngine.Object
                .FindObjectsByType<XROrigin>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Count(o => o != null && o.gameObject.activeInHierarchy);
            int fpcs = UnityEngine.Object
                .FindObjectsByType<StarterAssets.FirstPersonController>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Count(f => f != null && f.gameObject.activeInHierarchy);

            if (expected == BCaTPlatformId.Quest)
            {
                Check(origins == 1, $"exactly one active XROrigin on Quest (got {origins})");
                Check(fpcs == 0, $"no active desktop FirstPersonController on Quest (got {fpcs})");
            }
            else
            {
                Check(origins == 0, $"no active XROrigin on Desktop (got {origins})");
                Check(fpcs == 1, $"exactly one active FirstPersonController on Desktop (got {fpcs})");
            }
        }

        static void AssertEventSystem(BCaTPlatformProfile profile)
        {
            var active = UnityEngine.Object
                .FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(e => e != null && e.isActiveAndEnabled)
                .ToList();

            if (active.Count == 0)
            {
                Line("INFO: no active EventSystem (scene has no UI yet).");
                return;
            }

            Check(active.Count == 1,
                $"exactly one active EventSystem (got {active.Count}: " +
                $"{string.Join(" | ", active.Select(DescribeObject))})");

            if (active.Count != 1)
            {
                // XRI auto-provisions an EventSystem when it cannot find an
                // ACTIVE one (RegisteredUIInteractorCache + ComponentLocatorUtility
                // with createComponent), so an authored-inactive EventSystem
                // yields two. Inventory everything to make that traceable.
                foreach (EventSystem any in UnityEngine.Object
                             .FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    Line($"  inventory: {DescribeObject(any)} id={any.GetInstanceID()} " +
                         $"activeSelf={any.gameObject.activeSelf} enabled={any.enabled} " +
                         $"modules=[{string.Join(",", any.GetComponents<BaseInputModule>().Select(m => m.GetType().Name + (m.enabled ? "+" : "-")))}]");
                }
            }

            foreach (EventSystem es in active)
            {
                var modules = es.GetComponents<BaseInputModule>().Where(m => m != null && m.enabled).ToList();
                Check(modules.Count == 1,
                    $"'{es.name}' has exactly one enabled input module (got {modules.Count}: " +
                    $"{string.Join(", ", modules.Select(m => m.GetType().Name))})");

                if (modules.Count != 1)
                    continue;

                Type expectedModule = profile.uiInputModule == BCaTUiInputModuleKind.XRUI
                    ? typeof(XRUIInputModule)
                    : typeof(InputSystemUIInputModule);
                Check(expectedModule.IsInstanceOfType(modules[0]),
                    $"'{es.name}' input module is {expectedModule.Name} " +
                    $"(got {modules[0].GetType().Name})");
            }
        }

        static void AssertCamera()
        {
            Camera main = Camera.main;
            Check(main != null, "Camera.main resolves");
            if (main == null)
                return;

            ScenePlayerRig owner = main.GetComponentInParent<ScenePlayerRig>();
            Check(owner != null,
                $"Camera.main ('{main.name}') belongs to a ScenePlayerRig " +
                $"(got '{(owner != null ? owner.name : "none")}')");
        }

        static void AssertNoWrongPlatformComponents(BCaTPlatformId expected)
        {
            string[] questTypeNames =
            {
                "NearFarInteractor", "XRPokeInteractor", "XRRayInteractor",
                "XRInputModalityManager", "XRDeviceSimulator",
            };

            var offenders = new List<string>();
            foreach (MonoBehaviour behaviour in UnityEngine.Object
                         .FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (behaviour == null || !behaviour.isActiveAndEnabled)
                    continue;

                string typeName = behaviour.GetType().Name;
                bool isQuestComponent = questTypeNames.Contains(typeName);
                bool isDesktopComponent = typeName == "FirstPersonController" ||
                                          typeName == "StarterAssetsInputs";

                if (expected == BCaTPlatformId.Desktop && isQuestComponent)
                    offenders.Add($"{typeName} on '{behaviour.name}'");
                if (expected == BCaTPlatformId.Quest && isDesktopComponent)
                    offenders.Add($"{typeName} on '{behaviour.name}'");
            }

            // The XR Device Simulator is legitimately active in Quest-simulated mode.
            if (expected == BCaTPlatformId.Quest)
                offenders.RemoveAll(o => o.StartsWith("XRDeviceSimulator"));

            Check(offenders.Count == 0,
                $"no active components from the other platform (got {offenders.Count}" +
                (offenders.Count > 0 ? ": " + string.Join(", ", offenders.Take(6)) : "") + ")");
        }

        static void AssertPromptWording(BCaTPlatformProfile profile)
        {
            string verb = InteractionPromptText.Verb;
            string expectedVerb = profile.usesXRPrompts
                ? InteractionPromptText.XRVerb
                : InteractionPromptText.DesktopVerb;
            Check(verb == expectedVerb,
                $"shared prompt verb is '{expectedVerb}' (got '{verb}')");

            Check(PlatformCapabilities.UseXRPrompts == profile.usesXRPrompts,
                $"PlatformCapabilities.UseXRPrompts is {profile.usesXRPrompts} " +
                $"(got {PlatformCapabilities.UseXRPrompts})");
        }

        // ---- Reporting -------------------------------------------------------

        static void Check(bool condition, string description)
        {
            if (condition)
            {
                Line("PASS: " + description);
            }
            else
            {
                Failures.Add(description);
                Line("FAIL: " + description);
            }
        }

        static void Fail(string message)
        {
            Failures.Add(message);
            Line("FAIL: " + message);
        }

        static void Line(string message)
        {
            Report.AppendLine(message);
            Debug.Log("[BCaTPlatformValidation] " + message);
        }

        /// <summary>Hierarchy path plus owning scene, so duplicates are traceable.</summary>
        static string DescribeObject(Component component)
        {
            if (component == null) return "(null)";

            string path = component.name;
            Transform parent = component.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            string sceneName = component.gameObject.scene.IsValid()
                ? component.gameObject.scene.name
                : "DontDestroyOnLoad";
            return $"{path} [{sceneName}]";
        }
    }
}
