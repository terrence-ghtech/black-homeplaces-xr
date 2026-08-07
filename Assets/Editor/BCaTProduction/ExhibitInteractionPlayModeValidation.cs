using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BCaT.Production.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Play Mode validation for the router-driven link/symbol exhibits: confirms the
// Rhythm and Rope jump rope and the five staged Adinkra symbols are discoverable
// by InteractionRouter, resolve the correct desktop and XR prompt strings, carry
// no missing script references, and that the jump rope's configured URL actually
// reaches the existing InteractableLinkLauncher on both the desktop keyboard path
// and the XR select path.
//
// Follows the same mechanism as BlackKitchenAudioPlayModeValidation (SessionState
// flag + domain-reload resume + EditorApplication.update stepping). Run headed:
//   Unity -projectPath <proj> -executeMethod ExhibitInteractionPlayModeValidation.Run
// Results: Library/ExhibitInteractionValidation.log, exit code 0 (pass) / 1 (fail).
//
// The URL is dispatched exactly twice (once per platform path) so the launcher's
// own log line proves the URL; it is never dispatched in a loop.
public static class ExhibitInteractionPlayModeValidation
{
    private const string PendingKey = "ExhibitInteractionValidation.Pending";
    private const string ScenePath = "Assets/BH_XR_MainScene.unity";
    private const string JumpRopePrefab =
        "Assets/BCaT/Exhibits/RhythmAndRope/Prefabs/RhythmAndRope_JumpRope.prefab";
    private const string ExpectedUrl = "https://diamondebp.itch.io/rhythm-and-rope";
    private const string ExpectedDesktopPrompt = "Press E to Explore Rhythm and Rope";
    private const string ExpectedXrPrompt = "Interact to Explore Rhythm and Rope";

    private static readonly string ResultPath =
        Path.Combine("Library", "ExhibitInteractionValidation.log");

    private static readonly string[] AdinkraSlots =
        { "Sankofa", "GyeNyame", "Adinkrahene", "Funtunfunefu", "Nsaa" };

    private static readonly StringBuilder Report = new StringBuilder();
    private static readonly List<string> Failures = new List<string>();
    private static readonly List<string> LauncherLogs = new List<string>();

    private static int step;
    private static float stepStartTime = -1f;
    private static bool finished;
    private static int desktopBaseline;
    private static bool desktopKeyboardAvailable;

