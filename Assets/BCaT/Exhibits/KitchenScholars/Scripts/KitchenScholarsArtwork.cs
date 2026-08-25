using BCaT.Production.Interaction;
using BCaT.Production.Media;
using BCaT.Production.Settings;
using UnityEngine;

/// <summary>
/// One Kitchen Scholars collage (Azsaneé Truss &amp; Staci Jones). The framed
/// artwork is the interaction target: the central InteractionRouter owns
/// candidate selection, focus, prompts and input dispatch on both platforms,
/// and this controller owns only the outcome — toggling the piece's companion
/// narration clip.
///
/// Behaviour follows the collaborators' instructions: the visitor is offered
/// the option to listen while viewing the artwork, and the audio stops when
/// they move away from the piece. The four pieces never talk over each other:
/// starting one narration stops whichever Kitchen Scholars narration is
/// already playing.
///
/// Everything shared is reused rather than re-implemented: InteractionRouter /
/// IInteractionTarget for world interaction on desktop and Quest,
/// SharedInteractionPrompt for prompt wording, MediaPlaybackRegistry so the
/// kiosk reset can stop narration, AudioChannelService for the Narration mixer
/// channel, and SubtitleService for captions/transcripts.
/// </summary>
public sealed class KitchenScholarsArtwork : MonoBehaviour, IInteractionTarget
{
    [Header("Piece")]
    [Tooltip("Collaborator-given piece title, e.g. \"My Grandmother's Recipes\".")]
    [SerializeField] private string pieceTitle = "Kitchen Scholars Piece";

    [Tooltip("The framed artwork visual (informational reference).")]
    [SerializeField] private Transform artworkRoot;

    [Header("Narration")]
    [SerializeField] private AudioClip narrationClip;
    [SerializeField] private AudioSource narrationSource;
    [Range(0f, 1f)]
    [SerializeField] private float narrationVolume = 0.9f;

    [Tooltip("Media id used for subtitles/transcripts. Matches a SubtitleTrack mediaId when one is authored.")]
    [SerializeField] private string narrationMediaId = "";

    [Header("World Interaction")]
    [SerializeField] private Transform focusPoint;
    [SerializeField] private Transform colliderRoot;
    [SerializeField] private float interactionDistance = 3.5f;

    [Tooltip("Playing narration stops when the visitor's camera is farther than this from the artwork. " +
             "Kept slightly larger than the interaction distance so small steps back don't cut the audio.")]
    [SerializeField] private float narrationStopDistance = 5f;

    [SerializeField] private float maxViewAngle = 25f;

    /// <summary>The one Kitchen Scholars piece allowed to narrate at a time.</summary>
    private static KitchenScholarsArtwork activeNarration;

    private Collider[] ownColliders;
    private Collider focusCollider;
    private bool narrationRegistered;

    public string PieceTitle => pieceTitle;
    public AudioClip NarrationClip => narrationClip;
    public float NarrationStopDistance => narrationStopDistance;
    public static KitchenScholarsArtwork ActiveNarration => activeNarration;

    public bool IsNarrationPlaying =>
        narrationSource != null && narrationClip != null &&
        narrationSource.isPlaying && narrationSource.clip == narrationClip;

    // ---- IInteractionTarget ---------------------------------------------

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
    public bool IsAvailable => isActiveAndEnabled;
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
        string title = string.IsNullOrWhiteSpace(pieceTitle) ? "Kitchen Scholars Piece" : pieceTitle.Trim();

        // The prompt is a listen/stop toggle; the router refreshes the shared
        // prompt text every frame, so the wording tracks the playback state.
        if (IsNarrationPlaying)
            return xr
                ? SharedInteractionPrompt.Format(true, SharedInteractionVerb.Stop, title)
                : $"Press E to Stop — {title}";

