using System.Collections;
using BCaT.Production;
using BCaT.Production.Interaction;
using BCaT.Production.Media;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Reusable drop-in video exhibit controller (billboard or hologram popup).
/// One component drives desktop (interaction router selection + E) and
/// Quest/XR (wire XRSimpleInteractable.SelectEntered to <see cref="OnXRSelect"/>;
/// the select request is validated by the shared router).
/// Platform-safe video source and audio routing:
///  - Desktop/Quest: URL (packaged StreamingAssets first, remote fallback).
///  - WebGL remnant: always URL, Direct audio.
///  - Editor: optional VideoClip fallback for quick testing.
/// Registers with the central InteractionRouter instead of polling the
/// keyboard; while the popup is open it registers a Media interaction blocker
/// so background exhibits cannot be triggered, and E/Escape close it through
/// FocusedUiInput. Playback is tracked by MediaPlaybackRegistry (kiosk resets,
/// return-to-entrance) and failures are reported through MediaErrorLog.
/// </summary>
public class MediaVideoController : MonoBehaviour, IInteractionTarget
{
    public enum DesktopActivation
    {
        ProximityTrigger, // walk into trigger collider on this object, press E
        LookRaycast       // aim center of screen at this object within range, press E
    }

    [Header("Exhibit Info (project-specific data)")]
    [SerializeField] private string title;
    [SerializeField] private string artistCreator;
    [TextArea]
    [SerializeField] private string description;

    [Header("Optional billboard texts (auto-filled from Exhibit Info if set)")]
    [SerializeField] private TMPro.TMP_Text billboardText;

    [Header("Popup")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Canvas popupCanvas;
    [SerializeField] private RawImage videoImage;

    [Header("Video Source")]
    [Tooltip("File name inside StreamingAssets, e.g. my_video.mp4")]
    [SerializeField] private string videoFileName;
    [Tooltip("Optional full hosted URL; overrides StreamingAssets file name.")]
    [SerializeField] private string videoUrlOverride;
    [Tooltip("Editor-only preview clip. Builds always stream by URL.")]
    [SerializeField] private VideoClip editorPreviewClip;
    [SerializeField] private VideoPlayer videoPlayer;
    [Tooltip("Seconds allowed for Prepare() before the exhibit reports the media as unavailable.")]
    [SerializeField] private float prepareTimeoutSeconds = 20f;

    [Header("Audio")]
    [Tooltip("Route video audio through this AudioSource (spatial). Ignored on WebGL (Direct is used).")]
    [SerializeField] private AudioSource videoAudioSource;
    [Tooltip("Separate soundtrack AudioSource played alongside a muted video (legacy quilt setup).")]
    [SerializeField] private AudioSource separateAudioSource;
    [Tooltip("Ambient AudioSource paused while the popup is open (e.g. sewing machine loop).")]
    [SerializeField] private AudioSource pauseWhileOpen;

    [Header("Desktop Input (selection is owned by the InteractionRouter)")]
    [SerializeField] private DesktopActivation desktopActivation = DesktopActivation.LookRaycast;
#pragma warning disable 0414 // retained for scene-data compatibility; router owns input/camera now
    [Tooltip("Retained for scene-data compatibility; the router now owns the interact key.")]
    [SerializeField] private Key interactKey = Key.E;
    [SerializeField] private Camera playerCamera;
#pragma warning restore 0414
    [SerializeField] private float interactionDistance = 5f;
    [Tooltip("Prompt object shown while the player is inside the proximity trigger.")]
    [SerializeField] private GameObject promptRoot;

    private bool isOpen;
    private bool playerInRange;
    private bool playWhenPrepared;
    private bool prepareRequested;
    private bool ownsTargetTexture;
    private GameObject loadingIndicator;
    private Collider[] ownColliders;
    private Coroutine prepareWatchdog;

    private string LogTag => $"[MediaVideo:{gameObject.name}]";

    private string ExhibitName => string.IsNullOrEmpty(title) ? gameObject.name : title;

    // ---- IInteractionTarget --------------------------------------------

    public Vector3 FocusPoint => transform.position;

    public float MaxDistance =>
        desktopActivation == DesktopActivation.ProximityTrigger ? 999f : interactionDistance;

    public float MaxViewAngle =>
        desktopActivation == DesktopActivation.ProximityTrigger ? 0f : 16f;

    public bool RequireLineOfSight => desktopActivation == DesktopActivation.LookRaycast;

    public int Priority => 0;

    public bool IsAvailable =>
        isActiveAndEnabled && !isOpen &&
        (desktopActivation != DesktopActivation.ProximityTrigger || playerInRange);

    public bool AllowDesktopClick => false;

    public bool Exists => this != null;

    public Collider[] OwnColliders
    {
        get
        {
            if (ownColliders == null)
                ownColliders = GetComponentsInChildren<Collider>(true);
            return ownColliders;
        }
    }

    public string GetPrompt(bool xr) =>
        xr ? "Interact to watch" : "Press E to watch";

    public void OnFocusChanged(bool focused)
    {
        // Prompt-object visibility keeps its authored per-mode behavior
        // (trigger mode: shown while in range; raycast mode: shown until open);
        // the router adds the screen-space prompt on desktop.
    }

    public void OnInteract(InteractionActivation activation)
    {
        Debug.Log($"{LogTag} Interaction dispatched ({activation})");
        TogglePopUp();
    }

    // ---------------------------------------------------------------------

    private void OnEnable()
    {
        InteractionRouter.Register(this);
    }

    private void Start()
    {
        HidePopup();

        // In trigger mode the prompt only appears while the player is in range.
        // In raycast mode the prompt/billboard stays visible until the popup opens.
        if (promptRoot != null && desktopActivation == DesktopActivation.ProximityTrigger)
            promptRoot.SetActive(false);

        ApplyBillboardText();
        ConfigureVideoPlayer();
        ConfigureAudio();

        if (videoAudioSource != null)
            BCaT.Production.Settings.AudioChannelService.Register(
                videoAudioSource, BCaT.Production.Settings.AudioChannel.Media);
        if (separateAudioSource != null)
            BCaT.Production.Settings.AudioChannelService.Register(
                separateAudioSource, BCaT.Production.Settings.AudioChannel.Media);
    }

    private void OnDestroy()
    {
        ReleaseVideoResources(destroyOwnedTexture: true);

        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnVideoPrepared;
            videoPlayer.errorReceived -= OnVideoError;
            videoPlayer.loopPointReached -= OnVideoEnded;
        }

        VideoExhibitCoordinator.NotifyClosed(this);
        MediaPlaybackRegistry.NotifyStopped(this);
        InteractionState.Unblock(this);
    }

