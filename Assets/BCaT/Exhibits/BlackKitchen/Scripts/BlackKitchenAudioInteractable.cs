using UnityEngine;

// One Black Kitchen audio station: a single story with its own trigger, prompt label,
// and toggle behavior. All five stations share this component; playback goes only
// through the coordinator. This component reads no input itself — the
// BlackKitchenInteractionManager selects one station and forwards activation.
public class BlackKitchenAudioInteractable : MonoBehaviour
{
    [Header("Story")]
    [SerializeField] private string narrativeId;
    [SerializeField] private string displayName;
    [SerializeField] private AudioClip clip;
    [SerializeField] private AudioSource source;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.85f;
    [SerializeField] private float fadeIn = 0.4f;

    [Header("Interaction")]
    [SerializeField] private Collider interactionTrigger;
    [SerializeField] private Transform focusTarget;
    [Tooltip("Player distance at which this station becomes eligible for selection.")]
    [SerializeField] private float interactionRadius = 1.3f;

    [Header("Wiring")]
    [SerializeField] private BlackKitchenAudioCoordinator coordinator;

    public string NarrativeId => narrativeId;
    public string DisplayName => displayName;
    public Collider InteractionTrigger => interactionTrigger;
    public float InteractionRadius => interactionRadius;
    public Vector3 FocusPoint => focusTarget != null ? focusTarget.position : transform.position + Vector3.up;

    public bool IsPlaying => source != null && clip != null && source.isPlaying && source.clip == clip;

    private void Start()
    {
        ConfigureSource();
        if (coordinator != null)
            coordinator.RegisterNarrativeSource(source);
    }

    public void Toggle()
    {
        if (coordinator == null || source == null || clip == null)
        {
            Debug.LogWarning($"[BlackKitchenAudioInteractable] '{narrativeId}' cannot toggle: missing coordinator, source, or clip.");
            return;
        }

        coordinator.RequestNarrative(narrativeId, source, clip, volume, false, fadeIn);
    }

    public void OnXRSelect()
    {
        Debug.Log($"[BlackKitchenAudioInteractable] XR select accepted for '{narrativeId}'.");
        Toggle();
    }

    public void Configure(string id, string label, AudioClip storyClip, AudioSource audioSource, Collider trigger, Transform focus, BlackKitchenAudioCoordinator audioCoordinator, float storyVolume)
    {
        narrativeId = id;
        displayName = label;
        clip = storyClip;
        source = audioSource;
        interactionTrigger = trigger;
        focusTarget = focus;
        coordinator = audioCoordinator;
        volume = storyVolume;
        ConfigureSource();
    }

    public bool OwnsCollider(Collider candidate)
    {
        return candidate != null && (candidate == interactionTrigger || candidate.transform == transform || candidate.transform.IsChildOf(transform));
    }

    private void ConfigureSource()
    {
        if (source == null)
            return;

        source.playOnAwake = false;
        source.loop = false;
    }
}
