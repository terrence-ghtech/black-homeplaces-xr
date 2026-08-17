using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

/// <summary>
/// Makes the Black Kitchen exit discoverable, and nothing else.
///
/// Why the existing plaque rendered as a blank black rectangle: the exit sign is
/// a world-space canvas holding a dark background Image plus a TMP_Text that
/// <see cref="BlackKitchenExperienceController"/> writes and then force-disables
/// every frame (`exitPromptText.enabled = false`). That line implements the
/// project-wide policy in <see cref="WorldInteractionPromptVisual"/> — generic
/// floating *activation* prompts are hidden everywhere, because the shared
/// bottom-of-view prompt is the only activation surface. The background was never
/// hidden with the text, so all that survived was the empty panel.
///
/// This component does not fight that policy. The activation prompt stays exactly
/// as it is; instead the plaque gets its own permanent SIGNAGE label, which is not
/// an activation prompt and so is not policy-managed. It carries no
/// PlatformInteractionPrompt, so LegacyInteractionPromptSuppressor (which
/// identifies legacy prompts by that component's ownership) never touches it.
///
/// The sign is world-space and parented to the exit interface, so it is spatially
/// part of the exit on both platforms and is never head-locked.
///
/// On Quest it also supplies the exit's interaction affordance, reusing the
/// canonical Black Kitchen Quest model (head gaze targets, trigger confirms)
/// rather than introducing controller rays or any XRI interaction stack:
///   * gaze acquisition highlights the plaque; the "pull trigger to exit"
///     instruction is carried by the shared prompt, which the interaction manager
///     gives priority over every audio prompt while the exit is targeted
///   * acquisition and activation produce a short local haptic pulse
/// Activation itself is untouched: BlackKitchenInteractionManager already routes
/// gaze-at-exit plus trigger to BlackKitchenExperienceController.RequestExitChoice().
/// This component never activates anything.
/// </summary>
[DisallowMultipleComponent]
public sealed class BlackKitchenExitSign : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Owns IsAimingAtExit(). Resolved from the scene when empty.")]
    [SerializeField] private BlackKitchenExperienceController controller;

    [Tooltip("The plaque background whose colour signals gaze targeting. Resolved from children when empty.")]
    [SerializeField] private Image background;

    [Header("Signage")]
    [SerializeField] private string signText = "Exit Black Kitchen";
    [SerializeField] private float desktopFontSize = 26f;
    [SerializeField] private float questFontSize = 34f;


    static readonly Color IdleBackground = new Color(0.02f, 0.025f, 0.028f, 0.88f);
    static readonly Color TargetedBackground = new Color(0.30f, 0.22f, 0.09f, 0.94f);
    static readonly Color IdleLabel = new Color(0.94f, 0.92f, 0.87f, 1f);
    static readonly Color TargetedLabel = new Color(1f, 0.93f, 0.74f, 1f);

    TMP_Text signLabel;
    bool isXr;
    bool targeted;

    void Start()
    {
        isXr = BCaT.Production.PlatformCapabilities.IsXRActive;

        if (controller == null)
            controller = FindAnyObjectByType<BlackKitchenExperienceController>();

        if (background == null)
            background = GetComponentInChildren<Image>(true);

        BuildSignage();
        ApplyTargeted(false, force: true);

        Debug.Log($"[BlackKitchenExitSign] Signage ready on '{name}' " +
                  $"(text='{signText}', xr={isXr}).");
    }

    void Update()
    {
        // Desktop keeps its existing behaviour untouched: no targeting state, no
        // hint, no haptics — only the static sign.
        if (!isXr || controller == null)
            return;

        // While the exit-choice panel is open the panel is the only interface:
        // the targeting highlight stands down with the exit-target instruction.
        bool nowTargeted = !BCaT.Production.Interaction.InteractionState.IsBlocked &&
                           controller.IsAimingAtExit();
        if (nowTargeted == targeted)
            return;

        ApplyTargeted(nowTargeted, force: false);

        if (nowTargeted)
            BlackKitchenQuestExitHaptics.PulseAcquired();
    }

    void OnDisable() => ApplyTargeted(false, force: true);

    // ---- signage -----------------------------------------------------------

    /// <summary>
    /// Adds the signage label (and, on Quest, the activation hint) as new children
    /// of the plaque. The pre-existing activation TMP_Text is left completely
    /// alone — it stays policy-managed and hidden.
    /// </summary>
    void BuildSignage()
    {
        Transform host = background != null ? background.transform : transform;

        signLabel = FindOrCreate(host, "ExitSignLabel");
        signLabel.text = signText;
        signLabel.fontSize = isXr ? questFontSize : desktopFontSize;
        signLabel.fontStyle = FontStyles.Bold;
        signLabel.alignment = TextAlignmentOptions.Center;
        signLabel.color = IdleLabel;
        signLabel.raycastTarget = false;
        StretchOver(signLabel.rectTransform);

    }

    static TMP_Text FindOrCreate(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            TMP_Text found = existing.GetComponent<TMP_Text>();
            if (found != null)
                return found;
        }

        var go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        return go.GetComponent<TMP_Text>();
    }

    static void StretchOver(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    void ApplyTargeted(bool value, bool force)
    {
        if (!force && targeted == value)
            return;

        targeted = value;

        if (background != null)
            background.color = value ? TargetedBackground : IdleBackground;

        if (signLabel != null)
            signLabel.color = value ? TargetedLabel : IdleLabel;
    }
}

/// <summary>
/// The smallest possible haptic response for the Black Kitchen Quest exit, kept
/// deliberately local to this affordance: no global haptic service, no XRI
/// interactor or HapticImpulsePlayer, and nothing shared with Main House.
///
/// Amplitudes and durations match what the Main House rig's SimpleHapticFeedback
/// produces (hover 0.25 for 0.1 s, select 0.5 for 0.1 s), so the exit feels like
/// the rest of the experience. No-ops on desktop and on any device without
/// impulse support.
/// </summary>
public static class BlackKitchenQuestExitHaptics
{
    const float AcquiredAmplitude = 0.25f;
    const float ActivatedAmplitude = 0.5f;
    const float Duration = 0.1f;

    static readonly List<InputDevice> Devices = new List<InputDevice>();

    public static void PulseAcquired() => Pulse(AcquiredAmplitude);

    public static void PulseActivated() => Pulse(ActivatedAmplitude);

    static void Pulse(float amplitude)
    {
        if (!BCaT.Production.PlatformCapabilities.IsXRActive)
            return;

        Devices.Clear();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand,
            Devices);

        foreach (InputDevice device in Devices)
        {
            if (!device.isValid)
                continue;

            if (device.TryGetHapticCapabilities(out HapticCapabilities capabilities) &&
                capabilities.supportsImpulse)
                device.SendHapticImpulse(0u, amplitude, Duration);
        }
    }
}
