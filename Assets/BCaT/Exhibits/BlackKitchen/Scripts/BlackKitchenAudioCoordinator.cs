using System.Collections;
using UnityEngine;

public class BlackKitchenAudioCoordinator : MonoBehaviour
{
    public enum NarrativeOverlapPolicy
    {
        PreventSimultaneousStories,
        CrossfadeToNewStory
    }

    [Header("Ambient Sources")]
    [Tooltip("Kitchen conversation source controlled by the ambient dwell zone.")]
    [SerializeField] private AudioSource kitchenConversationSource;
    [Tooltip("Cultural grounding source controlled by the experience controller.")]
    [SerializeField] private AudioSource culturalBackgroundSource;

    [Header("Audio Ducking")]
    [Tooltip("Multiplier applied to kitchen conversation while an object story plays.")]
    [Range(0f, 1f)]
    [SerializeField] private float kitchenConversationDuckMultiplier = 0.4f;
    [Tooltip("Seconds used to fade ducking in and out.")]
    [SerializeField] private float duckFadeDuration = 1f;
    [Tooltip("How competing object stories are handled.")]
    [SerializeField] private NarrativeOverlapPolicy narrativeOverlapPolicy = NarrativeOverlapPolicy.CrossfadeToNewStory;

    private AudioSource activeNarrativeSource;
    private AudioClip activeNarrativeClip;
    private Coroutine narrativeRoutine;
    private Coroutine duckRoutine;
    private float conversationBaseMultiplier = 1f;

    public bool HasActiveStory => activeNarrativeSource != null && activeNarrativeSource.isPlaying;

    public void SetAmbientSources(AudioSource conversation, AudioSource cultural)
    {
        kitchenConversationSource = conversation;
        culturalBackgroundSource = cultural;
    }

    public void SetConversationBaseMultiplier(float multiplier)
    {
        conversationBaseMultiplier = Mathf.Clamp01(multiplier);
        ApplyDuckingImmediate();
    }

    public void SetCulturalBaseMultiplier(float multiplier)
    {
        _ = multiplier;
    }

    public bool TryPlayStory(AudioSource source, AudioClip clip, float volume, bool restartIfAlreadyPlaying, float fadeIn, float fadeOut)
    {
        return TryPlayNarrative(source, clip, volume, restartIfAlreadyPlaying, fadeIn, fadeOut);
    }

    public bool TryPlayNarrative(AudioSource source, AudioClip clip, float volume, bool restartIfAlreadyPlaying, float fadeIn, float fadeOut)
    {
        if (source == null || clip == null)
        {
            Debug.Log("[BlackKitchenAudioCoordinator] Narrative rejected: missing source or clip.");
            return false;
        }

        bool sameNarrativeIsPlaying = activeNarrativeSource == source && activeNarrativeClip == clip && source.isPlaying;
        bool differentNarrativeIsPlaying = activeNarrativeSource != null && activeNarrativeSource.isPlaying && activeNarrativeSource != source;

        if (sameNarrativeIsPlaying)
        {
            if (!restartIfAlreadyPlaying)
            {
                Debug.Log($"[BlackKitchenAudioCoordinator] Narrative rejected: '{clip.name}' is already active.");
                return false;
            }
        }

        if (differentNarrativeIsPlaying && narrativeOverlapPolicy == NarrativeOverlapPolicy.PreventSimultaneousStories)
        {
            Debug.Log($"[BlackKitchenAudioCoordinator] Narrative rejected by policy while '{ActiveNarrativeName()}' is active.");
            return false;
        }

        if (differentNarrativeIsPlaying)
        {
            Debug.Log($"[BlackKitchenAudioCoordinator] Fading active narrative '{ActiveNarrativeName()}' before '{clip.name}'.");
        }

        if (narrativeRoutine != null)
            StopCoroutine(narrativeRoutine);

        narrativeRoutine = StartCoroutine(PlayNarrativeRoutine(source, clip, volume, fadeIn, fadeOut));
        return true;
    }

    public bool IsNarrativeActive(AudioSource source, AudioClip clip)
    {
        return source != null && clip != null && activeNarrativeSource == source && activeNarrativeClip == clip;
    }

    public bool PlayNarrativeReplacingActive(AudioSource source, AudioClip clip, float volume, float fadeIn, float fadeOut)
    {
        if (source == null || clip == null)
        {
            Debug.Log("[BlackKitchenAudioCoordinator] Narrative rejected: missing source or clip.");
            return false;
        }

        if (activeNarrativeSource == source && activeNarrativeClip == clip && source.isPlaying)
        {
            Debug.Log($"[BlackKitchenAudioCoordinator] Narrative rejected: '{clip.name}' is already active.");
            return false;
        }

        if (activeNarrativeSource != null && activeNarrativeSource.isPlaying && activeNarrativeSource != source)
            Debug.Log($"[BlackKitchenAudioCoordinator] Fading active narrative '{ActiveNarrativeName()}' before '{clip.name}'.");

        if (narrativeRoutine != null)
            StopCoroutine(narrativeRoutine);

        narrativeRoutine = StartCoroutine(PlayNarrativeRoutine(source, clip, volume, fadeIn, fadeOut));
        return true;
    }

