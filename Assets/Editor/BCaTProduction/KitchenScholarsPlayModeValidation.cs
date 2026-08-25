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

// Play Mode validation for the four Kitchen Scholars (Azsaneé Truss & Staci
// Jones) framed collage exhibits staged under TEMP_KitchenScholars_FrontYard:
//   1. router discovery, image/audio pairing, prompts, non-trigger colliders,
//      missing-script scan (staged row + prefabs);
//   2. desktop path: warp/aim at a piece, focus selection, synthetic 'E'
//      press starts its narration;
//   3. XR path: RequestXRSelect on a second piece starts it AND stops the
//      first (single-narration exclusivity across platform paths);
//   4. proximity exit: warping the rig away stops the playing narration;
//   5. toggle: XR select twice on a third piece starts then stops it.
//
// Follows the same mechanism as ExhibitInteractionPlayModeValidation
// (SessionState flag + domain-reload resume + EditorApplication.update
// stepping). Run headed:
//   Unity -projectPath <proj> -executeMethod KitchenScholarsPlayModeValidation.Run
// Results: Library/KitchenScholarsValidation.log, exit code 0 (pass) / 1 (fail).
public static class KitchenScholarsPlayModeValidation
{
    private const string PendingKey = "KitchenScholarsValidation.Pending";
    private const string ScenePath = "Assets/BH_XR_MainScene.unity";
    private const string StagingRootName = "TEMP_KitchenScholars_FrontYard";
    private const string PrefabFolder = "Assets/BCaT/Exhibits/KitchenScholars/Prefabs";

    private static readonly string ResultPath =
        Path.Combine("Library", "KitchenScholarsValidation.log");

    // Authoritative title -> narration clip pairing, mirroring the matching
    // Drive artwork/audio filenames.
    private static readonly Dictionary<string, string> ExpectedPairings = new Dictionary<string, string>
    {
        { "My Grandmother's Recipes", "MyGrandmothersRecipes" },
        { "My Aunt Pat's House", "MyAuntPatsHouse" },
        { "Renovated Kitchen", "RenovatedKitchen" },
        { "Ancestor Critical Fabulation", "AncestorCriticalFabulation" },
    };

    private const string DesktopPieceTitle = "My Grandmother's Recipes";
    private const string XrPieceTitle = "My Aunt Pat's House";
    private const string TogglePieceTitle = "Renovated Kitchen";

    private static readonly StringBuilder Report = new StringBuilder();
    private static readonly List<string> Failures = new List<string>();
    private static readonly List<string> ScholarLogs = new List<string>();

    private static int step;
    private static float stepStartTime = -1f;
    private static bool finished;
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
        if (condition.Contains("[KitchenScholars:"))
            ScholarLogs.Add(condition);

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
                ValidateDiscoveryAndPairings();
                ValidateNoMissingScripts();
                Advance();
                return;

            case 1:
                if (elapsed < 0.5f) return;
                FocusPiece(DesktopPieceTitle);
                Advance();
                return;

            case 2:
                // Give the router a few frames to select the focused target.
                if (elapsed < 1.5f) return;
                ValidateDesktopFocusAndPrompt();
                PressInteractKey();
                Advance();
                return;

            case 3:
                if (elapsed < 1f) return;
                ReleaseInteractKey();
                ValidateDesktopStartedNarration();
                Advance();
                return;

            case 4:
                // Clear the router cooldown, then exercise the Quest path on a
                // second piece: it must start AND silence the first piece.
                if (elapsed < 1.5f) return;
                ValidateXrSelectAndExclusivity();
                Advance();
                return;

            case 5:
                if (elapsed < 1f) return;
                WarpAwayFromRow();
                Advance();
                return;

            case 6:
                if (elapsed < 1.5f) return;
                ValidateProximityExitStopped();
                Advance();
                return;

            case 7:
                if (elapsed < 0.5f) return;
                BeginToggleCheck();
                Advance();
                return;

            case 8:
                if (elapsed < 1.5f) return;
                FinishToggleCheck();
                Advance();
                return;

            case 9:
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

    // ---- Discovery ---------------------------------------------------------