    private void OnDisable()
    {
        InteractionRouter.Unregister(this);

        if (!isOpen && videoPlayer == null)
            return;

        isOpen = false;
        playWhenPrepared = false;
        InteractionState.Unblock(this);
        MediaPlaybackRegistry.NotifyStopped(this);
        ReleaseVideoResources(destroyOwnedTexture: false);

        if (separateAudioSource != null)
            StopAndUnloadIfSafe(separateAudioSource);
    }

    private void Update()
    {
        // While open this popup is a focused media interface: it owns E/Escape
        // through the central modal input helper (world interaction is blocked).
        if (isOpen && (FocusedUiInput.InteractPressed || FocusedUiInput.CancelPressed))
        {
            Debug.Log($"{LogTag} Close requested from focused-media input");
            ClosePopUp();
        }
    }

    /// <summary>Wire XRSimpleInteractable.SelectEntered here.</summary>
    public void OnXRSelect()
    {
        Debug.Log($"{LogTag} XR SelectEntered received");
        if (isOpen)
        {
            // Closing an open popup is always allowed.
            ClosePopUp();
        }
        else if (InteractionRouter.Instance != null)
        {
            InteractionRouter.Instance.RequestXRSelect(this);
        }
        else
        {
            TogglePopUp();
        }
    }

    public void TogglePopUp()
    {
        if (isOpen)
            ClosePopUp();
        else
            OpenPopUp();
    }