        return xr
            ? SharedInteractionPrompt.Format(true, SharedInteractionVerb.Listen, title)
            : $"Press E to Listen — {title}";
    }

    public void OnFocusChanged(bool focused)
    {
        // The shared bottom-center prompt (InteractionPromptUi) is the only
        // prompt surface for this exhibit; nothing world-space to toggle.
    }

    public void OnInteract(InteractionActivation activation)
    {
        if (IsNarrationPlaying)
            StopNarration($"toggled off via {activation}");
        else
            PlayNarration(activation.ToString());
    }

    /// <summary>Quest relay entry point for hand-wired XRSimpleInteractable twins.
    /// The staged pieces use XrSelectSurface instead, which dispatches through the
    /// router on its own; this stays for parity with the other exhibits.</summary>
    public void OnXRSelect()
    {
        if (InteractionRouter.Instance != null)
            InteractionRouter.Instance.RequestXRSelect(this);
        else
            OnInteract(InteractionActivation.XRSelect);
    }

    // ---- Lifecycle ------------------------------------------------------

    private void Awake() => ConfigureNarrationSource();

    private void OnEnable() => InteractionRouter.Register(this);

    private void OnDisable()
    {
        InteractionRouter.Unregister(this);
        StopNarration("exhibit disabled");
    }

    private void OnDestroy()
    {
        StopNarration("exhibit destroyed");
        MediaPlaybackRegistry.NotifyStopped(this);
    }

    private void Update()
    {
        if (!narrationRegistered)
            return;

        // Narration that reached its natural end resets state without input.
        if (!IsNarrationPlaying)
        {
            StopNarration("narration finished");
            return;
        }

        PublishNarrationTime();

        // Collaborator instruction: if/when the visitor moves away from the
        // image, the audio stops. Distance is measured from the active camera
        // so the same rule serves the desktop rig and the Quest headset.
        Camera camera = FindActiveCamera();
        if (camera == null)
            return;

        float distance = Vector3.Distance(camera.transform.position, FocusPoint);
        if (distance > narrationStopDistance)
            StopNarration($"visitor left range ({distance:F2} m > {narrationStopDistance:F2} m)");
    }

    // ---- Narration ------------------------------------------------------

    private void ConfigureNarrationSource()
    {
        if (narrationSource == null)
            return;

        narrationSource.playOnAwake = false;
        narrationSource.loop = false;
        narrationSource.volume = narrationVolume;
        AudioChannelService.Register(narrationSource, AudioChannel.Narration);
    }

    public void PlayNarration(string via = "direct")
    {
        if (narrationSource == null || narrationClip == null)
        {
            Debug.LogWarning($"[KitchenScholars:{pieceTitle}] Narration requested with no clip or AudioSource assigned.");
            return;
        }

        // Exclusivity across the four pieces: starting this narration stops
        // whichever Kitchen Scholars narration is already playing.
        if (activeNarration != null && activeNarration != this)
            activeNarration.StopNarration($"replaced by '{pieceTitle}'");
        activeNarration = this;

        narrationSource.clip = narrationClip;
        narrationSource.time = 0f;
        narrationSource.volume = AudioChannelService.ScaledVolume(narrationSource, narrationVolume);
        narrationSource.Play();

        narrationRegistered = true;
        MediaPlaybackRegistry.NotifyStarted(this, StopNarrationForReset);
        BCaT.Production.Access.SubtitleService.Instance?.NotifyMediaStarted(SubtitleMediaId);

        Debug.Log($"[KitchenScholars:{pieceTitle}] Narration started (via {via}).");
    }

    public void StopNarration(string reason)
    {
        bool wasPlaying = narrationRegistered;

        if (narrationSource != null && narrationSource.clip == narrationClip)
            narrationSource.Stop();

        if (activeNarration == this)
            activeNarration = null;

        if (narrationRegistered)
        {
            narrationRegistered = false;
            BCaT.Production.Access.SubtitleService.Instance?.NotifyMediaStopped(SubtitleMediaId);
        }

        MediaPlaybackRegistry.NotifyStopped(this);

        if (wasPlaying)
            Debug.Log($"[KitchenScholars:{pieceTitle}] Narration stopped ({reason}).");
    }

    private void StopNarrationForReset() => StopNarration("media registry stop-all");

    private void PublishNarrationTime()
    {
        if (!narrationRegistered || !IsNarrationPlaying)
            return;

        BCaT.Production.Access.SubtitleService.Instance?
            .NotifyMediaTime(SubtitleMediaId, narrationSource.time);
    }

    private string SubtitleMediaId =>
        !string.IsNullOrWhiteSpace(narrationMediaId)
            ? narrationMediaId
            : (narrationClip != null ? narrationClip.name : pieceTitle);

    // ---------------------------------------------------------------------

    private Camera FindActiveCamera()
    {
        if (Camera.main != null && Camera.main.isActiveAndEnabled)
            return Camera.main;

        foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (camera != null && camera.isActiveAndEnabled)
                return camera;

        return null;
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
                        if (candidate != null && candidate.enabled && !candidate.isTrigger)
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => activeNarration = null;
}
