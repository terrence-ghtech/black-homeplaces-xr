using System.Collections;
using System.Collections.Generic;
using BCaT.Production.Interaction;
using BCaT.Production.Media;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed class MuralExhibitController : MonoBehaviour, IInteractionTarget
{
    public enum GalleryItemType
    {
        Image,
        Video
    }

    [System.Serializable]
    public sealed class GalleryItem
    {
        public GalleryItemType type = GalleryItemType.Image;
        public string displayName;
        [TextArea] public string caption;
        public Sprite image;
        public string videoFileName;
        public string videoUrlOverride;
        public VideoClip editorPreviewClip;
    }

    [Header("World Interaction")]
    [SerializeField] private Transform focusPoint;
    [SerializeField] private Transform colliderRoot;
    [SerializeField] private float interactionDistance = 4.5f;
    [SerializeField] private float maxViewAngle = 18f;
    [SerializeField] private TMP_Text worldPromptText;
    [SerializeField] private SharedInteractionPromptConfig prompt =
        new SharedInteractionPromptConfig
        {
            desktopPrompt = "Press E",
            xrPrompt = "Interact"
        };

    [Header("Gallery")]
    [SerializeField] private GameObject galleryRoot;
    [SerializeField] private Canvas galleryCanvas;
    [SerializeField] private Image imageDisplay;
    [SerializeField] private RawImage videoDisplay;
    [SerializeField] private AspectRatioFitter imageAspect;
    [SerializeField] private AspectRatioFitter videoAspect;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text captionText;
    [SerializeField] private TMP_Text counterText;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private float openDistanceFromCamera = 1.75f;
    [SerializeField] private List<GalleryItem> items = new();

    [Header("Video")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private AudioSource videoAudioSource;
    [SerializeField] private float prepareTimeoutSeconds = 20f;

    private Collider[] ownColliders;
    private Collider focusCollider;
    private bool isOpen;
    private bool playWhenPrepared;
    private bool prepareRequested;
    private bool ownsTargetTexture;
    private Coroutine prepareWatchdog;
    private int currentIndex;
    private int openedFrame = -1;
    private bool closeKeyReleasedSinceOpen;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockState;

    private bool CurrentItemIsVideo =>
        isOpen && items != null && currentIndex >= 0 && currentIndex < items.Count &&
        items[currentIndex].type == GalleryItemType.Video;

    public bool IsOpen => isOpen;
    public int CurrentIndex => currentIndex;
    public int ItemCount => items != null ? items.Count : 0;

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
            prompt.desktopPrompt = "Press E";
        if (string.IsNullOrWhiteSpace(prompt.xrPrompt))
            prompt.xrPrompt = "Interact";

        return SharedInteractionPrompt.Format(xr, prompt);
    }

    public void OnFocusChanged(bool focused)
    {
        WorldInteractionPromptVisual.SetText(worldPromptText, GetPrompt(InteractionPromptText.IsXRActive()));
    }

    public void OnInteract(InteractionActivation activation) => OpenGallery();

    public void OnXRSelect()
    {
        if (isOpen)
        {
            CloseGallery();
            return;
        }

        if (InteractionRouter.Instance != null)
            InteractionRouter.Instance.RequestXRSelect(this);
        else
            OpenGallery();
    }

    private void Awake()
    {
        if (previousButton != null)
            previousButton.onClick.AddListener(Previous);
        if (nextButton != null)
            nextButton.onClick.AddListener(Next);
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseGallery);

        ConfigureVideoPlayer();
        HideGallery();
    }

    private void OnEnable() => InteractionRouter.Register(this);

    private void OnDisable()
    {
        InteractionRouter.Unregister(this);
        if (isOpen)
            CloseGallery();
    }

    private void OnDestroy()
    {
        ReleaseVideoResources(true);
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnVideoPrepared;
            videoPlayer.errorReceived -= OnVideoError;
            videoPlayer.loopPointReached -= OnVideoEnded;
        }

        MediaPlaybackRegistry.NotifyStopped(this);
        InteractionState.Unblock(this);
    }

    private void Update()
    {
        WorldInteractionPromptVisual.SetText(worldPromptText, GetPrompt(InteractionPromptText.IsXRActive()));

        if (!isOpen)
            return;

        if (Time.frameCount > openedFrame && !FocusedUiInput.InteractHeld)
            closeKeyReleasedSinceOpen = true;

        if (Time.frameCount <= openedFrame)
            return;

        if (FocusedUiInput.CancelPressed ||
            (closeKeyReleasedSinceOpen && FocusedUiInput.InteractPressed))
        {
            CloseGallery();
            return;
        }

        if (FocusedUiInput.NextPressed)
            Next();
        else if (FocusedUiInput.PreviousPressed)
            Previous();
    }

    public void OpenGallery()
    {
        if (isOpen)
            return;

        isOpen = true;
        openedFrame = Time.frameCount;
        closeKeyReleasedSinceOpen = !FocusedUiInput.InteractHeld;
        currentIndex = 0;

        ShowGallery();
        PositionGalleryInFrontOfCamera();
        CaptureInput();
        Refresh();

        InteractionState.Block(this, InteractionBlockReason.Modal, CloseGallery);
    }

    public void CloseGallery()
    {
        if (!isOpen)
            return;

        InteractionState.SuppressInputForCurrentFrame();
        isOpen = false;
        StopCurrentVideo();
        ReleaseVideoResources(false);
        MediaPlaybackRegistry.NotifyStopped(this);
        InteractionState.Unblock(this);
        HideGallery();
        RestoreInput();
    }

    public void Next()
    {
        if (!isOpen || items == null || items.Count == 0)
            return;

        bool wasVideo = CurrentItemIsVideo;
        currentIndex = (currentIndex + 1) % items.Count;
        if (wasVideo)
            StopCurrentVideo();
        Refresh();
    }

    public void Previous()
    {
        if (!isOpen || items == null || items.Count == 0)
            return;

        bool wasVideo = CurrentItemIsVideo;
        currentIndex = (currentIndex - 1 + items.Count) % items.Count;
        if (wasVideo)
            StopCurrentVideo();
        Refresh();
    }

    private void Refresh()
    {
        if (items == null || items.Count == 0)
        {
            ShowImage(null);
            SetText(string.Empty, string.Empty, "0 / 0");
            return;
        }

        currentIndex = Mathf.Clamp(currentIndex, 0, items.Count - 1);
        GalleryItem item = items[currentIndex];
        SetText(item.displayName, item.caption, $"{currentIndex + 1} / {items.Count}");

        if (item.type == GalleryItemType.Video)
        {
            ShowVideo();
            BeginVideoItem(item);
        }
        else
        {
            StopCurrentVideo();
            ShowImage(item.image);
        }
    }

    private void SetText(string title, string caption, string counter)
    {
        if (titleText != null)
            titleText.text = title;
        if (captionText != null)
            captionText.text = caption;
        if (counterText != null)
            counterText.text = counter;
    }

    private void ShowImage(Sprite sprite)
    {
        if (videoDisplay != null)
            videoDisplay.enabled = false;

        if (imageDisplay == null)
            return;

        imageDisplay.enabled = sprite != null;
        imageDisplay.sprite = sprite;
        imageDisplay.preserveAspect = true;

        if (imageAspect != null && sprite != null && sprite.texture != null && sprite.texture.height > 0)
            imageAspect.aspectRatio = (float)sprite.texture.width / sprite.texture.height;
    }

    private void ShowVideo()
    {
        if (imageDisplay != null)
            imageDisplay.enabled = false;
        if (videoDisplay != null)
            videoDisplay.enabled = true;
    }

    private void ConfigureVideoPlayer()
    {
        if (videoPlayer == null)
            return;

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.prepareCompleted += OnVideoPrepared;
        videoPlayer.errorReceived += OnVideoError;
        videoPlayer.loopPointReached += OnVideoEnded;

        if (videoAudioSource != null)
        {
            videoAudioSource.playOnAwake = false;
            videoAudioSource.Stop();
#if UNITY_WEBGL && !UNITY_EDITOR
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
#else
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.SetTargetAudioSource(0, videoAudioSource);
#endif
        }
        else
        {
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        }
    }

    private void BeginVideoItem(GalleryItem item)
    {
        if (videoPlayer == null || item == null)
            return;

        EnsureVideoSource(item);
        EnsureRenderTexture();
        if (videoAspect != null)
            videoAspect.aspectRatio = 16f / 9f;

        videoPlayer.time = 0;
        playWhenPrepared = true;
        if (videoPlayer.isPrepared)
        {
            PlayPreparedVideo();
            return;
        }

        if (!prepareRequested)
        {
            prepareRequested = true;
            videoPlayer.Prepare();
            StartPrepareWatchdog();
        }
    }

    private void EnsureVideoSource(GalleryItem item)
    {
#if UNITY_EDITOR
        if (item.editorPreviewClip != null)
        {
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = item.editorPreviewClip;
            return;
        }
#endif
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = string.IsNullOrWhiteSpace(item.videoUrlOverride)
            ? RuntimeMediaPaths.ResolveMediaUrl(item.videoFileName)
            : item.videoUrlOverride;
    }

    private void EnsureRenderTexture()
    {
        if (videoPlayer == null)
            return;

        if (videoPlayer.renderMode == VideoRenderMode.RenderTexture && videoPlayer.targetTexture == null)
        {
            RenderTexture rt = new(1280, 720, 0)
            {
                name = gameObject.name + "_MuralVideoRT"
            };
            videoPlayer.targetTexture = rt;
            ownsTargetTexture = true;
        }

        if (videoDisplay != null)
            videoDisplay.texture = videoPlayer.targetTexture;
    }

    private void OnVideoPrepared(VideoPlayer preparedPlayer)
    {
        prepareRequested = false;
        StopPrepareWatchdog();

        if (preparedPlayer.height > 0 && videoAspect != null)
            videoAspect.aspectRatio = (float)preparedPlayer.width / preparedPlayer.height;

        if (isOpen && CurrentItemIsVideo && playWhenPrepared)
            PlayPreparedVideo();
    }

    private void PlayPreparedVideo()
    {
        if (videoPlayer == null)
            return;

        playWhenPrepared = false;
        videoPlayer.time = 0;
        videoPlayer.Play();
        MediaPlaybackRegistry.NotifyStarted(this, CloseGallery);
    }

    private void OnVideoError(VideoPlayer source, string message)
    {
        prepareRequested = false;
        playWhenPrepared = false;
        StopPrepareWatchdog();
        MediaPlaybackRegistry.NotifyStopped(this);

        string path = source != null && source.source == VideoSource.Url ? source.url : "editor preview clip";
        MediaErrorLog.LogFailure("Mural Exhibit", path, message, remoteAttempted: true, recovered: true);

        if (captionText != null)
            captionText.text = "The mural process video is currently unavailable.";
    }

    private void OnVideoEnded(VideoPlayer endedPlayer)
    {
        StopCurrentVideo();
    }

    private void StopCurrentVideo()
    {
        playWhenPrepared = false;
        prepareRequested = false;
        StopPrepareWatchdog();

        if (videoPlayer != null)
        {
            videoPlayer.Stop();
            ClearTargetTexture();
        }

        if (videoAudioSource != null)
            videoAudioSource.Stop();

        MediaPlaybackRegistry.NotifyStopped(this);
    }

    private void ReleaseVideoResources(bool destroyOwnedTexture)
    {
        StopCurrentVideo();

        if (videoPlayer == null)
            return;

        if (videoPlayer.source == VideoSource.Url)
            videoPlayer.url = string.Empty;
#if UNITY_EDITOR
        else if (videoPlayer.source == VideoSource.VideoClip)
            videoPlayer.clip = null;
#endif

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

    private void StartPrepareWatchdog()
    {
        StopPrepareWatchdog();
        if (prepareTimeoutSeconds > 0f)
            prepareWatchdog = StartCoroutine(PrepareWatchdog());
    }

    private void StopPrepareWatchdog()
    {
        if (prepareWatchdog != null)
        {
            StopCoroutine(prepareWatchdog);
            prepareWatchdog = null;
        }
    }

    private IEnumerator PrepareWatchdog()
    {
        yield return new WaitForSecondsRealtime(prepareTimeoutSeconds);
        prepareWatchdog = null;

        if (!prepareRequested || videoPlayer == null || videoPlayer.isPrepared)
            yield break;

        string path = videoPlayer.source == VideoSource.Url ? videoPlayer.url : "editor preview clip";
        StopCurrentVideo();
        MediaErrorLog.LogFailure("Mural Exhibit", path,
            $"prepare timeout after {prepareTimeoutSeconds:F0}s", remoteAttempted: true, recovered: true);

        if (captionText != null)
            captionText.text = "The mural process video is currently unavailable.";
    }

    private void ClearTargetTexture()
    {
        RenderTexture texture = videoPlayer != null ? videoPlayer.targetTexture : null;
        if (texture == null || !texture.IsCreated())
            return;

        RenderTexture previous = RenderTexture.active;
        try
        {
            RenderTexture.active = texture;
            GL.Clear(true, true, Color.clear);
        }
        finally
        {
            RenderTexture.active = previous;
        }
    }

    private void ShowGallery()
    {
        if (galleryRoot != null)
            galleryRoot.SetActive(true);
        if (galleryCanvas != null)
        {
            galleryCanvas.enabled = true;
            galleryCanvas.overrideSorting = true;
            galleryCanvas.sortingOrder = 120;
        }
    }

    private void HideGallery()
    {
        if (galleryRoot != null)
            galleryRoot.SetActive(false);
        if (galleryCanvas != null)
            galleryCanvas.enabled = false;
        if (imageDisplay != null)
            imageDisplay.enabled = false;
        if (videoDisplay != null)
            videoDisplay.enabled = false;
    }

    private void PositionGalleryInFrontOfCamera()
    {
        Camera camera = FindActiveCamera();
        if (camera == null || galleryRoot == null)
            return;

        galleryRoot.transform.position = camera.transform.position + camera.transform.forward * openDistanceFromCamera;
        Vector3 away = (galleryRoot.transform.position - camera.transform.position).normalized;
        galleryRoot.transform.rotation = Quaternion.LookRotation(away, Vector3.up);

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
                        if (candidate != null && candidate.enabled)
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