    public void OpenPopUp()
    {
        isOpen = true;
        Debug.Log($"{LogTag} OpenPopUp");

        // Close any other open video exhibit so two videos never buffer at once.
        VideoExhibitCoordinator.NotifyOpened(this, ClosePopUp);

        // Focused media interface: block background world interaction and give
        // the kiosk reset a close handle.
        InteractionState.Block(this, InteractionBlockReason.Media, ClosePopUp);

        if (promptRoot != null)
            promptRoot.SetActive(false);

        if (pauseWhileOpen != null)
            pauseWhileOpen.Pause();

        ShowPopup();

        if (videoPlayer == null)
        {
            Debug.LogWarning($"{LogTag} No VideoPlayer assigned");
            return;
        }

        EnsureVideoSource();
        EnsureRenderTexture();
        videoPlayer.time = 0;
        BeginVideoPlayback();
    }

    public void ClosePopUp()
    {
        isOpen = false;
        playWhenPrepared = false;
        Debug.Log($"{LogTag} ClosePopUp");

        VideoExhibitCoordinator.NotifyClosed(this);
        MediaPlaybackRegistry.NotifyStopped(this);
        InteractionState.Unblock(this);
        BCaT.Production.Access.SubtitleService.Instance?.NotifyMediaStopped(videoFileName);
        VideoLoadingIndicator.Hide(ref loadingIndicator);
        StopPrepareWatchdog();
        ReleaseVideoResources(destroyOwnedTexture: false);

        if (separateAudioSource != null)
            StopAndUnloadIfSafe(separateAudioSource);

        HidePopup();

        if (pauseWhileOpen != null)
            pauseWhileOpen.Play();

        if (promptRoot != null && (playerInRange || desktopActivation == DesktopActivation.LookRaycast))
            promptRoot.SetActive(true);
    }

    private void ConfigureVideoPlayer()
    {
        if (videoPlayer == null)
        {
            Debug.LogWarning($"{LogTag} VideoPlayer reference missing");
            return;
        }

        videoPlayer.playOnAwake = false;
        videoPlayer.prepareCompleted += OnVideoPrepared;
        videoPlayer.errorReceived += OnVideoError;
        videoPlayer.loopPointReached += OnVideoEnded;

        EnsureVideoSource();
    }

    private void EnsureVideoSource()
    {
        if (videoPlayer == null)
            return;

#if UNITY_EDITOR
        if (editorPreviewClip != null)
        {
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = editorPreviewClip;
            Debug.Log($"{LogTag} Editor preview clip: {editorPreviewClip.name}");
        }
        else
#endif
        {
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = ResolveUrl();
            Debug.Log($"{LogTag} Video URL: {videoPlayer.url}");
        }
    }

    private void EnsureRenderTexture()
    {
        if (videoPlayer == null)
            return;

        // Make the popup self-sufficient: create a render texture if none is wired.
        if (videoPlayer.renderMode == VideoRenderMode.RenderTexture && videoPlayer.targetTexture == null)
        {
            var rt = new RenderTexture(1280, 720, 0);
            rt.name = gameObject.name + "_VideoRT";
            videoPlayer.targetTexture = rt;
            ownsTargetTexture = true;
            if (videoImage != null)
                videoImage.texture = rt;
            Debug.Log($"{LogTag} Auto-created 1280x720 RenderTexture");
        }

        if (videoImage != null && videoImage.texture == null)
            videoImage.texture = videoPlayer.targetTexture;
    }

