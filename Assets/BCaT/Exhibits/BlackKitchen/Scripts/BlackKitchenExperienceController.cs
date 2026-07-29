using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class BlackKitchenExperienceController : MonoBehaviour
{
    [Header("Spawn and Return")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private string mainHouseSceneName = SceneTransitionState.MainHouseSceneName;
    [SerializeField] private string loadingSceneName = SceneTransitionState.LoadingSceneName;

    [Header("Fall Recovery")]
    [Tooltip("Returns the active player to SpawnPoint while in Black Kitchen if their root falls below this world-space Y value.")]
    [SerializeField] private float fallRecoveryYThreshold = -2.5f;
    [SerializeField] private bool enableFallRecovery = true;

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

    private float exitModalCloseTime = -999f;
    private const float ExitModalReopenCooldown = 0.35f;
    private bool exitInProgress;
    private bool exitReflectionRequested;
    private bool exitModalOpen;
    private bool exitModalChoiceHandled;
    private bool exitModalUsesXR;
    private Canvas exitReflectionModalCanvas;
    private CanvasGroup exitReflectionModalGroup;
    private CanvasGroup sceneFadeGroup;
    private const float ExitReflectionStartFadeDuration = 0.05f;
    private Transform fallbackPlayerRoot;
    private readonly List<Behaviour> modalDisabledDesktopControls = new();
    private bool cursorStateCaptured;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;

    public Transform SpawnPoint => spawnPoint;
    public bool IsExitModalOpen => exitModalOpen;

    private void Awake()
    {
        if (resetExitReflectionSessionFlagOnStart)
            BlackKitchenSessionState.ResetForTesting();
    }

    private void Start()
    {
        if (exitReflectionSource != null)
        {
            exitReflectionSource.playOnAwake = false;
            exitReflectionSource.loop = false;
            exitReflectionSource.volume = 0f;
        }

        if (audioCoordinator != null)
            audioCoordinator.RegisterNarrativeSource(exitReflectionSource);
    }

    private void Update()
    {
        UpdateFallRecovery();

        if (exitPromptText != null)
            exitPromptText.text = InteractionPromptText.IsXRActive() ? xrExitPrompt : desktopExitPrompt;

        if (exitReflectionModalCanvas != null && exitReflectionModalCanvas.gameObject.activeSelf)
        {
            PlaceExitReflectionModal();
            HandleExitModalKeyboard();
            return;
        }

        if (exitInProgress || Keyboard.current == null || !Keyboard.current[interactionKey].wasPressedThisFrame)
            return;

        if (IsLookingAtExit())
            ExitBlackKitchen();
    }

    public void ExitBlackKitchen()
    {
        if (exitInProgress || exitModalOpen)
            return;

        if (Time.unscaledTime - exitModalCloseTime < ExitModalReopenCooldown)
        {
            Debug.Log($"[BlackKitchenExperienceController] Exit Reflection reopen suppressed within {ExitModalReopenCooldown:0.00}s of closing.");
            return;
        }

        StartExitReflectionIfNeeded();
        ShowExitReflectionModal();
    }

    public void OnXRExitSelect()
    {
        ExitBlackKitchen();
    }

    private void UpdateFallRecovery()
    {
        if (!enableFallRecovery || spawnPoint == null || exitInProgress)
            return;

        Transform player = ResolveFallbackPlayerRoot();
        if (player != null && player.position.y < fallRecoveryYThreshold)
            SceneArrivalController.PlaceActivePlayerAt(spawnPoint);
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

    private void StartExitReflectionIfNeeded()
    {
        if (exitReflectionRequested || IsExitReflectionPlaying())
            return;

        if (exitReflectionSource == null || exitReflectionSource.clip == null)
        {
            Debug.LogWarning("[BlackKitchenExperienceController] Exit Reflection unavailable: missing source or clip.");
            return;
        }

        if (audioCoordinator == null)
        {
            Debug.LogWarning("[BlackKitchenExperienceController] Exit Reflection not started: no audio coordinator assigned. The coordinator is the sole authority for narrative playback.");
            return;
        }

        float startFadeDuration = Mathf.Min(exitReflectionFadeDuration, ExitReflectionStartFadeDuration);
        bool started = audioCoordinator.PlayNarrativeReplacingActive(exitReflectionSource, exitReflectionSource.clip, 1f, startFadeDuration, startFadeDuration);
        if (!started)
        {
            Debug.Log("[BlackKitchenExperienceController] Exit Reflection was not started by the audio coordinator.");
            return;
        }

        exitReflectionRequested = true;
        BlackKitchenSessionState.MarkExitReflectionPlayed();
    }

    private bool IsExitReflectionPlaying()
    {
        if (exitReflectionSource == null || exitReflectionSource.clip == null)
            return false;

        if (audioCoordinator != null && audioCoordinator.IsNarrativeActive(exitReflectionSource, exitReflectionSource.clip))
            return true;

        return exitReflectionSource.isPlaying;
    }

    private void ShowExitReflectionModal()
    {
        EnsureExitReflectionModal();
        if (exitReflectionModalCanvas == null)
            return;

        exitModalOpen = true;
        exitModalChoiceHandled = false;
        exitModalUsesXR = ScenePlatformRigSelector.ShouldUseXR();
        ConfigureExitReflectionModalForPlatform(exitModalUsesXR);
        EnsureActiveEventSystemForModal(exitModalUsesXR);
        SetDesktopModalControls(false);

        exitReflectionModalCanvas.gameObject.SetActive(true);
        exitReflectionModalCanvas.enabled = true;
        if (exitReflectionModalGroup != null)
        {
            exitReflectionModalGroup.alpha = 1f;
            exitReflectionModalGroup.interactable = true;
            exitReflectionModalGroup.blocksRaycasts = true;
        }

        PlaceExitReflectionModal();
        Debug.Log($"[BlackKitchenExperienceController] Scene '{gameObject.scene.name}' exit modal opened for platform '{(exitModalUsesXR ? "XR" : "Desktop")}'.");
    }

    private void HideExitReflectionModal()
    {
        exitModalOpen = false;
        exitModalCloseTime = Time.unscaledTime;

        if (exitReflectionModalGroup != null)
        {
            exitReflectionModalGroup.interactable = false;
            exitReflectionModalGroup.blocksRaycasts = false;
        }

        if (exitReflectionModalCanvas != null)
            exitReflectionModalCanvas.gameObject.SetActive(false);
    }

    public void FinishListeningBeforeExit()
    {
        SelectStay();
    }

    public void SelectStay()
    {
        if (exitModalChoiceHandled)
            return;

        exitModalChoiceHandled = true;
        Debug.Log($"[BlackKitchenExperienceController] Scene '{gameObject.scene.name}' Stay selected.");
        HideExitReflectionModal();
        StopExitReflectionImmediate();
        exitReflectionRequested = false;
        exitInProgress = false;
        SetDesktopModalControls(true);
    }

    public void ExitNow()
    {
        SelectExitNow();
    }

    public void SelectExitNow()
    {
        if (exitInProgress || exitModalChoiceHandled)
            return;

        exitModalChoiceHandled = true;
        Debug.Log($"[BlackKitchenExperienceController] Scene '{gameObject.scene.name}' Exit Now selected.");
        if (exitReflectionModalGroup != null)
        {
            exitReflectionModalGroup.interactable = false;
            exitReflectionModalGroup.blocksRaycasts = false;
        }

        HideExitReflectionModal();
        StopExitReflectionImmediate();
        SetDesktopModalControls(true);
        StartCoroutine(ExitToMainHouseRoutine());
    }

    private IEnumerator ExitToMainHouseRoutine()
    {
        exitInProgress = true;

        if (!SceneTransitionState.RequestTransition(mainHouseSceneName, SceneTransitionState.MainHouseKitchenReturnSpawnId, gameObject.scene.name))
        {
            Debug.LogWarning($"[BlackKitchenExperienceController] Return transition request blocked: {SceneTransitionState.LastError}");
            exitInProgress = false;
            SetDesktopModalControls(true);
            yield break;
        }

        Debug.Log($"[BlackKitchenExperienceController] Scene '{gameObject.scene.name}' Main House transition requested: scene '{mainHouseSceneName}', spawn '{SceneTransitionState.MainHouseKitchenReturnSpawnId}'.");
        PrepareAudioForSceneExit();
        yield return FadeSceneToBlack(0.7f);

        AsyncOperation load = null;
        try
        {
            Debug.Log($"[BlackKitchenExperienceController] Scene '{gameObject.scene.name}' loading scene load requested: '{loadingSceneName}'.");
            load = SceneManager.LoadSceneAsync(loadingSceneName, LoadSceneMode.Single);
        }
        catch (System.Exception exception)
        {
            string message = $"[BlackKitchenExperienceController] Failed to load '{loadingSceneName}' for Main House return: {exception.Message}";
            Debug.LogError(message);
            SceneTransitionState.CancelTransition(message);
            exitInProgress = false;
            SetDesktopModalControls(true);
            yield break;
        }

        if (load == null)
        {
            string message = $"[BlackKitchenExperienceController] Failed to load '{loadingSceneName}' for Main House return.";
            Debug.LogError(message);
            SceneTransitionState.CancelTransition(message);
            exitInProgress = false;
            SetDesktopModalControls(true);
            yield break;
        }

        while (!load.isDone)
            yield return null;
    }

    private void StopExitReflectionImmediate()
    {
        if (audioCoordinator != null)
            audioCoordinator.StopAllNarrativesImmediate();

        if (exitReflectionSource != null)
        {
            if (exitReflectionSource.isPlaying)
                exitReflectionSource.Stop();
            exitReflectionSource.volume = 0f;
        }

        Debug.Log($"[BlackKitchenExperienceController] Scene '{gameObject.scene.name}' exit audio stopped.");
    }

    private void PrepareAudioForSceneExit()
    {
        if (audioCoordinator != null)
            audioCoordinator.PrepareForSceneExit();

        foreach (AudioSource source in GetComponentsInChildren<AudioSource>(true))
        {
            if (source == null)
                continue;

            source.Stop();
            source.clip = null;
        }
    }

    private IEnumerator FadeSceneToBlack(float duration)
    {
        EnsureSceneFadeOverlay();
        if (sceneFadeGroup == null)
            yield break;

        sceneFadeGroup.blocksRaycasts = true;
        float start = sceneFadeGroup.alpha;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = duration <= 0f ? 1f : elapsed / duration;
            sceneFadeGroup.alpha = Mathf.Lerp(start, 1f, t);
            yield return null;
        }

        sceneFadeGroup.alpha = 1f;
        sceneFadeGroup.blocksRaycasts = true;
    }

    private void EnsureSceneFadeOverlay()
    {
        if (sceneFadeGroup != null)
            return;

        GameObject canvasObject = new GameObject("BlackKitchenExitFade", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32750;

        sceneFadeGroup = canvasObject.GetComponent<CanvasGroup>();
        sceneFadeGroup.alpha = 0f;
        sceneFadeGroup.blocksRaycasts = false;

        GameObject imageObject = new GameObject("Black", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.color = Color.black;
    }

    private void EnsureExitReflectionModal()
    {
        if (exitReflectionModalCanvas != null)
            return;

        GameObject canvasObject = new GameObject("ExitReflectionModal", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        exitReflectionModalCanvas = canvasObject.GetComponent<Canvas>();
        exitReflectionModalCanvas.sortingOrder = 32000;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(900f, 520f);
        canvasRect.localScale = Vector3.one * 0.0018f;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 1f;

        exitReflectionModalGroup = canvasObject.GetComponent<CanvasGroup>();

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.025f, 0.028f, 0.03f, 0.96f);

        TMP_Text title = CreateModalText(panel.transform, "Title", "Leaving the Black Kitchen", 42f, FontStyles.Bold);
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -54f);
        titleRect.sizeDelta = new Vector2(-96f, 72f);

        TMP_Text body = CreateModalText(panel.transform, "Body", "Exit Reflection has begun playing.\n\nChoose Stay to remain in the Black Kitchen or Exit Now to return to the Main House.\n\nKeyboard: Esc/S = Stay, Enter/E/L = Exit Now", 26f, FontStyles.Normal);
        RectTransform bodyRect = body.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0.5f);
        bodyRect.anchorMax = new Vector2(1f, 0.5f);
        bodyRect.pivot = new Vector2(0.5f, 0.5f);
        bodyRect.anchoredPosition = new Vector2(0f, 30f);
        bodyRect.sizeDelta = new Vector2(-120f, 190f);

        Button stayButton = CreateModalButton(panel.transform, "StayButton", "Stay", new Vector2(-170f, -180f));
        stayButton.onClick.AddListener(SelectStay);

        Button exitButton = CreateModalButton(panel.transform, "ExitNowButton", "Exit Now", new Vector2(170f, -180f));
        exitButton.onClick.AddListener(SelectExitNow);

        canvasObject.SetActive(false);
    }

    private void ConfigureExitReflectionModalForPlatform(bool useXR)
    {
        if (exitReflectionModalCanvas == null)
            return;

        if (useXR)
        {
            exitReflectionModalCanvas.renderMode = RenderMode.WorldSpace;
            exitReflectionModalCanvas.worldCamera = Camera.main;
            RectTransform canvasRect = exitReflectionModalCanvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(900f, 520f);
            canvasRect.localScale = Vector3.one * 0.0018f;
        }
        else
        {
            exitReflectionModalCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            exitReflectionModalCanvas.worldCamera = null;
            RectTransform canvasRect = exitReflectionModalCanvas.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            canvasRect.localScale = Vector3.one;
            canvasRect.localPosition = Vector3.zero;
            canvasRect.localRotation = Quaternion.identity;
        }
    }

    private static TMP_Text CreateModalText(Transform parent, string name, string value, float fontSize, FontStyles style)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.93f, 0.91f, 0.86f, 1f);
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateModalButton(Transform parent, string name, string label, Vector2 anchoredPosition)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(280f, 72f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.78f, 0.66f, 0.44f, 1f);
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.92f, 0.8f, 0.55f, 1f);
        colors.pressedColor = new Color(0.58f, 0.48f, 0.32f, 1f);
        button.colors = colors;

        TMP_Text text = CreateModalText(buttonObject.transform, "Label", label, 26f, FontStyles.Bold);
        text.color = new Color(0.04f, 0.035f, 0.03f, 1f);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        return button;
    }

    private void PlaceExitReflectionModal()
    {
        if (exitReflectionModalCanvas == null || exitReflectionModalCanvas.renderMode != RenderMode.WorldSpace)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Transform modalTransform = exitReflectionModalCanvas.transform;
        modalTransform.position = cam.transform.position + cam.transform.forward * 1.8f;
        modalTransform.rotation = Quaternion.LookRotation(modalTransform.position - cam.transform.position, Vector3.up);
    }

    private void HandleExitModalKeyboard()
    {
        if (!exitModalOpen || exitModalChoiceHandled || exitModalUsesXR || Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
        {
            SelectStay();
            return;
        }

        if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame || Keyboard.current[interactionKey].wasPressedThisFrame || Keyboard.current.lKey.wasPressedThisFrame)
            SelectExitNow();
    }

    private void SetDesktopModalControls(bool enabled)
    {
        if (exitModalUsesXR)
            return;

        if (!enabled)
        {
            modalDisabledDesktopControls.Clear();
            Transform root = ResolveFallbackPlayerRoot();
            if (root != null)
            {
                foreach (Behaviour behaviour in root.GetComponentsInChildren<Behaviour>(true))
                {
                    if (behaviour == null || !behaviour.enabled)
                        continue;

                    string typeName = behaviour.GetType().Name;
                    if (typeName == "FirstPersonController" || typeName == "StarterAssetsInputs" || typeName == "PlayerInput")
                    {
                        behaviour.enabled = false;
                        modalDisabledDesktopControls.Add(behaviour);
                    }
                }
            }

            if (!cursorStateCaptured)
            {
                previousCursorVisible = Cursor.visible;
                previousCursorLockMode = Cursor.lockState;
                cursorStateCaptured = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log($"[BlackKitchenExperienceController] Scene '{gameObject.scene.name}' cursor unlocked.");
            return;
        }

        foreach (Behaviour behaviour in modalDisabledDesktopControls)
        {
            if (behaviour != null && behaviour.gameObject.activeInHierarchy)
                behaviour.enabled = true;
        }
        modalDisabledDesktopControls.Clear();

        if (cursorStateCaptured)
        {
            Cursor.visible = previousCursorVisible;
            Cursor.lockState = previousCursorLockMode;
            cursorStateCaptured = false;
        }
        else
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        Debug.Log($"[BlackKitchenExperienceController] Scene '{gameObject.scene.name}' player controls restored.");
    }

    private void EnsureActiveEventSystemForModal(bool useXR)
    {
        EventSystem activeEventSystem = EventSystem.current;
        if (activeEventSystem == null || !activeEventSystem.gameObject.activeInHierarchy)
            activeEventSystem = FindFirstObjectByType<EventSystem>();

        if (activeEventSystem == null)
        {
            GameObject eventSystemObject = new GameObject(useXR ? "XRModalEventSystem" : "DesktopModalEventSystem", typeof(EventSystem));
            activeEventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        int activeEventSystemCount = FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        Debug.Log($"[BlackKitchenExperienceController] Scene '{gameObject.scene.name}' active EventSystem count for exit modal: {activeEventSystemCount}, current '{activeEventSystem.gameObject.name}'.");

        if (useXR)
            return;

        if (activeEventSystem.GetComponent<BaseInputModule>() != null)
            return;

#if ENABLE_INPUT_SYSTEM
        InputSystemUIInputModule module = activeEventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
#else
        activeEventSystem.gameObject.AddComponent<StandaloneInputModule>();
#endif
    }

    public bool IsAimingAtExit()
    {
        return IsLookingAtExit();
    }

    public bool IsExitCollider(Collider candidate)
    {
        if (candidate == null)
            return false;

        Transform root = exitInteractionRoot != null ? exitInteractionRoot : transform;
        return candidate.transform == root || candidate.transform.IsChildOf(root);
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