    public static void Run()
    {
        SessionState.SetBool(PendingKey, true);
        Directory.CreateDirectory("Library");
        File.WriteAllText(ResultPath, "STARTED\n");
        EditorSceneManager.OpenScene(ScenePath);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    private static void ResumeAfterDomainReload()
    {
        if (!SessionState.GetBool(PendingKey, false) || !EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Application.logMessageReceived += OnLog;
        step = 0;
        stepStartTime = -1f;
        finished = false;
        EditorApplication.update += Tick;
        Log("Play Mode validation resumed after domain reload.");
    }

    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (condition.Contains("[LinkLauncher:"))
            LauncherLogs.Add(condition);

        if (condition.Contains("[InteractionRouter]") &&
            (condition.Contains("focus gained") || condition.Contains("XR select")))
            Report.AppendLine("ROUTER: " + condition);
    }

    private static void Tick()
    {
        if (finished || !EditorApplication.isPlaying)
            return;

        if (stepStartTime < 0f)
            stepStartTime = Time.realtimeSinceStartup;

        float elapsed = Time.realtimeSinceStartup - stepStartTime;

        switch (step)
        {
            case 0:
                // Let the scene, router and rigs initialize.
                if (elapsed < 3f) return;
                ValidateRegistryAndPrompts();
                Advance();
                return;

            case 1:
                if (elapsed < 0.5f) return;
                ValidateAllLinkExhibits();
                ValidateNoMissingScripts();
                Advance();
                return;

            case 2:
                if (elapsed < 0.5f) return;
                FocusJumpRope();
                Advance();
                return;

            case 3:
                // Give the router a few frames to select the focused target.
                if (elapsed < 1.5f) return;
                ValidateDesktopFocusAndPrompt();
                Advance();
                return;

            case 4:
                if (elapsed < 0.5f) return;
                PressInteractKey();
                Advance();
                return;

            case 5:
                // Let the router consume the key press and dispatch.
                if (elapsed < 1f) return;
                ReleaseInteractKey();
                ValidateDesktopKeyDispatch();
                Advance();
                return;

            case 6:
                // Clear the router cooldown before exercising the XR path.
                if (elapsed < 1.5f) return;
                ValidateXrSelectDispatch();
                Advance();
                return;

            case 7:
                if (elapsed < 1.5f) return;
                ValidateAdinkraXrSelect();
                Advance();
                return;

            case 8:
                if (elapsed < 1f) return;
                Finish();
                return;
        }
    }

    private static void Advance()
    {
        step++;
        stepStartTime = -1f;
    }

    // ---- Checks ----------------------------------------------------------

    private static List<IInteractionTarget> Registry()
    {
        FieldInfo field = typeof(InteractionRouter)
            .GetField("registry", BindingFlags.Static | BindingFlags.NonPublic);
        var list = field?.GetValue(null) as List<IInteractionTarget>;
        return list ?? new List<IInteractionTarget>();
    }

    private static void ValidateRegistryAndPrompts()
    {
        Log("== Router discovery ==");

        if (InteractionRouter.Instance == null)
        {
            Fail("InteractionRouter.Instance is null in the main scene.");
            return;
        }

        Log($"Router present. Platform: {BCaT.Production.PlatformCapabilities.Describe()}");
        Log($"IsXRActive={BCaT.Production.PlatformCapabilities.IsXRActive}");

        var registry = Registry();
        Log($"Registered interaction targets: {registry.Count}");

        // --- Jump rope ---
        var launcher = Object.FindObjectsByType<InteractableLinkLauncher>(FindObjectsInactive.Include)
            .FirstOrDefault(l => l != null && l.gameObject.name.Contains("RhythmAndRope_JumpRope"));

        if (launcher == null)
        {
            Fail("Rhythm and Rope jump rope (InteractableLinkLauncher) not found in the scene.");
        }
        else
        {
            bool registered = registry.Any(t => ReferenceEquals(t, launcher));
            Check(registered, "jump rope is registered with InteractionRouter");

            string desktop = launcher.GetPrompt(false);
            string xr = launcher.GetPrompt(true);
            Check(desktop == ExpectedDesktopPrompt,
                $"jump rope desktop prompt == '{ExpectedDesktopPrompt}' (got '{desktop}')");
            Check(xr == ExpectedXrPrompt,
                $"jump rope XR prompt == '{ExpectedXrPrompt}' (got '{xr}')");

            var so = new SerializedObject(launcher);
            string url = so.FindProperty("targetUrl").stringValue;
            bool allowDesktop = so.FindProperty("allowDesktop").boolValue;
            bool allowQuest = so.FindProperty("allowQuest").boolValue;
            Check(url == ExpectedUrl, $"jump rope targetUrl == expected URL (got '{url}')");
            Check(allowDesktop, "jump rope allowDesktop is true");
            Check(allowQuest, "jump rope allowQuest is true (Quest platform gate open)");
            Check(launcher.IsAvailable, "jump rope IsAvailable is true");
            Check(launcher.MaxDistance > 0f, $"jump rope MaxDistance > 0 (={launcher.MaxDistance})");

            Collider[] own = launcher.OwnColliders;
            bool hasSolid = own != null && own.Any(c => c != null && !c.isTrigger);
            Check(hasSolid, "jump rope has a non-trigger collider (required for XRI ray select)");
        }

        // --- Adinkra symbols ---
        var symbols = Object.FindObjectsByType<AdinkraSymbolExhibit>(FindObjectsInactive.Include);
        Log($"Adinkra symbol components found: {symbols.Length}");
        Check(symbols.Length == 5, "all five Adinkra symbol exhibits exist in the scene");

        foreach (string slot in AdinkraSlots)
        {
            var symbol = symbols.FirstOrDefault(s =>
                s != null && s.transform.parent != null && s.transform.parent.name == slot);

            if (symbol == null)
            {
                Fail($"Adinkra symbol for slot '{slot}' not found.");
                continue;
            }

            bool registered = Registry().Any(t => ReferenceEquals(t, symbol));
            string desktop = symbol.GetPrompt(false);
            string xr = symbol.GetPrompt(true);

            Check(registered, $"Adinkra '{slot}' ({symbol.SymbolName}) is registered with InteractionRouter");
            Check(desktop.StartsWith("Press E"), $"Adinkra '{slot}' desktop prompt starts with 'Press E' ('{desktop}')");
            Check(xr.StartsWith("Interact"), $"Adinkra '{slot}' XR prompt starts with 'Interact' ('{xr}')");
            Check(symbol.IsAvailable, $"Adinkra '{slot}' IsAvailable is true");

            Collider[] own = symbol.OwnColliders;
            bool hasSolid = own != null && own.Any(c => c != null && !c.isTrigger);
            Check(hasSolid, $"Adinkra '{slot}' has a non-trigger collider (XRI ray select)");
        }

        // --- XR input provider carries no keyboard dependency ---
        var quest = new QuestInteractionInputProvider();
        Check(!quest.InteractPressedThisFrame && !quest.ClickPressedThisFrame,
            "QuestInteractionInputProvider reports no polled input (XR select is event-driven)");
    }

    /// <summary>
    /// Every InteractableLinkLauncher in the running scene must be registered
    /// with the router, be available, and have both platform gates open, so no
    /// link exhibit is silently disabled on desktop or Quest. Prompts are read
    /// for both platforms; no URL is dispatched here (that would open a browser
    /// tab per exhibit).
    /// </summary>
    private static void ValidateAllLinkExhibits()
    {
        Log("== All link exhibits: router availability + platform gates ==");

        var launchers = Object.FindObjectsByType<InteractableLinkLauncher>(FindObjectsInactive.Include)
            .OrderBy(l => l.gameObject.name)
            .ToArray();

        Check(launchers.Length > 0, "scene contains InteractableLinkLauncher exhibits");
        Log($"Link exhibits found: {launchers.Length}");

        var registry = Registry();

        foreach (InteractableLinkLauncher launcher in launchers)
        {
            string name = HierarchyPath(launcher.transform);
            var so = new SerializedObject(launcher);
            bool allowDesktop = so.FindProperty("allowDesktop").boolValue;
            bool allowQuest = so.FindProperty("allowQuest").boolValue;
            string url = so.FindProperty("targetUrl").stringValue;

            bool registered = registry.Any(t => ReferenceEquals(t, launcher));
            Collider[] own = launcher.OwnColliders;
            bool hasSolid = own != null && own.Any(c => c != null && !c.isTrigger);

            Check(registered, $"link exhibit registered with router: {name}");
            Check(allowDesktop, $"allowDesktop true: {name}");
            Check(allowQuest, $"allowQuest true (Quest gate open): {name}");
            Check(launcher.IsAvailable, $"IsAvailable true: {name}");
            Check(!string.IsNullOrWhiteSpace(url), $"targetUrl non-empty: {name}");
            Check(hasSolid, $"has non-trigger collider for XRI ray select: {name}");

            Log($"    {name}\n      url='{url}'\n" +
                $"      desktopPrompt='{launcher.GetPrompt(false)}'\n" +
                $"      xrPrompt='{launcher.GetPrompt(true)}'");
        }
    }

    private static string HierarchyPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    private static void ValidateNoMissingScripts()
    {
        Log("== Missing script references ==");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(JumpRopePrefab);
        Check(prefab != null, "jump rope prefab asset loads");
        if (prefab != null)
            CheckHierarchyComponents(prefab.transform, "prefab asset");

        GameObject staged = GameObject.Find("TEST_RhythmAndRope_FrontYard");
        Check(staged != null, "TEST_RhythmAndRope_FrontYard exists in the scene");
        if (staged != null)
            CheckHierarchyComponents(staged.transform, "staged jump rope");

        GameObject adinkra = GameObject.Find("AdinkraSymbols_Test");
        Check(adinkra != null, "AdinkraSymbols_Test exists in the scene");
        if (adinkra != null)
            CheckHierarchyComponents(adinkra.transform, "staged Adinkra row");

        // The audited link exhibits (contributor installations) and their prefabs.
        GameObject installations = GameObject.Find("_SceneContent/ImplementedContributorInstallations");
        Check(installations != null, "_SceneContent/ImplementedContributorInstallations exists");
        if (installations != null)
            CheckHierarchyComponents(installations.transform, "contributor link installations");

        foreach (string prefabPath in new[]
                 {
                     "Assets/BCaT_assets/LindaLeaks/Prefabs/LindaLeaks_Exhibit_HousingMap.prefab",
                     "Assets/BCaT_assets/HOMED/Prefabs/HOMED.prefab",
                     "Assets/BCaT_assets/BlackParlors/Prefabs/Black_Parlors.prefab",
                     "Assets/BCaT_assets/BlackFamilyMuseumArchive/Prefabs/BFM_Chest_OnChair_W_Text.prefab",
                 })
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Check(asset != null, $"link exhibit prefab loads: {prefabPath}");
            if (asset != null)
                CheckHierarchyComponents(asset.transform, Path.GetFileName(prefabPath));
        }
    }