    private void ConfigureAudio()
    {
        if (videoPlayer == null)
            return;

        if (separateAudioSource != null)
        {
            // Muted video + separate soundtrack (legacy quilt setup, works everywhere).
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            separateAudioSource.playOnAwake = false;
            separateAudioSource.loop = false;
            separateAudioSource.Stop();
            Debug.Log($"{LogTag} Audio: separate AudioSource ({separateAudioSource.name})");
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL does not support AudioSource output from VideoPlayer.
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        Debug.Log($"{LogTag} Audio: Direct (WebGL)");
#else
        if (videoAudioSource != null)
        {
            videoAudioSource.playOnAwake = false;
            videoAudioSource.Stop();
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.SetTargetAudioSource(0, videoAudioSource);
            Debug.Log($"{LogTag} Audio: routed to AudioSource ({videoAudioSource.name})");
        }
        else
        {
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            Debug.Log($"{LogTag} Audio: Direct");
        }
#endif
    }

    private string ResolveUrl()
    {
        if (!string.IsNullOrWhiteSpace(videoUrlOverride))
            return videoUrlOverride;

        return RuntimeMediaPaths.ResolveMediaUrl(videoFileName);
    }

    private bool HasPlayableSource()
    {
        return videoPlayer != null
            && (videoPlayer.source != VideoSource.Url || !string.IsNullOrEmpty(videoPlayer.url));
    }

    private void BeginVideoPlayback()
    {
        if (!HasPlayableSource())
        {
            MediaErrorLog.LogFailure(ExhibitName, videoFileName,
                "no playable source (file missing locally and no remote URL configured)",
                remoteAttempted: RemoteMediaConfig.Instance != null, recovered: true);
            if (loadingIndicator == null)
                loadingIndicator = VideoLoadingIndicator.Show(IndicatorParent(), "");
            VideoLoadingIndicator.SetMessage(loadingIndicator,
                "This media is currently unavailable.\nPress E to close, or try again later.");
            return;
        }

        if (!videoPlayer.isPrepared)
        {
            playWhenPrepared = true;
            Debug.Log($"{LogTag} Not prepared yet; Prepare() and play when ready (url={videoPlayer.url})");
            if (loadingIndicator == null)
                loadingIndicator = VideoLoadingIndicator.Show(IndicatorParent(), "Loading video…");
            if (!prepareRequested)
            {
                prepareRequested = true;
                videoPlayer.Prepare();
                StartPrepareWatchdog();
            }

            return;
        }

        Debug.Log($"{LogTag} Play() called (url={videoPlayer.url})");
        videoPlayer.Play();
        PlaySeparateAudio();
        NotifyPlaybackStarted();
    }

    private void PrepareVideoIfNeeded()
    {
        if (videoPlayer == null || videoPlayer.isPrepared || prepareRequested)
            return;

        EnsureVideoSource();
        if (!HasPlayableSource())
            return;

        EnsureRenderTexture();
        prepareRequested = true;
        videoPlayer.Prepare();
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

        // The decoder/network stalled: fail gracefully instead of an endless
        // "Loading video…" state, and leave the exhibit closeable.
        prepareRequested = false;
        playWhenPrepared = false;
        videoPlayer.Stop();

        MediaErrorLog.LogFailure(ExhibitName, videoPlayer.url,
            $"prepare timeout after {prepareTimeoutSeconds:F0}s",
            remoteAttempted: true, recovered: true);

        if (isOpen)
        {
            if (loadingIndicator == null)
                loadingIndicator = VideoLoadingIndicator.Show(IndicatorParent(), "");
            VideoLoadingIndicator.SetMessage(loadingIndicator,
                "This exhibit requires an internet connection.\nPress E to close and try again later.");
        }
    }

    private void PlaySeparateAudio()
    {
        if (separateAudioSource == null)
            return;

        separateAudioSource.time = 0;
        separateAudioSource.Play();
    }

    private void NotifyPlaybackStarted()
    {
        MediaPlaybackRegistry.NotifyStarted(this, ClosePopUp);
        BCaT.Production.Access.SubtitleService.Instance?.NotifyMediaStarted(videoFileName);
    }

    private void OnVideoPrepared(VideoPlayer preparedPlayer)
    {
        Debug.Log($"{LogTag} prepareCompleted (duration={preparedPlayer.length:F1}s, size={preparedPlayer.width}x{preparedPlayer.height})");
        prepareRequested = false;
        StopPrepareWatchdog();
        VideoLoadingIndicator.Hide(ref loadingIndicator);

        if (!isOpen || !playWhenPrepared)
            return;

        playWhenPrepared = false;
        preparedPlayer.time = 0;
        Debug.Log($"{LogTag} Play() called after prepare");
        preparedPlayer.Play();
        PlaySeparateAudio();
        NotifyPlaybackStarted();
    }

    private void OnVideoError(VideoPlayer source, string message)
    {
        MediaErrorLog.LogFailure(ExhibitName, source.url, message,
            remoteAttempted: true, recovered: true);
        prepareRequested = false;
        playWhenPrepared = false;
        StopPrepareWatchdog();
        MediaPlaybackRegistry.NotifyStopped(this);

        if (isOpen)
        {
            if (loadingIndicator == null)
                loadingIndicator = VideoLoadingIndicator.Show(IndicatorParent(), "");
            VideoLoadingIndicator.SetMessage(loadingIndicator,
                "The media file could not be loaded.\nPress E to close, or try again later.");
        }
    }

    private void OnVideoEnded(VideoPlayer endedPlayer)
    {
        // Release decoder/buffer resources once playback finishes (non-looping).
        if (!endedPlayer.isLooping)
        {
            Debug.Log($"{LogTag} Playback ended; releasing video resources");
            endedPlayer.Stop();
            prepareRequested = false;
            MediaPlaybackRegistry.NotifyStopped(this);
            BCaT.Production.Access.SubtitleService.Instance?.NotifyMediaStopped(videoFileName);
        }
    }

    private Transform IndicatorParent()
    {
        if (videoImage != null)
            return videoImage.transform.parent;
        if (popupCanvas != null)
            return popupCanvas.transform;
        return popupRoot != null ? popupRoot.transform : null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (desktopActivation != DesktopActivation.ProximityTrigger || !other.CompareTag("Player"))
            return;

        Debug.Log($"{LogTag} Player entered trigger");
        playerInRange = true;

        if (!isOpen && promptRoot != null)
            promptRoot.SetActive(true);

#if !UNITY_WEBGL || UNITY_EDITOR
        PrepareVideoIfNeeded();
#endif
    }

    private void OnTriggerExit(Collider other)
    {
        if (desktopActivation != DesktopActivation.ProximityTrigger || !other.CompareTag("Player"))
            return;

        Debug.Log($"{LogTag} Player left trigger");
        playerInRange = false;

        if (promptRoot != null)
            promptRoot.SetActive(false);

        if (!isOpen)
            ReleaseVideoResources(destroyOwnedTexture: false);
    }

    private void ApplyBillboardText()
    {
        if (billboardText == null || string.IsNullOrEmpty(title))
            return;

        billboardText.text = $"<b>{title}</b>\n\n{artistCreator}\n\n<i>{description}</i>";
    }

    private void ShowPopup()
    {
        if (popupRoot != null)
            popupRoot.SetActive(true);

        if (popupCanvas != null)
            popupCanvas.enabled = true;

        if (videoImage != null)
            videoImage.enabled = true;
    }

    private void HidePopup()
    {
        if (popupRoot != null)
            popupRoot.SetActive(false);

        if (popupCanvas != null)
            popupCanvas.enabled = false;

        if (videoImage != null)
            videoImage.enabled = false;
    }

    private void ReleaseVideoResources(bool destroyOwnedTexture)
    {
        if (videoPlayer == null)
            return;

        videoPlayer.Stop();

        prepareRequested = false;
        playWhenPrepared = false;
        StopPrepareWatchdog();
        ClearTargetTexture();

        if (videoPlayer.source == VideoSource.Url)
            videoPlayer.url = string.Empty;
#if UNITY_EDITOR
        else if (videoPlayer.source == VideoSource.VideoClip)
            videoPlayer.clip = null;
#endif

        if (destroyOwnedTexture && ownsTargetTexture && videoPlayer.targetTexture != null)
        {
            RenderTexture texture = videoPlayer.targetTexture;
            videoPlayer.targetTexture = null;
            if (videoImage != null && videoImage.texture == texture)
                videoImage.texture = null;
            texture.Release();
            Destroy(texture);
            ownsTargetTexture = false;
        }
    }

    private void ClearTargetTexture()
    {
        RenderTexture texture = videoPlayer != null ? videoPlayer.targetTexture : null;
        if (texture == null)
            return;

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = texture;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = previous;
    }

    private static void StopAndUnloadIfSafe(AudioSource source)
    {
        if (source == null)
            return;

        source.Stop();

        AudioClip clip = source.clip;
        if (clip == null || clip.loadType == AudioClipLoadType.Streaming || clip.loadState != AudioDataLoadState.Loaded)
            return;

        clip.UnloadAudioData();
    }
}
