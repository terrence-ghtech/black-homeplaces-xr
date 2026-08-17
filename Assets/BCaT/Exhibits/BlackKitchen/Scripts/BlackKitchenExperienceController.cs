using System.Collections;
using System.Collections.Generic;
using BCaT.Production.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Black Kitchen session controller: spawn/fall recovery, the Exit Reflection
/// audio, the three-way exit choice, and the return transition.
///
/// Interaction input arrives through the shared interaction architecture: the
/// BlackKitchenInteractionManager (the router's exclusive zone) forwards the
/// interact press via <see cref="HandleExitInteract"/>. While the choice is open
/// it registers a Modal interaction blocker so nothing else in the scene can
/// receive input.
///
/// This class owns the exit *decision* only —
/// <see cref="ChooseListen"/>, <see cref="ChooseLeaveNow"/> and
/// <see cref="ChooseCancel"/> — and delegates presentation to a platform adapter
/// (<see cref="IBlackKitchenExitChoiceUi"/>). Desktop keeps its screen-space
/// overlay with mouse and keyboard; Quest gets a world-anchored gaze-and-trigger
/// panel. The two UIs are siloed so neither platform's input model constrains the
/// other, and both terminate at the same three methods here.
///
/// Choosing Listen starts the existing reflection audio through the existing
/// coordinator and returns the player to the kitchen; it never waits for the clip
/// to finish, and the exit interface stays live so the player can leave at any
/// point while it plays.
/// </summary>
public class BlackKitchenExperienceController : MonoBehaviour, IBlackKitchenExitChoiceHandler
{
    [Header("Spawn and Return")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private string mainHouseSceneName = SceneTransitionState.MainHouseSceneName;
    [SerializeField] private string loadingSceneName = SceneTransitionState.LoadingSceneName;

    [Header("Fall Recovery")]
    [Tooltip("Returns the active player to SpawnPoint while in Black Kitchen if their root falls below this world-space Y value.")]
    [SerializeField] private float fallRecoveryYThreshold = -2.5f;
    [SerializeField] private bool enableFallRecovery = true;

    [Header("Exit Reflection")]
    [SerializeField] private AudioSource exitReflectionSource;
    [SerializeField] private float exitReflectionFadeDuration = 3f;

    [Header("Exit Interface")]
    [SerializeField] private float exitInteractionDistance = 4f;
#pragma warning disable 0414 // retained for scene-data compatibility; router owns the interact key now
    [SerializeField] private Key interactionKey = Key.E;
#pragma warning restore 0414
    [SerializeField] private TMP_Text exitPromptText;
    [SerializeField] private string desktopExitPrompt = "Press E to Exit Black Kitchen";
    [SerializeField] private Transform exitInteractionRoot;

    [Header("Audio Ducking")]
    [SerializeField] private BlackKitchenAudioCoordinator audioCoordinator;

    [Header("Debug")]
    [SerializeField] private bool resetExitReflectionSessionFlagOnStart;

    private float exitModalCloseTime = -999f;
    private const float ExitModalReopenCooldown = 0.35f;
    private bool exitInProgress;
    private bool exitReflectionRequested;
    private bool exitModalOpen;
    private bool exitModalChoiceHandled;
    private IBlackKitchenExitChoiceUi exitChoiceUi;
    private CanvasGroup sceneFadeGroup;
    private const float ExitReflectionStartFadeDuration = 0.05f;
    private Transform fallbackPlayerRoot;
    private readonly List<Behaviour> modalDisabledDesktopControls = new();
    private bool cursorStateCaptured;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;

    public Transform SpawnPoint => spawnPoint;
    public bool IsExitModalOpen => exitModalOpen;

    private void Awake()
    {
        if (resetExitReflectionSessionFlagOnStart)
            BlackKitchenSessionState.ResetForTesting();
    }

    private void Start()
    {
        if (exitReflectionSource != null)
        {
            exitReflectionSource.playOnAwake = false;
            exitReflectionSource.loop = false;
            exitReflectionSource.volume = 0f;
        }

        if (audioCoordinator != null)
            audioCoordinator.RegisterNarrativeSource(exitReflectionSource);
    }

    private void OnDestroy()
    {
        InteractionState.Unblock(this);
        BCaT.Production.Media.MediaPlaybackRegistry.NotifyStopped(this);
        exitChoiceUi?.Dispose();
        exitChoiceUi = null;
    }

    private void Update()
    {
        UpdateFallRecovery();

        if (exitPromptText != null)
        {
            exitPromptText.text = GetExitPrompt();
            exitPromptText.enabled = false;
        }

        if (exitModalOpen)
            exitChoiceUi?.Tick();
    }

    /// <summary>
    /// Interact press forwarded by the BlackKitchenInteractionManager while the
    /// player is aiming at the exit interface (replaces direct key polling).
    /// </summary>
    public void HandleExitInteract()
    {
        if (exitInProgress || exitModalOpen)
            return;

        ExitBlackKitchen();
    }

    public string GetExitPrompt() => desktopExitPrompt;

    /// <summary>Compatibility entry point: opens the exit choice.</summary>
    public void ExitBlackKitchen() => RequestExitChoice();

    /// <summary>
    /// Present the three-way exit choice. Nothing is started or left here — the
    /// reflection audio only begins if the user actually chooses Listen, which is
    /// what makes this a choice before leaving rather than an announcement.
    /// </summary>
    public void RequestExitChoice()
    {
        if (exitInProgress || exitModalOpen)
            return;

        if (Time.unscaledTime - exitModalCloseTime < ExitModalReopenCooldown)
        {
            Debug.Log($"[BlackKitchenExperienceController] Exit choice reopen suppressed within {ExitModalReopenCooldown:0.00}s of closing.");
            return;
        }

        // Activation confirmation for the Quest exit affordance. Placed after every
        // guard so it only fires on an accepted activation, and it no-ops on
        // desktop. The decision flow below is unchanged.
        BlackKitchenQuestExitHaptics.PulseActivated();

        ShowExitChoice();
    }

    private void UpdateFallRecovery()
    {
        if (!enableFallRecovery || spawnPoint == null || exitInProgress)
            return;

        Transform player = ResolveFallbackPlayerRoot();
        if (player != null && player.position.y < fallRecoveryYThreshold)
            SceneArrivalController.PlaceActivePlayerAt(spawnPoint);
    }

    private Transform ResolveFallbackPlayerRoot()
    {
        if (fallbackPlayerRoot != null)
            return fallbackPlayerRoot;

        Camera cam = Camera.main;
        if (cam != null)
        {
            CharacterController controller = cam.GetComponentInParent<CharacterController>();
            fallbackPlayerRoot = controller != null ? controller.transform : cam.transform.root;
        }

        return fallbackPlayerRoot;
    }

    private void StartExitReflectionIfNeeded()
    {
        if (exitReflectionRequested || IsExitReflectionPlaying())
            return;

        if (exitReflectionSource == null || exitReflectionSource.clip == null)
        {
            Debug.LogWarning("[BlackKitchenExperienceController] Exit Reflection unavailable: missing source or clip.");
            return;
        }

        if (audioCoordinator == null)
        {
            Debug.LogWarning("[BlackKitchenExperienceController] Exit Reflection not started: no audio coordinator assigned. The coordinator is the sole authority for narrative playback.");
            return;
        }

        float startFadeDuration = Mathf.Min(exitReflectionFadeDuration, ExitReflectionStartFadeDuration);
        bool started = audioCoordinator.PlayNarrativeReplacingActive(exitReflectionSource, exitReflectionSource.clip, 1f, startFadeDuration, startFadeDuration);
        if (!started)
        {
            Debug.Log("[BlackKitchenExperienceController] Exit Reflection was not started by the audio coordinator.");
            return;
        }

        exitReflectionRequested = true;
        BlackKitchenSessionState.MarkExitReflectionPlayed();

        // Long-form narration: the kiosk inactivity policy must know about it.
        BCaT.Production.Media.MediaPlaybackRegistry.NotifyStarted(this, StopExitReflectionImmediate);
    }

    private bool IsExitReflectionPlaying()
    {
        if (exitReflectionSource == null || exitReflectionSource.clip == null)
            return false;

        if (audioCoordinator != null && audioCoordinator.IsNarrativeActive(exitReflectionSource, exitReflectionSource.clip))
            return true;

        return exitReflectionSource.isPlaying;
    }

    // ---- Exit choice: shared decisions, siloed platform presentation --------

    /// <summary>
    /// The platform adapter in use. Desktop keeps the screen-space overlay with
    /// mouse and keyboard; Quest gets a world-anchored gaze-and-trigger panel.
    /// Chosen once, lazily, so neither platform's implementation can affect the
    /// other's.
    /// </summary>
    private IBlackKitchenExitChoiceUi ExitChoiceUi
    {
        get
        {
            if (exitChoiceUi == null)
            {
                exitChoiceUi = BCaT.Production.PlatformCapabilities.IsXRActive
                    ? new BlackKitchenExitChoiceQuestUi(transform)
                    : (IBlackKitchenExitChoiceUi)new BlackKitchenExitChoiceDesktopUi(transform);
                Debug.Log($"[BlackKitchenExperienceController] Exit choice UI: {exitChoiceUi.GetType().Name}.");
            }
            return exitChoiceUi;
        }
    }

    private void ShowExitChoice()
    {
        exitModalOpen = true;
        exitModalChoiceHandled = false;

        // Desktop-only: cursor + FirstPersonController handling. Quest keeps its
        // canonical movement untouched, and the panel is world-anchored (it
        // re-centres if the wearer walks off) rather than freezing locomotion.
        if (!BCaT.Production.PlatformCapabilities.IsXRActive)
            SetDesktopModalControls(false);

        // Focused choice: block other interaction; a kiosk reset resolves as Stay.
        InteractionState.Block(this, InteractionBlockReason.Modal, ChooseCancel);

        ExitChoiceUi.Show(this, offerListen: !IsExitReflectionPlaying());

        Debug.Log($"[BlackKitchenExperienceController] Scene '{gameObject.scene.name}' exit choice opened " +
                  $"(reflectionPlaying={IsExitReflectionPlaying()}).");
    }

    private void HideExitChoice()
    {
        exitModalOpen = false;
        exitModalCloseTime = Time.unscaledTime;
        InteractionState.Unblock(this);
        exitChoiceUi?.Hide();
    }

    /// <summary>
    /// Listen: start the existing reflection audio through the existing
    /// coordinator and stay in the kitchen. Deliberately does NOT stop audio and
    /// does NOT wait for it — the exit interface stays live, so the user can
    /// re-open this choice and leave at any point while it plays.
    /// </summary>
    public void ChooseListen()
    {
        if (exitModalChoiceHandled)
            return;

        exitModalChoiceHandled = true;
        Debug.Log($"[BlackKitchenExperienceController] Scene '{gameObject.scene.name}' Listen selected.");
        HideExitChoice();
        StartExitReflectionIfNeeded();
        exitInProgress = false;
        if (!BCaT.Production.PlatformCapabilities.IsXRActive)
            SetDesktopModalControls(true);
    }

    /// <summary>Cancel: stay in the kitchen. Leaves any already-playing
    /// reflection audio alone, since cancelling the exit should change nothing.</summary>
    public void ChooseCancel()
    {
        if (exitModalChoiceHandled)
            return;

        exitModalChoiceHandled = true;
        Debug.Log($"[BlackKitchenExperienceController] Scene '{gameObject.scene.name}' Stay selected.");
        HideExitChoice();
        exitInProgress = false;
        if (!BCaT.Production.PlatformCapabilities.IsXRActive)
            SetDesktopModalControls(true);
    }

    /// <summary>Leave now: return to the Main House without waiting for audio.</summary>
    public void ChooseLeaveNow()
    {
        if (exitInProgress || exitModalChoiceHandled)
            return;

        exitModalChoiceHandled = true;
        Debug.Log($"[BlackKitchenExperienceController] Scene '{gameObject.scene.name}' Leave Now selected.");
        HideExitChoice();
        StopExitReflectionImmediate();
        if (!BCaT.Production.PlatformCapabilities.IsXRActive)
            SetDesktopModalControls(true);
        StartCoroutine(ExitToMainHouseRoutine());
    }

    // ---- Compatibility wrappers for existing callers ------------------------

    public void FinishListeningBeforeExit() => ChooseCancel();

    /// <summary>Legacy name kept for the Play Mode validation harness.</summary>
    public void SelectStay() => ChooseCancel();

    public void ExitNow() => ChooseLeaveNow();

    public void SelectExitNow() => ChooseLeaveNow();

    private IEnumerator ExitToMainHouseRoutine()
    {
        exitInProgress = true;

        if (!SceneTransitionState.RequestTransition(mainHouseSceneName, SceneTransitionState.MainHouseKitchenReturnSpawnId, gameObject.scene.name))
        {
            Debug.LogWarning($"[BlackKitchenExperienceController] Return transition request blocked: {SceneTransitionState.LastError}");
            exitInProgress = false;
            SetDesktopModalControls(true);
            yield break;
        }

        Debug.Log($"[BlackKitchenExperienceController] Scene '{gameObject.scene.name}' Main House transition requested: scene '{mainHouseSceneName}', spawn '{SceneTransitionState.MainHouseKitchenReturnSpawnId}'.");
        PrepareAudioForSceneExit();
        yield return FadeSceneToBlack(0.7f);

        AsyncOperation load = null;
        try
        {
            Debug.Log($"[BlackKitchenExperienceController] Scene '{gameObject.scene.name}' loading scene load requested: '{loadingSceneName}'.");
            load = SceneManager.LoadSceneAsync(loadingSceneName, LoadSceneMode.Single);
        }
        catch (System.Exception exception)
        {
            string message = $"[BlackKitchenExperienceController] Failed to load '{loadingSceneName}' for Main House return: {exception.Message}";
            Debug.LogError(message);
            SceneTransitionState.CancelTransition(message);
            exitInProgress = false;
            SetDesktopModalControls(true);
            yield break;
        }

        if (load == null)
        {
            string message = $"[BlackKitchenExperienceController] Failed to load '{loadingSceneName}' for Main House return.";
            Debug.LogError(message);
            SceneTransitionState.CancelTransition(message);
            exitInProgress = false;
            SetDesktopModalControls(true);
            yield break;
        }

        while (!load.isDone)
            yield return null;
    }

    private void StopExitReflectionImmediate()
    {
        if (audioCoordinator != null)
            audioCoordinator.StopAllNarrativesImmediate();

        if (exitReflectionSource != null)
        {
            if (exitReflectionSource.isPlaying)
                exitReflectionSource.Stop();
            exitReflectionSource.volume = 0f;
        }

        BCaT.Production.Media.MediaPlaybackRegistry.NotifyStopped(this);
        Debug.Log($"[BlackKitchenExperienceController] Scene '{gameObject.scene.name}' exit audio stopped.");
    }

    private void PrepareAudioForSceneExit()
    {
        if (audioCoordinator != null)
            audioCoordinator.PrepareForSceneExit();

        foreach (AudioSource source in GetComponentsInChildren<AudioSource>(true))
        {
            if (source == null)
                continue;

            source.Stop();
            source.clip = null;
        }
    }

    private IEnumerator FadeSceneToBlack(float duration)
    {
        EnsureSceneFadeOverlay();
        if (sceneFadeGroup == null)
            yield break;

        sceneFadeGroup.blocksRaycasts = true;
        float start = sceneFadeGroup.alpha;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = duration <= 0f ? 1f : elapsed / duration;
            sceneFadeGroup.alpha = Mathf.Lerp(start, 1f, t);
            yield return null;
        }

        sceneFadeGroup.alpha = 1f;
        sceneFadeGroup.blocksRaycasts = true;
    }

