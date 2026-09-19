using System.Collections;
using System.Collections.Generic;
using BCaT.Production.Interaction;
using BCaT.Production.Media;
using BCaT.Production.Shell;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// One coordinated, mixed-media exhibit opened from the backyard well.
/// Desktop and XR both enter through InteractionRouter, and the whole tabbed
/// panel is one IFocusedExhibit so changing content never creates competing
/// exhibit-open state.
/// </summary>
public sealed class HouseGroundsExhibitController : MonoBehaviour, IInteractionTarget, IFocusedExhibit
{
    public enum ExhibitTab
    {
        HouseRecording,
        Excavation,
        HouseImage
    }

    [System.Serializable]
    public sealed class ExcavationItem
    {
        public bool isVideo;
        public string displayName;
        [TextArea] public string caption;
        public Sprite image;
        public string videoFileName;
    }

    [Header("World Interaction")]
    [SerializeField] private Transform focusPoint;
    [SerializeField] private Transform colliderRoot;
    [SerializeField] private float interactionDistance = 4.5f;
    [SerializeField] private float maxViewAngle = 20f;
    [SerializeField] private SharedInteractionPromptConfig prompt = new()
    {
        verb = SharedInteractionVerb.View,
        objectName = "House and Grounds"
    };

    [Header("Popup")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Canvas popupCanvas;
    [SerializeField] private Image imageDisplay;
    [SerializeField] private RawImage videoDisplay;
    [SerializeField] private AspectRatioFitter imageAspect;
    [SerializeField] private AspectRatioFitter videoAspect;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text captionText;
    [SerializeField] private TMP_Text counterText;
    [SerializeField] private Button recordingTabButton;
    [SerializeField] private Button excavationTabButton;
    [SerializeField] private Button houseImageTabButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button presentationButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private float openDistanceFromCamera = 1.75f;

    [Header("Content")]
    [SerializeField] private string houseRecordingFileName = "HouseGrounds/HouseRecording.mp4";
    [SerializeField] private Sprite houseInProgressImage;
    [SerializeField] private List<ExcavationItem> excavationItems = new();
    [SerializeField] private string presentationUrl;

    [Header("Video")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private AudioSource videoAudioSource;
    [SerializeField] private float prepareTimeoutSeconds = 20f;

    static readonly Color SelectedTabColor = new(0.27f, 0.17f, 0.42f, 1f);
    static readonly Color UnselectedTabColor = new(0.12f, 0.12f, 0.14f, 1f);

    Collider[] ownColliders;
    Collider focusCollider;
    ExhibitTab currentTab;
    int excavationIndex;
    bool isOpen;
    bool playWhenPrepared;
    bool prepareRequested;
    bool ownsTargetTexture;
    bool controlsSuspended;
    bool closeKeyReleasedSinceOpen;
    int openedFrame = -1;
    Coroutine prepareWatchdog;

    public bool IsOpen => isOpen;
    public ExhibitTab CurrentTab => currentTab;
    public int ExcavationIndex => excavationIndex;

    public Vector3 FocusPoint => FocusCollider != null
        ? FocusCollider.bounds.center
        : focusPoint != null ? focusPoint.position : transform.position;
    public float MaxDistance => interactionDistance;
    public float MaxViewAngle => maxViewAngle;
    public bool RequireLineOfSight => true;
    // Desktop selection can overlap Privacy Law's deliberately unfocused,
    // proximity-gated router target.  This is the well's explicit local
    // interaction, so it must win while it is in range and in view. Quest
    // still invokes this controller directly through OnXRSelect.
    public int Priority => 1;
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
        prompt ??= new SharedInteractionPromptConfig
        {
            verb = SharedInteractionVerb.View,
            objectName = "House and Grounds"
        };
        return SharedInteractionPrompt.Format(xr, prompt);
    }

    public void OnFocusChanged(bool focused) { }
    public void OnInteract(InteractionActivation activation) => RequestFocusedOpen();

    /// <summary>Persistent XRSimpleInteractable SelectEntered target.</summary>
    public void OnXRSelect()
    {
        if (InteractionRouter.Instance != null)
            InteractionRouter.Instance.RequestXRSelect(this);
        else
            RequestFocusedOpen();
    }

    void Awake()
    {
        recordingTabButton?.onClick.AddListener(ShowHouseRecording);
        excavationTabButton?.onClick.AddListener(ShowExcavation);
        houseImageTabButton?.onClick.AddListener(ShowHouseImage);
        previousButton?.onClick.AddListener(PreviousExcavationItem);
        nextButton?.onClick.AddListener(NextExcavationItem);
        presentationButton?.onClick.AddListener(OpenPresentation);
        closeButton?.onClick.AddListener(CloseExhibit);

        ConfigureVideoPlayer();
        ConfigureMediaTitleLayout();
        HidePopup();
    }

    // Keep the long excavation titles in their own band below the tabs. The
    // popup is shared by Desktop and Quest, so this is intentionally platform-neutral.
    void ConfigureMediaTitleLayout()
    {
        if (titleText == null)
            return;

        if (titleText.transform is RectTransform titleRect)
        {
            titleRect.anchoredPosition = new Vector2(0f, 286f);
            titleRect.sizeDelta = new Vector2(1120f, 58f);
        }

        titleText.fontSize = 22f;
        titleText.textWrappingMode = TextWrappingModes.Normal;
        titleText.overflowMode = TextOverflowModes.Ellipsis;

        Transform background = titleText.transform.parent;
        RectTransform mediaFrame = background != null
            ? background.Find("MediaFrame") as RectTransform
            : null;
        if (mediaFrame != null)
        {
            mediaFrame.anchoredPosition = new Vector2(0f, -5f);
            mediaFrame.sizeDelta = new Vector2(1160f, 470f);
        }
    }

    void OnEnable() => InteractionRouter.Register(this);

    void OnDisable()
    {
        InteractionRouter.Unregister(this);
        if (isOpen)
            CloseExhibit();
        else
        {
            FocusedExhibitCoordinator.Instance?.NotifyClosed(this);
            ReleasePlayerControls();
        }
    }

    void OnDestroy()
    {
        FocusedExhibitCoordinator.Instance?.NotifyClosed(this);
        ReleaseVideoResources(true);
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnVideoPrepared;
            videoPlayer.errorReceived -= OnVideoError;
            videoPlayer.loopPointReached -= OnVideoEnded;
        }
        MediaPlaybackRegistry.NotifyStopped(this);
        InteractionState.Unblock(this);
        ReleasePlayerControls();
    }

