using System;
using System.IO;
using UnityEngine;

namespace BCaT.Production
{
    /// <summary>Institutional application modes for the desktop edition.</summary>
    public enum ApplicationMode
    {
        /// <summary>Researchers, students, staff-guided use. Full menus and quit.</summary>
        Standard,

        /// <summary>Galleries, classrooms, museums. Fullscreen, restricted, self-resetting.</summary>
        Kiosk,
    }

    /// <summary>
    /// Administrator-editable kiosk configuration. Persisted as JSON at
    /// [persistentDataPath]/BCaT/kiosk.config.json so institutions can adjust
    /// it without rebuilding. Command-line arguments override the file.
    /// </summary>
    [Serializable]
    public class KioskConfiguration
    {
        public int schemaVersion = 1;

        [Tooltip("Seconds without visitor activity before the kiosk resets. 0 disables.")]
        public float inactivityTimeoutSeconds = 300f;

        [Tooltip("When false, actively playing narration/video defers the inactivity reset.")]
        public bool allowResetDuringMedia = false;

        [Tooltip("Quality tier name locked in kiosk mode (Desktop Low/Desktop Standard/Desktop High).")]
        public string fixedQualityTier = "Desktop Standard";

        [Tooltip("Enable the hidden administrator exit chord (Ctrl+Shift+Q held).")]
        public bool allowAdminExit = true;

        [Tooltip("Enable the hidden administrator settings chord (Ctrl+Shift+F10).")]
        public bool allowAdminSettings = true;

        [Tooltip("Seconds the admin exit chord must be held.")]
        public float adminChordHoldSeconds = 2f;
    }

    /// <summary>
    /// Resolves the active ApplicationMode once at startup, from (highest priority first):
    /// 1. Command line: -kiosk / -standard / -bcatMode=Kiosk
    /// 2. Config file: [persistentDataPath]/BCaT/mode.config.json { "mode": "Kiosk" }
    /// 3. Default: Standard.
    /// Kiosk mode is only honored on desktop platforms.
    /// </summary>
    public static class ApplicationModeService
    {
        [Serializable]
        class ModeFile { public string mode = "Standard"; }

        static ApplicationMode? resolved;
        static KioskConfiguration kioskConfig;

        public static string ConfigDirectory =>
            Path.Combine(Application.persistentDataPath, "BCaT");

        public static string ModeFilePath => Path.Combine(ConfigDirectory, "mode.config.json");
        public static string KioskConfigPath => Path.Combine(ConfigDirectory, "kiosk.config.json");

        public static ApplicationMode Mode
        {
            get
            {
                if (!resolved.HasValue)
                    resolved = Resolve();
                return resolved.Value;
            }
        }

        public static bool IsKiosk => Mode == ApplicationMode.Kiosk;

        public static KioskConfiguration Kiosk
        {
            get
            {
                if (kioskConfig == null)
                    kioskConfig = LoadKioskConfig();
                return kioskConfig;
            }
        }

        static ApplicationMode Resolve()
        {
            ApplicationMode mode = ApplicationMode.Standard;

            try
            {
                if (File.Exists(ModeFilePath))
                {
                    var file = JsonUtility.FromJson<ModeFile>(File.ReadAllText(ModeFilePath));
                    if (file != null &&
                        string.Equals(file.mode, "Kiosk", StringComparison.OrdinalIgnoreCase))
                        mode = ApplicationMode.Kiosk;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ApplicationMode] Could not read {ModeFilePath}: {e.Message}. Using Standard.");
            }

            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (string.Equals(arg, "-kiosk", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "-bcatMode=Kiosk", StringComparison.OrdinalIgnoreCase))
                    mode = ApplicationMode.Kiosk;
                else if (string.Equals(arg, "-standard", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(arg, "-bcatMode=Standard", StringComparison.OrdinalIgnoreCase))
                    mode = ApplicationMode.Standard;
            }

            if (mode == ApplicationMode.Kiosk && !Application.isEditor &&
                Application.platform != RuntimePlatform.WindowsPlayer &&
                Application.platform != RuntimePlatform.OSXPlayer)
            {
                Debug.LogWarning("[ApplicationMode] Kiosk mode requested on a non-desktop platform; using Standard.");
                mode = ApplicationMode.Standard;
            }

            Debug.Log($"[ApplicationMode] Active mode: {mode}");
            return mode;
        }

        static KioskConfiguration LoadKioskConfig()
        {
            var config = new KioskConfiguration();
            try
            {
                if (File.Exists(KioskConfigPath))
                {
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(KioskConfigPath), config);
                }
                else
                {
                    // Write defaults so administrators have a template to edit.
                    Directory.CreateDirectory(ConfigDirectory);
                    File.WriteAllText(KioskConfigPath, JsonUtility.ToJson(config, true));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ApplicationMode] Kiosk config unreadable ({e.Message}); using defaults.");
            }

            foreach (string arg in Environment.GetCommandLineArgs())
            {
                const string timeoutPrefix = "-bcatKioskTimeout=";
                if (arg.StartsWith(timeoutPrefix, StringComparison.OrdinalIgnoreCase) &&
                    float.TryParse(arg.Substring(timeoutPrefix.Length), out float t))
                    config.inactivityTimeoutSeconds = t;

                const string qualityPrefix = "-bcatKioskQuality=";
                if (arg.StartsWith(qualityPrefix, StringComparison.OrdinalIgnoreCase))
                    config.fixedQualityTier = arg.Substring(qualityPrefix.Length);
            }

            return config;
        }
    }
}
