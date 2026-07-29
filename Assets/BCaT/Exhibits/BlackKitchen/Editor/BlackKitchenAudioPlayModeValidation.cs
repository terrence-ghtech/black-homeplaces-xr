using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Automated headed Play Mode validation of the Black Kitchen audio-station redesign:
// silence on entry, no proximity autoplay, single prompt/selection, toggle play/stop,
// exclusive replacement, exit flow, and scene-exit teardown.
// Run (never with -batchmode; that disables audio):
//   Unity -projectPath <proj> -executeMethod BlackKitchenAudioPlayModeValidation.Run
// Results: Library/BlackKitchenAudioValidation.log, exit code 0 (pass) / 1 (fail).
public static class BlackKitchenAudioPlayModeValidation
{
    private const string PendingKey = "BKAudioValidation.Pending";
    private const string ScenePath = "Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity";
    private static readonly string ResultPath = Path.Combine("Library", "BlackKitchenAudioValidation.log");

    private static readonly StringBuilder Report = new();
    private static readonly List<string> Failures = new();
    private static int violationCount;
    private static int autoplayEvents;
    private static bool intentionalPlaybackPhase;
    private static bool silencePhaseDirty;
    private static int step;
    private static float stepStartTime;
    private static float runStartTime = -1f;
    private static bool finished;
    private static int walkIndex;

    private static readonly (string id, Vector3 standAt)[] StationTour =
    {
        ("cultural_background", new Vector3(-0.7f, 0f, -3.2f)),
        ("kitchen_conversation", new Vector3(0.5f, 0f, 0.5f)),
        ("rice_and_bean_pot", new Vector3(-1.0f, 0f, 2.23f)),
        ("birthday_cake", new Vector3(-0.5f, 0f, 3.3f)),
        ("niece_cake", new Vector3(2.4f, 0f, 3.3f)),
    };

    private static readonly Vector3 FarPoint = new Vector3(0f, 0f, -4.4f);

    public static void Run()
    {
        SessionState.SetBool(PendingKey, true);
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
        stepStartTime = -1f;
        EditorApplication.update += Tick;
        Log("Play Mode validation resumed after domain reload.");
    }

    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (condition.Contains("EXCLUSIVITY VIOLATION") || condition.Contains("verification failed"))
        {
            violationCount++;
            Report.AppendLine($"VIOLATION LOGGED: {condition}");
        }

        if (!intentionalPlaybackPhase && condition.Contains("Started exclusive narrative"))
        {
            autoplayEvents++;
            Report.AppendLine($"AUTOPLAY LOGGED: {condition}");
        }

