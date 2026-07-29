using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Single authority for Black Kitchen audio-station input and prompting. Exactly one
// station can be selected at a time; only that station receives E, and only its
// prompt is shown, on one small shared UI. Selection order: direct camera-ray hit
// first, nearest to screen center second, nearest physical distance third.
public class BlackKitchenInteractionManager : MonoBehaviour
{
    [SerializeField] private Key interactionKey = Key.E;
    [Tooltip("Maximum distance for camera-ray selection of a station trigger.")]
    [SerializeField] private float rayDistance = 3f;
    [Tooltip("Eligible stations within this angle of screen center are ranked by angle; otherwise by distance.")]
    [SerializeField] private float screenCenterMaxAngle = 40f;
    [SerializeField] private BlackKitchenExperienceController experienceController;

    private readonly List<BlackKitchenAudioInteractable> stations = new();
    private BlackKitchenAudioInteractable selected;
    private Canvas promptCanvas;
    private CanvasGroup promptGroup;
    private TMP_Text promptLabel;
    private RectTransform promptRect;

    public BlackKitchenAudioInteractable SelectedTarget => selected;
    public bool PromptVisible => promptGroup != null && promptGroup.alpha > 0.5f;
    public string PromptText => promptLabel != null ? promptLabel.text : string.Empty;

    private void Start()
    {
        stations.Clear();
        stations.AddRange(FindObjectsByType<BlackKitchenAudioInteractable>(FindObjectsSortMode.None));
        if (experienceController == null)
            experienceController = FindAnyObjectByType<BlackKitchenExperienceController>();

        EnsurePromptUI();
        Debug.Log($"[BlackKitchenInteractionManager] Managing {stations.Count} audio stations.");
    }

    private void Update()
    {
        if (experienceController != null && experienceController.IsExitModalOpen)
        {
            SetSelected(null);
            UpdatePrompt();
            return;
        }

        SetSelected(ResolveTarget());
        UpdatePrompt();

        if (selected == null || Keyboard.current == null || !Keyboard.current[interactionKey].wasPressedThisFrame)
            return;

        // The exit interface owns E while the player is aiming at it.
        if (experienceController != null && experienceController.IsAimingAtExit())
            return;

        ActivateSelected();
    }

    public void ActivateSelected()
    {
        if (selected == null)
            return;

        Debug.Log($"[BlackKitchenInteractionManager] Activating target: {selected.NarrativeId}");
        selected.Toggle();
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
        if (promptGroup == null)
            return;

        if (selected == null)
        {
            promptGroup.alpha = 0f;
            return;
        }

        bool xr = InteractionPromptText.IsXRActive();
        string verb = selected.IsPlaying ? "Stop" : "Play";
        promptLabel.text = xr
            ? $"Interact to {verb} {selected.DisplayName}"
            : $"Press E to {verb} {selected.DisplayName}";
        promptGroup.alpha = 1f;

        if (xr)
            PlacePromptInWorld();
        else
            ConfigurePromptForScreen();
    }

    private void EnsurePromptUI()
    {
        if (promptCanvas != null)
            return;

        GameObject canvasObject = new GameObject("BlackKitchenSharedPrompt", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        promptCanvas = canvasObject.GetComponent<Canvas>();
        promptCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        promptCanvas.sortingOrder = 30000;
        promptGroup = canvasObject.GetComponent<CanvasGroup>();
        promptGroup.alpha = 0f;
        promptGroup.interactable = false;
        promptGroup.blocksRaycasts = false;

        GameObject bar = new GameObject("PromptBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bar.transform.SetParent(canvasObject.transform, false);
        promptRect = bar.GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0.5f, 0f);
        promptRect.anchorMax = new Vector2(0.5f, 0f);
        promptRect.pivot = new Vector2(0.5f, 0f);
        promptRect.anchoredPosition = new Vector2(0f, 64f);
        promptRect.sizeDelta = new Vector2(480f, 44f);
        Image barImage = bar.GetComponent<Image>();
        barImage.color = new Color(0.02f, 0.025f, 0.028f, 0.82f);
        barImage.raycastTarget = false;

        GameObject textObject = new GameObject("PromptText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(bar.transform, false);
        promptLabel = textObject.GetComponent<TMP_Text>();
        promptLabel.fontSize = 21f;
        promptLabel.alignment = TextAlignmentOptions.Center;
        promptLabel.color = new Color(0.93f, 0.91f, 0.86f, 1f);
        promptLabel.raycastTarget = false;
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private void ConfigurePromptForScreen()
    {
        if (promptCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return;

        promptCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        promptCanvas.worldCamera = null;
        RectTransform canvasRect = promptCanvas.GetComponent<RectTransform>();
        canvasRect.localScale = Vector3.one;
        canvasRect.localPosition = Vector3.zero;
        canvasRect.localRotation = Quaternion.identity;
    }

    private void PlacePromptInWorld()
    {
        Camera cam = Camera.main;
        if (cam == null || selected == null)
            return;

        if (promptCanvas.renderMode != RenderMode.WorldSpace)
        {
            promptCanvas.renderMode = RenderMode.WorldSpace;
            promptCanvas.worldCamera = cam;
            RectTransform canvasRect = promptCanvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(480f, 60f);
            canvasRect.localScale = Vector3.one * 0.0012f;
        }

        Transform canvasTransform = promptCanvas.transform;
        canvasTransform.position = selected.FocusPoint + Vector3.up * 0.4f;
        canvasTransform.rotation = Quaternion.LookRotation(canvasTransform.position - cam.transform.position, Vector3.up);
    }
}