    private static List<IInteractionTarget> Registry()
    {
        FieldInfo field = typeof(InteractionRouter)
            .GetField("registry", BindingFlags.Static | BindingFlags.NonPublic);
        var list = field?.GetValue(null) as List<IInteractionTarget>;
        return list ?? new List<IInteractionTarget>();
    }

    private static KitchenScholarsArtwork[] Pieces() =>
        Object.FindObjectsByType<KitchenScholarsArtwork>(FindObjectsInactive.Include, FindObjectsSortMode.None);

    private static KitchenScholarsArtwork Piece(string title) =>
        Pieces().FirstOrDefault(p => p != null && p.PieceTitle == title);

    private static void ValidateDiscoveryAndPairings()
    {
        Log("== Router discovery, pairings, prompts ==");

        if (InteractionRouter.Instance == null)
        {
            Fail("InteractionRouter.Instance is null in the main scene.");
            return;
        }

        Log($"Router present. Platform: {BCaT.Production.PlatformCapabilities.Describe()}");

        GameObject stagingRoot = GameObject.Find(StagingRootName);
        Check(stagingRoot != null, $"{StagingRootName} exists in the scene");

        var pieces = Pieces();
        Log($"Kitchen Scholars components found: {pieces.Length}");
        Check(pieces.Length == 4, "all four Kitchen Scholars pieces exist in the scene");

        var registry = Registry();

        foreach (KeyValuePair<string, string> expected in ExpectedPairings)
        {
            KitchenScholarsArtwork piece = pieces.FirstOrDefault(p => p != null && p.PieceTitle == expected.Key);
            if (piece == null)
            {
                Fail($"piece '{expected.Key}' not found in the scene.");
                continue;
            }

            Check(stagingRoot != null && piece.transform.IsChildOf(stagingRoot.transform),
                $"'{expected.Key}' is parented under {StagingRootName}");

            bool registered = registry.Any(t => ReferenceEquals(t, piece));
            Check(registered, $"'{expected.Key}' is registered with InteractionRouter");
            Check(piece.IsAvailable, $"'{expected.Key}' IsAvailable is true");

            Check(piece.NarrationClip != null, $"'{expected.Key}' has a narration clip assigned");
            if (piece.NarrationClip != null)
                Check(piece.NarrationClip.name == expected.Value,
                    $"'{expected.Key}' is paired with clip '{expected.Value}' (got '{piece.NarrationClip.name}')");

            string desktop = piece.GetPrompt(false);
            string xr = piece.GetPrompt(true);
            Check(desktop == $"Press E to Listen — {expected.Key}",
                $"'{expected.Key}' desktop prompt is the Listen wording (got '{desktop}')");
            Check(xr == $"Listen — {expected.Key}",
                $"'{expected.Key}' XR prompt is the Listen wording (got '{xr}')");

            Collider[] own = piece.OwnColliders;
            bool hasSolid = own != null && own.Any(c => c != null && !c.isTrigger);
            Check(hasSolid, $"'{expected.Key}' has a non-trigger collider (required for XRI ray select)");

            var source = piece.GetComponent<AudioSource>();
            Check(source != null && !source.playOnAwake, $"'{expected.Key}' AudioSource does not play on awake");
            Check(source != null && !source.loop, $"'{expected.Key}' AudioSource does not loop");
            Check(!piece.IsNarrationPlaying, $"'{expected.Key}' is silent at scene start (zero autoplay)");
        }
    }

    private static void ValidateNoMissingScripts()
    {
        Log("== Missing script references ==");

        GameObject stagingRoot = GameObject.Find(StagingRootName);
        if (stagingRoot != null)
            CheckHierarchyComponents(stagingRoot.transform, "staged Kitchen Scholars row");

        foreach (string title in ExpectedPairings.Keys)
        {
            string prefabPath = $"{PrefabFolder}/KitchenScholars_{title.Replace("'", string.Empty).Replace(" ", string.Empty).Replace("’", string.Empty)}.prefab";
            // Prefab names drop punctuation/spaces: resolve by listing instead
            // of reconstructing, so naming stays authoritative in the builder.
            prefabPath = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p)
                    .EndsWith(ExpectedPairings[title], System.StringComparison.Ordinal)) ?? prefabPath;

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Check(asset != null, $"prefab loads: {prefabPath}");
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