    void Update()
    {
        if (!isOpen)
            return;

        if (Time.frameCount > openedFrame && !FocusedUiInput.InteractHeld)
            closeKeyReleasedSinceOpen = true;
        if (Time.frameCount <= openedFrame)
            return;

        if (FocusedUiInput.CancelPressed)
        {
            CloseExhibit();
            return;
        }

        if (closeKeyReleasedSinceOpen && FocusedUiInput.InteractPressed)
        {
            IInteractionTarget target = InteractionRouter.Instance?.CurrentTarget;
            if (FocusedExhibitCoordinator.Instance?.IsReplacementTarget(target, this) != true)
                CloseExhibit();
            return;
        }

        if (currentTab != ExhibitTab.Excavation)
            return;

        if (FocusedUiInput.NextPressed)
            NextExcavationItem();
        else if (FocusedUiInput.PreviousPressed)
            PreviousExcavationItem();
    }

    public void OpenExhibit() => RequestFocusedOpen();
    void IFocusedExhibit.Open() => OpenInternal();
    void IFocusedExhibit.Close() => CloseExhibit();

    void RequestFocusedOpen()
    {
        if (FocusedExhibitCoordinator.Instance != null)
            FocusedExhibitCoordinator.Instance.RequestOpen(this);
        else
        {
            Debug.LogWarning("[HouseGrounds] FocusedExhibitCoordinator unavailable; opening without coordination.");
            OpenInternal();
        }
    }

    void OpenInternal()
    {
        if (isOpen)
            return;

        isOpen = true;
        openedFrame = Time.frameCount;
        closeKeyReleasedSinceOpen = !FocusedUiInput.InteractHeld;
        excavationIndex = 0;
        ShowPopup();
        PositionPopupInFrontOfCamera();
        SuspendPlayerControls();
        ShowHouseRecording();
    }

