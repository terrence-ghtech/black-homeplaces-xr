using BCaT.Production.Interaction;
using BCaT.Production.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Look-at-and-interact toggle for a spatialized ambient AudioSource.
/// Interaction selection/input is owned by the central InteractionRouter
/// (no keyboard polling here); the source is routed through the Ambience
/// audio channel so the settings mixer controls it.
/// </summary>
public class SpatialAudioToggle : MonoBehaviour, IInteractionTarget
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
        bool playing = audioSource != null && audioSource.isPlaying;
        SharedInteractionVerb verb = playing ? SharedInteractionVerb.Pause : SharedInteractionVerb.Listen;
        if (prompt == null)
            prompt = new SharedInteractionPromptConfig();
        prompt.verb = verb;
        if (string.IsNullOrWhiteSpace(prompt.objectName))
            prompt.objectName = displayName;
        return SharedInteractionPrompt.Format(xr, prompt);
    }

    public void OnFocusChanged(bool focused) { }

    public void OnInteract(InteractionActivation activation) => ToggleAudio();

    // ---------------------------------------------------------------------

    private void OnEnable() => InteractionRouter.Register(this);

    private void OnDisable()
    {
        InteractionRouter.Unregister(this);
        // Do not leave ambience playing on a disabled exhibit.
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
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

        if (audioSource.isPlaying)
            audioSource.Pause();
        else
            audioSource.Play();
    }
}