        if (condition.Contains("[BlackKitchenAudioCoordinator]") || condition.Contains("[BlackKitchenInteractionManager]"))
            Report.AppendLine($"LOG: {condition}");
    }

    private static void Tick()
    {
        if (finished || !EditorApplication.isPlaying)
            return;

        if (stepStartTime < 0f)
            stepStartTime = Time.realtimeSinceStartup;
        if (runStartTime < 0f)
            runStartTime = Time.realtimeSinceStartup;

        if (Time.realtimeSinceStartup - runStartTime > 240f)
        {
            Fail($"Global timeout: validation did not finish within 240s (stuck at step {step}).");
            Finish();
            return;
        }

        try
        {
            RunStateMachine();
        }
        catch (Exception exception)
        {
            Fail($"Exception in step {step}: {exception}");
            Finish();
        }
    }

    private static float StepElapsed => Time.realtimeSinceStartup - stepStartTime;

    private static void NextStep()
    {
        step++;
        stepStartTime = Time.realtimeSinceStartup;
    }

    private static void RunStateMachine()
    {
        var coordinator = UnityEngine.Object.FindAnyObjectByType<BlackKitchenAudioCoordinator>();
        var manager = UnityEngine.Object.FindAnyObjectByType<BlackKitchenInteractionManager>();
        var controller = UnityEngine.Object.FindAnyObjectByType<BlackKitchenExperienceController>();
        var stations = UnityEngine.Object.FindObjectsByType<BlackKitchenAudioInteractable>(FindObjectsSortMode.None);
        if (coordinator == null || manager == null || controller == null || stations.Length < 5)
        {
            if (StepElapsed > 20f)
            {
                Fail($"Scene components not found within 20s (stations found: {stations.Length}).");
                Finish();
            }
            return;
        }

        switch (step)
        {
            case 0:
                DisableDesktopControlScripts();
                Log($"Found {stations.Length} stations: {string.Join(", ", stations.Select(s => s.NarrativeId))}");
                NextStep();
                return;

            case 1: // Steps 1-4: stand still 10 seconds; nothing may play.
                if (AnyRegisteredPlaying(coordinator))
                    silencePhaseDirty = true;
                if (StepElapsed < 10f)
                    return;
                Check("Silence test (stand still 10s): zero sources played", !silencePhaseDirty, coordinator);
                walkIndex = 0;
                NextStep();
                return;

            case 2: // Steps 5-12: walk to every station without pressing E: no audio, exactly one prompt.
                if (AnyRegisteredPlaying(coordinator))
                    silencePhaseDirty = true;

                if (walkIndex < StationTour.Length)
                {
                    var (id, standAt) = StationTour[walkIndex];
                    if (StepElapsed < 0.2f)
                        return;
                    if (StepElapsed < 0.3f)
                    {
                        TeleportPlayerTo(standAt);
                        AimAt(FindStation(stations, id).FocusPoint);
                        return;
                    }
                    if (StepElapsed < 1.5f)
                        return;

                    Check($"Walk test '{id}': no audio started from proximity", !AnyRegisteredPlaying(coordinator), coordinator);
                    Check($"Prompt test '{id}': it is the single selected target", manager.SelectedTarget != null && manager.SelectedTarget.NarrativeId == id, coordinator);
                    Check($"Prompt test '{id}': prompt visible with Play verb", manager.PromptVisible && manager.PromptText == $"Press E to Play {FindStation(stations, id).DisplayName}", coordinator, manager.PromptText);
                    walkIndex++;
                    stepStartTime = Time.realtimeSinceStartup;
                    return;
                }

                TeleportPlayerTo(FarPoint);
                AimAt(FarPoint + Vector3.up * 10f);
                NextStep();
                return;

            case 3:
                if (StepElapsed < 1f)
                    return;
                Check("Prompt test: prompt hidden and target cleared away from stations", manager.SelectedTarget == null && !manager.PromptVisible, coordinator, manager.PromptText);
                Check("Walk test: still zero audio after full tour", !silencePhaseDirty && !AnyRegisteredPlaying(coordinator), coordinator);
                intentionalPlaybackPhase = true;
                NextStep();
                return;

            case 4: // Steps 13-16: play Cultural Background, then toggle it off.
                GoToStation(stations, "cultural_background");
                NextStep();
                return;

            case 5:
                if (StepElapsed < 1f)
                    return;
                manager.ActivateSelected();
                NextStep();
                return;

            case 6:
                if (StepElapsed < 0.6f)
                    return;
                Check("Playback: only cultural_background playing", ExactlyPlaying(coordinator, "cultural_background"), coordinator);
                Check("Prompt switched to Stop verb", manager.PromptText == "Press E to Stop Cultural Background", coordinator, manager.PromptText);
                manager.ActivateSelected();
                NextStep();
                return;

            case 7:
                if (StepElapsed < 0.6f)
                    return;
                Check("Toggle: cultural_background stopped; scene silent", !AnyRegisteredPlaying(coordinator), coordinator);
                GoToStation(stations, "rice_and_bean_pot");
                NextStep();
                return;

            case 8: // Steps 17-19: rice and beans, then birthday cake replaces it.
                if (StepElapsed < 1f)
                    return;
                manager.ActivateSelected();
                NextStep();
                return;

            case 9:
                if (StepElapsed < 0.6f)
                    return;
                Check("Playback: only rice_and_bean_pot playing", ExactlyPlaying(coordinator, "rice_and_bean_pot"), coordinator);
                GoToStation(stations, "birthday_cake");
                NextStep();
                return;

            case 10:
                if (StepElapsed < 1f)
                    return;
                Check("Selection moved to birthday_cake while rice plays", manager.SelectedTarget != null && manager.SelectedTarget.NarrativeId == "birthday_cake", coordinator);
                manager.ActivateSelected();
                NextStep();
                return;

            case 11:
                if (StepElapsed < 0.6f)
                    return;
                Check("Replacement: rice stopped, only birthday_cake playing", ExactlyPlaying(coordinator, "birthday_cake"), coordinator);
                GoToStation(stations, "niece_cake");
                NextStep();
                return;

            case 12: // Steps 20-23: niece cake replaces birthday cake, then toggles off.
                if (StepElapsed < 1f)
                    return;
                manager.ActivateSelected();
                NextStep();
                return;

            case 13:
                if (StepElapsed < 0.6f)
                    return;
                Check("Replacement: birthday stopped, only niece_cake playing", ExactlyPlaying(coordinator, "niece_cake"), coordinator);
                manager.ActivateSelected();
                NextStep();
                return;

            case 14:
                if (StepElapsed < 0.6f)
                    return;
                Check("Toggle: niece_cake stopped; scene silent", !AnyRegisteredPlaying(coordinator), coordinator);
                GoToStation(stations, "kitchen_conversation");
                NextStep();
                return;

            case 15: // Steps 24-25: kitchen conversation plays, does not loop, nothing follows.
                if (StepElapsed < 1f)
                    return;
                manager.ActivateSelected();
                NextStep();
                return;

            case 16:
                if (StepElapsed < 2f)
                    return;
                Check("Playback: only kitchen_conversation playing", ExactlyPlaying(coordinator, "kitchen_conversation"), coordinator);
                var conversationStation = FindStation(stations, "kitchen_conversation");
                var conversationSource = conversationStation.GetComponent<AudioSource>();
                Check("kitchen_conversation source does not loop", conversationSource != null && !conversationSource.loop, coordinator);
                manager.ActivateSelected();
                NextStep();
                return;

            case 17: // Steps 26-29: exit reflection replaces everything; Stay silences.
                if (StepElapsed < 0.6f)
                    return;
                GoToStation(stations, "rice_and_bean_pot");
                NextStep();
                return;

            case 18:
                if (StepElapsed < 1f)
                    return;
                manager.ActivateSelected();
                NextStep();
                return;

            case 19:
                if (StepElapsed < 0.6f)
                    return;
                Check("Story playing before exit test", ExactlyPlaying(coordinator, "rice_and_bean_pot"), coordinator);
                controller.ExitBlackKitchen();
                NextStep();
                return;

            case 20:
                if (StepElapsed < 0.6f)
                    return;
                Check("Exit Reflection: story stopped, only exit_reflection playing", ExactlyPlaying(coordinator, "exit_reflection"), coordinator);
                controller.SelectStay();
                NextStep();
                return;

            case 21:
                if (StepElapsed < 0.6f)
                    return;
                Check("Stay: complete silence", !AnyRegisteredPlaying(coordinator), coordinator);
                NextStep();
                return;

            case 22: // Steps 30-31: scene-exit teardown leaves nothing audible.
                coordinator.PrepareForSceneExit();
                NextStep();
                return;

            case 23:
                if (StepElapsed < 0.3f)
                    return;
                Check("Scene exit: zero AudioSources playing anywhere in scene", NoSceneSourcePlaying(coordinator), coordinator);
                NextStep();
                return;

            case 24:
                Check("Zero autoplay events during silent phases", autoplayEvents == 0, coordinator);
                Check("Zero EXCLUSIVITY VIOLATION messages during the whole run", violationCount == 0, coordinator);
                Finish();
                return;
        }
    }

    private static void GoToStation(BlackKitchenAudioInteractable[] stations, string id)
    {
        foreach (var (tourId, standAt) in StationTour)
        {
            if (tourId != id)
                continue;

            TeleportPlayerTo(standAt);
            AimAt(FindStation(stations, id).FocusPoint);
            return;
        }
    }

    private static BlackKitchenAudioInteractable FindStation(BlackKitchenAudioInteractable[] stations, string id)
    {
        return stations.First(s => s.NarrativeId == id);
    }

    private static List<string> PlayingNames(BlackKitchenAudioCoordinator coordinator)
    {
        return coordinator.NarrativeSources
            .Where(s => s != null && s.isPlaying)
            .Select(s => s.clip != null ? s.clip.name : s.gameObject.name)
            .ToList();
    }

    private static bool AnyRegisteredPlaying(BlackKitchenAudioCoordinator coordinator)
    {
        return PlayingNames(coordinator).Count > 0;
    }

    private static bool ExactlyPlaying(BlackKitchenAudioCoordinator coordinator, string clipName)
    {
        List<string> playing = PlayingNames(coordinator);
        return playing.Count == 1 && playing[0] == clipName;
    }

    private static bool NoSceneSourcePlaying(BlackKitchenAudioCoordinator coordinator)
    {
        return coordinator.gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<AudioSource>(true))
            .All(s => !s.isPlaying);
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

    private static void DisableDesktopControlScripts()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        Transform root = cam.transform.root;
        foreach (Behaviour behaviour in root.GetComponentsInChildren<Behaviour>(true))
        {
            if (behaviour == null || !behaviour.enabled)
                continue;

            string typeName = behaviour.GetType().Name;
            if (typeName == "FirstPersonController" || typeName == "StarterAssetsInputs" || typeName == "PlayerInput")
            {
                behaviour.enabled = false;
                Log($"Disabled '{typeName}' so the harness controls movement and aim.");
            }
        }
    }

    private static void Check(string description, bool condition, BlackKitchenAudioCoordinator coordinator, string extra = null)
    {
        string state = string.Join(", ", PlayingNames(coordinator));
        string line = $"{(condition ? "PASS" : "FAIL")}: {description} [playing: {(state.Length == 0 ? "none" : state)}{(extra != null ? $"; prompt: '{extra}'" : string.Empty)}]";
        Log(line);
        if (!condition)
            Failures.Add(line);
    }

    private static void Fail(string message)
    {
        Failures.Add(message);
        Log($"FAIL: {message}");
    }

    private static void Log(string message)
    {
        Report.AppendLine(message);
        Debug.Log($"[BKAudioValidation] {message}");
    }

    private static void Finish()
    {
        finished = true;
        SessionState.SetBool(PendingKey, false);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;

        bool passed = Failures.Count == 0;
        Report.AppendLine(passed ? "RESULT: PASS" : $"RESULT: FAIL ({Failures.Count} failures)");
        File.WriteAllText(ResultPath, Report.ToString());
        EditorApplication.isPlaying = false;
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
