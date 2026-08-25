using TMPro;
using UnityEngine;

/// <summary>
/// Quest-only entry orientation: a short, non-blocking card that teaches the
/// Black Kitchen interaction model once, then gets out of the way.
///
/// It is deliberately passive. It registers no interaction blocker, has
/// blocksRaycasts off and is not interactable, so walking, turning, gaze
/// discovery and trigger activation all work normally while it is up. It never
/// touches audio, stations, the coordinator or the exit flow.
///
/// It disappears on whichever comes first: the display timeout, or the first
/// successful story activation (read from
/// <see cref="BlackKitchenInteractionManager.HasActivatedAnyStory"/>), because at
/// that point the visitor has demonstrably understood the model.
///
/// World-anchored in front of the entry rather than head-locked, so it reads as
/// part of the room and does not follow the wearer's view.
/// </summary>
[DisallowMultipleComponent]
public sealed class BlackKitchenQuestOrientation : MonoBehaviour
{
    const string SharedInstructionPanelName = "InstructionPanel";

    [Header("Wiring")]
    [Tooltip("Supplies HasActivatedAnyStory. Resolved from the scene when empty.")]
    [SerializeField] private BlackKitchenInteractionManager interactionManager;

    [Tooltip("Authored BlackKitchenEntry spawn. Resolved from the scene when empty.")]
    [SerializeField] private Transform entrySpawn;

    [Header("Copy")]
    [SerializeField] private string title = "Explore the Black Kitchen";
    [SerializeField] private string body =
        "Walk around to discover stories.\n\n" +
        "When a story appears, pull either trigger to listen.";

    [Header("Behaviour")]
    [Tooltip("Seconds the card stays up if the visitor has not yet started a story.")]
    [SerializeField] private float displaySeconds = 14f;

    [Tooltip("Metres in front of the visitor's entry position.")]
    [SerializeField] private float placeDistance = 2.0f;

    [Tooltip("Vertical offset from eye height, in metres.")]
    [SerializeField] private float heightOffset = -0.1f;

    [Tooltip("World-space height above the entry floor for the Quest card anchor.")]
    [SerializeField] private float entryEyeHeight = 1.55f;

    [Tooltip("Seconds to fade out over.")]
    [SerializeField] private float fadeSeconds = 0.6f;

    Canvas canvas;
    CanvasGroup group;
    bool finished;
    float shownAt = -1f;

    void Awake()
    {
        if (BCaT.Production.PlatformCapabilities.IsXRActive)
            SuppressSharedInstructionPanel();
    }

    void Start()
    {
        // Desktop is untouched: nothing is built and nothing runs.
        if (!BCaT.Production.PlatformCapabilities.IsXRActive)
        {
            enabled = false;
            return;
        }

        if (interactionManager == null)
            interactionManager = FindAnyObjectByType<BlackKitchenInteractionManager>();

        if (entrySpawn == null)
            entrySpawn = ResolveEntrySpawn();

        Build();
        PlaceAtEntry();
        shownAt = Time.unscaledTime;
        Debug.Log($"[BlackKitchenQuestOrientation] Shown at {canvas.transform.position} " +
                  $"using BlackKitchenEntry forward for up to {displaySeconds:0.#}s, " +
                  "or until the first story activation.");
    }

    void Update()
    {
        if (finished || canvas == null)
            return;

        bool storyStarted = interactionManager != null && interactionManager.HasActivatedAnyStory;
        bool timedOut = Time.unscaledTime - shownAt >= displaySeconds;
        if (!storyStarted && !timedOut)
            return;

        finished = true;
        Debug.Log("[BlackKitchenQuestOrientation] Dismissed " +
                  $"({(storyStarted ? "first story activated" : "timed out")}).");
        StartCoroutine(FadeOutAndDestroy());
    }

    System.Collections.IEnumerator FadeOutAndDestroy()
    {
        float start = group != null ? group.alpha : 1f;
        for (float elapsed = 0f; elapsed < fadeSeconds; elapsed += Time.unscaledDeltaTime)
        {
            if (group != null)
                group.alpha = Mathf.Lerp(start, 0f, fadeSeconds <= 0f ? 1f : elapsed / fadeSeconds);
            yield return null;
        }

        if (canvas != null)
            Destroy(canvas.gameObject);
        canvas = null;
        group = null;
        enabled = false;
    }

    void SuppressSharedInstructionPanel()
    {
        Transform panel = transform.Find(SharedInstructionPanelName);
        if (panel == null)
            return;

        panel.gameObject.SetActive(false);
        Debug.Log($"[BlackKitchenQuestOrientation] Suppressed shared '{SharedInstructionPanelName}' for Quest before reveal.");
    }

    static Transform ResolveEntrySpawn()
    {
        foreach (SceneSpawnPoint spawn in FindObjectsByType<SceneSpawnPoint>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (spawn != null && spawn.SpawnId == SceneTransitionState.BlackKitchenEntrySpawnId)
                return spawn.transform;
        }

        return null;
    }

    void PlaceAtEntry()
    {
        Vector3 origin = entrySpawn != null ? entrySpawn.position : transform.position;
        Vector3 forward = entrySpawn != null ? entrySpawn.forward : Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-4f)
            forward = Vector3.forward;
        forward.Normalize();

        canvas.transform.SetPositionAndRotation(
            origin + Vector3.up * (entryEyeHeight + heightOffset) + forward * placeDistance,
            Quaternion.LookRotation(forward, Vector3.up));
    }

    void Build()
    {
        var canvasObject = new GameObject("BlackKitchenQuestOrientation",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 31000; // below the exit choice panel (32000)

        group = canvasObject.GetComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = false;
        group.blocksRaycasts = false; // non-blocking: never eats a controller ray

        var canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1100f, 460f);
        canvasRect.localScale = Vector3.one * 0.0011f;

        GameObject panel = ExitChoiceUiBuilder.Panel(canvasObject.transform);
        ExitChoiceUiBuilder.Title(panel.transform, title, 50f, -52f);

        TMP_Text bodyText = ExitChoiceUiBuilder.Body(panel.transform, body, 32f, -20f, 260f);
        bodyText.alignment = TextAlignmentOptions.Center;
    }
}
