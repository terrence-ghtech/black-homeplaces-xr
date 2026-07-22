using System.Collections;
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

    public static bool IsXRActive()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return false;
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
