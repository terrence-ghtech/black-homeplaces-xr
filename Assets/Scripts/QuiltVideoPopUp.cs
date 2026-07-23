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

    private void Start()
    {
        if (popUpPanel != null)
            popUpPanel.SetActive(false);

        if (promptText != null)
            promptText.SetActive(false);

        if (videoPlayer != null)
        {
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = RuntimeMediaPaths.StreamingAssetUrl("in_my_sisters_room_xr.mp4");
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.prepareCompleted += OnVideoPrepared;
        }

        if (videoAudioSource != null)
        {
            videoAudioSource.playOnAwake = false;
            videoAudioSource.loop = false;
            videoAudioSource.Stop();
        }
    }

    private void Update()
    {
        if (playerInRange && Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame)
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

        if (promptText != null)
            promptText.SetActive(false);

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

        playWhenPrepared = false;
        ReleaseVideoResources();

        if (videoAudioSource != null)
            StopAndUnloadIfSafe(videoAudioSource);

        if (popUpPanel != null)
            popUpPanel.SetActive(false);

        if (sewingMachineAudio != null)
            sewingMachineAudio.Play();

        if (playerInRange && promptText != null)
            promptText.SetActive(true);
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
            videoPlayer.prepareCompleted -= OnVideoPrepared;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;

            if (!isOpen && promptText != null)
                promptText.SetActive(true);

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

            if (promptText != null)
                promptText.SetActive(false);

            if (!isOpen)
                ReleaseVideoResources();
        }
    }

    private void BeginVideoPlayback()
    {
        if (videoPlayer == null)
            return;

        if (!videoPlayer.isPrepared)
        {
            playWhenPrepared = true;
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
        videoPlayer.url = RuntimeMediaPaths.StreamingAssetUrl("in_my_sisters_room_xr.mp4");
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

    private void OnVideoPrepared(VideoPlayer preparedPlayer)
    {
        prepareRequested = false;
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