    public void CloseExhibit()
    {
        if (!isOpen)
        {
            FocusedExhibitCoordinator.Instance?.NotifyClosed(this);
            return;
        }

        InteractionState.SuppressInputForCurrentFrame();
        isOpen = false;
        StopCurrentVideo();
        ReleaseVideoResources(false);
        HidePopup();
        FocusedExhibitCoordinator.Instance?.NotifyClosed(this);
        InteractionState.Unblock(this);
        ReleasePlayerControls();
    }

    public void ShowHouseRecording()
    {
        if (!isOpen)
            return;
        currentTab = ExhibitTab.HouseRecording;
        SetNavigationVisible(false);
        SetText("House in Progress — Screen Recording",
            "A screen recording of the house while it was being developed.", string.Empty);
        ShowVideo(houseRecordingFileName);
        RefreshTabVisuals();
    }

    public void ShowExcavation()
    {
        if (!isOpen)
            return;
        currentTab = ExhibitTab.Excavation;
        StopCurrentVideo();
        SetNavigationVisible(excavationItems != null && excavationItems.Count > 1);
        RefreshExcavationItem();
        RefreshTabVisuals();
    }

    public void ShowHouseImage()
    {
        if (!isOpen)
            return;
        currentTab = ExhibitTab.HouseImage;
        StopCurrentVideo();
        SetNavigationVisible(false);
        SetText("House in Progress — Still Image",
            "An in-progress view of the house model.", string.Empty);
        ShowImage(houseInProgressImage);
        RefreshTabVisuals();
    }

    public void NextExcavationItem()
    {
        if (!isOpen || currentTab != ExhibitTab.Excavation || excavationItems == null || excavationItems.Count == 0)
            return;
        StopCurrentVideo();
        excavationIndex = (excavationIndex + 1) % excavationItems.Count;
        RefreshExcavationItem();
    }

    public void PreviousExcavationItem()
    {
        if (!isOpen || currentTab != ExhibitTab.Excavation || excavationItems == null || excavationItems.Count == 0)
            return;
        StopCurrentVideo();
        excavationIndex = (excavationIndex - 1 + excavationItems.Count) % excavationItems.Count;
        RefreshExcavationItem();
    }

    public void OpenPresentation()
    {
        if (string.IsNullOrWhiteSpace(presentationUrl))
            return;

        StopCurrentVideo();
        BCaT.Production.QuestBrowserHeadTracking.OpenUrl(presentationUrl);
    }

    void RefreshExcavationItem()
    {
        if (excavationItems == null || excavationItems.Count == 0)
        {
            SetText("Excavation and Presentation", "No media is available.", "0 / 0");
            ShowImage(null);
            return;
        }

        excavationIndex = Mathf.Clamp(excavationIndex, 0, excavationItems.Count - 1);
        ExcavationItem item = excavationItems[excavationIndex];
        SetText(item.displayName, item.caption, $"{excavationIndex + 1} / {excavationItems.Count}");
        if (item.isVideo)
            ShowVideo(item.videoFileName);
        else
            ShowImage(item.image);
    }

    void SetText(string title, string caption, string counter)
    {
        if (titleText != null) titleText.text = title;
        if (captionText != null) captionText.text = caption;
        if (counterText != null) counterText.text = counter;
    }

    void ShowImage(Sprite sprite)
    {
        if (videoDisplay != null)
            videoDisplay.enabled = false;
        if (imageDisplay == null)
            return;

        imageDisplay.sprite = sprite;
        imageDisplay.enabled = sprite != null;
        imageDisplay.preserveAspect = true;
        if (imageAspect != null && sprite != null && sprite.texture != null && sprite.texture.height > 0)
            imageAspect.aspectRatio = (float)sprite.texture.width / sprite.texture.height;
    }

