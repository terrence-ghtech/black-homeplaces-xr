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
#if UNITY_WEBGL && !UNITY_EDITOR
            videoPlayer.prepareCompleted += OnVideoPrepared;
#else
            videoPlayer.Prepare();
#endif
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

        if (videoPlayer != null)
            videoPlayer.Stop();

        playWhenPrepared = false;

        if (videoAudioSource != null)
            videoAudioSource.Stop();

        if (popUpPanel != null)
            popUpPanel.SetActive(false);

        if (sewingMachineAudio != null)
            sewingMachineAudio.Play();

        if (playerInRange && promptText != null)
            promptText.SetActive(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;

            if (!isOpen && promptText != null)
                promptText.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;

            if (promptText != null)
                promptText.SetActive(false);
        }
    }

    private void BeginVideoPlayback()
    {
        if (videoPlayer == null)
            return;

#if UNITY_WEBGL && !UNITY_EDITOR
        if (!videoPlayer.isPrepared)
        {
            playWhenPrepared = true;
            if (!prepareRequested)
            {
                prepareRequested = true;
                videoPlayer.Prepare();
            }

            return;
        }
#endif

        videoPlayer.Play();
        PlayVideoAudio();
    }

    private void PlayVideoAudio()
    {
        if (videoAudioSource == null)
            return;

        videoAudioSource.time = 0;
        videoAudioSource.Play();
    }

#if UNITY_WEBGL && !UNITY_EDITOR
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
#endif
}