    private void EnsureSceneFadeOverlay()
    {
        if (sceneFadeGroup != null)
            return;

        sceneFadeGroup = BCaT.Production.Shell.FadeOverlayBuilder.Create(
            "BlackKitchenExitFade", 32750, transform);
        sceneFadeGroup.alpha = 0f;
        sceneFadeGroup.blocksRaycasts = false;
    }

    private void SetDesktopModalControls(bool enabled)
    {
        if (!enabled)
        {
            modalDisabledDesktopControls.Clear();
            Transform root = ResolveFallbackPlayerRoot();
            if (root != null)
            {
                foreach (Behaviour behaviour in root.GetComponentsInChildren<Behaviour>(true))
                {
                    if (behaviour == null || !behaviour.enabled)
                        continue;

                    string typeName = behaviour.GetType().Name;
                    if (typeName == "FirstPersonController" || typeName == "StarterAssetsInputs" || typeName == "PlayerInput")
                    {
                        behaviour.enabled = false;
                        modalDisabledDesktopControls.Add(behaviour);
                    }
                }
            }

            if (!cursorStateCaptured)
            {
                previousCursorVisible = Cursor.visible;
                previousCursorLockMode = Cursor.lockState;
                cursorStateCaptured = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log($"[BlackKitchenExperienceController] Scene '{gameObject.scene.name}' cursor unlocked.");
            return;
        }

        foreach (Behaviour behaviour in modalDisabledDesktopControls)
        {
            if (behaviour != null && behaviour.gameObject.activeInHierarchy)
                behaviour.enabled = true;
        }
        modalDisabledDesktopControls.Clear();

        if (cursorStateCaptured)
        {
            Cursor.visible = previousCursorVisible;
            Cursor.lockState = previousCursorLockMode;
            cursorStateCaptured = false;
        }
        else
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        Debug.Log($"[BlackKitchenExperienceController] Scene '{gameObject.scene.name}' player controls restored.");
    }

    // ---- Exit aiming (restored verbatim; unchanged behaviour) ---------------

    public bool IsAimingAtExit()
    {
        return IsLookingAtExit();
    }

    public bool IsExitCollider(Collider candidate)
    {
        if (candidate == null)
            return false;

        Transform root = exitInteractionRoot != null ? exitInteractionRoot : transform;
        return candidate.transform == root || candidate.transform.IsChildOf(root);
    }

    private bool IsLookingAtExit()
    {
        if (exitInteractionRoot == null)
            exitInteractionRoot = transform;

        Camera cam = Camera.main;
        if (cam == null)
            return false;

        RaycastHit[] hits = Physics.RaycastAll(new Ray(cam.transform.position, cam.transform.forward), exitInteractionDistance, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform == exitInteractionRoot || hit.collider.transform.IsChildOf(exitInteractionRoot))
                return true;
            if (!hit.collider.isTrigger)
                return false;
        }

        return false;
    }
}