    void ShowVideo(string fileName)
    {
        StopCurrentVideo();
        if (imageDisplay != null)
            imageDisplay.enabled = false;
        if (videoPlayer == null || string.IsNullOrWhiteSpace(fileName))
            return;

        EnsureRenderTexture();
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = RuntimeMediaPaths.ResolveMediaUrl(fileName);
        videoPlayer.time = 0;
        playWhenPrepared = true;
        prepareRequested = true;
        if (videoDisplay != null)
            videoDisplay.enabled = false;
        videoPlayer.Prepare();
        StartPrepareWatchdog();
    }

    void ConfigureVideoPlayer()
    {
        if (videoPlayer == null)
            return;
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.aspectRatio = VideoAspectRatio.FitInside;
        videoPlayer.prepareCompleted += OnVideoPrepared;
        videoPlayer.errorReceived += OnVideoError;
        videoPlayer.loopPointReached += OnVideoEnded;

        if (videoAudioSource != null)
        {
            videoAudioSource.playOnAwake = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
#else
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.SetTargetAudioSource(0, videoAudioSource);
#endif
        }
        else
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
    }

    void EnsureRenderTexture()
    {
        if (videoPlayer.targetTexture == null)
        {
            videoPlayer.targetTexture = new RenderTexture(1920, 1080, 0)
            {
                name = gameObject.name + "_HouseGroundsVideoRT"
            };
            ownsTargetTexture = true;
        }
        if (videoDisplay != null)
            videoDisplay.texture = videoPlayer.targetTexture;
    }

    void OnVideoPrepared(VideoPlayer preparedPlayer)
    {
        prepareRequested = false;
        StopPrepareWatchdog();
        if (!isOpen || !playWhenPrepared || preparedPlayer == null || preparedPlayer.height == 0)
            return;

        ResizeOwnedRenderTexture((int)preparedPlayer.width, (int)preparedPlayer.height);
        if (videoAspect != null)
            videoAspect.aspectRatio = (float)preparedPlayer.width / preparedPlayer.height;
        ForegroundAudioCoordinator.Instance?.StopCurrent();
        playWhenPrepared = false;
        if (videoDisplay != null)
            videoDisplay.enabled = true;
        preparedPlayer.Play();
        MediaPlaybackRegistry.NotifyStarted(this, CloseExhibit);
    }

    void OnVideoError(VideoPlayer source, string message)
    {
        prepareRequested = false;
        playWhenPrepared = false;
        StopPrepareWatchdog();
        MediaPlaybackRegistry.NotifyStopped(this);
        string path = source != null ? source.url : "unknown source";
        MediaErrorLog.LogFailure("House and Grounds", path, message, remoteAttempted: true, recovered: true);
        if (captionText != null)
            captionText.text = "This video is currently unavailable.";
    }

    void OnVideoEnded(VideoPlayer endedPlayer) => StopCurrentVideo();

