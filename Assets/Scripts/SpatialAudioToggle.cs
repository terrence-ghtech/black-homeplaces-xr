using BCaT.Production.Interaction;
using BCaT.Production.Media;
using BCaT.Production.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Look-at-and-interact toggle for the two spatialized foreground soundscapes
/// in the production scene (Nine Night and Duppy).
/// Interaction selection/input is owned by the central InteractionRouter
/// (no keyboard polling here). Their established Ambience mixer routing is
/// retained, while foreground playback ownership is coordinated separately.
/// </summary>
public class SpatialAudioToggle : MonoBehaviour, IInteractionTarget, IForegroundAudio
{
    [SerializeField] private AudioSource audioSource;
#pragma warning disable 0414 // retained for scene-data compatibility; router owns input/camera now
    [SerializeField] private Key interactKey = Key.E;
    [SerializeField] private Camera playerCamera;
#pragma warning restore 0414
    [SerializeField] private float interactionDistance = 5f;
    [SerializeField] private string displayName;
    [SerializeField] private SharedInteractionPromptConfig prompt =
        new SharedInteractionPromptConfig { verb = SharedInteractionVerb.Listen };

    [Header("Spatial Defaults")]
    [SerializeField] private bool configureSpatialAudio = true;
    [SerializeField] private float spatialBlend = 1f;
    [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Custom;
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 4f;
    [SerializeField] private float dopplerLevel;

    private Collider[] ownColliders;

    // ---- IInteractionTarget --------------------------------------------

    public Vector3 FocusPoint => transform.position;
    public float MaxDistance => interactionDistance;
    public float MaxViewAngle => 16f;
    public bool RequireLineOfSight => true;
    public int Priority => 0;
    public bool IsAvailable => isActiveAndEnabled && audioSource != null;
    public bool AllowDesktopClick => true;
    public bool Exists => this != null;
    public bool IsPlaying => audioSource != null && audioSource.isPlaying;

    public Collider[] OwnColliders
    {
        get
        {
            if (ownColliders == null)
                ownColliders = GetComponentsInChildren<Collider>(true);
            return ownColliders;
        }
    }

    public string GetPrompt(bool xr)
    {
        SharedInteractionVerb verb = IsPlaying ? SharedInteractionVerb.Stop : SharedInteractionVerb.Play;
        if (prompt == null)
            prompt = new SharedInteractionPromptConfig();
        string objectName = string.IsNullOrWhiteSpace(prompt.objectName)
            ? displayName
            : prompt.objectName;

        // The production scene's legacy explicit strings always say "open".
        // Build this stateful prompt from the real AudioSource state instead.
        return SharedInteractionPrompt.Format(xr, verb, objectName);
    }

    public void OnFocusChanged(bool focused) { }

    public void OnInteract(InteractionActivation activation) => ToggleAudio();

    // ---------------------------------------------------------------------

    private void OnEnable() => InteractionRouter.Register(this);

    private void OnDisable()
    {
        InteractionRouter.Unregister(this);
        Stop();
    }

    private void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            Debug.LogWarning($"[SpatialAudioToggle:{gameObject.name}] No AudioSource found.");
            return;
        }

        if (configureSpatialAudio)
        {
            audioSource.spatialBlend = spatialBlend;
            audioSource.rolloffMode = rolloffMode;
            audioSource.minDistance = minDistance;
            audioSource.maxDistance = maxDistance;
            audioSource.dopplerLevel = dopplerLevel;
        }

        audioSource.playOnAwake = false;
        audioSource.Stop();
        MediaPlaybackRegistry.NotifyStopped(this);
        ForegroundAudioCoordinator.Instance?.NotifyStopped(this);

        AudioChannelService.Register(audioSource, AudioChannel.Ambience);
    }

    /// <summary>Wire XRSimpleInteractable.SelectEntered here.</summary>
    public void OnXRSelect()
    {
        if (InteractionRouter.Instance != null)
            InteractionRouter.Instance.RequestXRSelect(this);
        else
            ToggleAudio();
    }

    public void ToggleAudio()
    {
        if (audioSource == null)
            return;

        if (IsPlaying)
            Stop();
        else if (ForegroundAudioCoordinator.Instance != null)
            ForegroundAudioCoordinator.Instance.RequestPlay(this);
        else
        {
            Debug.LogWarning($"[SpatialAudioToggle:{gameObject.name}] ForegroundAudioCoordinator is unavailable; playing without exclusivity.");
            Play();
        }
    }

    public void Play()
    {
        if (audioSource == null || IsPlaying)
            return;

        audioSource.Play();
        if (IsPlaying)
            MediaPlaybackRegistry.NotifyStarted(this, Stop);
    }

    public void Stop()
    {
        if (audioSource != null)
            audioSource.Stop();

        MediaPlaybackRegistry.NotifyStopped(this);
        ForegroundAudioCoordinator.Instance?.NotifyStopped(this);
    }
}
