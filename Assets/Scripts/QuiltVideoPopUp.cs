using UnityEngine;
using UnityEngine.Video;
using UnityEngine.InputSystem;

public class QuiltVideoPopUp : MonoBehaviour
{
    [Header("Video Popup")]
    public GameObject popUpPanel;
    public VideoPlayer videoPlayer;

    [Header("Prompt")]
    public GameObject promptText;

    [Header("Audio")]
    public AudioSource sewingMachineAudio;
    public AudioSource videoAudioSource;

    [Header("Desktop Input")]
    public Key interactKey = Key.E;
    public bool playerInRange = false;

    private bool isOpen = false;
    private bool playWhenPrepared = false;
    private bool prepareRequested = false;
    private GameObject loadingIndicator;

    private void Start()
    {
        if (popUpPanel != null)
            popUpPanel.SetActive(false);

        WorldInteractionPromptVisual.SetRootVisible(promptText, false);

        if (videoPlayer != null)
        {
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = RuntimeMediaPaths.ResolveMediaUrl("in_my_sisters_room_xr.mp4");
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.prepareCompleted += OnVideoPrepared;
            videoPlayer.errorReceived += OnVideoError;
            videoPlayer.loopPointReached += OnVideoEnded;
        }

        if (videoAudioSource != null)
        {
            videoAudioSource.playOnAwake = false;
            videoAudioSource.loop = false;
            videoAudioSource.Stop();
        }
    }

    // Legacy remnant: this component is no longer referenced by any scene or
    // prefab (superseded by MediaVideoController). Its keyboard polling was
    // removed during the interaction-router migration so no stray E-key
    // listener can ever compete; XR/programmatic entry points remain.

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

        VideoExhibitCoordinator.NotifyOpened(this, ClosePopUp);

        WorldInteractionPromptVisual.SetRootVisible(promptText, false);

        if (sewingMachineAudio != null)
            sewingMachineAudio.Pause();

        if (popUpPanel != null)
            popUpPanel.SetActive(true);

        if (videoPlayer != null)
        {
            videoPlayer.time = 0;
            BeginVideoPlayback();
        }
    }

    public void ClosePopUp()
    {
        isOpen = false;

        VideoExhibitCoordinator.NotifyClosed(this);
        VideoLoadingIndicator.Hide(ref loadingIndicator);
        playWhenPrepared = false;
        ReleaseVideoResources();

        if (videoAudioSource != null)
            StopAndUnloadIfSafe(videoAudioSource);

        if (popUpPanel != null)
            popUpPanel.SetActive(false);

        if (sewingMachineAudio != null)
            sewingMachineAudio.Play();

        if (playerInRange)
            WorldInteractionPromptVisual.SetRootVisible(promptText, true);
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

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;

            if (!isOpen)
                WorldInteractionPromptVisual.SetRootVisible(promptText, true);

#if !UNITY_WEBGL || UNITY_EDITOR
            PrepareVideoIfNeeded();
#endif
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;

            WorldInteractionPromptVisual.SetRootVisible(promptText, false);

            if (!isOpen)
                ReleaseVideoResources();
        }
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
            Debug.LogError("[QuiltVideoPopUp] No playable video source (file missing locally and no remote URL configured); showing unavailable message");
            if (isOpen && popUpPanel != null)
            {
                if (loadingIndicator == null)
                    loadingIndicator = VideoLoadingIndicator.Show(popUpPanel.transform, "");
                VideoLoadingIndicator.SetMessage(loadingIndicator,
                    "Video unavailable.\nClose and reopen the exhibit to try again.");
            }
            return;
        }

        if (!videoPlayer.isPrepared)
        {
            playWhenPrepared = true;
            if (isOpen && loadingIndicator == null && popUpPanel != null)
                loadingIndicator = VideoLoadingIndicator.Show(popUpPanel.transform, "Loading video…");
            PrepareVideoIfNeeded();
            return;
        }

        videoPlayer.Play();
        PlayVideoAudio();
    }

    private void PrepareVideoIfNeeded()
    {
        if (videoPlayer == null || videoPlayer.isPrepared || prepareRequested)
            return;

        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = RuntimeMediaPaths.ResolveMediaUrl("in_my_sisters_room_xr.mp4");
        if (!HasPlayableSource())
            return;

        prepareRequested = true;
        videoPlayer.Prepare();
    }

    private void PlayVideoAudio()
    {
        if (videoAudioSource == null)
            return;

        videoAudioSource.time = 0;
        videoAudioSource.Play();
    }

    private void OnVideoError(VideoPlayer source, string message)
    {
        Debug.LogError($"[QuiltVideoPopUp] errorReceived: {message} (url={source.url})");
        prepareRequested = false;
        playWhenPrepared = false;

        if (isOpen && popUpPanel != null)
        {
            if (loadingIndicator == null)
                loadingIndicator = VideoLoadingIndicator.Show(popUpPanel.transform, "");
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
        PlayVideoAudio();
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