    // ---- Desktop path ------------------------------------------------------

    private static void FocusPiece(string title)
    {
        Log($"== Desktop focus ({title}) ==");

        KitchenScholarsArtwork piece = Piece(title);
        Camera cam = Camera.main;

        if (piece == null || cam == null)
        {
            Fail($"Cannot stage focus (piece={(piece != null)}, mainCamera={(cam != null)}).");
            return;
        }

        DisableDesktopControlScripts();

        // Approach from the visitor's arrival side (south of the row) so the
        // eye stays inside the fenced yard, facing the artwork face (-Z side).
        Vector3 focus = piece.FocusPoint;
        Vector3 desiredEye = focus + new Vector3(0f, 0f, -2.2f);

        CharacterController cc = cam.GetComponentInParent<CharacterController>();
        Transform rig = cc != null ? cc.transform : cam.transform.parent;
        Vector3 eyeOffset = cam.transform.position - rig.position;

        TeleportPlayerTo(desiredEye - eyeOffset);
        AimAt(focus);

        float distance = Vector3.Distance(cam.transform.position, focus);
        float angle = Vector3.Angle(cam.transform.forward, focus - cam.transform.position);
        Log($"Rig '{rig.name}' moved; camera at {cam.transform.position} aimed at '{title}' focus {focus} " +
            $"(distance {distance:F2} m, angle {angle:F1}°, MaxDistance {piece.MaxDistance}).");
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
        KitchenScholarsArtwork piece = Piece(DesktopPieceTitle);
        IInteractionTarget current = InteractionRouter.Instance != null
            ? InteractionRouter.Instance.CurrentTarget
            : null;

        Check(current != null, "router selected a target while facing the artwork");
        Check(piece != null && ReferenceEquals(current, piece),
            $"router's CurrentTarget is '{DesktopPieceTitle}' (got '{(current == null ? "<null>" : current.GetPrompt(false))}')");
    }

    private static void PressInteractKey()
    {
        Log("== Desktop interaction (keyboard path through router) ==");

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            desktopKeyboardAvailable = false;
            Log("NOTE: no Keyboard device in this Play Mode session; the synthetic " +
                "key press is skipped and desktop dispatch is reported as unverified.");
            return;
        }

