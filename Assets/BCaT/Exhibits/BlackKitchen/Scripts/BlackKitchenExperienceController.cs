using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class BlackKitchenExperienceController : MonoBehaviour
{
    public enum CulturalTriggerMode
    {
        AfterEntryDelay,
        FirstInteraction
    }

    private static BlackKitchenExperienceController active;

    [Header("Spawn and Return")]
    [SerializeField] private Transform spawnPoint;

    [Header("Fall Recovery")]
    [Tooltip("Returns the active player to SpawnPoint while in Black Kitchen if their root falls below this world-space Y value.")]
    [SerializeField] private float fallRecoveryYThreshold = -2.5f;
    [SerializeField] private bool enableFallRecovery = true;

    [Header("Cultural Background")]
    [SerializeField] private AudioSource culturalBackgroundSource;
    [SerializeField] private CulturalTriggerMode triggerMode = CulturalTriggerMode.AfterEntryDelay;
    [SerializeField] private float entryDelay = 4f;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.18f;
    [SerializeField] private float fadeInDuration = 3f;
    [SerializeField] private float fadeOutDuration = 2f;
    [SerializeField] private bool playOncePerVisit = true;
    [SerializeField] private bool playOncePerApplicationSession = false;

    [Header("Exit Reflection")]
    [SerializeField] private AudioSource exitReflectionSource;
    [SerializeField] private float exitReflectionFadeDuration = 3f;

    [Header("Exit Interface")]
    [SerializeField] private float exitInteractionDistance = 4f;
    [SerializeField] private Key interactionKey = Key.E;
    [SerializeField] private TMP_Text exitPromptText;
    [SerializeField] private string desktopExitPrompt = "Press E to Exit Black Kitchen";
    [SerializeField] private string xrExitPrompt = "Interact to Exit Black Kitchen";
    [SerializeField] private Transform exitInteractionRoot;

    [Header("Audio Ducking")]
    [SerializeField] private BlackKitchenAudioCoordinator audioCoordinator;

    [Header("Debug")]
    [SerializeField] private bool resetExitReflectionSessionFlagOnStart;

    private bool culturalPlayedThisVisit;
    private bool exitInProgress;
    private static bool culturalPlayedThisSession;
    private Transform fallbackPlayerRoot;

    public Transform SpawnPoint => spawnPoint;

    private void Awake()
    {
        active = this;
        if (resetExitReflectionSessionFlagOnStart)
            BlackKitchenSessionState.ResetForTesting();
    }

    private void Start()
    {
        if (culturalBackgroundSource != null)
        {
            culturalBackgroundSource.playOnAwake = false;
            culturalBackgroundSource.loop = false;
            culturalBackgroundSource.volume = 0f;
        }

        if (triggerMode == CulturalTriggerMode.AfterEntryDelay)
            StartCoroutine(PlayCulturalAfterDelay());
    }

    private void Update()
    {
        UpdateFallRecovery();

        if (exitPromptText != null)
            exitPromptText.text = InteractionPromptText.IsXRActive() ? xrExitPrompt : desktopExitPrompt;

        if (exitInProgress || Keyboard.current == null || !Keyboard.current[interactionKey].wasPressedThisFrame)
            return;

        if (IsLookingAtExit())
            ExitBlackKitchen();
    }

    public static void NotifyMeaningfulInteraction()
    {
        if (active != null)
            active.OnMeaningfulInteraction();
    }

    public void ExitBlackKitchen()
    {
        if (exitInProgress)
            return;

        exitInProgress = true;
        bool shouldPlayReflection = exitReflectionSource != null && !BlackKitchenSessionState.ExitReflectionPlayed;
        AudioSource transitionReflectionSource = null;
        if (shouldPlayReflection)
        {
            AudioClip reflectionClip = exitReflectionSource.clip;
            transitionReflectionSource = BlackKitchenPersistentAudioHost.Instance.PrepareFrom(exitReflectionSource, reflectionClip, 1f);
            if (audioCoordinator != null)
                audioCoordinator.TryPlayNarrative(transitionReflectionSource, reflectionClip, 1f, false, 0.05f, exitReflectionFadeDuration);
            else
                transitionReflectionSource.Play();

            BlackKitchenSessionState.MarkExitReflectionPlayed();
        }
        else if (audioCoordinator != null)
        {
            audioCoordinator.FadeOutActiveNarrative(fadeOutDuration);
        }

        BlackKitchenPortalController.ReturnFromMemory(transitionReflectionSource, shouldPlayReflection ? exitReflectionFadeDuration : 0f);
    }

    public void OnXRExitSelect()
    {
        ExitBlackKitchen();
    }

    private void OnMeaningfulInteraction()
    {
        if (triggerMode == CulturalTriggerMode.FirstInteraction)
            StartCulturalBackground();
    }

    private void UpdateFallRecovery()
    {
        if (!enableFallRecovery || spawnPoint == null || exitInProgress)
            return;

        Transform player = ResolveFallbackPlayerRoot();
        if (player != null && player.position.y < fallRecoveryYThreshold)
            BlackKitchenPortalController.RecoverActivePlayerTo(spawnPoint);
    }

    private Transform ResolveFallbackPlayerRoot()
    {
        if (fallbackPlayerRoot != null)
            return fallbackPlayerRoot;

        Camera cam = Camera.main;
        if (cam != null)
        {
            CharacterController controller = cam.GetComponentInParent<CharacterController>();
            fallbackPlayerRoot = controller != null ? controller.transform : cam.transform.root;
        }

        return fallbackPlayerRoot;
    }

    private IEnumerator PlayCulturalAfterDelay()
    {
        yield return new WaitForSeconds(entryDelay);
        StartCulturalBackground();
    }

    private void StartCulturalBackground()
    {
        if (culturalBackgroundSource == null)
            return;
        if (playOncePerVisit && culturalPlayedThisVisit)
            return;
        if (playOncePerApplicationSession && culturalPlayedThisSession)
            return;

        culturalPlayedThisVisit = true;
        culturalPlayedThisSession = true;

        if (audioCoordinator != null)
            audioCoordinator.TryPlayNarrative(culturalBackgroundSource, culturalBackgroundSource.clip, volume, false, fadeInDuration, fadeOutDuration);
        else
            StartCoroutine(FadeCultural(volume, fadeInDuration, true));
    }

    private IEnumerator FadeCultural(float target, float duration, bool play)
    {
        if (play && !culturalBackgroundSource.isPlaying)
            culturalBackgroundSource.Play();

        float start = culturalBackgroundSource.volume;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = duration <= 0f ? 1f : elapsed / duration;
            culturalBackgroundSource.volume = Mathf.Lerp(start, target, t);
            if (audioCoordinator != null)
                audioCoordinator.SetCulturalBaseMultiplier(volume <= 0f ? 0f : culturalBackgroundSource.volume / volume);
            yield return null;
        }

        culturalBackgroundSource.volume = target;
        if (audioCoordinator != null)
            audioCoordinator.SetCulturalBaseMultiplier(volume <= 0f ? 0f : culturalBackgroundSource.volume / volume);

        if (target <= 0f)
            culturalBackgroundSource.Stop();
    }

    private bool IsLookingAtExit()
    {
        if (exitInteractionRoot == null)
            exitInteractionRoot = transform;

        Camera cam = Camera.main;
        if (cam == null)
            return false;

        RaycastHit[] hits = Physics.RaycastAll(new Ray(cam.transform.position, cam.transform.forward), exitInteractionDistance, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform == exitInteractionRoot || hit.collider.transform.IsChildOf(exitInteractionRoot))
                return true;
            if (!hit.collider.isTrigger)
                return false;
        }

        return false;
    }
}
