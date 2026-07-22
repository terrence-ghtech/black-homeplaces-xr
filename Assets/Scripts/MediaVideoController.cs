using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Reusable drop-in video exhibit controller (billboard or hologram popup).
/// One component drives desktop/WebGL (E key via proximity trigger or look
/// raycast) and Quest/XR (wire XRSimpleInteractable.SelectEntered to
/// <see cref="OnXRSelect"/>). Platform-safe video source and audio routing:
///  - WebGL: always URL (StreamingAssets or hosted), Direct audio.
///  - Device builds (Quest etc.): always URL.
///  - Editor: optional VideoClip fallback for quick testing.
/// Every lifecycle step logs with the object name for on-device debugging.
/// </summary>
public class MediaVideoController : MonoBehaviour
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

    [Header("Audio")]
    [Tooltip("Route video audio through this AudioSource (spatial). Ignored on WebGL (Direct is used).")]
    [SerializeField] private AudioSource videoAudioSource;
    [Tooltip("Separate soundtrack AudioSource played alongside a muted video (legacy quilt setup).")]
    [SerializeField] private AudioSource separateAudioSource;
    [Tooltip("Ambient AudioSource paused while the popup is open (e.g. sewing machine loop).")]
    [SerializeField] private AudioSource pauseWhileOpen;

    [Header("Desktop / WebGL Input")]
    [SerializeField] private DesktopActivation desktopActivation = DesktopActivation.LookRaycast;
    [SerializeField] private Key interactKey = Key.E;
    [SerializeField] private float interactionDistance = 5f;
    [SerializeField] private Camera playerCamera;
    [Tooltip("Prompt object shown while the player is inside the proximity trigger.")]
    [SerializeField] private GameObject promptRoot;

    private bool isOpen;
    private bool playerInRange;
    private bool playWhenPrepared;
    private bool prepareRequested;

    private string LogTag => $"[MediaVideo:{gameObject.name}]";

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
    }

    private void OnDestroy()
    {
        if (videoPlayer == null)
            return;

        videoPlayer.prepareCompleted -= OnVideoPrepared;
        videoPlayer.errorReceived -= OnVideoError;
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current[interactKey].wasPressedThisFrame)
            return;

        if (desktopActivation == DesktopActivation.ProximityTrigger)
        {
            if (playerInRange)
            {
                Debug.Log($"{LogTag} Desktop key {interactKey} pressed while in range");
                TogglePopUp();
            }
        }
        else if (IsPlayerLookingAtThisObject())
        {
            Debug.Log($"{LogTag} Desktop key {interactKey} pressed while looking at exhibit");
            TogglePopUp();
        }
    }

    /// <summary>Wire XRSimpleInteractable.SelectEntered here.</summary>
    public void OnXRSelect()
    {
        Debug.Log($"{LogTag} XR SelectEntered received");
        TogglePopUp();
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

        videoPlayer.time = 0;
        BeginVideoPlayback();
    }

    public void ClosePopUp()
    {
        isOpen = false;
        playWhenPrepared = false;
        Debug.Log($"{LogTag} ClosePopUp");

        if (videoPlayer != null)
            videoPlayer.Stop();

        if (separateAudioSource != null)
            separateAudioSource.Stop();

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

        // Make the popup self-sufficient: create a render texture if none is wired.
        if (videoPlayer.renderMode == VideoRenderMode.RenderTexture && videoPlayer.targetTexture == null)
        {
            var rt = new RenderTexture(1280, 720, 0);
            rt.name = gameObject.name + "_VideoRT";
            videoPlayer.targetTexture = rt;
            if (videoImage != null)
                videoImage.texture = rt;
            Debug.Log($"{LogTag} Auto-created 1280x720 RenderTexture");
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        // Preload so the first open starts fast. On WebGL, defer preparation
        // until the user opens the popup (browser gesture requirement).
        prepareRequested = true;
        videoPlayer.Prepare();
#endif
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

        return RuntimeMediaPaths.StreamingAssetUrl(videoFileName);
    }

    private void BeginVideoPlayback()
    {
        if (!videoPlayer.isPrepared)
        {
            playWhenPrepared = true;
            Debug.Log($"{LogTag} Not prepared yet; Prepare() and play when ready (url={videoPlayer.url})");
            if (!prepareRequested)
            {
                prepareRequested = true;
                videoPlayer.Prepare();
            }

            return;
        }

        Debug.Log($"{LogTag} Play() called (url={videoPlayer.url})");
        videoPlayer.Play();
        PlaySeparateAudio();
    }

    private void PlaySeparateAudio()
    {
        if (separateAudioSource == null)
            return;

        separateAudioSource.time = 0;
        separateAudioSource.Play();
    }

    private void OnVideoPrepared(VideoPlayer preparedPlayer)
    {
        Debug.Log($"{LogTag} prepareCompleted (duration={preparedPlayer.length:F1}s, size={preparedPlayer.width}x{preparedPlayer.height})");
        prepareRequested = false;

        if (!isOpen || !playWhenPrepared)
            return;

        playWhenPrepared = false;
        preparedPlayer.time = 0;
        Debug.Log($"{LogTag} Play() called after prepare");
        preparedPlayer.Play();
        PlaySeparateAudio();
    }

    private void OnVideoError(VideoPlayer source, string message)
    {
        Debug.LogError($"{LogTag} errorReceived: {message} (url={source.url})");
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

    private void OnTriggerEnter(Collider other)
    {
        if (desktopActivation != DesktopActivation.ProximityTrigger || !other.CompareTag("Player"))
            return;

        Debug.Log($"{LogTag} Player entered trigger");
        playerInRange = true;

        if (!isOpen && promptRoot != null)
            promptRoot.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (desktopActivation != DesktopActivation.ProximityTrigger || !other.CompareTag("Player"))
            return;

        Debug.Log($"{LogTag} Player left trigger");
        playerInRange = false;

        if (promptRoot != null)
            promptRoot.SetActive(false);
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
}