        desktopKeyboardAvailable = true;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
        Log("Queued synthetic 'E' key press onto Keyboard.current.");
    }

    private static void ReleaseInteractKey()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
    }

    private static void ValidateDesktopStartedNarration()
    {
        if (!desktopKeyboardAvailable)
        {
            Fail("desktop keyboard dispatch could not be exercised (no Keyboard device present).");
            return;
        }

        KitchenScholarsArtwork piece = Piece(DesktopPieceTitle);
        Check(piece != null && piece.IsNarrationPlaying,
            $"'{DesktopPieceTitle}' narration is playing after the desktop 'E' press");
        Check(ReferenceEquals(KitchenScholarsArtwork.ActiveNarration, piece),
            "exclusivity tracker points at the desktop-started piece");

        if (piece != null && piece.IsNarrationPlaying)
        {
            string prompt = piece.GetPrompt(false);
            Check(prompt == $"Press E to Stop — {DesktopPieceTitle}",
                $"prompt flipped to the Stop wording while playing (got '{prompt}')");
        }
    }

    // ---- XR path + exclusivity ----------------------------------------------

    private static void ValidateXrSelectAndExclusivity()
    {
        Log("== XR select (router RequestXRSelect path) + single-narration exclusivity ==");

        KitchenScholarsArtwork first = Piece(DesktopPieceTitle);
        KitchenScholarsArtwork second = Piece(XrPieceTitle);

        if (first == null || second == null || InteractionRouter.Instance == null)
        {
            Fail("Cannot exercise XR select path (pieces or router missing).");
            return;
        }

        Check(first.IsNarrationPlaying, $"'{DesktopPieceTitle}' still playing before the XR select");

        bool accepted = InteractionRouter.Instance.RequestXRSelect(second);
        Check(accepted, $"router accepted RequestXRSelect for '{XrPieceTitle}' (Quest select path)");
        Check(second.IsNarrationPlaying, $"'{XrPieceTitle}' narration started from the XR select path");
        Check(!first.IsNarrationPlaying,
            $"starting '{XrPieceTitle}' stopped '{DesktopPieceTitle}' (no simultaneous Kitchen Scholars playback)");
        Check(ReferenceEquals(KitchenScholarsArtwork.ActiveNarration, second),
            "exclusivity tracker moved to the XR-started piece");

        bool replacedLogged = ScholarLogs.Any(l =>
            l.Contains($"[KitchenScholars:{DesktopPieceTitle}]") && l.Contains("replaced by"));
        Check(replacedLogged, "replacement stop was logged by the first piece");
    }

    // ---- Proximity exit ------------------------------------------------------

    private static void WarpAwayFromRow()
    {
        Log("== Proximity exit (visitor walks away) ==");

        KitchenScholarsArtwork second = Piece(XrPieceTitle);
        if (second != null)
            Log($"'{XrPieceTitle}' stop distance: {second.NarrationStopDistance:F2} m; warping rig ~8 m away.");

        // Back toward the house-side arrival point, well outside the 5 m stop
        // radius of every piece in the row.
        TeleportPlayerTo(new Vector3(167.9f, Camera.main != null ? Camera.main.transform.position.y : 6f, 126.0f));
    }

    private static void ValidateProximityExitStopped()
    {
        KitchenScholarsArtwork second = Piece(XrPieceTitle);
        Check(second != null && !second.IsNarrationPlaying,
            $"'{XrPieceTitle}' narration stopped after the visitor moved out of range");
        Check(KitchenScholarsArtwork.ActiveNarration == null,
            "no Kitchen Scholars narration is active after the walk-away");

        bool leftRangeLogged = ScholarLogs.Any(l =>
            l.Contains($"[KitchenScholars:{XrPieceTitle}]") && l.Contains("left range"));
        Check(leftRangeLogged, "proximity stop was logged with the 'left range' reason");
    }

    // ---- Toggle --------------------------------------------------------------

    private static void BeginToggleCheck()
    {
        Log("== Toggle stop (second XR select on the same piece) ==");

        KitchenScholarsArtwork third = Piece(TogglePieceTitle);
        if (third == null || InteractionRouter.Instance == null)
        {
            Fail("Cannot exercise toggle path (piece or router missing).");
            return;
        }

        // Stand near the piece first so its own proximity rule stays satisfied.
        Vector3 focus = third.FocusPoint;
        TeleportPlayerTo(new Vector3(focus.x, Camera.main != null ? Camera.main.transform.position.y : 6f, focus.z - 2.2f));
        AimAt(focus);

        bool accepted = InteractionRouter.Instance.RequestXRSelect(third);
        Check(accepted, $"router accepted RequestXRSelect for '{TogglePieceTitle}'");
        Check(third.IsNarrationPlaying, $"'{TogglePieceTitle}' narration started");
    }

    private static void FinishToggleCheck()
    {
        KitchenScholarsArtwork third = Piece(TogglePieceTitle);
        if (third == null || InteractionRouter.Instance == null)
        {
            Fail("Cannot finish toggle path (piece or router missing).");
            return;
        }

        bool accepted = InteractionRouter.Instance.RequestXRSelect(third);
        Check(accepted, $"router accepted the second RequestXRSelect for '{TogglePieceTitle}'");
        Check(!third.IsNarrationPlaying,
            $"second select toggled '{TogglePieceTitle}' narration off");
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
        Report.AppendLine("== Kitchen Scholars log markers ==");
        foreach (string line in ScholarLogs)
            Report.AppendLine("  " + line);

        Report.AppendLine();
        Report.AppendLine(Failures.Count == 0
            ? "RESULT: PASS"
            : $"RESULT: FAIL ({Failures.Count} failures)");
        foreach (string f in Failures)
            Report.AppendLine("  - " + f);

        File.WriteAllText(ResultPath, Report.ToString());
        Debug.Log("KITCHENSCHOLARS_VALIDATION_COMPLETE\n" + Report);

        bool passed = Failures.Count == 0;
        EditorApplication.isPlaying = false;
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
