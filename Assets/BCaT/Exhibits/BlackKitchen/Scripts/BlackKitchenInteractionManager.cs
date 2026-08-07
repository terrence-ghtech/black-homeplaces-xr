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
    private BlackKitchenAudioInteractable selected;
    private BlackKitchenAudioInteractable xrHoveredStation;
    private BlackKitchenExperienceController xrHoveredExit;
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
            xrHoveredExit = null;
            UpdatePrompt();
            return;
        }

        bool xr = InteractionPromptText.IsXRActive();
        if (xr && xrHoveredExit != null)
        {
            SetSelected(null);
            ShowSharedPrompt(xrHoveredExit.GetExitPrompt(xr));
            if (interactPressed)
                xrHoveredExit.HandleExitInteract();
            return;
        }

        SetSelected(xr && xrHoveredStation != null ? xrHoveredStation : ResolveTarget());
        UpdatePrompt();

        if (!interactPressed)
            return;

        // The exit interface owns the interaction while the player aims at it.
        if (experienceController != null && experienceController.IsAimingAtExit())
        {
            experienceController.HandleExitInteract();
            return;
        }

        if (selected != null)
            ActivateSelected();
    }

    public void ZoneSuppressPrompts()
    {
        SetSelected(null);
        HideSharedPrompt();
    }

    // -----------------------------------------------------------------------

    private void OnEnable() => InteractionRouter.RegisterZone(this);

    private void OnDisable()
    {
        InteractionRouter.UnregisterZone(this);
        HideSharedPrompt();
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
    }

    public bool RequestXRSelect(BlackKitchenAudioInteractable target)
    {
        if (target == null || InteractionState.IsBlocked)
            return false;

        EnsureStationRegistered(target);
        if (!stations.Contains(target))
            return false;

        SetSelected(target);
        UpdatePrompt();
        ActivateSelected();
        return true;
    }

    public void RequestXRHover(BlackKitchenAudioInteractable target)
    {
        if (target == null)
            return;

        EnsureStationRegistered(target);
        xrHoveredStation = target;
        xrHoveredExit = null;
        SetSelected(target);
        UpdatePrompt();
    }

    public void ClearXRHover(BlackKitchenAudioInteractable target)
    {
        if (xrHoveredStation != target)
            return;

        xrHoveredStation = null;
        SetSelected(null);
        UpdatePrompt();
    }

    public void RequestXRExitHover(BlackKitchenExperienceController controller)
    {
        if (controller == null)
            return;

        xrHoveredExit = controller;
        xrHoveredStation = null;
        SetSelected(null);
        ShowSharedPrompt(controller.GetExitPrompt(InteractionPromptText.IsXRActive()));
    }

    public void ClearXRExitHover()
    {
        xrHoveredExit = null;
        HideSharedPrompt();
    }

    public bool RequestXRExit()
    {
        if (InteractionState.IsBlocked)
            return false;

        if (experienceController == null)
            experienceController = FindAnyObjectByType<BlackKitchenExperienceController>();
        if (experienceController == null)
            return false;

        SetSelected(null);
        UpdatePrompt();
        experienceController.HandleExitInteract();
        return true;
    }

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

    private void UpdatePrompt()
    {
        if (selected == null)
        {
            HideSharedPrompt();
            return;
        }

        bool xr = InteractionPromptText.IsXRActive();
        SharedInteractionVerb verb = selected.IsPlaying ? SharedInteractionVerb.Stop : SharedInteractionVerb.Play;
        string legacyVerb = selected.IsPlaying ? "Stop" : "Play";
        // Desktop keeps its authored wording; Quest uses the shared
        // "<Action> — <Station name>" form (no keyboard wording, no "Interact to").
        ShowSharedPrompt(SharedInteractionPrompt.Format(
            xr,
            verb,
            selected.DisplayName,
            $"Press E to {legacyVerb} {selected.DisplayName}"));
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

    private void EnsureStationRegistered(BlackKitchenAudioInteractable target)
    {
        if (stations.Contains(target))
            return;

        stations.RemoveAll(station => station == null);
        stations.AddRange(FindObjectsByType<BlackKitchenAudioInteractable>(FindObjectsSortMode.None));
    }

}
