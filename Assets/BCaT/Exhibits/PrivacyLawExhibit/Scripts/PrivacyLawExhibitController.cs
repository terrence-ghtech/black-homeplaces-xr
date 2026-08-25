using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PrivacyLawExhibitController : MonoBehaviour
{
    private enum ExhibitState { Hidden, Idle, Open }

    [Header("State Objects")]
    [SerializeField] private GameObject idleHologramRoot;
    [SerializeField] private GameObject expandedExhibitRoot;
    [SerializeField] private GameObject interactionPromptRoot;
    [SerializeField] private CanvasGroup idleHologramGroup;
    [SerializeField] private CanvasGroup expandedPanelGroup;
    [SerializeField] private Canvas expandedCanvas;

    [Header("Page Objects")]
    [SerializeField] private GameObject page01Root;
    [SerializeField] private GameObject page02Root;
    [SerializeField] private GameObject page03Root;
    [SerializeField] private ScrollRect page03ScrollRect;
    [SerializeField] private TMP_Text pageIndicatorText;
    [SerializeField] private int startingPage = 0;

    [Header("Navigation Controls")]
    [SerializeField] private Button pageButton01;
    [SerializeField] private Button pageButton02;
    [SerializeField] private Button pageButton03;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button closeIconButton;
    [SerializeField] private Image[] pageButtonBackgrounds;
    [SerializeField] private Color selectedPageColor = new Color(0.16f, 0.62f, 1f, 0.42f);
    [SerializeField] private Color unselectedPageColor = new Color(0.02f, 0.14f, 0.24f, 0.42f);

    [Header("Proximity Detection")]
    [Tooltip("Hologram visibility and interaction distance are controlled by the ProximityTrigger collider on this exhibit. Adjust that collider in the Inspector; this script does not set its size.")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool acceptMainCamera = true;
    [SerializeField] private Key interactionKey = Key.E;

    [Header("Focused View")]
    [Tooltip("Camera-relative reading distance for the expanded panel. This is not the proximity/interaction range.")]
    [SerializeField] private float focusedViewDistanceFromCamera = 1.75f;
    [SerializeField] private bool positionExpandedViewInFrontOfCamera = true;

    [Header("Fade Settings")]
    [SerializeField] private float fadeDuration = 0.35f;
    [Range(0f, 1f)]
    [SerializeField] private float idleOpacity = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float openedHologramOpacity = 0.16f;

    [Header("Hologram Animation")]
    [SerializeField] private Transform hologramAnimationRoot;
    [SerializeField] private Transform blueprintPanel;
    [SerializeField] private Transform orbitRing01;
    [SerializeField] private Transform orbitRing02;
    [SerializeField] private Transform orbitRing03;
    [SerializeField] private float floatAmplitudeMeters = 0.035f;
    [SerializeField] private float floatSpeed = 0.65f;
    [SerializeField] private float panelYawDegrees = 4f;
    [SerializeField] private float panelYawSpeed = 0.4f;
    [SerializeField] private Vector3 ring01RotationSpeed = new Vector3(0f, 10f, 0f);
    [SerializeField] private Vector3 ring02RotationSpeed = new Vector3(0f, -7f, 0f);
    [SerializeField] private Vector3 ring03RotationSpeed = new Vector3(0f, 5f, 0f);

    [Header("Platform Prompts")]
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private string desktopPrompt = "Press E to Examine Privacy Exhibit";
    [Tooltip("Quest wording for the floating hologram prompt. No keyboard wording.")]
    [SerializeField] private string xrPrompt = "View — Front Home Privacy Zones";

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;

    private readonly List<Behaviour> disabledWorldInputBehaviours = new List<Behaviour>();
    private readonly GameObject[] pages = new GameObject[3];
    private Coroutine fadeRoutine;
    private ExhibitState state = ExhibitState.Hidden;
    private int currentPage;
    private bool playerNearby;
    private bool capturedDesktopInput;
    private bool previousCursorVisible;
    private bool closeKeyReleasedSinceOpen;
    private int openedFrame = -1;
    private CursorLockMode previousCursorLockState;
    private Vector3 hologramStartLocalPosition;
    private Quaternion blueprintStartLocalRotation;

    public bool IsOpen => state == ExhibitState.Open;

    public void Configure(
        GameObject idleRoot,
        GameObject expandedRoot,
        GameObject promptRoot,
        CanvasGroup idleGroup,
        CanvasGroup expandedGroup,
        Canvas expanded,
        GameObject page01,
        GameObject page02,
        GameObject page03,
        ScrollRect page03Scroll,
        TMP_Text indicatorText,
        Button nav01,
        Button nav02,
        Button nav03,
        Button previous,
        Button next,
        Button close,
        Button closeIcon,
        Image[] navBackgrounds,
        Transform animationRoot,
        Transform panel,
        Transform ring01,
        Transform ring02,
        Transform ring03,
        TMP_Text prompt)
    {
        idleHologramRoot = idleRoot;
        expandedExhibitRoot = expandedRoot;
        interactionPromptRoot = promptRoot;
        idleHologramGroup = idleGroup;
        expandedPanelGroup = expandedGroup;
        expandedCanvas = expanded;
        page01Root = page01;
        page02Root = page02;
        page03Root = page03;
        page03ScrollRect = page03Scroll;
        pageIndicatorText = indicatorText;
        pageButton01 = nav01;
        pageButton02 = nav02;
        pageButton03 = nav03;
        previousButton = previous;
        nextButton = next;
        closeButton = close;
        closeIconButton = closeIcon;
        pageButtonBackgrounds = navBackgrounds;
        hologramAnimationRoot = animationRoot;
        blueprintPanel = panel;
        orbitRing01 = ring01;
        orbitRing02 = ring02;
        orbitRing03 = ring03;
        promptText = prompt;
    }

    private void Awake()
    {
        pages[0] = page01Root;
        pages[1] = page02Root;
        pages[2] = page03Root;

        if (hologramAnimationRoot != null)
            hologramStartLocalPosition = hologramAnimationRoot.localPosition;

        if (blueprintPanel != null)
            blueprintStartLocalRotation = blueprintPanel.localRotation;

        WireButtons();
        SetState(ExhibitState.Hidden, true);
    }

    private void OnDestroy()
    {
        RestoreDesktopInput();
        UnwireButtons();
    }

    private void OnDisable()
    {
        BCaT.Production.Interaction.InteractionRouter.Unregister(routerTarget);
        BCaT.Production.Interaction.InteractionState.Unblock(this);
        RestoreDesktopInput();
    }

    private void OnEnable()
    {
        if (routerTarget == null)
            routerTarget = new PrivacyLawInteractionTarget(this);
        BCaT.Production.Interaction.InteractionRouter.Register(routerTarget);
    }

    private PrivacyLawInteractionTarget routerTarget;

    /// <summary>
    /// Router adapter for the proximity-gated open interaction. The exhibit's
    /// ProximityTrigger collider keeps deciding availability (playerNearby);
    /// the router owns input, prompts, and blocking.
    /// </summary>
    private sealed class PrivacyLawInteractionTarget : BCaT.Production.Interaction.IInteractionTarget
    {
        readonly PrivacyLawExhibitController owner;
        Collider[] ownColliders;

        public PrivacyLawInteractionTarget(PrivacyLawExhibitController owner) => this.owner = owner;

        public Vector3 FocusPoint => owner != null ? owner.transform.position : Vector3.zero;
        public float MaxDistance => 999f; // proximity handled by the exhibit's trigger
        public float MaxViewAngle => 0f;  // proximity-based, no camera focus requirement
        public bool RequireLineOfSight => false;
        public int Priority => 0;
        public bool AllowDesktopClick => true;
        public bool Exists => owner != null;

        public bool IsAvailable =>
            owner != null && owner.isActiveAndEnabled &&
            owner.playerNearby && owner.state == ExhibitState.Idle;

        public Collider[] OwnColliders
        {
            get
            {
                if (ownColliders == null && owner != null)
                    ownColliders = owner.GetComponentsInChildren<Collider>(true);
                return ownColliders;
            }
        }

        /// <summary>
        /// On Quest this exhibit's affordance is its floating hologram prompt
        /// (one of the two sanctioned world-space exceptions), so the shared
        /// bottom HUD returns nothing here — an empty prompt hides it and
        /// prevents a duplicate. Desktop wording is unchanged.
        /// </summary>
        public string GetPrompt(bool xr) =>
            xr ? string.Empty : owner.desktopPrompt;

        public void OnFocusChanged(bool focused) { }

        public void OnInteract(BCaT.Production.Interaction.InteractionActivation activation) =>
            owner.OpenExhibit();
    }

    private void Update()
    {
        AnimateIdleHologram();
        RefreshPrompt();

        // Focused-modal input reads the central FocusedUiInput helper; the
        // idle-state open interaction is owned by the InteractionRouter.
        if (state == ExhibitState.Open)
        {
            if (Time.frameCount > openedFrame &&
                !BCaT.Production.Interaction.FocusedUiInput.KeyHeld(interactionKey))
                closeKeyReleasedSinceOpen = true;

            if (Time.frameCount > openedFrame
                && (BCaT.Production.Interaction.FocusedUiInput.CancelPressed
                    || (closeKeyReleasedSinceOpen &&
                        BCaT.Production.Interaction.FocusedUiInput.KeyPressed(interactionKey))))
            {
                CloseExhibit();
            }
        }
    }

    public void HandleProximityEnter(Collider other)
    {
        if (!IsVisitor(other))
            return;

        playerNearby = true;
        if (state == ExhibitState.Hidden)
            SetState(ExhibitState.Idle, false);
    }

    public void HandleProximityExit(Collider other)
    {
        if (!IsVisitor(other))
            return;

        playerNearby = false;
        // On Quest locomotion stays live while the exhibit is open, so the
        // visitor can leave the trigger with the Modal block still held.
        // Release exactly the block this exhibit owns (idempotent when idle).
        BCaT.Production.Interaction.InteractionState.Unblock(this);
        RestoreDesktopInput();
        SetState(ExhibitState.Hidden, false);
    }

    public void OpenFromXR()
    {
        if (routerTarget != null && BCaT.Production.Interaction.InteractionRouter.Instance != null)
        {
            BCaT.Production.Interaction.InteractionRouter.Instance.RequestXRSelect(routerTarget);
            return;
        }

        if (BCaT.Production.Interaction.InteractionState.IsBlocked)
        {
            Debug.Log("[PrivacyLawExhibit] XR open suppressed (interaction blocked).");
            return;
        }
        OpenExhibit();
    }

    public void OpenExhibit()
    {
        if (!playerNearby && Application.isPlaying)
            return;

        currentPage = Mathf.Clamp(startingPage, 0, pages.Length - 1);
        openedFrame = Time.frameCount;
        closeKeyReleasedSinceOpen =
            !BCaT.Production.Interaction.FocusedUiInput.KeyHeld(interactionKey);
        SetState(ExhibitState.Open, false);
        PositionExpandedViewInFrontOfCamera();
        CaptureDesktopInput();

        // Focused exhibit interface: block background world interaction and
        // give the kiosk reset a close handle.
        BCaT.Production.Interaction.InteractionState.Block(this,
            BCaT.Production.Interaction.InteractionBlockReason.Modal, CloseExhibit);
    }

    public void CloseExhibit()
    {
        currentPage = Mathf.Clamp(startingPage, 0, pages.Length - 1);
        BCaT.Production.Interaction.InteractionState.Unblock(this);
        RestoreDesktopInput();
        ResetPage03Scroll();
        SetState(playerNearby ? ExhibitState.Idle : ExhibitState.Hidden, false);
    }

    public void SelectPage(int pageIndex)
    {
        currentPage = Mathf.Clamp(pageIndex, 0, pages.Length - 1);
        if (currentPage == 2)
            ResetPage03Scroll();
        RefreshPages();
    }

    public void PreviousPage()
    {
        if (currentPage <= 0)
            return;

        SelectPage(currentPage - 1);
    }

    public void NextPage()
    {
        if (currentPage >= pages.Length - 1)
            return;

        SelectPage(currentPage + 1);
    }

    private void SetState(ExhibitState nextState, bool immediate)
    {
        state = nextState;

        if (logStateChanges)
            Debug.Log($"[PrivacyLawExhibit] State -> {state}");

        if (expandedExhibitRoot != null)
            expandedExhibitRoot.SetActive(state == ExhibitState.Open);

        // Front Home Privacy Zones is one of the two sanctioned world-space
        // prompt systems: its floating hologram prompt is restored on Quest
        // (and only on Quest) with its original idle-state visibility rule.
        // This exhibit has no separate canvas, so the hologram prompt IS its
        // interaction affordance and must not be replaced by the bottom HUD.
        WorldInteractionPromptVisual.SetSanctionedRootVisible(
            interactionPromptRoot, state == ExhibitState.Idle);

        if (idleHologramRoot != null)
            idleHologramRoot.SetActive(state == ExhibitState.Idle);

        if (expandedCanvas != null)
        {
            expandedCanvas.enabled = state == ExhibitState.Open;
            expandedCanvas.overrideSorting = true;
            expandedCanvas.sortingOrder = 100;
        }

        if (expandedPanelGroup != null)
        {
            expandedPanelGroup.interactable = state == ExhibitState.Open;
            expandedPanelGroup.blocksRaycasts = state == ExhibitState.Open;
        }

        if (state != ExhibitState.Open)
            DeactivateAllPages();
        else
            RefreshPages();

        float targetIdleAlpha = state == ExhibitState.Idle ? idleOpacity : 0f;
        float targetExpandedAlpha = state == ExhibitState.Open ? 1f : 0f;

        FadeTo(targetIdleAlpha, targetExpandedAlpha, immediate);
    }

    private void FadeTo(float idleAlpha, float expandedAlpha, bool immediate)
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        if (immediate || !Application.isPlaying)
        {
            ApplyAlpha(idleAlpha, expandedAlpha);
            return;
        }

        fadeRoutine = StartCoroutine(FadeRoutine(idleAlpha, expandedAlpha));
    }

    private IEnumerator FadeRoutine(float idleTarget, float expandedTarget)
    {
        float idleStart = idleHologramGroup != null ? idleHologramGroup.alpha : idleTarget;
        float expandedStart = expandedPanelGroup != null ? expandedPanelGroup.alpha : expandedTarget;

        for (float elapsed = 0f; elapsed < fadeDuration; elapsed += Time.deltaTime)
        {
            float t = fadeDuration <= 0f ? 1f : elapsed / fadeDuration;
            ApplyAlpha(Mathf.Lerp(idleStart, idleTarget, t), Mathf.Lerp(expandedStart, expandedTarget, t));
            yield return null;
        }

        ApplyAlpha(idleTarget, expandedTarget);
        fadeRoutine = null;
    }

    private void ApplyAlpha(float idleAlpha, float expandedAlpha)
    {
        if (idleHologramGroup != null)
            idleHologramGroup.alpha = idleAlpha;

        if (expandedPanelGroup != null)
            expandedPanelGroup.alpha = expandedAlpha;
    }

    private void RefreshPages()
    {
        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] != null)
                pages[i].SetActive(i == currentPage);
        }

        if (pageIndicatorText != null)
            pageIndicatorText.text = $"{currentPage + 1} / {pages.Length}";

        if (previousButton != null)
            previousButton.interactable = currentPage > 0;

        if (nextButton != null)
            nextButton.interactable = currentPage < pages.Length - 1;

        for (int i = 0; i < pageButtonBackgrounds.Length; i++)
        {
            if (pageButtonBackgrounds[i] != null)
                pageButtonBackgrounds[i].color = i == currentPage ? selectedPageColor : unselectedPageColor;
        }
    }

    private void DeactivateAllPages()
    {
        ResetPage03Scroll();

        foreach (GameObject page in pages)
        {
            if (page != null)
                page.SetActive(false);
        }
    }

    private void PositionExpandedViewInFrontOfCamera()
    {
        if (!positionExpandedViewInFrontOfCamera || expandedExhibitRoot == null)
            return;

        Camera activeCamera = FindActiveCamera();
        if (activeCamera == null)
            return;

        Transform panelTransform = expandedExhibitRoot.transform;
        Vector3 cameraForward = activeCamera.transform.forward;
        panelTransform.position = activeCamera.transform.position + cameraForward * focusedViewDistanceFromCamera;

        Vector3 directionAwayFromCamera = (panelTransform.position - activeCamera.transform.position).normalized;
        panelTransform.rotation = Quaternion.LookRotation(directionAwayFromCamera, Vector3.up);

        EnsureCameraRendersUiLayer(activeCamera);
    }

    private Camera FindActiveCamera()
    {
        if (Camera.main != null && Camera.main.isActiveAndEnabled)
            return Camera.main;

        foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (camera != null && camera.isActiveAndEnabled)
                return camera;
        }

        return null;
    }

    private void EnsureCameraRendersUiLayer(Camera activeCamera)
    {
        int uiLayer = LayerMask.NameToLayer("UI");
        if (activeCamera == null || uiLayer < 0)
            return;

        int uiMask = 1 << uiLayer;
        if ((activeCamera.cullingMask & uiMask) == 0)
            activeCamera.cullingMask |= uiMask;
    }

    private void CaptureDesktopInput()
    {
        if (capturedDesktopInput || InteractionPromptText.IsXRActive())
            return;

        capturedDesktopInput = true;
        previousCursorLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        DisableWorldInput();
    }

    private void RestoreDesktopInput()
    {
        if (!capturedDesktopInput)
            return;

        RestoreWorldInput();
        Cursor.lockState = previousCursorLockState;
        Cursor.visible = previousCursorVisible;
        capturedDesktopInput = false;
    }

    private void DisableWorldInput()
    {
        disabledWorldInputBehaviours.Clear();
        foreach (Behaviour behaviour in FindObjectsByType<Behaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (behaviour == null || !behaviour.enabled || behaviour == this || behaviour.transform.IsChildOf(transform.root))
                continue;

            if (!ShouldDisableWhileOpen(behaviour))
                continue;

            behaviour.enabled = false;
            disabledWorldInputBehaviours.Add(behaviour);
        }
    }

    private bool ShouldDisableWhileOpen(Behaviour behaviour)
    {
        string typeName = behaviour.GetType().Name;
        string fullName = behaviour.GetType().FullName ?? typeName;

        return typeName == "FirstPersonController"
            || typeName == "StarterAssetsInputs"
            || typeName == "SimpleImagePopupInteractor"
            || typeName == "LindaLeaksPanelOpener"
            || typeName == "MediaVideoController"
            || typeName == "MeshellArticleNotebookInputRouter"
            || typeName == "MeshellArticleNotebookOpener"
            || typeName == "InteractableLinkLauncher"
            || typeName == "SpatialAudioToggle"
            || typeName == "QuiltVideoPopUp"
            || typeName == "LindaLeaksVideoPopUp"
            || fullName.Contains("ContinuousMoveProvider")
            || fullName.Contains("ContinuousTurnProvider")
            || fullName.Contains("SnapTurnProvider")
            || fullName.Contains("TeleportationProvider")
            || fullName.Contains("XRSimpleInteractable");
    }

    private void RestoreWorldInput()
    {
        foreach (Behaviour behaviour in disabledWorldInputBehaviours)
        {
            if (behaviour != null)
                behaviour.enabled = true;
        }

        disabledWorldInputBehaviours.Clear();
    }

    private void ResetPage03Scroll()
    {
        if (page03ScrollRect == null)
            return;

        page03ScrollRect.verticalNormalizedPosition = 1f;
    }

    private void RefreshPrompt()
    {
        if (promptText == null)
            return;

        bool xr = InteractionPromptText.IsXRActive();
        // Sanctioned floating prompt: visible on Quest while the hologram is in
        // its idle (approachable) state, hidden while the panel is open.
        WorldInteractionPromptVisual.SetSanctionedText(
            promptText,
            xr ? xrPrompt : desktopPrompt,
            state == ExhibitState.Idle);
    }

    private bool IsVisitor(Collider other)
    {
        if (other == null)
            return false;

        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag))
            return true;

        if (other.GetComponentInParent<CharacterController>() != null)
            return true;

        if (acceptMainCamera && Camera.main != null && other.transform.IsChildOf(Camera.main.transform.root))
            return true;

        return false;
    }

    private void AnimateIdleHologram()
    {
        if (state == ExhibitState.Hidden)
            return;

        float time = Time.time;

        if (hologramAnimationRoot != null)
        {
            Vector3 offset = Vector3.up * (Mathf.Sin(time * floatSpeed) * floatAmplitudeMeters);
            hologramAnimationRoot.localPosition = hologramStartLocalPosition + offset;
        }

        if (blueprintPanel != null)
        {
            float yaw = Mathf.Sin(time * panelYawSpeed) * panelYawDegrees;
            blueprintPanel.localRotation = blueprintStartLocalRotation * Quaternion.Euler(0f, yaw, 0f);
        }

        if (orbitRing01 != null)
            orbitRing01.Rotate(ring01RotationSpeed * Time.deltaTime, Space.Self);
        if (orbitRing02 != null)
            orbitRing02.Rotate(ring02RotationSpeed * Time.deltaTime, Space.Self);
        if (orbitRing03 != null)
            orbitRing03.Rotate(ring03RotationSpeed * Time.deltaTime, Space.Self);
    }

    private void WireButtons()
    {
        if (pageButton01 != null)
            pageButton01.onClick.AddListener(() => SelectPage(0));
        if (pageButton02 != null)
            pageButton02.onClick.AddListener(() => SelectPage(1));
        if (pageButton03 != null)
            pageButton03.onClick.AddListener(() => SelectPage(2));
        if (previousButton != null)
            previousButton.onClick.AddListener(PreviousPage);
        if (nextButton != null)
            nextButton.onClick.AddListener(NextPage);
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseExhibit);
        if (closeIconButton != null)
            closeIconButton.onClick.AddListener(CloseExhibit);
    }

    private void UnwireButtons()
    {
        if (pageButton01 != null)
            pageButton01.onClick.RemoveAllListeners();
        if (pageButton02 != null)
            pageButton02.onClick.RemoveAllListeners();
        if (pageButton03 != null)
            pageButton03.onClick.RemoveAllListeners();
        if (previousButton != null)
            previousButton.onClick.RemoveListener(PreviousPage);
        if (nextButton != null)
            nextButton.onClick.RemoveListener(NextPage);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseExhibit);
        if (closeIconButton != null)
            closeIconButton.onClick.RemoveListener(CloseExhibit);
    }
}