    void StopCurrentVideo()
    {
        playWhenPrepared = false;
        prepareRequested = false;
        StopPrepareWatchdog();
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
            ClearTargetTexture();
        }
        videoAudioSource?.Stop();
        MediaPlaybackRegistry.NotifyStopped(this);
    }

    void ReleaseVideoResources(bool destroyOwnedTexture)
    {
        StopCurrentVideo();
        if (videoPlayer == null)
            return;
        if (videoPlayer.source == VideoSource.Url)
            videoPlayer.url = string.Empty;
        if (videoDisplay != null)
            videoDisplay.texture = null;
        if (destroyOwnedTexture && ownsTargetTexture && videoPlayer.targetTexture != null)
        {
            RenderTexture texture = videoPlayer.targetTexture;
            videoPlayer.targetTexture = null;
            texture.Release();
            Destroy(texture);
            ownsTargetTexture = false;
        }
    }

    void ResizeOwnedRenderTexture(int width, int height)
    {
        if (videoPlayer == null || width <= 0 || height <= 0)
            return;
        RenderTexture current = videoPlayer.targetTexture;
        if (current != null && current.width == width && current.height == height)
            return;
        if (ownsTargetTexture && current != null)
        {
            videoPlayer.targetTexture = null;
            current.Release();
            Destroy(current);
        }
        RenderTexture replacement = new(width, height, 0)
        {
            name = gameObject.name + "_HouseGroundsVideoRT"
        };
        videoPlayer.targetTexture = replacement;
        ownsTargetTexture = true;
        if (videoDisplay != null)
            videoDisplay.texture = replacement;
    }

    void ClearTargetTexture()
    {
        RenderTexture texture = videoPlayer != null ? videoPlayer.targetTexture : null;
        if (texture == null || !texture.IsCreated())
            return;
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = texture;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = previous;
    }

    void StartPrepareWatchdog()
    {
        StopPrepareWatchdog();
        if (prepareTimeoutSeconds > 0f)
            prepareWatchdog = StartCoroutine(PrepareWatchdog());
    }

    void StopPrepareWatchdog()
    {
        if (prepareWatchdog == null)
            return;
        StopCoroutine(prepareWatchdog);
        prepareWatchdog = null;
    }

    IEnumerator PrepareWatchdog()
    {
        yield return new WaitForSecondsRealtime(prepareTimeoutSeconds);
        prepareWatchdog = null;
        if (!prepareRequested || videoPlayer == null || videoPlayer.isPrepared)
            yield break;
        string path = videoPlayer.url;
        StopCurrentVideo();
        MediaErrorLog.LogFailure("House and Grounds", path,
            $"prepare timeout after {prepareTimeoutSeconds:F0}s", remoteAttempted: true, recovered: true);
        if (captionText != null)
            captionText.text = "This video is currently unavailable.";
    }

    void RefreshTabVisuals()
    {
        SetTabColor(recordingTabButton, currentTab == ExhibitTab.HouseRecording);
        SetTabColor(excavationTabButton, currentTab == ExhibitTab.Excavation);
        SetTabColor(houseImageTabButton, currentTab == ExhibitTab.HouseImage);
    }

    static void SetTabColor(Button button, bool selected)
    {
        if (button != null && button.targetGraphic is Graphic graphic)
            graphic.color = selected ? SelectedTabColor : UnselectedTabColor;
        Outline outline = button != null ? button.GetComponent<Outline>() : null;
        if (outline != null)
            outline.enabled = selected;
    }

    void SetNavigationVisible(bool visible)
    {
        if (previousButton != null) previousButton.gameObject.SetActive(visible);
        if (nextButton != null) nextButton.gameObject.SetActive(visible);
        if (counterText != null) counterText.gameObject.SetActive(visible);
    }

    void ShowPopup()
    {
        popupRoot?.SetActive(true);
        if (popupCanvas != null)
        {
            popupCanvas.enabled = true;
            popupCanvas.overrideSorting = true;
            popupCanvas.sortingOrder = 130;
        }
    }

    void HidePopup()
    {
        popupRoot?.SetActive(false);
        if (popupCanvas != null) popupCanvas.enabled = false;
        if (imageDisplay != null) imageDisplay.enabled = false;
        if (videoDisplay != null) videoDisplay.enabled = false;
    }

    void PositionPopupInFrontOfCamera()
    {
        Camera camera = FindActiveCamera();
        if (camera == null || popupRoot == null)
            return;
        popupRoot.transform.position = camera.transform.position + camera.transform.forward * openDistanceFromCamera;
        Vector3 away = (popupRoot.transform.position - camera.transform.position).normalized;
        popupRoot.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        if (popupCanvas != null)
            popupCanvas.worldCamera = camera;
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
            camera.cullingMask |= 1 << uiLayer;
    }

    static Camera FindActiveCamera()
    {
        if (Camera.main != null && Camera.main.isActiveAndEnabled)
            return Camera.main;
        foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (camera != null && camera.isActiveAndEnabled)
                return camera;
        return null;
    }

    void SuspendPlayerControls()
    {
        if (controlsSuspended) return;
        PlayerControlGate.Suspend(this);
        controlsSuspended = true;
    }

    void ReleasePlayerControls()
    {
        if (!controlsSuspended) return;
        PlayerControlGate.Resume(this);
        controlsSuspended = false;
    }

    Collider FocusCollider
    {
        get
        {
            if (focusCollider != null)
                return focusCollider;
            foreach (Collider candidate in OwnColliders)
                if (candidate != null && candidate.enabled)
                    return focusCollider = candidate;
            return null;
        }
    }
}
