using UnityEngine;

namespace BCaT.Production
{
    /// <summary>
    /// Capability facade over <see cref="BCaTPlatform"/>, the single platform
    /// authority. Retained because a large amount of production code already
    /// asks here; every member now forwards to the resolver or to the active
    /// platform profile, so there is exactly one place a platform decision is
    /// made.
    ///
    /// Deliberately contains no capability definitions for phones, tablets,
    /// WebGL, or non-Quest XR headsets: those targets are out of scope.
    /// </summary>
    public static class PlatformCapabilities
    {
        /// <summary>True when the resolved platform is flat-screen desktop.</summary>
        public static bool IsDesktop => BCaTPlatform.IsDesktop;

        public static bool IsWindows =>
            Application.platform == RuntimePlatform.WindowsPlayer ||
            Application.platform == RuntimePlatform.WindowsEditor;

        public static bool IsMacOS =>
            Application.platform == RuntimePlatform.OSXPlayer ||
            Application.platform == RuntimePlatform.OSXEditor;

        /// <summary>
        /// True when this binary is the Meta Quest (Android + OpenXR) configuration.
        /// Quest is the only supported Android target, so the Android platform
        /// implies the Quest configuration for this project.
        /// </summary>
        public static bool IsQuestConfiguration => BCaTPlatform.IsQuestPlayerBinary;

        /// <summary>
        /// True when the resolved platform is Quest/XR. Named for history: this
        /// is the resolved platform, not a raw device probe. For the raw probe
        /// use <see cref="BCaTPlatform.ProbeXRDevice"/>.
        /// </summary>
        public static bool IsXRActive => BCaTPlatform.IsQuest;

        /// <summary>
        /// Whether prompts should use Quest/XR wording. True for the whole life
        /// of the Quest player, including the first frames before XR Management
        /// reports an active device — otherwise desktop "Press E" wording leaks
        /// into headset prompts. Always use this (not <see cref="IsXRActive"/>)
        /// when choosing prompt text.
        /// </summary>
        public static bool UseXRPrompts => BCaTPlatform.UseXRPrompts;

        public static bool SupportsKeyboardMouse => BCaTPlatform.SupportsKeyboardMouse;

        public static bool SupportsQuestControllers => BCaTPlatform.IsQuest;

        /// <summary>
        /// Application.OpenURL is supported on all three targets, but on Quest the
        /// system browser replaces the immersive app, so exhibits should prefer
        /// in-app presentation there.
        /// </summary>
        public static bool SupportsExternalLinks => true;

        /// <summary>
        /// True where StreamingAssets is a directly readable file path.
        /// On Android/Quest StreamingAssets lives inside the APK and must be
        /// addressed by URL rather than File APIs.
        /// </summary>
        public static bool SupportsLocalMediaFileChecks =>
            BCaTPlatform.MediaSourcePolicy == BCaTMediaSourcePolicy.FileSystemFirst;

        public static bool SupportsLocalMediaPaths => true;

        public static bool SupportsRemoteMedia => true;

        /// <summary>Kiosk mode is a desktop-only institutional feature.</summary>
        public static bool SupportsKioskMode => BCaTPlatform.AllowsKioskMode;

        public static ApplicationMode ActiveMode => ApplicationModeService.Mode;

        /// <summary>Name of the active quality tier (Unity quality level name).</summary>
        public static string ActiveQualityTier
        {
            get
            {
                var names = QualitySettings.names;
                int level = QualitySettings.GetQualityLevel();
                return level >= 0 && level < names.Length ? names[level] : "Unknown";
            }
        }

        public static string Describe() => BCaTPlatform.Describe();
    }
}
