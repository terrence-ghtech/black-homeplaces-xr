using UnityEngine;

public class BlackKitchenAmbientZone : MonoBehaviour
{
    [Header("Ambient Conversation")]
    [Tooltip("Distance where dwell counts and full volume is reached.")]
    [SerializeField] private float innerRadius = 2f;
    [Tooltip("Distance where the layer reaches silence.")]
    [SerializeField] private float outerRadius = 4f;
    [Tooltip("Seconds the visitor must remain inside the inner radius before playback fades in.")]
    [SerializeField] private float dwellDuration = 2.5f;
    [Tooltip("Maximum unducked volume for this layer.")]
    [Range(0f, 1f)]
    [SerializeField] private float maximumVolume = 0.28f;
    [Tooltip("Fade-in smoothing time.")]
    [SerializeField] private float fadeInDuration = 3f;
    [Tooltip("Fade-out smoothing time.")]
    [SerializeField] private float fadeOutDuration = 2.5f;
    [Tooltip("Whether kitchen_conversation.mp3 loops softly after it becomes active.")]
    [SerializeField] private bool loop = true;
    [Tooltip("Visitor transform. If empty, Camera.main is used.")]
    [SerializeField] private Transform visitor;
    [SerializeField] private AudioSource source;
    [SerializeField] private BlackKitchenAudioCoordinator coordinator;

    private float dwellTimer;
    private bool dwellComplete;

    private void Awake()
    {
        if (source != null)
        {
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = 0f;
        }
    }

    private void Update()
    {
        if (visitor == null && Camera.main != null)
            visitor = Camera.main.transform;

        if (visitor == null || source == null)
            return;

        float distance = Vector3.Distance(visitor.position, transform.position);
        if (distance <= innerRadius)
        {
            dwellTimer += Time.deltaTime;
            if (dwellTimer >= dwellDuration)
                dwellComplete = true;
        }
        else if (distance >= outerRadius)
        {
            dwellTimer = 0f;
            dwellComplete = false;
        }

        float distanceFactor = 0f;
        if (distance < outerRadius)
            distanceFactor = Mathf.InverseLerp(outerRadius, innerRadius, distance);

        float target = dwellComplete ? maximumVolume * distanceFactor : 0f;
        if (target > 0f && !source.isPlaying)
            source.Play();

        float duration = target > source.volume ? fadeInDuration : fadeOutDuration;
        float step = duration <= 0f ? 1f : Time.deltaTime / duration;
        source.volume = Mathf.MoveTowards(source.volume, target, maximumVolume * step);

        if (coordinator != null)
            coordinator.SetConversationBaseMultiplier(maximumVolume <= 0f ? 0f : source.volume / maximumVolume);

        if (source.isPlaying && source.volume <= 0.001f && target <= 0f)
            source.Pause();
    }

    public void Configure(Transform visitorTransform, AudioSource audioSource, BlackKitchenAudioCoordinator audioCoordinator)
    {
        visitor = visitorTransform;
        source = audioSource;
        coordinator = audioCoordinator;
    }
}
