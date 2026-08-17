using System.Collections.Generic;
using BCaT.Production.Interaction;
using BCaT.Production.Shell;
using UnityEngine;
using UnityEngine.InputSystem;

// Single authority for Black Kitchen audio-station selection and prompting.
// Registered with the central InteractionRouter as an exclusive interaction
// zone: the router keeps ownership of the interact input, blockers, and
// cooldowns, and forwards one per-frame input signal to this manager, which
// keeps its validated station-selection rules (direct camera-ray hit first,
// nearest to screen center second, nearest physical distance third). Exactly
// one station can be selected at a time; only that station receives the
// interaction, and only its prompt is shown, on one small shared UI.
public class BlackKitchenInteractionManager : MonoBehaviour, IExclusiveInteractionZone
{
#pragma warning disable 0414 // retained for scene-data compatibility; router owns the interact key now
    [SerializeField] private Key interactionKey = Key.E;
#pragma warning restore 0414
    [Tooltip("Maximum distance for camera-ray selection of a station trigger.")]
    [SerializeField] private float rayDistance = 3f;
    [Tooltip("Eligible stations within this angle of screen center are ranked by angle; otherwise by distance.")]
    [SerializeField] private float screenCenterMaxAngle = 40f;
    [SerializeField] private BlackKitchenExperienceController experienceController;

    private readonly List<BlackKitchenAudioInteractable> stations = new();
    private InputAction questInteractAction;
    private BlackKitchenAudioInteractable selected;
    private bool sharedPromptVisible;
    private string sharedPromptText = string.Empty;

    public BlackKitchenAudioInteractable SelectedTarget => selected;
    public bool PromptVisible => sharedPromptVisible;
    public string PromptText => sharedPromptText;

    // ---- IExclusiveInteractionZone ----------------------------------------

    public bool ZoneActive => isActiveAndEnabled;

    /// <summary>
    /// Called once per frame by the InteractionRouter with the shared input
    /// state. Replaces the previous direct keyboard polling.
    /// </summary>
    public void ZoneTick(bool interactPressed)
    {
        if (experienceController != null && experienceController.IsExitModalOpen)
        {
            SetSelected(null);
            UpdatePrompt();
            return;
        }

        SetSelected(ResolveTarget());
        UpdatePrompt();

        // On Quest the router's shared per-frame signal always reports nothing:
        // QuestInteractionInputProvider.InteractPressedThisFrame is hardcoded
        // false because Quest activation is normally event-driven through XRI
        // select events. This zone has no XRI interactors, so it reads the
        // controller trigger itself and treats it exactly as the desktop
        // interact key. Everything downstream — station selection, the shared
        // prompt, ActivateSelected() and Toggle() — is the existing path.
        bool pressed = interactPressed || QuestTriggerPressedThisFrame();

        if (!pressed)
            return;

        // The exit interface owns the interaction while the player aims at it.
        // Both platforms now have their own exit-choice presentation, so this is
        // no longer gated to desktop: on Quest the trigger opens the world-space
        // gaze panel rather than the keyboard-only modal it used to open.
        if (experienceController != null && experienceController.IsAimingAtExit())
        {
            experienceController.HandleExitInteract();
            return;
        }

        if (selected != null)
            ActivateSelected();
    }

    /// <summary>
    /// Quest trigger, standing in for the desktop interact key. Bound to either
    /// controller's trigger with a directly serialized action, so nothing global
    /// changes and no XRI interactor, interaction manager or input asset is
    /// involved. WasPressedThisFrame gives exactly one activation per deliberate
    /// press, so the existing toggle/replay behaviour is preserved and holding
    /// the trigger cannot retrigger.
    /// </summary>
    private bool QuestTriggerPressedThisFrame()
    {
        EnsureQuestInteractAction();
        return questInteractAction != null && questInteractAction.WasPressedThisFrame();
    }

    /// <summary>
    /// Created lazily: XR can finish initializing after this component enables,
    /// so the platform is re-checked rather than sampled once in OnEnable.
    /// Never created on desktop, which therefore reads no controller input.
    /// </summary>
    private void EnsureQuestInteractAction()
    {
        if (questInteractAction != null || !BCaT.Production.PlatformCapabilities.IsXRActive)
            return;

        questInteractAction = new InputAction("BlackKitchenQuestInteract", InputActionType.Button);
        questInteractAction.AddBinding("<XRController>{LeftHand}/{TriggerButton}");
        questInteractAction.AddBinding("<XRController>{RightHand}/{TriggerButton}");
        questInteractAction.Enable();
        Debug.Log("[BlackKitchenInteractionManager] Quest interact action enabled " +
                  "(<XRController>{LeftHand|RightHand}/{TriggerButton}).");
    }

    private void DisposeQuestInteractAction()
    {
        if (questInteractAction == null)
            return;

        questInteractAction.Disable();
        questInteractAction.Dispose();
        questInteractAction = null;
    }

    public void ZoneSuppressPrompts()
    {
        SetSelected(null);
        HideSharedPrompt();
    }

    // -----------------------------------------------------------------------

    private void OnEnable()
    {
        InteractionRouter.RegisterZone(this);
        EnsureQuestInteractAction();
    }

    private void OnDisable()
    {
        InteractionRouter.UnregisterZone(this);
        HideSharedPrompt();
        DisposeQuestInteractAction();
    }

    private void Start()
    {
        stations.Clear();
        stations.AddRange(FindObjectsByType<BlackKitchenAudioInteractable>(FindObjectsSortMode.None));
        if (experienceController == null)
            experienceController = FindAnyObjectByType<BlackKitchenExperienceController>();

        Debug.Log($"[BlackKitchenInteractionManager] Managing {stations.Count} audio stations.");
    }

