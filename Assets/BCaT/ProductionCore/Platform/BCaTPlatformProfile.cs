using UnityEngine;

namespace BCaT.Production
{
    /// <summary>The two supported runtime platforms. There is no third.</summary>
    public enum BCaTPlatformId
    {
        /// <summary>Windows 11 x64 and Apple Silicon macOS, flat screen.</summary>
        Desktop = 0,

        /// <summary>Meta Quest (Android + OpenXR).</summary>
        Quest = 1,
    }

    /// <summary>How the active platform was decided. Logged at startup.</summary>
    public enum BCaTPlatformSource
    {
        /// <summary>Editor Platform Test Mode override (development only).</summary>
        EditorOverride,

        /// <summary>-bcatPlatform=Desktop|Quest on the command line.</summary>
        CommandLine,

        /// <summary>The build define: the Quest player is always Quest.</summary>
        BuildDefine,

        /// <summary>An XR device is initialized and running.</summary>
        XRDevice,

        /// <summary>Nothing said otherwise: desktop.</summary>
        Default,
    }

    public enum BCaTInputProviderKind
    {
        DesktopKeyboardMouse,
        QuestXRI,
    }

    public enum BCaTPromptStyle
    {
        /// <summary>Screen-space overlay text at the bottom of the view.</summary>
        ScreenSpaceOverlay,

        /// <summary>World-space canvas parented to the head camera.</summary>
        WorldSpaceHud,
    }

    public enum BCaTUiInputModuleKind
    {
        InputSystemUI,
        XRUI,
    }

    public enum BCaTMediaSourcePolicy
    {
        /// <summary>StreamingAssets is a readable file path; File.Exists decides.</summary>
        FileSystemFirst,

        /// <summary>StreamingAssets lives inside the package; the manifest decides.</summary>
        PackagedManifestFirst,
    }

    public enum BCaTLocomotionKind
    {
        DesktopCharacterController,
        XRLocomotion,
    }

    /// <summary>
    /// Per-platform configuration data. Every platform difference that is a
    /// *policy* rather than a *mechanism* lives here, so answering "what does
    /// this platform do?" is a field lookup instead of a scattered #if.
    ///
    /// Profiles are optional at runtime: <see cref="BCaTPlatform"/> falls back to
    /// an equivalent code-built profile when the asset is missing, so a lost or
    /// unloadable asset degrades to today's behavior rather than breaking the
    /// app. Assets live in a Resources folder so they load without a scene
    /// reference (the same pattern RemoteMediaConfig uses).
    /// </summary>
    [CreateAssetMenu(menuName = "BCaT/Platform Profile", fileName = "BCaTPlatformProfile")]
    public sealed class BCaTPlatformProfile : ScriptableObject
    {
        public const string ResourcesFolder = "BCaT/Platform";
        public const string DesktopResourceName = ResourcesFolder + "/BCaTPlatformProfile_Desktop";
        public const string QuestResourceName = ResourcesFolder + "/BCaTPlatformProfile_Quest";

        [Header("Identity")]
        public BCaTPlatformId platformId = BCaTPlatformId.Desktop;

        [Tooltip("Human-readable name used in logs and diagnostics.")]
        public string displayName = "Desktop";

        [Header("Rig and locomotion")]
        [Tooltip("Which ScenePlayerRig kind this platform activates.")]
        public ScenePlayerRig.RigKind rigKind = ScenePlayerRig.RigKind.Desktop;

        public BCaTLocomotionKind locomotion = BCaTLocomotionKind.DesktopCharacterController;

        [Header("Input and prompts")]
        public BCaTInputProviderKind inputProvider = BCaTInputProviderKind.DesktopKeyboardMouse;

        public BCaTPromptStyle promptStyle = BCaTPromptStyle.ScreenSpaceOverlay;

        [Tooltip("UI input module assigned to the scene's single EventSystem at runtime.")]
        public BCaTUiInputModuleKind uiInputModule = BCaTUiInputModuleKind.InputSystemUI;

        [Tooltip("Prompts use XR wording ('Play — Name') instead of keyboard wording ('Press E to play').")]
        public bool usesXRPrompts = false;

