using BCaT.Production.Interaction;
using BCaT.Production.Media;
using BCaT.Production.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One interactive Adinkra symbol. The 3D symbol is the interaction target
/// (same model as the Linda Leaks artifacts and the mural panel): the central
/// InteractionRouter owns candidate selection, focus and input dispatch, and
/// this controller only owns the outcome — a focused modal carrying the symbol
/// name, its meaning, and a narration button that drives one AudioSource.
///
/// Everything shared is reused rather than re-implemented: InteractionRouter /
/// IInteractionTarget for world interaction, SharedInteractionPrompt for prompt
/// wording, InteractionState for modal blocking, MediaPlaybackRegistry so the
/// kiosk reset can stop narration, AudioChannelService for the Narration mixer
/// channel, SubtitleService for captions/transcripts, and the same
/// Application.OpenURL path as InteractableLinkLauncher for external links.
/// </summary>
public sealed class AdinkraSymbolExhibit : MonoBehaviour, IInteractionTarget
{
    [Header("Symbol Content")]
    [Tooltip("Symbol title shown in the modal, e.g. 'Sankofa'.")]
    [SerializeField] private string symbolName = "Adinkra Symbol";

    [Tooltip("Meaning shown in the modal body.")]
    [TextArea(3, 8)]
    [SerializeField] private string meaning = "";

    [Tooltip("The 3D model that represents this symbol (the GLB instance).")]
    [SerializeField] private Transform modelRoot;

    [Header("Narration")]
    [SerializeField] private AudioClip narrationClip;
    [SerializeField] private AudioSource narrationSource;
    [Range(0f, 1f)]
    [SerializeField] private float narrationVolume = 0.9f;

    [Tooltip("Media id used for subtitles/transcripts. Matches a SubtitleTrack mediaId when one is authored.")]
    [SerializeField] private string narrationMediaId = "";

    [Header("Optional Website")]
    [Tooltip("Leave empty to hide the website button.")]
    [SerializeField] private string websiteUrl = "";
    [SerializeField] private string websiteButtonLabel = "Visit Website";

    [Header("Optional Future Video")]
    [Tooltip("Shows the titled 'Video' section in the modal as a placeholder for media added later.")]
    [SerializeField] private bool showVideoSection;

    [Tooltip("Reserved for the future clip (StreamingAssets-relative name, resolved by RuntimeMediaPaths when wired).")]
    [SerializeField] private string futureVideoFileName = "";

    [TextArea(2, 4)]
    [SerializeField] private string videoPlaceholderNote = "Video coming soon.";

    [Header("World Interaction")]
    [SerializeField] private Transform focusPoint;
    [SerializeField] private Transform colliderRoot;
    [SerializeField] private float interactionDistance = 3.5f;
    [SerializeField] private float maxViewAngle = 18f;
    [SerializeField] private TMP_Text worldPromptText;
    [SerializeField] private SharedInteractionPromptConfig prompt =
        new SharedInteractionPromptConfig
        {
            desktopPrompt = "Press E to Examine Symbol",
            xrPrompt = "Interact to Examine Symbol"
        };

    [Header("Modal")]
    [SerializeField] private GameObject modalRoot;
    [SerializeField] private Canvas modalCanvas;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text meaningText;
    [SerializeField] private Button narrationButton;
    [SerializeField] private TMP_Text narrationButtonLabel;
    [SerializeField] private TMP_Text narrationStatusText;
    [SerializeField] private GameObject videoSection;
    [SerializeField] private TMP_Text videoPlaceholderText;
    [SerializeField] private Button websiteButton;
    [SerializeField] private TMP_Text websiteButtonLabelText;
    [SerializeField] private Button closeButton;
    [SerializeField] private float openDistanceFromCamera = 1.6f;

    private const string PlayLabel = "Play Narration";
    private const string StopLabel = "Stop Narration";

    private Collider[] ownColliders;
    private Collider focusCollider;
    private bool isOpen;
    private bool narrationRegistered;
    private int openedFrame = -1;
    private bool closeKeyReleasedSinceOpen;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockState;

    public bool IsOpen => isOpen;
    public string SymbolName => symbolName;
    public bool IsNarrationPlaying =>
        narrationSource != null && narrationClip != null &&
        narrationSource.isPlaying && narrationSource.clip == narrationClip;

    // ---- IInteractionTarget ---------------------------------------------

    public Vector3 FocusPoint
    {
        get
        {
            Collider collider = FocusCollider;
            if (collider != null)
                return collider.bounds.center;
            return focusPoint != null ? focusPoint.position : transform.position;
        }
    }

    public float MaxDistance => interactionDistance;
    public float MaxViewAngle => maxViewAngle;
    public bool RequireLineOfSight => true;
    public int Priority => 0;
    public bool IsAvailable => isActiveAndEnabled && !isOpen;
    public bool AllowDesktopClick => true;
    public bool Exists => this != null;