    public void ActivateSelected()
    {
        if (selected == null)
            return;

        Debug.Log($"[BlackKitchenInteractionManager] Activating target: {selected.NarrativeId}");
        selected.Toggle();

        // Prompt-hierarchy bookkeeping only: after the visitor has successfully
        // started a story once, the teaching suffix is dropped. Activation itself
        // is unchanged.
        HasActivatedAnyStory = true;
    }

    /// <summary>
    /// True once any story has been activated this session. Read by the Quest
    /// entry orientation and by the prompt wording below.
    /// </summary>
    public bool HasActivatedAnyStory { get; private set; }

    private BlackKitchenAudioInteractable ResolveTarget()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return null;

        // 1. Direct camera-ray hit on a station trigger, blocked by solid geometry
        //    and by the exit interface (which takes focus for its own interaction).
        BlackKitchenAudioInteractable rayTarget = ResolveRayTarget(cam);
        if (rayTarget != null)
            return rayTarget;

        // 2/3. Stations in radius, ranked by screen-center angle, then by distance.
        Vector3 playerPosition = ResolvePlayerPosition(cam);
        BlackKitchenAudioInteractable byAngle = null;
        BlackKitchenAudioInteractable byDistance = null;
        float bestAngle = screenCenterMaxAngle;
        float bestDistance = float.PositiveInfinity;
        foreach (BlackKitchenAudioInteractable station in stations)
        {
            if (station == null)
                continue;

            Vector3 focus = station.FocusPoint;
            Vector3 flatOffset = focus - playerPosition;
            flatOffset.y = 0f;
            float distance = flatOffset.magnitude;
            if (distance > station.InteractionRadius)
                continue;

            float angle = Vector3.Angle(cam.transform.forward, focus - cam.transform.position);
            if (angle <= bestAngle)
            {
                bestAngle = angle;
                byAngle = station;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                byDistance = station;
            }
        }

        return byAngle != null ? byAngle : byDistance;
    }

    private BlackKitchenAudioInteractable ResolveRayTarget(Camera cam)
    {
        RaycastHit[] hits = Physics.RaycastAll(new Ray(cam.transform.position, cam.transform.forward), rayDistance, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            foreach (BlackKitchenAudioInteractable station in stations)
            {
                if (station != null && station.OwnsCollider(hit.collider))
                    return station;
            }

            if (experienceController != null && experienceController.IsExitCollider(hit.collider))
                return null;

            if (!hit.collider.isTrigger)
                return null;
        }

        return null;
    }

    private static Vector3 ResolvePlayerPosition(Camera cam)
    {
        CharacterController controller = cam.GetComponentInParent<CharacterController>();
        return controller != null ? controller.transform.position : cam.transform.position;
    }

    private void SetSelected(BlackKitchenAudioInteractable target)
    {
        if (selected == target)
            return;

        if (selected != null)
            Debug.Log($"[BlackKitchenInteractionManager] Cleared target: {selected.NarrativeId}");
        selected = target;
        if (selected != null)
            Debug.Log($"[BlackKitchenInteractionManager] Selected target: {selected.NarrativeId}");
    }

    // The shared prompt is one line, so these read as "<label> · <instruction>".
    // Two-line text would overflow InteractionPromptUi's panel, and that widget is
    // shared with the Main House.
    private const string QuestListenSuffix = " · Pull trigger to listen";
    private const string QuestExitPrompt = "Exit Black Kitchen · Pull trigger to exit";

    private void UpdatePrompt()
    {
        // Priority: exit-choice panel (handled by the router's blocker, which
        // skips ZoneTick entirely) > exit target > audio discovery > nothing.
        // Targeting the exit suppresses every audio prompt, so "Play — <story>"
        // and the exit instruction can never be on screen together.
        // The exit now teaches itself through the same universal bottom prompt as
        // every other interaction, on BOTH platforms. Desktop gets the authored
        // "Press E to Exit Black Kitchen"; Quest keeps its trigger wording. The
        // floating "Exit Black Kitchen" plaque stays visible either way as
        // environmental signage — it is not this prompt.
        if (experienceController != null && experienceController.IsAimingAtExit())
        {
            ShowSharedPrompt(BCaT.Production.PlatformCapabilities.IsXRActive
                ? QuestExitPrompt
                : experienceController.GetExitPrompt());
            return;
        }

        if (selected == null)
        {
            HideSharedPrompt();
            return;
        }

        SharedInteractionVerb verb = selected.IsPlaying ? SharedInteractionVerb.Stop : SharedInteractionVerb.Play;
        string legacyVerb = selected.IsPlaying ? "Stop" : "Play";
        // Desktop passes false and therefore still resolves to the authored
        // "Press E to <verb> <name>" override, unchanged. Quest gets the shared
        // "<Verb> — <Name>" wording instead of a keyboard key it does not have.
        string prompt = SharedInteractionPrompt.Format(
            BCaT.Production.PlatformCapabilities.UseXRPrompts,
            verb,
            selected.DisplayName,
            $"Press E to {legacyVerb} {selected.DisplayName}");

        // Quest only, and only until the visitor has managed it once: spell out
        // how to activate. Desktop wording is untouched.
        if (BCaT.Production.PlatformCapabilities.IsXRActive && !HasActivatedAnyStory)
            prompt += QuestListenSuffix;

        ShowSharedPrompt(prompt);
    }

    private void ShowSharedPrompt(string text)
    {
        sharedPromptText = text;
        sharedPromptVisible = !string.IsNullOrWhiteSpace(text);
        if (sharedPromptVisible)
            InteractionPromptUi.Show(text);
        else
            InteractionPromptUi.Hide();
    }

    private void HideSharedPrompt()
    {
        sharedPromptText = string.Empty;
        sharedPromptVisible = false;
        InteractionPromptUi.Hide();
    }

}
