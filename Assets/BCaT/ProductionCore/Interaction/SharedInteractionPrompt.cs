using System;
using UnityEngine;

namespace BCaT.Production.Interaction
{
    public enum SharedInteractionVerb
    {
        Interact,
        Open,
        View,
        Watch,
        Play,
        Listen,
        Pause,
        Stop,
        Read,
        Enter,
    }

    [Serializable]
    public class SharedInteractionPromptConfig
    {
        [Tooltip("Optional explicit desktop prompt. When empty, the shared provider builds one from the verb.")]
        public string desktopPrompt;

        [Tooltip("Optional explicit Quest/XR prompt. When empty, the shared provider builds one from the verb.")]
        public string xrPrompt;

        [Tooltip("Action used when explicit prompt text is empty.")]
        public SharedInteractionVerb verb = SharedInteractionVerb.Interact;

        [Tooltip("Optional object/display name appended after the shared action.")]
        public string objectName;
    }

    public static class SharedInteractionPrompt
    {
        public static string Format(bool xr, SharedInteractionPromptConfig config)
        {
            if (config != null)
            {
                string explicitPrompt = xr ? config.xrPrompt : config.desktopPrompt;
                if (!string.IsNullOrWhiteSpace(explicitPrompt))
                    return explicitPrompt;
            }

            SharedInteractionVerb verb = config != null ? config.verb : SharedInteractionVerb.Interact;
            string objectName = config != null ? config.objectName : string.Empty;
            return Compose(xr, verb, objectName);
        }

        public static string Format(bool xr, SharedInteractionVerb verb, string objectName = "",
            string desktopOverride = "", string xrOverride = "")
        {
            if (xr && !string.IsNullOrWhiteSpace(xrOverride))
                return xrOverride;
            if (!xr && !string.IsNullOrWhiteSpace(desktopOverride))
                return desktopOverride;

            return Compose(xr, verb, objectName);
        }

        /// <summary>
        /// Desktop keeps the authored "Press E to &lt;action&gt; &lt;name&gt;" wording.
        /// Quest/XR uses "&lt;Action&gt; — &lt;Name&gt;" and never mentions a
        /// keyboard key, because Quest has no keyboard.
        /// </summary>
        static string Compose(bool xr, SharedInteractionVerb verb, string objectName)
        {
            string name = string.IsNullOrWhiteSpace(objectName) ? string.Empty : objectName.Trim();

            if (!xr)
            {
                string suffix = string.IsNullOrEmpty(name) ? string.Empty : " " + name;
                return $"Press E to {ActionText(verb)}{suffix}";
            }

            string action = XRActionText(verb);
            return string.IsNullOrEmpty(name) ? action : $"{action} — {name}";
        }

        static string ActionText(SharedInteractionVerb verb) => verb switch
        {
            SharedInteractionVerb.Open => "open",
            SharedInteractionVerb.View => "view",
            SharedInteractionVerb.Watch => "watch",
            SharedInteractionVerb.Play => "play",
            SharedInteractionVerb.Listen => "listen",
            SharedInteractionVerb.Pause => "pause",
            SharedInteractionVerb.Stop => "stop",
            SharedInteractionVerb.Read => "read",
            SharedInteractionVerb.Enter => "enter",
            _ => "interact",
        };

        /// <summary>Capitalized Quest action label. Video verbs read as "Play".</summary>
        static string XRActionText(SharedInteractionVerb verb) => verb switch
        {
            SharedInteractionVerb.Open => "Open",
            SharedInteractionVerb.View => "View",
            SharedInteractionVerb.Watch => "Play",
            SharedInteractionVerb.Play => "Play",
            SharedInteractionVerb.Listen => "Listen",
            SharedInteractionVerb.Pause => "Pause",
            SharedInteractionVerb.Stop => "Stop",
            SharedInteractionVerb.Read => "Read",
            SharedInteractionVerb.Enter => "Enter",
            _ => "Interact",
        };
    }
}
