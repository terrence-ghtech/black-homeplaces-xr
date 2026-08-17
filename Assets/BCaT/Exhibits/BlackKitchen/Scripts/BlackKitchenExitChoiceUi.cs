using System.Collections.Generic;
using BCaT.Production.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// The three decisions the Black Kitchen exit flow can produce. Implemented by
/// <see cref="BlackKitchenExperienceController"/>; the platform UIs below know
/// nothing except how to call these.
/// </summary>
public interface IBlackKitchenExitChoiceHandler
{
    /// <summary>Start the existing exit reflection audio and stay in the kitchen.</summary>
    void ChooseListen();

    /// <summary>Leave for the Main House now, without waiting for audio.</summary>
    void ChooseLeaveNow();

    /// <summary>Dismiss the choice and remain in the Black Kitchen.</summary>
    void ChooseCancel();
}

/// <summary>
/// A platform's exit-choice presentation. Desktop and Quest each implement this
/// independently so neither platform's working input model constrains the other.
/// </summary>
public interface IBlackKitchenExitChoiceUi
{
    bool IsVisible { get; }

    /// <param name="handler">Receives the decision.</param>
    /// <param name="offerListen">
    /// False when the reflection audio is already playing, in which case the
    /// Listen option is meaningless and is omitted.
    /// </param>
    void Show(IBlackKitchenExitChoiceHandler handler, bool offerListen);

    void Hide();

    /// <summary>Driven from the controller's Update while visible.</summary>
    void Tick();

    void Dispose();
}

/// <summary>Shared label text, so both platforms word the same decision identically.</summary>
public static class BlackKitchenExitChoiceCopy
{
    public const string Title = "Leaving the Black Kitchen";
    public const string Listen = "Listen to Reflection";
    public const string LeaveNow = "Leave Now";
    public const string Stay = "Stay";

    public const string BodyOfferListen =
        "You can listen to the Exit Reflection before you go, leave now, or stay a while longer.\n\n" +
        "Listening keeps you in the kitchen — you can leave at any time while it plays.";

    public const string BodyAlreadyPlaying =
        "The Exit Reflection is playing. You can leave whenever you are ready, " +
        "or stay a while longer.";

    // Desktop wording is authored separately from the Quest wording above so the
    // validated Quest panel keeps its own copy untouched.
    public const string DesktopTitle = "Before You Leave";
    public const string DesktopBody = "Would you like to listen to the closing reflection?";
    public const string DesktopListen = "Listen to Reflection";
    public const string DesktopLeave = "Leave Black Kitchen";
    public const string DesktopStay = "Stay in Black Kitchen";
}

// ---------------------------------------------------------------------------
// Desktop
// ---------------------------------------------------------------------------

/// <summary>
/// Desktop presentation: the existing screen-space overlay architecture, with a
/// third choice added. Mouse buttons plus keyboard shortcuts read through the
/// sanctioned <see cref="FocusedUiInput"/> helper, exactly as before.
/// </summary>
public sealed class BlackKitchenExitChoiceDesktopUi : IBlackKitchenExitChoiceUi
{
    readonly Transform parent;

    Canvas canvas;
    CanvasGroup group;
    TMP_Text body;
    GameObject listenButtonObject;
    IBlackKitchenExitChoiceHandler handler;
    bool choiceTaken;

    // The keypress that opens this panel is the same key the panel uses for
    // "Leave". Without arming, Show() and the first Tick() happen while
    // eKey.wasPressedThisFrame is still true, so the panel opened and instantly
    // chose Leave Now — which is exactly the "press E and teleport straight out"
    // behaviour. Keyboard shortcuts stay inert until the key has been released
    // once; mouse clicks on the buttons are unaffected.
    bool keyboardArmed;

    public BlackKitchenExitChoiceDesktopUi(Transform parent) => this.parent = parent;

    public bool IsVisible => canvas != null && canvas.gameObject.activeSelf;

