using UnityEngine;

namespace BCaT.Production
{
    /// <summary>
    /// Central runtime platform and capability service for the three supported
    /// production targets: Windows 11 x64 desktop, Apple Silicon macOS desktop,
    /// and Meta Quest. All platform decisions in production code should go
    /// through this class (or the existing InteractionPromptText.IsXRActive()
    /// helper, which this class wraps) instead of scattered platform checks.
    ///
    /// Deliberately contains no capability definitions for phones, tablets,
    /// WebGL, or non-Quest XR headsets: those targets are out of scope.
    /// </summary>
    public static class PlatformCapabilities
    {
        /// <summary>True on Windows/macOS player and in the editor.</summary>
        public static bool IsDesktop =>
#if UNITY_STANDALONE || UNITY_EDITOR
            !IsXRActive;
#else
            false;
#endif

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
        public static bool IsQuestConfiguration =>
#if UNITY_ANDROID && !UNITY_EDITOR
            true;
#else
            false;
#endif

        /// <summary>True when an XR device is actually initialized and running.</summary>
        public static bool IsXRActive => InteractionPromptText.IsXRActive();

        public static bool SupportsKeyboardMouse => !IsQuestConfiguration;

        public static bool SupportsQuestControllers => IsQuestConfiguration;

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
        public static bool SupportsLocalMediaFileChecks => !IsQuestConfiguration;

        public static bool SupportsLocalMediaPaths => true;

        public static bool SupportsRemoteMedia => true;

        /// <summary>Kiosk mode is a desktop-only institutional feature.</summary>
        public static bool SupportsKioskMode => IsDesktop;

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

        public static string Describe() =>
            $"platform={Application.platform}, desktop={IsDesktop}, quest={IsQuestConfiguration}, " +
            $"xrActive={IsXRActive}, mode={ActiveMode}, quality={ActiveQualityTier}";
    }
}