    public Collider[] OwnColliders
    {
        get
        {
            if (ownColliders == null)
            {
                Transform root = colliderRoot != null ? colliderRoot : transform;
                ownColliders = root.GetComponentsInChildren<Collider>(true);
            }

            return ownColliders;
        }
    }

    public string GetPrompt(bool xr)
    {
        if (prompt == null)
            prompt = new SharedInteractionPromptConfig();

        if (string.IsNullOrWhiteSpace(prompt.desktopPrompt))
            prompt.desktopPrompt = "Press E to Examine Symbol";

        // Quest names the actual symbol ("View — Sankofa") instead of the
        // generic "Interact to Examine Symbol", using the exhibit's own
        // authored symbolName so no wording is invented here.
        if (string.IsNullOrWhiteSpace(prompt.xrPrompt) && xr)
        {
            string name = string.IsNullOrWhiteSpace(symbolName) ? "Adinkra Symbol" : symbolName.Trim();
            return SharedInteractionPrompt.Format(true, SharedInteractionVerb.View, name);
        }

        return SharedInteractionPrompt.Format(xr, prompt);
    }

    public void OnFocusChanged(bool focused)
    {
        WorldInteractionPromptVisual.SetText(worldPromptText, GetPrompt(InteractionPromptText.IsXRActive()));
    }

    public void OnInteract(InteractionActivation activation) => OpenModal();

    /// <summary>Quest relay entry point (XRSimpleInteractable.selectEntered).</summary>
    public void OnXRSelect()
    {
        if (isOpen)
        {
            CloseModal();
            return;
        }

        if (InteractionRouter.Instance != null)
            InteractionRouter.Instance.RequestXRSelect(this);
        else
            OpenModal();
    }

    // ---- Lifecycle ------------------------------------------------------

    private void Awake()
    {
        if (narrationButton != null)
            narrationButton.onClick.AddListener(ToggleNarration);
        if (websiteButton != null)
            websiteButton.onClick.AddListener(OpenWebsite);
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseModal);

