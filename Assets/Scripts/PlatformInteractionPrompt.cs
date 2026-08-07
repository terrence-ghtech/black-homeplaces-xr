using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Single source of truth for the interaction verb shown on exhibit canvases.
/// WebGL/desktop builds show "Press E"; Quest/XR builds show "Interact".
/// </summary>
public static class InteractionPromptText
{
    public const string DesktopVerb = "Press E";
    public const string XRVerb = "Interact";

    public static string Verb => IsXRActive() ? XRVerb : DesktopVerb;

    /// <summary>
    /// True when this binary is the Meta Quest (Android) player. Quest is the
    /// only supported Android target, so Android implies the Quest runtime and
    /// therefore implies XR wording — regardless of whether XR Management has
    /// finished initializing this frame.
    /// </summary>
    public static bool IsQuestRuntime =>
#if UNITY_ANDROID && !UNITY_EDITOR
        true;
#else
        false;
#endif

    public static bool IsXRActive()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return false;
#elif UNITY_EDITOR
        // In the Editor, XR Management can keep an active loader initialized
        // even while the desktop rig is the intended runtime path. Treat only
        // an actually active XR device as XR so normal macOS/Windows Play Mode
        // keeps desktop prompts and keyboard/mouse interaction.
        return XRSettings.isDeviceActive;
#elif UNITY_ANDROID
        // Quest player: always XR. XRSettings/loader state is false for the
        // first frames after load, which used to leak desktop "Press E"
        // wording into headset prompts.
        return true;
#else
        if (XRSettings.isDeviceActive)
            return true;

        var settings = UnityEngine.XR.Management.XRGeneralSettings.Instance;
        return settings != null
            && settings.Manager != null
            && settings.Manager.isInitializationComplete
            && settings.Manager.activeLoader != null;
#endif
    }
}

/// <summary>
/// Visibility policy for legacy object-attached ("floating") interaction
/// prompts.
///
/// Policy: generic floating activation prompts stay hidden on every platform —
/// the shared bottom-of-view prompt (<see cref="BCaT.Production.Shell.InteractionPromptUi"/>)
/// is the only activation prompt surface. There are exactly two sanctioned
/// world-space exceptions, and they are restored on Quest only:
///   1. Front Home Privacy Zones hologram (PrivacyLawExhibitController)
///   2. Black Kitchen entrance prompt (BlackKitchenPortalController)
///
/// Desktop behavior is deliberately unchanged: sanctioned prompts resolve to
/// the same hidden state desktop already ships.
/// </summary>
public static class WorldInteractionPromptVisual
{
    /// <summary>Generic (non-sanctioned) floating prompts are never shown.</summary>
    public static bool ShouldShow => false;

    /// <summary>
    /// Sanctioned world-space prompts are restored on the Quest runtime only.
    /// </summary>
    public static bool SanctionedPromptsVisible =>
        InteractionPromptText.IsQuestRuntime || InteractionPromptText.IsXRActive();

    // Explicit identity registry: the prompt suppressor consults this instead
    // of guessing from GameObject names, so a sanctioned prompt can never be
    // hidden by the generic sweep.
    static readonly HashSet<TMP_Text> sanctionedTexts = new HashSet<TMP_Text>();
    static readonly HashSet<GameObject> sanctionedRoots = new HashSet<GameObject>();

    public static void RegisterSanctioned(GameObject root, TMP_Text text)
    {
        if (root != null)
            sanctionedRoots.Add(root);
        if (text != null)
            sanctionedTexts.Add(text);
    }

    public static bool IsSanctioned(TMP_Text text) =>
        text != null && sanctionedTexts.Contains(text);

    public static bool IsSanctioned(GameObject root) =>
        root != null && sanctionedRoots.Contains(root);

    public static void SetRootVisible(GameObject root, bool visible)
    {
        if (root != null)
            root.SetActive(false);
    }

    public static void SetText(TMP_Text text, string value)
    {
        if (text == null)
            return;

        text.text = value;
        text.enabled = false;
    }

