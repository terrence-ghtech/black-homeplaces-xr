#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Development-only diagnostic monitor. Watches every AudioSource in the Black Kitchen
// scene (registered or not) and logs whenever the set of playing sources changes,
// exposing hidden, duplicate, or unregistered sources instead of only stating that a
// violation occurred. Added automatically by BlackKitchenAudioCoordinator.
public class BlackKitchenAudioAudit : MonoBehaviour
{
    private const float SourceRescanInterval = 2f;

    private BlackKitchenAudioCoordinator coordinator;
    private readonly List<AudioSource> sceneSources = new();
    private readonly HashSet<int> previouslyPlaying = new();
    private float nextRescanTime;

    public void Configure(BlackKitchenAudioCoordinator owner)
    {
        coordinator = owner;
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextRescanTime)
        {
            RescanSources();
            nextRescanTime = Time.unscaledTime + SourceRescanInterval;
        }

        List<AudioSource> playing = new List<AudioSource>();
        HashSet<int> playingIds = new HashSet<int>();
        int playingNarratives = 0;
        foreach (AudioSource source in sceneSources)
        {
            if (source == null || !source.isPlaying)
                continue;

            playing.Add(source);
            playingIds.Add(source.GetInstanceID());
            // Unclassified playing sources are treated as suspect narratives so hidden
            // or dynamically added sources cannot evade the violation check.
            if (coordinator == null
                || !coordinator.TryGetClassification(source, out BlackKitchenAudioCoordinator.AudioRole role)
                || role == BlackKitchenAudioCoordinator.AudioRole.Narrative)
                playingNarratives++;
        }

        if (!playingIds.SetEquals(previouslyPlaying))
        {
            previouslyPlaying.Clear();
            previouslyPlaying.UnionWith(playingIds);
            LogPlayingSet(playing);
        }

        if (playingNarratives > 1)
            LogViolation(playing);
    }

    private void RescanSources()
    {
        sceneSources.Clear();
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            sceneSources.AddRange(root.GetComponentsInChildren<AudioSource>(true));
    }

    private void LogPlayingSet(List<AudioSource> playing)
    {
        StringBuilder log = new StringBuilder("[BlackKitchenAudioAudit] Currently playing:");
        if (playing.Count == 0)
        {
            log.Append("\n- (none)");
        }
        else
        {
            foreach (AudioSource source in playing)
                log.Append($"\n- {source.gameObject.name} | {ClipName(source)} | {Classify(source)}");
        }

        Debug.Log(log.ToString());
    }

    private void LogViolation(List<AudioSource> playing)
    {
        StringBuilder log = new StringBuilder("[BlackKitchenAudioAudit] EXCLUSIVITY VIOLATION");
        foreach (AudioSource source in playing)
        {
            log.Append($"\n- path {BlackKitchenAudioCoordinator.GetHierarchyPath(source.transform)}");
            log.Append($" | clip '{ClipName(source)}'");
            log.Append($" | instanceID {source.GetInstanceID()}");
            log.Append($" | registered {(coordinator != null && coordinator.IsRegisteredNarrative(source))}");
            log.Append($" | classification {Classify(source)}");
            log.Append($" | playOnAwake {source.playOnAwake}");
            log.Append($" | loop {source.loop}");
            log.Append($" | time {source.time:0.00}");
            log.Append($" | volume {source.volume:0.00}");
        }

        log.Append($"\nLast coordinator request: {(coordinator != null ? coordinator.LastRequestDescription : "no coordinator")}");
        Debug.LogError(log.ToString());
    }

    private string Classify(AudioSource source)
    {
        if (coordinator == null)
            return "Unknown";

        return coordinator.TryGetClassification(source, out BlackKitchenAudioCoordinator.AudioRole role) ? role.ToString() : "Unclassified";
    }

    private static string ClipName(AudioSource source)
    {
        return source.clip != null ? source.clip.name : "none";
    }
}
#endif
