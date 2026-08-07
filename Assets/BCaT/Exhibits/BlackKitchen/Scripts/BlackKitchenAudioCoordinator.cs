using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class BlackKitchenAudioCoordinator : MonoBehaviour
{
    public enum NarrativeOverlapPolicy
    {
        PreventSimultaneousStories,
        CrossfadeToNewStory
    }

    public enum AudioRole
    {
        Narrative,
        Ambience,
        UI,
        Ignored
    }

    [Header("Ambient Sources (legacy)")]
    [Tooltip("Legacy reference. Kitchen conversation is now a normal audio station; leave empty unless a nonverbal ambience source is approved.")]
    [SerializeField] private AudioSource kitchenConversationSource;
    [Tooltip("Legacy reference. Cultural background is now a normal audio station; leave empty.")]
    [SerializeField] private AudioSource culturalBackgroundSource;

    [Header("Audio Ducking")]
    [Tooltip("Multiplier applied to approved ambience while a narrative plays.")]
    [Range(0f, 1f)]
    [SerializeField] private float kitchenConversationDuckMultiplier = 0.4f;
    [Tooltip("Seconds used to fade ducking in and out.")]
    [SerializeField] private float duckFadeDuration = 1f;
    [Tooltip("Legacy setting. Narrative playback is always exclusive: the newest accepted request stops every other registered narrative source.")]
    [SerializeField] private NarrativeOverlapPolicy narrativeOverlapPolicy = NarrativeOverlapPolicy.CrossfadeToNewStory;

    private readonly List<AudioSource> registeredNarrativeSources = new();
    private readonly List<AudioSource> ambienceSources = new();
    private readonly Dictionary<AudioSource, AudioRole> classifications = new();
    private readonly Dictionary<AudioSource, float> registeredDefaultVolumes = new();
    private AudioSource activeNarrativeSource;
    private AudioClip activeNarrativeClip;
    private Coroutine narrativeRoutine;
    private Coroutine duckRoutine;
    private float conversationBaseMultiplier = 1f;
    private int playbackGeneration;
    private bool sceneExitInProgress;
    private bool registryLogged;
    private bool tearingDown;
    private string lastRequestDescription = "none";

    public bool HasActiveStory => AnyRegisteredNarrativePlaying();
    public bool SceneExitInProgress => sceneExitInProgress;
    public string LastRequestDescription => lastRequestDescription;
    public IReadOnlyList<AudioSource> NarrativeSources => registeredNarrativeSources;
    public IReadOnlyList<AudioSource> AmbienceSources => ambienceSources;

    private void Awake()
    {
        RegisterNarrativeSource(culturalBackgroundSource);
        if (narrativeOverlapPolicy == NarrativeOverlapPolicy.PreventSimultaneousStories)
            Debug.Log("[BlackKitchenAudioCoordinator] narrativeOverlapPolicy is legacy; narrative playback is always exclusive and the newest request replaces the current narrative.");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (GetComponent<BlackKitchenAudioAudit>() == null)
            gameObject.AddComponent<BlackKitchenAudioAudit>().Configure(this);
#endif
    }

    private void Start()
    {
        DiscoverAndClassifySceneSources();
        LogNarrativeRegistry();
        StopAllRegisteredNarrativeSources(null);
        Debug.Log("[BlackKitchenAudioCoordinator] Scene ready: all audio stopped; awaiting user interaction.");
    }

    private void LateUpdate()
    {
        if (tearingDown)
            return;

        ValidateExclusivity();
    }

    private void OnDisable()
    {
        CleanupForTeardown();
    }

    private void OnDestroy()
    {
        CleanupForTeardown();
    }

    // Scene-wide authority: every AudioSource in this scene is classified. Anything not
    // explicitly claimed as Ambience/UI/Ignored is treated as spoken narrative so no
    // unknown source can ever play outside exclusivity control.
    private void DiscoverAndClassifySceneSources()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
            {
                if (source == null || classifications.ContainsKey(source))
                    continue;

                RegisterNarrativeSource(source);
            }
        }
    }

    public void LogNarrativeRegistry()
    {
        if (registryLogged)
            return;

        registryLogged = true;
        StringBuilder log = new StringBuilder("[BlackKitchenAudioCoordinator] Narrative registry:");
        foreach (AudioSource source in registeredNarrativeSources)
        {
            if (source != null)
                log.Append($"\n- {GetHierarchyPath(source.transform)} (clip '{ClipName(source)}', playOnAwake {source.playOnAwake}, loop {source.loop})");
        }

        foreach (AudioSource source in ambienceSources)
        {
            if (source != null)
                log.Append($"\n- {GetHierarchyPath(source.transform)} (clip '{ClipName(source)}') [Ambience]");
        }

        Debug.Log(log.ToString());
    }

    public void RegisterNarrativeSource(AudioSource source)
    {
        if (source == null)
            return;

        if (classifications.TryGetValue(source, out AudioRole existing) && existing != AudioRole.Narrative)
            return;

        classifications[source] = AudioRole.Narrative;
        if (!registeredNarrativeSources.Contains(source))
        {
            registeredNarrativeSources.Add(source);
            registeredDefaultVolumes[source] = source.volume;
        }
    }

    public void ClassifyAsAmbience(AudioSource source)
    {
        if (source == null)
            return;

        classifications[source] = AudioRole.Ambience;
        registeredNarrativeSources.Remove(source);
        if (!ambienceSources.Contains(source))
            ambienceSources.Add(source);
    }

    public void ClassifyAsIgnored(AudioSource source)
    {
        if (source == null)
            return;

        classifications[source] = AudioRole.Ignored;
        registeredNarrativeSources.Remove(source);
        ambienceSources.Remove(source);
    }

    public bool IsRegisteredNarrative(AudioSource source)
    {
        return source != null && classifications.TryGetValue(source, out AudioRole role) && role == AudioRole.Narrative;
    }

    public AudioRole GetClassification(AudioSource source)
    {
        return source != null && classifications.TryGetValue(source, out AudioRole role) ? role : AudioRole.Ignored;
    }

    public bool TryGetClassification(AudioSource source, out AudioRole role)
    {
        role = AudioRole.Ignored;
        return source != null && classifications.TryGetValue(source, out role);
    }

    public void SetAmbientSources(AudioSource conversation, AudioSource cultural)
    {
        kitchenConversationSource = conversation;
        culturalBackgroundSource = cultural;
        RegisterNarrativeSource(conversation);
        RegisterNarrativeSource(cultural);
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
        _ = fadeOut;
        return RequestNarrative(clip != null ? clip.name : "unknown", source, clip, volume, restartIfAlreadyPlaying, fadeIn);
    }

    public bool TryPlayNarrative(AudioSource source, AudioClip clip, float volume, bool restartIfAlreadyPlaying, float fadeIn, float fadeOut)
    {
        _ = fadeOut;
        return RequestNarrative(clip != null ? clip.name : "unknown", source, clip, volume, restartIfAlreadyPlaying, fadeIn);
    }

    public bool PlayNarrativeReplacingActive(AudioSource source, AudioClip clip, float volume, float fadeIn, float fadeOut)
    {
        _ = fadeOut;
        // Exit Reflection path: replace whatever plays, but never toggle itself off.
        return StartNarrativeInternal(clip != null ? clip.name : "unknown", source, clip, volume, false, fadeIn);
    }

    // Sole entry point for starting any Black Kitchen narrative. Requesting the
    // narrative that is currently playing toggles it off instead of rejecting.
    public bool RequestNarrative(string narrativeId, AudioSource source, AudioClip clip, float volume = 1f, bool restartIfAlreadyPlaying = false, float fadeIn = 0f)
    {
        if (source != null && clip != null && !restartIfAlreadyPlaying
            && activeNarrativeSource == source && activeNarrativeClip == clip && source.isPlaying)
        {
            Debug.Log($"[BlackKitchenAudioCoordinator] Toggle stop: '{narrativeId}'");
            StopAllNarrativesImmediate();
            if (isActiveAndEnabled && !tearingDown)
                StartCoroutine(VerifySilenceNextFrame(narrativeId));
            return false;
        }

        return StartNarrativeInternal(narrativeId, source, clip, volume, restartIfAlreadyPlaying, fadeIn);
    }

    private bool StartNarrativeInternal(string narrativeId, AudioSource source, AudioClip clip, float volume, bool restartIfAlreadyPlaying, float fadeIn)
    {
        if (tearingDown)
            return false;

        if (source == null || clip == null)
        {
            Debug.Log($"[BlackKitchenAudioCoordinator] Request '{narrativeId}' rejected: missing source or clip.");
            return false;
        }

        if (sceneExitInProgress)
        {
            Debug.Log($"[BlackKitchenAudioCoordinator] Request '{narrativeId}' rejected: scene exit in progress.");
            return false;
        }

        Debug.Log($"[BlackKitchenAudioCoordinator] Request: '{narrativeId}'");
        RegisterNarrativeSource(source);

        bool sameNarrativeIsPlaying = activeNarrativeSource == source && activeNarrativeClip == clip && source.isPlaying;
        if (sameNarrativeIsPlaying && !restartIfAlreadyPlaying)
        {
            Debug.Log($"[BlackKitchenAudioCoordinator] Request '{narrativeId}' rejected: already active.");
            return false;
        }

        lastRequestDescription = $"'{narrativeId}' via {GetHierarchyPath(source.transform)} at t={Time.time:0.00}";

        CancelPendingNarrativeOperations();
        StopAllRegisteredNarrativeSources(null);

        activeNarrativeSource = source;
        activeNarrativeClip = clip;
        narrativeRoutine = StartCoroutine(PlayNarrativeRoutine(playbackGeneration, narrativeId, source, clip, Mathf.Clamp01(volume), fadeIn));
        return true;
    }

    public bool IsNarrativeActive(AudioSource source, AudioClip clip)
    {
        return source != null && clip != null && activeNarrativeSource == source && activeNarrativeClip == clip;
    }

    public void StopNarrativeImmediate(AudioSource source, AudioClip clip)
    {
        if (IsNarrativeActive(source, clip))
        {
            StopAllNarrativesImmediate();
            return;
        }

        // Not the tracked narrative, but never trust the pointer alone: silence the source anyway.
        if (source != null && source.isPlaying)
        {
            Debug.Log($"[BlackKitchenAudioCoordinator] Stopped untracked source '{source.gameObject.name}' clip '{ClipName(source)}'");
            HardStopSource(source);
        }
    }

    public void StopActiveNarrativeImmediate()
    {
        StopAllNarrativesImmediate();
    }

    public void StopAllNarrativesImmediate()
    {
        string stoppedClipName = activeNarrativeClip != null ? activeNarrativeClip.name : string.Empty;
        CancelPendingNarrativeOperations();
        StopAllRegisteredNarrativeSources(null);
        activeNarrativeSource = null;
        activeNarrativeClip = null;
        BCaT.Production.Media.MediaPlaybackRegistry.NotifyStopped(this);
        if (!string.IsNullOrEmpty(stoppedClipName))
            BCaT.Production.Access.SubtitleService.Instance?.NotifyMediaStopped(stoppedClipName);

        if (sceneExitInProgress || tearingDown || !isActiveAndEnabled)
        {
            StopAmbientDuckingRoutine();
            ApplyDuckingImmediate();
            return;
        }

        StartAmbientDucking(false);
    }

    public void PrepareForSceneExit()
    {
        sceneExitInProgress = true;
        BCaT.Production.Media.MediaPlaybackRegistry.NotifyStopped(this);
        StopAllNarrativesImmediate();

        StopAmbientDuckingRoutine();

        foreach (AudioSource source in ambienceSources)
        {
            if (source == null)
                continue;

            if (source.isPlaying)
                Debug.Log($"[BlackKitchenAudioCoordinator] Stopped ambient source '{source.gameObject.name}' for scene exit.");
            HardStopSource(source);
        }
    }

    public void CancelPendingNarrativeOperations()
    {
        playbackGeneration++;
        if (narrativeRoutine != null)
        {
            StopCoroutine(narrativeRoutine);
            narrativeRoutine = null;
        }
    }

    private void CleanupForTeardown()
    {
        if (tearingDown)
            return;

        tearingDown = true;
        sceneExitInProgress = true;
        BCaT.Production.Media.MediaPlaybackRegistry.NotifyStopped(this);
        CancelPendingNarrativeOperations();
        StopAmbientDuckingRoutine();
        StopAllRegisteredNarrativeSources(null);
        activeNarrativeSource = null;
        activeNarrativeClip = null;

        foreach (AudioSource source in ambienceSources)
        {
            if (source != null)
                HardStopSource(source);
        }
    }

    private IEnumerator PlayNarrativeRoutine(int generation, string narrativeId, AudioSource source, AudioClip clip, float volume, float fadeIn)
    {
        if (tearingDown)
            yield break;

        StartAmbientDucking(true);

        source.clip = clip;
        source.loop = false;
        source.volume = 0f;
        source.time = 0f;
        source.Play();
        Debug.Log($"[BlackKitchenAudioCoordinator] Started exclusive narrative '{narrativeId}'");

        // Long-form narration: registered so the kiosk inactivity policy can
        // defer resets and shell flows can stop it centrally.
        BCaT.Production.Media.MediaPlaybackRegistry.NotifyStarted(this, StopAllNarrativesImmediate);
        BCaT.Production.Access.SubtitleService.Instance?.NotifyMediaStarted(narrativeId);

        // The isPlaying state is authoritative: verify one frame later that exclusivity
        // physically holds, and enforce it if anything else slipped through.
        yield return null;
        if (generation != playbackGeneration)
            yield break;
        VerifyAndEnforceExclusivity(source, narrativeId);

        yield return FadeSourceVolume(generation, source, volume * NarrationUserScale(), fadeIn);
        if (generation != playbackGeneration)
            yield break;

        while (!tearingDown && source != null && source.isPlaying)
        {
            if (generation != playbackGeneration)
                yield break;
            yield return null;
        }

        if (generation != playbackGeneration)
            yield break;

        if (source != null)
            HardStopSource(source);

        Debug.Log($"[BlackKitchenAudioCoordinator] Narrative finished: '{narrativeId}'");
        activeNarrativeSource = null;
        activeNarrativeClip = null;
        BCaT.Production.Media.MediaPlaybackRegistry.NotifyStopped(this);
        BCaT.Production.Access.SubtitleService.Instance?.NotifyMediaStopped(narrativeId);
        StartAmbientDucking(false);
        narrativeRoutine = null;
    }

    private IEnumerator VerifySilenceNextFrame(string narrativeId)
    {
        yield return null;
        bool silent = true;
        foreach (AudioSource source in registeredNarrativeSources)
        {
            if (source == null || !source.isPlaying)
                continue;

            silent = false;
            Debug.LogError($"[BlackKitchenAudioCoordinator] Silence verification failed after toggle stop of '{narrativeId}': '{GetHierarchyPath(source.transform)}' (clip '{ClipName(source)}') still playing. Forcing stop.");
            HardStopSource(source);
        }

        if (silent)
            Debug.Log($"[BlackKitchenAudioCoordinator] Silence verified after toggle stop of '{narrativeId}'.");
    }

    private void VerifyAndEnforceExclusivity(AudioSource current, string narrativeId)
    {
        bool clean = true;
        foreach (AudioSource source in registeredNarrativeSources)
        {
            if (source == null || source == current || !source.isPlaying)
                continue;

            clean = false;
            Debug.LogError($"[BlackKitchenAudioCoordinator] Post-start verification: '{GetHierarchyPath(source.transform)}' (clip '{ClipName(source)}') was still playing after '{narrativeId}' started. Forcing stop.");
            HardStopSource(source);
        }

        if (clean)
            Debug.Log($"[BlackKitchenAudioCoordinator] Post-start verification passed: only '{narrativeId}' is playing.");
    }

    private void StopAllRegisteredNarrativeSources(AudioSource except)
    {
        foreach (AudioSource source in registeredNarrativeSources)
        {
            if (source == null || source == except)
                continue;

            if (source.isPlaying)
                Debug.Log($"[BlackKitchenAudioCoordinator] Stopped source '{source.gameObject.name}' clip '{ClipName(source)}'");
            HardStopSource(source);
        }
    }

    // Stop() also clears any paused state; volume returns to the value captured at registration.
    private void HardStopSource(AudioSource source)
    {
        source.Stop();
        if (source.clip != null)
            source.time = 0f;
        source.volume = registeredDefaultVolumes.TryGetValue(source, out float defaultVolume) ? defaultVolume : 0f;
    }

    public static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
            return "?";

        StringBuilder path = new StringBuilder(transform.name);
        Transform parent = transform.parent;
        while (parent != null)
        {
            path.Insert(0, parent.name + "/");
            parent = parent.parent;
        }

        return path.ToString();
    }

    private static string ClipName(AudioSource source)
    {
        return source != null && source.clip != null ? source.clip.name : "none";
    }

    private bool AnyRegisteredNarrativePlaying()
    {
        foreach (AudioSource source in registeredNarrativeSources)
        {
            if (source != null && source.isPlaying)
                return true;
        }

        return false;
    }

    private void ValidateExclusivity()
    {
        int playing = 0;
        foreach (AudioSource source in registeredNarrativeSources)
        {
            if (source != null && source.isPlaying)
                playing++;
        }

        if (playing <= 1)
            return;

        foreach (AudioSource source in registeredNarrativeSources)
        {
            if (source == null || !source.isPlaying || source == activeNarrativeSource)
                continue;

            Debug.LogError($"[BlackKitchenAudioCoordinator] EXCLUSIVITY VIOLATION: forcing stop of '{GetHierarchyPath(source.transform)}' (clip '{ClipName(source)}') while '{ClipName(activeNarrativeSource)}' is active. Last request: {lastRequestDescription}");
            HardStopSource(source);
        }
    }

    private void StartAmbientDucking(bool duck)
    {
        if (tearingDown || sceneExitInProgress || !isActiveAndEnabled)
            return;

        if (duckRoutine != null)
            StopCoroutine(duckRoutine);

        duckRoutine = StartCoroutine(FadeAmbientDucking(duck));
    }

    private void StopAmbientDuckingRoutine()
    {
        if (duckRoutine == null)
            return;

        StopCoroutine(duckRoutine);
        duckRoutine = null;
    }

    private IEnumerator FadeAmbientDucking(bool duck)
    {
        List<float> startVolumes = new List<float>();
        foreach (AudioSource source in ambienceSources)
            startVolumes.Add(source != null ? source.volume : 0f);

        float target = conversationBaseMultiplier * (duck ? kitchenConversationDuckMultiplier : 1f);
        for (float elapsed = 0f; elapsed < duckFadeDuration; elapsed += Time.deltaTime)
        {
            float t = duckFadeDuration <= 0f ? 1f : elapsed / duckFadeDuration;
            for (int i = 0; i < ambienceSources.Count && i < startVolumes.Count; i++)
            {
                if (ambienceSources[i] != null)
                    ambienceSources[i].volume = Mathf.Lerp(startVolumes[i], target, t);
            }
            yield return null;
        }

        ApplyDuckingImmediate();
        duckRoutine = null;
    }

    private void ApplyDuckingImmediate()
    {
        bool duck = AnyRegisteredNarrativePlaying();
        float target = conversationBaseMultiplier * (duck ? kitchenConversationDuckMultiplier : 1f) * AmbienceUserScale();
        foreach (AudioSource source in ambienceSources)
        {
            if (source != null)
                source.volume = target;
        }
    }

    // User audio-settings integration: the coordinator stays the sole authority
    // over Black Kitchen volumes; it folds the visitor's Narration/Ambience
    // levels into its own targets whenever it computes them. (Master volume is
    // applied globally through AudioListener.volume by the settings service.)
    private static float NarrationUserScale() =>
        Mathf.Clamp01(BCaT.Production.Settings.SettingsManager.Current.audio.narration);

    private static float AmbienceUserScale() =>
        Mathf.Clamp01(BCaT.Production.Settings.SettingsManager.Current.audio.ambience);

    private IEnumerator FadeSourceVolume(int generation, AudioSource source, float target, float duration)
    {
        if (source == null)
            yield break;

        float start = source.volume;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            if (generation != playbackGeneration || source == null)
                yield break;

            float t = duration <= 0f ? 1f : elapsed / duration;
            source.volume = Mathf.Lerp(start, target, t);
            yield return null;
        }

        if (generation == playbackGeneration && source != null)
            source.volume = target;
    }
}