    public void Show(IBlackKitchenExitChoiceHandler choiceHandler, bool offerListen)
    {
        handler = choiceHandler;
        choiceTaken = false;
        keyboardArmed = false;
        Build();
        if (canvas == null)
            return;

        body.text = offerListen
            ? BlackKitchenExitChoiceCopy.DesktopBody + "\n\nKeys: L Listen · Enter Leave · Esc Stay"
            : "The closing reflection is already playing.\n\nKeys: Enter Leave · Esc Stay";

        if (listenButtonObject != null)
            listenButtonObject.SetActive(offerListen);

        EnsureEventSystem();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        var rect = canvas.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localPosition = Vector3.zero;
        rect.localRotation = Quaternion.identity;

        canvas.gameObject.SetActive(true);
        canvas.enabled = true;
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    public void Hide()
    {
        if (group != null)
        {
            group.interactable = false;
            group.blocksRaycasts = false;
        }
        if (canvas != null)
            canvas.gameObject.SetActive(false);
    }

    public void Tick()
    {
        if (!IsVisible || choiceTaken || handler == null)
            return;

        // Wait for the activation key to be released before accepting shortcuts.
        if (!keyboardArmed)
        {
            if (!FocusedUiInput.InteractHeld)
                keyboardArmed = true;
            return;
        }

        if (FocusedUiInput.CancelPressed || FocusedUiInput.KeyPressed(Key.S))
        {
            Take(handler.ChooseCancel);
            return;
        }

        if (listenButtonObject != null && listenButtonObject.activeSelf &&
            FocusedUiInput.KeyPressed(Key.L))
        {
            Take(handler.ChooseListen);
            return;
        }

        if (FocusedUiInput.SubmitPressed || FocusedUiInput.InteractPressed)
            Take(handler.ChooseLeaveNow);
    }

    public void Dispose()
    {
        if (canvas != null)
            Object.Destroy(canvas.gameObject);
        canvas = null;
        group = null;
    }

    void Take(System.Action choice)
    {
        choiceTaken = true;
        choice();
    }

    void Build()
    {
        if (canvas != null)
            return;

        var canvasObject = new GameObject("BlackKitchenExitChoice_Desktop",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasObject.transform.SetParent(parent, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.sortingOrder = 32000;
        canvasObject.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 1f;
        group = canvasObject.GetComponent<CanvasGroup>();

        GameObject panel = ExitChoiceUiBuilder.Panel(canvasObject.transform);

        ExitChoiceUiBuilder.Title(panel.transform, BlackKitchenExitChoiceCopy.DesktopTitle, 42f, -54f);
        body = ExitChoiceUiBuilder.Body(panel.transform, string.Empty, 26f, 34f, 210f);

        listenButtonObject = ExitChoiceUiBuilder.Button(
            panel.transform, "ListenButton", BlackKitchenExitChoiceCopy.DesktopListen,
            new Vector2(-300f, -186f), () => Take(handler.ChooseListen)).gameObject;

        ExitChoiceUiBuilder.Button(
            panel.transform, "LeaveNowButton", BlackKitchenExitChoiceCopy.DesktopLeave,
            new Vector2(0f, -186f), () => Take(handler.ChooseLeaveNow));

        ExitChoiceUiBuilder.Button(
            panel.transform, "StayButton", BlackKitchenExitChoiceCopy.DesktopStay,
            new Vector2(300f, -186f), () => Take(handler.ChooseCancel));

        var canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(980f, 560f);
        canvasObject.SetActive(false);
    }

    /// <summary>
    /// Unchanged from the previous desktop modal: guarantee an EventSystem with
    /// an input module so the buttons receive clicks.
    /// </summary>
    static void EnsureEventSystem()
    {
        EventSystem active = EventSystem.current;
        if (active == null || !active.gameObject.activeInHierarchy)
            active = Object.FindFirstObjectByType<EventSystem>();

        if (active == null)
            active = new GameObject("DesktopModalEventSystem", typeof(EventSystem))
                .GetComponent<EventSystem>();

        if (active.GetComponent<BaseInputModule>() != null)
            return;

        var module = active.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        module.AssignDefaultActions();
    }
}

// ---------------------------------------------------------------------------
// Quest
// ---------------------------------------------------------------------------

/// <summary>
/// Quest presentation: a view-placed, world-stable panel driven directly by
/// controller input, not head gaze.
///
/// Flow: gaze at the exit sign -> trigger opens this panel -> move either
/// thumbstick left/right between Listen, Leave Now, and Stay -> trigger confirms.
///
/// The panel is placed from the head pose once when opened and remains
/// world-stable after that. There is no cursor, no controller ray, and head
/// movement never changes the selected option.
/// </summary>
public sealed class BlackKitchenExitChoiceQuestUi : IBlackKitchenExitChoiceUi
{
    const float PlaceDistance = 1.6f;
    const float PanelHeightOffset = -0.12f;
    const float AxisThreshold = 0.55f;
    const float AxisResetThreshold = 0.25f;

    readonly Transform parent;

    Canvas canvas;
    CanvasGroup group;
    TMP_Text body;
    GameObject listenButtonObject;
    Button listenButton;
    Button leaveButton;
    Button stayButton;
    IBlackKitchenExitChoiceHandler handler;
    bool choiceTaken;

    InputAction navigateAction;
    InputAction confirmAction;
    bool navigationLatched;
    bool confirmArmed;
    bool offerListen;
    Option selectedOption = Option.Listen;

    static readonly Color QuestIdleColor = new Color(0.52f, 0.43f, 0.29f, 1f);
    static readonly Color QuestSelectedColor = new Color(1f, 0.91f, 0.58f, 1f);
    static readonly Color QuestUnavailableColor = new Color(0.2f, 0.19f, 0.17f, 0.82f);
    static readonly Vector3 QuestIdleScale = Vector3.one;
    static readonly Vector3 QuestSelectedScale = Vector3.one * 1.08f;

    enum Option
    {
        Listen,
        LeaveNow,
        Stay
    }

    public BlackKitchenExitChoiceQuestUi(Transform parent) => this.parent = parent;

    public bool IsVisible => canvas != null && canvas.gameObject.activeSelf;

    public void Show(IBlackKitchenExitChoiceHandler choiceHandler, bool offerListen)
    {
        handler = choiceHandler;
        choiceTaken = false;
        this.offerListen = offerListen;
        selectedOption = this.offerListen ? Option.Listen : Option.LeaveNow;
        navigationLatched = false;
        confirmArmed = false;
        Build();
        if (canvas == null)
            return;

        body.text = offerListen
            ? BlackKitchenExitChoiceCopy.BodyOfferListen
            : BlackKitchenExitChoiceCopy.BodyAlreadyPlaying;

        if (listenButtonObject != null)
            listenButtonObject.SetActive(true);

        canvas.worldCamera = Camera.main;

        canvas.gameObject.SetActive(true);
        canvas.enabled = true;
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;

        Place();
        EnsureInputActions();
        SetInputActionsEnabled(true);
        ApplySelectionVisuals();

        Debug.Log("[BlackKitchenExitChoiceQuestUi] Shown; thumbstick selects and trigger confirms " +
                  $"(offerListen={offerListen}, initial={selectedOption}).");
    }

    public void Hide()
    {
        SetInputActionsEnabled(false);

        if (group != null)
        {
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        if (canvas != null)
            canvas.gameObject.SetActive(false);
    }

    public void Tick()
    {
        if (!IsVisible || choiceTaken)
            return;

        EnsureInputActions();

        Vector2 axis = navigateAction != null ? navigateAction.ReadValue<Vector2>() : Vector2.zero;
        if (!navigationLatched && Mathf.Abs(axis.x) >= AxisThreshold)
        {
            MoveSelection(axis.x > 0f ? 1 : -1);
            navigationLatched = true;
        }
        else if (navigationLatched && Mathf.Abs(axis.x) <= AxisResetThreshold)
        {
            navigationLatched = false;
        }

        if (confirmAction == null)
            return;

        if (!confirmArmed)
        {
            if (!confirmAction.IsPressed())
                confirmArmed = true;
            return;
        }

        if (confirmAction.WasPressedThisFrame())
            ConfirmSelection();
    }

    public void Dispose()
    {
        DisposeInputActions();
        if (canvas != null)
            Object.Destroy(canvas.gameObject);
        canvas = null;
        group = null;
    }

    void MoveSelection(int direction)
    {
        Option previous = selectedOption;
        selectedOption = NextOption(selectedOption, direction);
        if (selectedOption == previous)
            return;

        ApplySelectionVisuals();
        BlackKitchenQuestExitHaptics.PulseAcquired();
        Debug.Log($"[BlackKitchenExitChoiceQuestUi] Selection -> {selectedOption}.");
    }

    Option NextOption(Option current, int direction)
    {
        var options = offerListen
            ? new[] { Option.Listen, Option.LeaveNow, Option.Stay }
            : new[] { Option.LeaveNow, Option.Stay };

        int index = System.Array.IndexOf(options, current);
        if (index < 0)
            index = 0;

        index = (index + direction + options.Length) % options.Length;
        return options[index];
    }

    void ConfirmSelection()
    {
        switch (selectedOption)
        {
            case Option.Listen:
                if (offerListen)
                    Take(handler.ChooseListen);
                else
                    Take(handler.ChooseLeaveNow);
                break;
            case Option.LeaveNow:
                Take(handler.ChooseLeaveNow);
                break;
            case Option.Stay:
                Take(handler.ChooseCancel);
                break;
        }
    }

    void ApplySelectionVisuals()
    {
        ApplyButtonVisual(listenButton, Option.Listen, offerListen);
        ApplyButtonVisual(leaveButton, Option.LeaveNow, true);
        ApplyButtonVisual(stayButton, Option.Stay, true);
    }

    void ApplyButtonVisual(Button button, Option option, bool available)
    {
        if (button == null)
            return;

        bool selected = available && selectedOption == option;
        var image = button.GetComponent<Image>();
        if (image != null)
            image.color = !available ? QuestUnavailableColor : selected ? QuestSelectedColor : QuestIdleColor;

        button.interactable = available;
        button.transform.localScale = selected ? QuestSelectedScale : QuestIdleScale;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.color = selected ? new Color(0.025f, 0.022f, 0.018f, 1f) : new Color(0.95f, 0.91f, 0.82f, 1f);
    }

    void Take(System.Action choice)
    {
        if (choiceTaken)
            return;

        choiceTaken = true;
        BlackKitchenQuestExitHaptics.PulseActivated();
        choice();
    }

    // ---- placement -------------------------------------------------------

    void Place()
    {
        Camera cam = Camera.main;
        if (cam == null || canvas == null)
            return;

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-4f)
            forward = Vector3.forward;
        forward.Normalize();

        canvas.transform.SetPositionAndRotation(
            cam.transform.position + forward * PlaceDistance + Vector3.up * PanelHeightOffset,
            Quaternion.LookRotation(forward, Vector3.up));
    }

    // ---- controller input, alive only while the panel is open -------------

    void EnsureInputActions()
    {
        if (navigateAction == null)
        {
            navigateAction = new InputAction("BlackKitchenExitMenuNavigate", InputActionType.Value);
            navigateAction.AddBinding("<XRController>{LeftHand}/thumbstick");
            navigateAction.AddBinding("<XRController>{RightHand}/thumbstick");
            navigateAction.AddBinding("<XRController>{LeftHand}/primary2DAxis");
            navigateAction.AddBinding("<XRController>{RightHand}/primary2DAxis");
        }

        if (confirmAction == null)
        {
            confirmAction = new InputAction("BlackKitchenExitMenuConfirm", InputActionType.Button);
            confirmAction.AddBinding("<XRController>{LeftHand}/{TriggerButton}");
            confirmAction.AddBinding("<XRController>{RightHand}/{TriggerButton}");
        }
    }

    void SetInputActionsEnabled(bool enabled)
    {
        EnsureInputActions();

        if (enabled)
        {
            if (!navigateAction.enabled)
                navigateAction.Enable();
            if (!confirmAction.enabled)
                confirmAction.Enable();
        }
        else
        {
            if (navigateAction != null && navigateAction.enabled)
                navigateAction.Disable();
            if (confirmAction != null && confirmAction.enabled)
                confirmAction.Disable();
        }
    }

    void DisposeInputActions()
    {
        if (navigateAction != null)
        {
            navigateAction.Disable();
            navigateAction.Dispose();
            navigateAction = null;
        }

        if (confirmAction != null)
        {
            confirmAction.Disable();
            confirmAction.Dispose();
            confirmAction = null;
        }
    }

    // ---- construction ----------------------------------------------------

    void Build()
    {
        if (canvas != null)
            return;

        var canvasObject = new GameObject("BlackKitchenExitChoice_Quest",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup),
            typeof(TrackedDeviceGraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 32000;
        group = canvasObject.GetComponent<CanvasGroup>();

        var canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1200f, 620f);
        canvasRect.localScale = Vector3.one * 0.0011f;

        GameObject panel = ExitChoiceUiBuilder.Panel(canvasObject.transform);

        ExitChoiceUiBuilder.Title(panel.transform, BlackKitchenExitChoiceCopy.Title, 52f, -46f);
        body = ExitChoiceUiBuilder.Body(panel.transform, string.Empty, 32f, 46f, 240f);

        // Target size matters for headset readability even though selection is
        // controller-driven: 360x150 gives each option a strong visual state at
        // the 1.6 m placement distance and 0.0011 canvas scale.
        Vector2 optionSize = new Vector2(360f, 150f);
        const float optionY = -196f;

        listenButton = ExitChoiceUiBuilder.Button(
            panel.transform, "ListenButton", BlackKitchenExitChoiceCopy.Listen,
            new Vector2(-390f, optionY), () => Take(handler.ChooseListen),
            optionSize, 30f);
        listenButtonObject = listenButton.gameObject;

        leaveButton = ExitChoiceUiBuilder.Button(
            panel.transform, "LeaveNowButton", BlackKitchenExitChoiceCopy.LeaveNow,
            new Vector2(0f, optionY), () => Take(handler.ChooseLeaveNow),
            optionSize, 30f);

        stayButton = ExitChoiceUiBuilder.Button(
            panel.transform, "StayButton", BlackKitchenExitChoiceCopy.Stay,
            new Vector2(390f, optionY), () => Take(handler.ChooseCancel),
            optionSize, 30f);

        canvasObject.SetActive(false);
    }
}

// ---------------------------------------------------------------------------

/// <summary>Small shared builders so both adapters produce a consistent panel.</summary>
static class ExitChoiceUiBuilder
{
    public static GameObject Panel(Transform parent)
    {
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = panel.GetComponent<Image>();
        image.color = new Color(0.025f, 0.028f, 0.03f, 0.96f);
        // Decoration only. A canvas-sized raycast target competes with the
        // buttons in every hit test and has no purpose of its own.
        image.raycastTarget = false;
        return panel;
    }

    public static TMP_Text Title(Transform parent, string value, float fontSize, float y)
    {
        TMP_Text title = Text(parent, "Title", value, fontSize, FontStyles.Bold);
        var rect = title.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(-96f, 80f);
        return title;
    }

    public static TMP_Text Body(Transform parent, string value, float fontSize, float y, float height)
    {
        TMP_Text body = Text(parent, "Body", value, fontSize, FontStyles.Normal);
        var rect = body.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(-140f, height);
        return body;
    }

    public static Button Button(Transform parent, string name, string label,
        Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick,
        Vector2? size = null, float fontSize = 26f)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size ?? new Vector2(280f, 76f);

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.78f, 0.66f, 0.44f, 1f);
        image.raycastTarget = true;

        var button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        // Deliberately high-contrast: with controller pointing this colour change
        // is the hover affordance, since no line renderer is drawn.
        colors.highlightedColor = new Color(1f, 0.90f, 0.62f, 1f);
        colors.selectedColor = new Color(1f, 0.90f, 0.62f, 1f);
        colors.pressedColor = new Color(0.52f, 0.42f, 0.26f, 1f);
        colors.fadeDuration = 0.05f;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        TMP_Text text = Text(buttonObject.transform, "Label", label, fontSize, FontStyles.Bold);
        text.color = new Color(0.04f, 0.035f, 0.03f, 1f);
        var textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        return button;
    }

    public static TMP_Text Text(Transform parent, string name, string value,
        float fontSize, FontStyles style)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
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
}