    private static void CheckHierarchyComponents(Transform root, string label)
    {
        int missing = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            foreach (Component c in t.GetComponents<Component>())
            {
                if (c == null)
                {
                    missing++;
                    Report.AppendLine($"  MISSING SCRIPT on '{t.name}' ({label})");
                }
            }
        }

        Check(missing == 0, $"{label}: no missing script references (found {missing})");
    }

    private static void FocusJumpRope()
    {
        Log("== Desktop focus ==");

        var launcher = Object.FindObjectsByType<InteractableLinkLauncher>(FindObjectsInactive.Include)
            .FirstOrDefault(l => l != null && l.gameObject.name.Contains("RhythmAndRope_JumpRope"));
        Camera cam = Camera.main;

        if (launcher == null || cam == null)
        {
            Fail($"Cannot stage focus (launcher={(launcher != null)}, mainCamera={(cam != null)}).");
            return;
        }

        // Same harness approach the Black Kitchen validation uses: stop the player
        // control scripts fighting the teleport/aim, warp the rig, then look at it.
        DisableDesktopControlScripts();

        // Approach from the visitor's arrival side so the eye position stays
        // inside the fenced yard (north of Boundary_Front at z 130.01) and well
        // off-axis from the Adinkra row at z 134.
        Vector3 focus = launcher.FocusPoint;
        Vector3 approach = (new Vector3(167.91f, focus.y, 130.61f) - focus).normalized;
        Vector3 desiredEye = focus + approach * 1.5f;

        // The rig that actually moves is the CharacterController's transform, not
        // transform.root (which is the shared scene container).
        CharacterController cc = cam.GetComponentInParent<CharacterController>();
        Transform rig = cc != null ? cc.transform : cam.transform.parent;
        Vector3 eyeOffset = cam.transform.position - rig.position;

        TeleportPlayerTo(desiredEye - eyeOffset);
        AimAt(focus);

        float distance = Vector3.Distance(cam.transform.position, focus);
        float angle = Vector3.Angle(cam.transform.forward, focus - cam.transform.position);
        Log($"Rig '{rig.name}' moved; camera at {cam.transform.position} aimed at jump rope focus {focus} " +
            $"(distance {distance:F2} m, angle {angle:F1}°, MaxDistance {launcher.MaxDistance}).");
    }

    private static void DisableDesktopControlScripts()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        foreach (Behaviour behaviour in cam.transform.root.GetComponentsInChildren<Behaviour>(true))
        {
            if (behaviour == null || !behaviour.enabled)
                continue;

            string typeName = behaviour.GetType().Name;
            if (typeName == "FirstPersonController" || typeName == "StarterAssetsInputs" ||
                typeName == "PlayerInput")
            {
                behaviour.enabled = false;
                Log($"Disabled '{typeName}' so the harness controls movement and aim.");
            }
        }
    }

    private static void TeleportPlayerTo(Vector3 position)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        CharacterController cc = cam.GetComponentInParent<CharacterController>();
        Transform root = cc != null ? cc.transform : cam.transform.root;
        if (cc != null)
            cc.enabled = false;
        root.position = position;
        if (cc != null)
            cc.enabled = true;
        Physics.SyncTransforms();
    }

    private static void AimAt(Vector3 worldPoint)
    {
        Camera cam = Camera.main;
        if (cam != null)
            cam.transform.LookAt(worldPoint);
    }

    private static void ValidateDesktopFocusAndPrompt()
    {
        var launcher = Object.FindObjectsByType<InteractableLinkLauncher>(FindObjectsInactive.Include)
            .FirstOrDefault(l => l != null && l.gameObject.name.Contains("RhythmAndRope_JumpRope"));

        IInteractionTarget current = InteractionRouter.Instance != null
            ? InteractionRouter.Instance.CurrentTarget
            : null;

        Check(current != null, "router selected a target while facing the jump rope");
        Check(launcher != null && ReferenceEquals(current, launcher),
            $"router's CurrentTarget is the jump rope (got '{(current == null ? "<null>" : current.GetType().Name)}')");

        if (current != null)
            Log($"Focused prompt (desktop wording): '{current.GetPrompt(false)}'");
    }

    private static void PressInteractKey()
    {
        Log("== Desktop interaction (keyboard path through router) ==");
        desktopBaseline = LauncherLogs.Count;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            desktopKeyboardAvailable = false;
            Log("NOTE: no Keyboard device in this Play Mode session; the synthetic " +
                "key press is skipped and desktop dispatch is reported as unverified.");
            return;
        }

        desktopKeyboardAvailable = true;
        // Drive the same Input System device DesktopInteractionInputProvider reads.
        // No new input action map is introduced.
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
        Log("Queued synthetic 'E' key press onto Keyboard.current.");
    }

    private static void ReleaseInteractKey()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
    }

    private static void ValidateDesktopKeyDispatch()
    {
        if (!desktopKeyboardAvailable)
        {
            Fail("desktop keyboard dispatch could not be exercised (no Keyboard device present).");
            return;
        }

        bool urlReached = LauncherLogs.Skip(desktopBaseline)
            .Any(l => l.Contains("Opening external link") && l.Contains(ExpectedUrl));

        Check(urlReached,
            $"configured URL reached InteractableLinkLauncher via the desktop 'E' key path ({ExpectedUrl})");
        Log($"launcher log lines: baseline={desktopBaseline} now={LauncherLogs.Count}");
    }

    private static void ValidateXrSelectDispatch()
    {
        Log("== XR select interaction (router RequestXRSelect path) ==");

        var launcher = Object.FindObjectsByType<InteractableLinkLauncher>(FindObjectsInactive.Include)
            .FirstOrDefault(l => l != null && l.gameObject.name.Contains("RhythmAndRope_JumpRope"));

        if (launcher == null || InteractionRouter.Instance == null)
        {
            Fail("Cannot exercise XR select path (launcher or router missing).");
            return;
        }

        int before = LauncherLogs.Count;
        bool accepted = InteractionRouter.Instance.RequestXRSelect(launcher);
        Check(accepted, "router accepted RequestXRSelect for the jump rope (Quest select path)");

        bool urlReached = LauncherLogs.Skip(before)
            .Any(l => l.Contains("Opening external link") && l.Contains(ExpectedUrl));
        Check(urlReached, $"configured URL reached InteractableLinkLauncher via XR select ({ExpectedUrl})");

        foreach (string line in LauncherLogs)
            Report.AppendLine("  LAUNCHER: " + line);
    }

    /// <summary>
    /// Exercises one Adinkra symbol through the same router XR-select path a
    /// Quest controller uses, proving the symbols are interactable (not merely
    /// registered) without any keyboard involvement.
    /// </summary>
    private static void ValidateAdinkraXrSelect()
    {
        Log("== Adinkra XR select interaction (router RequestXRSelect path) ==");

        var symbol = Object.FindObjectsByType<AdinkraSymbolExhibit>(FindObjectsInactive.Include)
            .FirstOrDefault(s => s != null && s.SymbolName == "Sankofa");

        if (symbol == null || InteractionRouter.Instance == null)
        {
            Fail("Cannot exercise Adinkra XR select path (Sankofa or router missing).");
            return;
        }

        bool accepted = InteractionRouter.Instance.RequestXRSelect(symbol);
        Check(accepted, "router accepted RequestXRSelect for Adinkra 'Sankofa'");
        Check(symbol.IsOpen, "Adinkra 'Sankofa' modal opened from the XR select path (no keyboard used)");
        Check(InteractionState.HasReason(InteractionBlockReason.Modal),
            "open Adinkra modal registers the shared Modal interaction blocker");

        symbol.CloseModal();
        Check(!symbol.IsOpen, "Adinkra 'Sankofa' modal closed again");
        Check(!InteractionState.HasReason(InteractionBlockReason.Modal),
            "Modal interaction blocker released after close");
    }

    // ---- Reporting -------------------------------------------------------

    private static void Check(bool condition, string description)
    {
        if (condition)
            Report.AppendLine("PASS: " + description);
        else
            Fail(description);
    }

    private static void Fail(string description)
    {
        Report.AppendLine("FAIL: " + description);
        Failures.Add(description);
    }

    private static void Log(string line) => Report.AppendLine(line);

    private static void Finish()
    {
        finished = true;
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        SessionState.SetBool(PendingKey, false);

        Report.AppendLine();
        Report.AppendLine(Failures.Count == 0
            ? "RESULT: PASS"
            : $"RESULT: FAIL ({Failures.Count} failures)");
        foreach (string f in Failures)
            Report.AppendLine("  - " + f);

        File.WriteAllText(ResultPath, Report.ToString());
        Debug.Log("EXHIBIT_VALIDATION_COMPLETE\n" + Report);

        bool passed = Failures.Count == 0;
        EditorApplication.isPlaying = false;
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