        public bool supportsKeyboardMouse = true;

        [Header("Application shell")]
        [Tooltip("Main menu, pause menu, crosshair, quit confirmation. Quest boots straight in.")]
        public bool showsAppShell = true;

        [Tooltip("Kiosk mode is a desktop institutional feature.")]
        public bool allowsKioskMode = true;

        [Header("Display and quality")]
        [Tooltip("True when XR owns the swapchain, so resolution/vsync/fullscreen settings are no-ops.")]
        public bool ownsSwapchain = false;

        [Tooltip("True when the quality tier is fixed on device and not user-editable.")]
        public bool qualityIsFixed = false;

        [Tooltip("Unity quality level applied for this platform. Empty leaves the current level.")]
        public string qualityTierName = "Desktop Standard";

        [Header("Media and content")]
        public BCaTMediaSourcePolicy mediaSourcePolicy = BCaTMediaSourcePolicy.FileSystemFirst;

        [Tooltip("Application.OpenURL leaves an immersive app on Quest, so exhibits may prefer in-app presentation.")]
        public bool prefersInAppPresentation = false;

        [Header("Diagnostics and build")]
        [Tooltip("Verbose transition diagnostics and the load-stall watchdog.")]
        public bool verboseTransitionDiagnostics = false;

        [Tooltip("Addressables profile expected for this platform's content build. Informational; asserted by the build pipeline.")]
        public string addressablesProfileName = "Default";

        /// <summary>
        /// The code-built fallback, identical to the shipped assets. Keeping
        /// this in sync with the assets is checked by BCAT-S008.
        /// </summary>
        public static BCaTPlatformProfile CreateFallback(BCaTPlatformId id)
        {
            var profile = CreateInstance<BCaTPlatformProfile>();
            profile.platformId = id;

            if (id == BCaTPlatformId.Quest)
            {
                profile.name = "BCaTPlatformProfile_Quest (fallback)";
                profile.displayName = "Meta Quest";
                profile.rigKind = ScenePlayerRig.RigKind.XR;
                profile.locomotion = BCaTLocomotionKind.XRLocomotion;
                profile.inputProvider = BCaTInputProviderKind.QuestXRI;
                profile.promptStyle = BCaTPromptStyle.WorldSpaceHud;
                profile.uiInputModule = BCaTUiInputModuleKind.XRUI;
                profile.usesXRPrompts = true;
                profile.supportsKeyboardMouse = false;
                profile.showsAppShell = false;
                profile.allowsKioskMode = false;
                profile.ownsSwapchain = true;
                profile.qualityIsFixed = true;
                profile.qualityTierName = "Quest";
                profile.mediaSourcePolicy = BCaTMediaSourcePolicy.PackagedManifestFirst;
                profile.prefersInAppPresentation = true;
                profile.verboseTransitionDiagnostics = true;
                profile.addressablesProfileName = "Default";
            }
            else
            {
                profile.name = "BCaTPlatformProfile_Desktop (fallback)";
                profile.displayName = "Desktop";
                profile.rigKind = ScenePlayerRig.RigKind.Desktop;
                profile.locomotion = BCaTLocomotionKind.DesktopCharacterController;
                profile.inputProvider = BCaTInputProviderKind.DesktopKeyboardMouse;
                profile.promptStyle = BCaTPromptStyle.ScreenSpaceOverlay;
                profile.uiInputModule = BCaTUiInputModuleKind.InputSystemUI;
                profile.usesXRPrompts = false;
                profile.supportsKeyboardMouse = true;
                profile.showsAppShell = true;
                profile.allowsKioskMode = true;
                profile.ownsSwapchain = false;
                profile.qualityIsFixed = false;
                profile.qualityTierName = "Desktop Standard";
                profile.mediaSourcePolicy = BCaTMediaSourcePolicy.FileSystemFirst;
                profile.prefersInAppPresentation = false;
                profile.verboseTransitionDiagnostics = false;
                profile.addressablesProfileName = "Default";
            }

            return profile;
        }
    }
}
