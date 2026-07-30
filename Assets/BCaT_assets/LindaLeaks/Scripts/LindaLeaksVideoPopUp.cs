using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

public class LindaLeaksVideoPopUp : MonoBehaviour
{
    [Header("Popup")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Canvas popupCanvas;
    [SerializeField] private RawImage videoImage;

    [Header("Video")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private AudioSource videoAudioSource;
    [SerializeField] private VideoClip videoClip;
    [SerializeField] private string videoFileName = "linda_leaks_hall_of_fame.mp4";

    // Desktop fallback only. No floating prompt is shown; the artifact itself is the
    // interaction target and the interaction hint lives on the accompanying plaque.
    [Header("Interaction")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactionDistance = 4f;
    [SerializeField] private Key interactionKey = Key.E;

    private bool isOpen;
    private bool playWhenPrepared;
    private bool prepareRequested;
    private GameObject loadingIndicator;

    private void Start()
    {
        HidePopup();
        ConfigureVideoPlayer();
    }

    private void OnDisable()
    {
        isOpen = false;
        playWhenPrepared = false;
        ReleaseVideoResources();

        if (videoAudioSource != null)
            StopAndUnloadIfSafe(videoAudioSource);
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnVideoPrepared;
            videoPlayer.errorReceived -= OnVideoError;
            videoPlayer.loopPointReached -= OnVideoEnded;
        }

        VideoExhibitCoordinator.NotifyClosed(this);
    }

    // Legacy remnant: this component is no longer referenced by any scene or
    // prefab (the shipped Linda Leaks exhibit uses MediaVideoController). Its
    // keyboard polling was removed during the interaction-router migration so
    // no stray E-key listener can ever compete; XR/programmatic entry points
    // remain for the legacy builder tool.

    public void OpenPopUp()
    {
        isOpen = true;

        VideoExhibitCoordinator.NotifyOpened(this, ClosePopUp);

        ShowPopup();

        if (videoPlayer == null)
            return;

        EnsureVideoSource();
        videoPlayer.time = 0;
        BeginVideoPlayback();
    }

    public void ClosePopUp()
    {
        isOpen = false;
        playWhenPrepared = false;

        VideoExhibitCoordinator.NotifyClosed(this);
        VideoLoadingIndicator.Hide(ref loadingIndicator);
        ReleaseVideoResources();

        if (videoAudioSource != null)
            StopAndUnloadIfSafe(videoAudioSource);

        HidePopup();
    }

    public void TogglePopUp()
    {
        if (isOpen)
            ClosePopUp();
        else
            OpenPopUp();
    }

    private void ConfigureVideoPlayer()
    {
        if (videoPlayer == null)
            return;

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

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL cannot decode imported VideoClip assets and does not support
        // AudioSource video output; stream by URL (remote CDN when configured,
        // otherwise StreamingAssets) with direct audio.
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = RuntimeMediaPaths.ResolveMediaUrl(videoFileName);
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
#else
        if (videoClip != null)
        {
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = videoClip;
        }
        else
        {
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = RuntimeMediaPaths.ResolveMediaUrl(videoFileName);
        }

        if (videoAudioSource != null)
        {
            videoAudioSource.playOnAwake = false;
            videoAudioSource.loop = false;
            videoAudioSource.Stop();
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.SetTargetAudioSource(0, videoAudioSource);
        }
#endif
    }

    private bool HasPlayableSource()
    {
        return videoPlayer != null
            && (videoPlayer.source != VideoSource.Url || !string.IsNullOrEmpty(videoPlayer.url));
    }

    private void BeginVideoPlayback()
    {
        if (videoPlayer == null)
            return;

        if (!HasPlayableSource())
        {
            Debug.LogError("[LindaLeaksVideoPopUp] No playable video source (file missing locally and no remote URL configured); showing unavailable message");
            Transform indicatorParent = popupCanvas != null ? popupCanvas.transform
                : (popupRoot != null ? popupRoot.transform : null);
            if (loadingIndicator == null)
                loadingIndicator = VideoLoadingIndicator.Show(indicatorParent, "");
            VideoLoadingIndicator.SetMessage(loadingIndicator,
                "Video unavailable.\nClose and reopen the exhibit to try again.");
            return;
        }

        if (!videoPlayer.isPrepared)
        {
            playWhenPrepared = true;
            if (loadingIndicator == null)
            {
                Transform parent = popupCanvas != null ? popupCanvas.transform
                    : (popupRoot != null ? popupRoot.transform : null);
                loadingIndicator = VideoLoadingIndicator.Show(parent, "Loading video…");
            }
            PrepareVideoIfNeeded();
            return;
        }

        videoPlayer.Play();
        PlayVideoAudioIfSeparate();
    }

    private void PrepareVideoIfNeeded()
    {
        if (videoPlayer == null || videoPlayer.isPrepared || prepareRequested)
            return;

        EnsureVideoSource();
        if (!HasPlayableSource())
            return;

        prepareRequested = true;
        videoPlayer.Prepare();
    }

    private void PlayVideoAudioIfSeparate()
    {
        if (videoAudioSource == null || videoPlayer.audioOutputMode == VideoAudioOutputMode.AudioSource || videoAudioSource.clip == null)
            return;

        videoAudioSource.time = 0;
        videoAudioSource.Play();
    }

    private bool IsPlayerLookingAtThisObject()
    {
        if (playerCamera == null || !playerCamera.gameObject.activeInHierarchy)
            playerCamera = Camera.main;

        if (playerCamera == null)
            return false;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
            return false;

        return hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform);
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

    private void OnVideoError(VideoPlayer source, string message)
    {
        Debug.LogError($"[LindaLeaksVideoPopUp] errorReceived: {message} (url={source.url})");
        prepareRequested = false;
        playWhenPrepared = false;

        if (isOpen)
        {
            Transform parent = popupCanvas != null ? popupCanvas.transform
                : (popupRoot != null ? popupRoot.transform : null);
            if (loadingIndicator == null)
                loadingIndicator = VideoLoadingIndicator.Show(parent, "");
            VideoLoadingIndicator.SetMessage(loadingIndicator,
                "Video unavailable.\nClose and reopen the exhibit to try again.");
        }
    }

    private void OnVideoEnded(VideoPlayer endedPlayer)
    {
        if (!endedPlayer.isLooping)
        {
            endedPlayer.Stop();
            prepareRequested = false;
        }
    }

    private void OnVideoPrepared(VideoPlayer preparedPlayer)
    {
        prepareRequested = false;
        VideoLoadingIndicator.Hide(ref loadingIndicator);
        if (!isOpen || !playWhenPrepared)
            return;

        playWhenPrepared = false;
        preparedPlayer.time = 0;
        preparedPlayer.Play();
        PlayVideoAudioIfSeparate();
    }

    private void ReleaseVideoResources()
    {
        if (videoPlayer == null)
            return;

        videoPlayer.Stop();
        prepareRequested = false;
        playWhenPrepared = false;
        ClearTargetTexture();

        if (videoPlayer.source == VideoSource.Url)
            videoPlayer.url = string.Empty;
#if !UNITY_WEBGL || UNITY_EDITOR
        else if (videoPlayer.source == VideoSource.VideoClip)
            videoPlayer.clip = null;
#endif
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