        ConfigureNarrationSource();
        HideModal();
    }

    private void OnEnable() => InteractionRouter.Register(this);

    private void OnDisable()
    {
        InteractionRouter.Unregister(this);
        if (isOpen)
            CloseModal();
    }

    private void OnDestroy()
    {
        StopNarration();
        MediaPlaybackRegistry.NotifyStopped(this);
        InteractionState.Unblock(this);
    }

    private void Update()
    {
        WorldInteractionPromptVisual.SetText(worldPromptText, GetPrompt(InteractionPromptText.IsXRActive()));

        if (!isOpen)
            return;

        PublishNarrationTime();

        // Narration that reached its end resets the button without a click.
        if (narrationRegistered && !IsNarrationPlaying)
            StopNarration();

        if (Time.frameCount > openedFrame && !FocusedUiInput.InteractHeld)
            closeKeyReleasedSinceOpen = true;

        if (Time.frameCount <= openedFrame)
            return;

        // Documented modal shortcuts for this exhibit: Escape/E close, Enter
        // toggles the narration so the modal is operable without the pointer.
        if (FocusedUiInput.CancelPressed ||
            (closeKeyReleasedSinceOpen && FocusedUiInput.InteractPressed))
        {
            CloseModal();
            return;
        }

        if (FocusedUiInput.SubmitPressed)
            ToggleNarration();
    }

    // ---- Modal ----------------------------------------------------------

    public void OpenModal()
    {
        if (isOpen)
            return;

        isOpen = true;
        openedFrame = Time.frameCount;
        closeKeyReleasedSinceOpen = !FocusedUiInput.InteractHeld;

        ShowModal();
        PositionModalInFrontOfCamera();
        CaptureInput();
        Refresh();

        InteractionState.Block(this, InteractionBlockReason.Modal, CloseModal);
        Debug.Log($"[Adinkra:{symbolName}] Modal opened.");
    }

    public void CloseModal()
    {
        if (!isOpen)
            return;

        InteractionState.SuppressInputForCurrentFrame();
        isOpen = false;
        StopNarration();
        InteractionState.Unblock(this);
        HideModal();
        RestoreInput();
    }

    private void Refresh()
    {
        if (titleText != null)
            titleText.text = symbolName;
        if (meaningText != null)
            meaningText.text = meaning;

        bool hasNarration = narrationClip != null && narrationSource != null;
        if (narrationButton != null)
            narrationButton.gameObject.SetActive(hasNarration);
        if (narrationButtonLabel != null)
            narrationButtonLabel.text = PlayLabel;
        if (narrationStatusText != null)
            narrationStatusText.text = hasNarration ? string.Empty : "Narration unavailable.";

        bool showWebsite = !string.IsNullOrWhiteSpace(websiteUrl) &&
                           BCaT.Production.PlatformCapabilities.SupportsExternalLinks;
        if (websiteButton != null)
            websiteButton.gameObject.SetActive(showWebsite);
        if (websiteButtonLabelText != null && !string.IsNullOrWhiteSpace(websiteButtonLabel))
            websiteButtonLabelText.text = websiteButtonLabel;

        if (videoSection != null)
            videoSection.SetActive(showVideoSection);
        if (videoPlaceholderText != null)
            videoPlaceholderText.text = videoPlaceholderNote;
    }

    private void ShowModal()
    {
        if (modalRoot != null)
            modalRoot.SetActive(true);
        if (modalCanvas != null)
        {
            modalCanvas.enabled = true;
            modalCanvas.overrideSorting = true;
            modalCanvas.sortingOrder = 120;
        }
    }

    private void HideModal()
    {
        if (modalRoot != null)
            modalRoot.SetActive(false);
        if (modalCanvas != null)
            modalCanvas.enabled = false;
    }

    private void PositionModalInFrontOfCamera()
    {
        Camera camera = FindActiveCamera();
        if (camera == null || modalRoot == null)
            return;

        modalRoot.transform.position = camera.transform.position + camera.transform.forward * openDistanceFromCamera;
        Vector3 away = (modalRoot.transform.position - camera.transform.position).normalized;
        modalRoot.transform.rotation = Quaternion.LookRotation(away, Vector3.up);

        if (modalCanvas != null)
            modalCanvas.worldCamera = camera;

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
            camera.cullingMask |= 1 << uiLayer;
    }

    private Camera FindActiveCamera()
    {
        if (Camera.main != null && Camera.main.isActiveAndEnabled)
            return Camera.main;

        foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (camera != null && camera.isActiveAndEnabled)
                return camera;

        return null;
    }

    private void CaptureInput()
    {
        previousCursorLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestoreInput()
    {
        Cursor.lockState = previousCursorLockState;
        Cursor.visible = previousCursorVisible;
    }

    // ---- Narration ------------------------------------------------------

    private void ConfigureNarrationSource()
    {
        if (narrationSource == null)
            return;

        narrationSource.playOnAwake = false;
        narrationSource.loop = false;
        narrationSource.volume = narrationVolume;
        AudioChannelService.Register(narrationSource, AudioChannel.Narration);
    }

    public void ToggleNarration()
    {
        if (IsNarrationPlaying)
            StopNarration();
        else
            PlayNarration();
    }

    public void PlayNarration()
    {
        if (narrationSource == null || narrationClip == null)
        {
            Debug.LogWarning($"[Adinkra:{symbolName}] Narration requested with no clip or AudioSource assigned.");
            if (narrationStatusText != null)
                narrationStatusText.text = "Narration unavailable.";
            return;
        }

        narrationSource.clip = narrationClip;
        narrationSource.time = 0f;
        narrationSource.volume = AudioChannelService.ScaledVolume(narrationSource, narrationVolume);
        narrationSource.Play();

        narrationRegistered = true;
        MediaPlaybackRegistry.NotifyStarted(this, StopNarration);
        BCaT.Production.Access.SubtitleService.Instance?.NotifyMediaStarted(SubtitleMediaId);

        if (narrationButtonLabel != null)
            narrationButtonLabel.text = StopLabel;
        if (narrationStatusText != null)
            narrationStatusText.text = "Playing narration…";
    }

    public void StopNarration()
    {
        if (narrationSource != null && narrationSource.clip == narrationClip)
            narrationSource.Stop();

        if (narrationRegistered)
        {
            narrationRegistered = false;
            BCaT.Production.Access.SubtitleService.Instance?.NotifyMediaStopped(SubtitleMediaId);
        }

        MediaPlaybackRegistry.NotifyStopped(this);

        if (narrationButtonLabel != null)
            narrationButtonLabel.text = PlayLabel;
        if (narrationStatusText != null && narrationClip != null)
            narrationStatusText.text = string.Empty;
    }

    private void PublishNarrationTime()
    {
        if (!narrationRegistered || !IsNarrationPlaying)
            return;

        BCaT.Production.Access.SubtitleService.Instance?
            .NotifyMediaTime(SubtitleMediaId, narrationSource.time);
    }

    private string SubtitleMediaId =>
        !string.IsNullOrWhiteSpace(narrationMediaId)
            ? narrationMediaId
            : (narrationClip != null ? narrationClip.name : symbolName);

    // ---- Optional website -----------------------------------------------

    public void OpenWebsite()
    {
        if (string.IsNullOrWhiteSpace(websiteUrl))
            return;

        if (!BCaT.Production.PlatformCapabilities.SupportsExternalLinks)
        {
            Debug.Log($"[Adinkra:{symbolName}] External links unsupported on this platform.");
            return;
        }

        Debug.Log($"[Adinkra:{symbolName}] Opening external link: {websiteUrl}");
        Application.OpenURL(websiteUrl);
    }

    // ---------------------------------------------------------------------

    private Collider FocusCollider
    {
        get
        {
            if (focusCollider == null)
            {
                Collider[] colliders = OwnColliders;
                if (colliders != null)
                {
                    foreach (Collider candidate in colliders)
                    {
                        if (candidate != null && candidate.enabled && !candidate.isTrigger)
                        {
                            focusCollider = candidate;
                            break;
                        }
                    }
                }
            }

            return focusCollider;
        }
    }
}
