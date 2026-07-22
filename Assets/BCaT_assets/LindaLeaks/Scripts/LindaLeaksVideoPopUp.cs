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

    private void Start()
    {
        HidePopup();
        ConfigureVideoPlayer();
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current[interactionKey].wasPressedThisFrame)
            return;

        if (IsPlayerLookingAtThisObject())
            TogglePopUp();
    }

    public void OpenPopUp()
    {
        isOpen = true;

        ShowPopup();

        if (videoPlayer == null)
            return;

        videoPlayer.time = 0;
        BeginVideoPlayback();
    }

    public void ClosePopUp()
    {
        isOpen = false;
        playWhenPrepared = false;

        if (videoPlayer != null)
            videoPlayer.Stop();

        if (videoAudioSource != null)
            videoAudioSource.Stop();

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

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL cannot decode imported VideoClip assets and does not support
        // AudioSource video output; stream from StreamingAssets with direct audio.
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = RuntimeMediaPaths.StreamingAssetUrl(videoFileName);
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
            videoPlayer.url = RuntimeMediaPaths.StreamingAssetUrl(videoFileName);
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

#if UNITY_WEBGL && !UNITY_EDITOR
        videoPlayer.prepareCompleted += OnVideoPrepared;
#else
        videoPlayer.Prepare();
#endif
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
        PlayVideoAudioIfSeparate();
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

#if UNITY_WEBGL && !UNITY_EDITOR
    private void OnVideoPrepared(VideoPlayer preparedPlayer)
    {
        prepareRequested = false;
        if (!isOpen || !playWhenPrepared)
            return;

        playWhenPrepared = false;
        preparedPlayer.time = 0;
        preparedPlayer.Play();
        PlayVideoAudioIfSeparate();
    }
#endif
}