    public void StopNarrativeImmediate(AudioSource source, AudioClip clip)
    {
        if (!IsNarrativeActive(source, clip))
            return;

        StopActiveNarrativeImmediate();
    }

    public void StopActiveNarrativeImmediate()
    {
        if (narrativeRoutine != null)
            StopCoroutine(narrativeRoutine);

        AudioSource source = activeNarrativeSource;
        AudioClip clip = activeNarrativeClip;
        if (source != null)
        {
            source.Stop();
            source.volume = 0f;
        }

        if (clip != null)
            Debug.Log($"[BlackKitchenAudioCoordinator] Narrative stopped: '{clip.name}'.");
        activeNarrativeSource = null;
        activeNarrativeClip = null;
        StartAmbientDucking(false);
        narrativeRoutine = null;
    }

    public Coroutine FadeOutActiveNarrative(float fadeOut)
    {
        if (narrativeRoutine != null)
            StopCoroutine(narrativeRoutine);

        narrativeRoutine = StartCoroutine(StopActiveNarrativeRoutine(fadeOut));
        return narrativeRoutine;
    }

    private IEnumerator PlayNarrativeRoutine(AudioSource source, AudioClip clip, float volume, float fadeIn, float fadeOut)
    {
        AudioSource previousSource = activeNarrativeSource;
        AudioClip previousClip = activeNarrativeClip;
        if (previousSource != null && previousSource.isPlaying)
            yield return FadeOutAndStop(previousSource, fadeOut);

        if (previousSource != null && previousSource != source && previousClip != null)
            Debug.Log($"[BlackKitchenAudioCoordinator] Narrative stopped: '{previousClip.name}'.");

        activeNarrativeSource = source;
        activeNarrativeClip = clip;
        StartAmbientDucking(true);

        source.clip = clip;
        source.loop = false;
        source.volume = 0f;
        source.Play();
        Debug.Log($"[BlackKitchenAudioCoordinator] Narrative started: '{clip.name}'.");

        yield return FadeSourceVolume(source, Mathf.Clamp01(volume), fadeIn);
        while (source != null && source.isPlaying && activeNarrativeSource == source && activeNarrativeClip == clip)
            yield return null;

        if (activeNarrativeSource == source && activeNarrativeClip == clip)
        {
            if (source != null && source.isPlaying)
                yield return FadeSourceVolume(source, 0f, fadeOut);
            if (source != null)
                source.Stop();

            Debug.Log($"[BlackKitchenAudioCoordinator] Narrative cleared: '{clip.name}'.");
            activeNarrativeSource = null;
            activeNarrativeClip = null;
            StartAmbientDucking(false);
        }

        narrativeRoutine = null;
    }

    private IEnumerator StopActiveNarrativeRoutine(float fadeOut)
    {
        AudioSource source = activeNarrativeSource;
        AudioClip clip = activeNarrativeClip;
        if (source != null && source.isPlaying)
            yield return FadeOutAndStop(source, fadeOut);

        if (clip != null)
            Debug.Log($"[BlackKitchenAudioCoordinator] Narrative stopped: '{clip.name}'.");

        activeNarrativeSource = null;
        activeNarrativeClip = null;
        StartAmbientDucking(false);
        narrativeRoutine = null;
    }

    private void StartAmbientDucking(bool duck)
    {
        if (duckRoutine != null)
            StopCoroutine(duckRoutine);

        duckRoutine = StartCoroutine(FadeAmbientDucking(duck));
    }

    private IEnumerator FadeAmbientDucking(bool duck)
    {
        float startConversation = kitchenConversationSource != null ? kitchenConversationSource.volume : 0f;
        float targetConversation = conversationBaseMultiplier * (duck ? kitchenConversationDuckMultiplier : 1f);

        for (float elapsed = 0f; elapsed < duckFadeDuration; elapsed += Time.deltaTime)
        {
            float t = duckFadeDuration <= 0f ? 1f : elapsed / duckFadeDuration;
            if (kitchenConversationSource != null)
                kitchenConversationSource.volume = Mathf.Lerp(startConversation, targetConversation, t);
            yield return null;
        }

        ApplyDuckingImmediate();
        duckRoutine = null;
    }

    private void ApplyDuckingImmediate()
    {
        bool duck = HasActiveStory;
        if (kitchenConversationSource != null)
            kitchenConversationSource.volume = conversationBaseMultiplier * (duck ? kitchenConversationDuckMultiplier : 1f);
    }

    private string ActiveNarrativeName()
    {
        if (activeNarrativeClip != null)
            return activeNarrativeClip.name;
        if (activeNarrativeSource != null && activeNarrativeSource.clip != null)
            return activeNarrativeSource.clip.name;
        return "unknown";
    }

    private static IEnumerator FadeSourceVolume(AudioSource source, float target, float duration)
    {
        if (source == null)
            yield break;

        float start = source.volume;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            if (source == null)
                yield break;

            float t = duration <= 0f ? 1f : elapsed / duration;
            source.volume = Mathf.Lerp(start, target, t);
            yield return null;
        }

        if (source != null)
            source.volume = target;
    }

    private static IEnumerator FadeOutAndStop(AudioSource source, float duration)
    {
        yield return FadeSourceVolume(source, 0f, duration);
        if (source != null)
            source.Stop();
    }
}