    /// <summary>
    /// Show/hide one of the two sanctioned world-space prompt roots. Honors
    /// <paramref name="visible"/> on Quest; stays hidden on desktop.
    /// </summary>
    public static void SetSanctionedRootVisible(GameObject root, bool visible)
    {
        if (root == null)
            return;

        RegisterSanctioned(root, null);
        root.SetActive(visible && SanctionedPromptsVisible);
    }

    /// <summary>
    /// Write and show/hide the text of a sanctioned world-space prompt. The
    /// text value is always applied so authored content stays correct; only
    /// visibility is platform-gated.
    /// </summary>
    public static void SetSanctionedText(TMP_Text text, string value, bool visible = true)
    {
        if (text == null)
            return;

        RegisterSanctioned(null, text);
        text.text = value;
        text.enabled = visible && SanctionedPromptsVisible;
    }
}

/// <summary>
/// Attach to a prompt TMP text (or point <see cref="targetText"/> at one).
/// Writes "<verb><textAfterVerb>" into the text field, re-checking while XR
/// initializes so the verb is correct on Quest without per-canvas logic.
/// </summary>
public class PlatformInteractionPrompt : MonoBehaviour
{
    public enum PromptMode { Auto, Desktop, XR }

    [Tooltip("Auto detects at runtime. Desktop/XR force a verb for testing in the Editor.")]
    [SerializeField] private PromptMode editorOverride = PromptMode.Auto;

    [Tooltip("Defaults to the TMP_Text on this GameObject.")]
    [SerializeField] private TMP_Text targetText;

    [Tooltip("Appended after the verb, e.g. \" to open project.\"")]
    [TextArea]
    [SerializeField] private string textAfterVerb = "";

    [Tooltip("Optional full TMP text used on desktop/WebGL. When set alongside XR text, these replace verb+suffx output.")]
    [TextArea]
    [SerializeField] private string fullDesktopText = "";

    [Tooltip("Optional full TMP text used in XR. When set alongside desktop text, these replace verb+suffix output.")]
    [TextArea]
    [SerializeField] private string fullXRText = "";

    private string lastApplied;

    private void OnEnable()
    {
        Apply();
        StartCoroutine(ReapplyWhileXRInitializes());
    }

    private IEnumerator ReapplyWhileXRInitializes()
    {
        // XR management can finish initializing after scene load; poll briefly.
        for (float elapsed = 0f; elapsed < 10f; elapsed += 1f)
        {
            yield return new WaitForSeconds(1f);
            Apply();
        }
    }

    /// <summary>
    /// The text this component writes into. Used by the prompt suppressor to
    /// identify legacy activation prompts by component ownership instead of by
    /// guessing from GameObject names.
    /// </summary>
    public TMP_Text ResolveTargetText()
    {
        if (targetText == null)
            targetText = GetComponent<TMP_Text>();
        return targetText;
    }

    public void Apply()
    {
        if (targetText == null)
            targetText = GetComponent<TMP_Text>();

        if (targetText == null)
            return;

        string text = ResolveText();
        if (text == lastApplied)
            return;

        lastApplied = text;
        targetText.text = text;
        targetText.enabled = WorldInteractionPromptVisual.ShouldShow;
    }

    private string ResolveVerb()
    {
#if UNITY_EDITOR
        if (editorOverride == PromptMode.Desktop)
            return InteractionPromptText.DesktopVerb;
        if (editorOverride == PromptMode.XR)
            return InteractionPromptText.XRVerb;
#endif
        return InteractionPromptText.Verb;
    }

    private string ResolveText()
    {
        if (!string.IsNullOrEmpty(fullDesktopText) && !string.IsNullOrEmpty(fullXRText))
            return IsXRMode() ? fullXRText : fullDesktopText;

        return ResolveVerb() + textAfterVerb;
    }

    private bool IsXRMode()
    {
#if UNITY_EDITOR
        if (editorOverride == PromptMode.Desktop)
            return false;
        if (editorOverride == PromptMode.XR)
            return true;
#endif
        return InteractionPromptText.IsXRActive();
    }
}
