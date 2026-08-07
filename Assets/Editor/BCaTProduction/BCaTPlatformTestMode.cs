using BCaT.Production;
using UnityEditor;
using UnityEngine;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Editor Platform Test Mode: which platform Play Mode should run as.
    ///
    /// This exists because the Quest hierarchy was previously impossible to
    /// exercise at the desk. XRGeneralSettingsPerBuildTarget has an Android
    /// entry only, so XRSettings.isDeviceActive is always false in the Editor,
    /// so the platform always resolved to Desktop, so the XR rig was always
    /// deactivated — and the XR Device Simulator, which does not set
    /// isDeviceActive either, had no rig to drive. Every Quest behavior
    /// therefore had to be validated on device.
    ///
    /// Forcing the resolved platform is what breaks that loop, which is why the
    /// editor override is the highest-precedence source in BCaTPlatform: it must
    /// outrank the device probe the simulator cannot satisfy.
    ///
    /// The mode is stored in SessionState — it survives domain reloads and
    /// resets when the Editor restarts, which is the right lifetime for a test
    /// setting. It has no effect on players, cannot be set from a build, and
    /// never modifies an asset.
    ///
    /// CI uses the equivalent -bcatPlatform=Desktop|Quest command line argument.
    /// </summary>
    [InitializeOnLoad]
    public static class BCaTPlatformTestMode
    {
        public const string Auto = "Auto";
        public const string Desktop = "Desktop";
        public const string QuestSimulated = "QuestSimulated";
        public const string QuestDevice = "QuestDevice";

        const string MenuRoot = "BCaT/Platform Test Mode/";
        const string AutoItem = MenuRoot + "Auto (probe XR device)";
        const string DesktopItem = MenuRoot + "Desktop";
        const string QuestSimulatedItem = MenuRoot + "Quest XR (Simulated)";
        const string QuestDeviceItem = MenuRoot + "Quest XR (Device)";

        static BCaTPlatformTestMode()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static string Mode
        {
            get
            {
                string value = SessionState.GetString(BCaTPlatform.EditorOverrideKey, Auto);
                return string.IsNullOrEmpty(value) ? Auto : value;
            }
        }

        /// <summary>
        /// True when the current mode wants the XR Device Simulator to run.
        /// Quest (Device) deliberately excludes it: a real headset must not
        /// compete with simulated devices.
        /// </summary>
        public static bool WantsDeviceSimulator => Mode == QuestSimulated;

        public static string Describe() => Mode switch
        {
            Desktop => "Desktop (forced)",
            QuestSimulated => "Quest XR (Simulated)",
            QuestDevice => "Quest XR (Device)",
            _ => "Auto (probe XR device)",
        };

        static void Set(string mode)
        {
            if (Mode == mode)
                return;

            SessionState.SetString(BCaTPlatform.EditorOverrideKey, mode);
            Debug.Log($"[BCaTPlatformTestMode] Platform Test Mode → {Describe()}. " +
                      "Exit and re-enter Play Mode for it to take effect.");

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[BCaTPlatformTestMode] The platform is resolved once per Play Mode " +
                                 "session and is already latched. Stop and restart Play Mode to apply " +
                                 "the new mode.");
            }
        }

        static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode)
                return;

            Debug.Log($"[BCaTPlatformTestMode] Entering Play Mode as: {Describe()} " +
                      $"(device simulator: {(WantsDeviceSimulator ? "on" : "off")}).");
        }

        // ---- Menu ---------------------------------------------------------

        [MenuItem(AutoItem, priority = 0)]
        static void SetAuto() => Set(Auto);

        [MenuItem(AutoItem, true)]
        static bool ValidateAuto()
        {
            Menu.SetChecked(AutoItem, Mode == Auto);
            return true;
        }

        [MenuItem(DesktopItem, priority = 1)]
        static void SetDesktop() => Set(Desktop);

        [MenuItem(DesktopItem, true)]
        static bool ValidateDesktop()
        {
            Menu.SetChecked(DesktopItem, Mode == Desktop);
            return true;
        }

        [MenuItem(QuestSimulatedItem, priority = 2)]
        static void SetQuestSimulated() => Set(QuestSimulated);

        [MenuItem(QuestSimulatedItem, true)]
        static bool ValidateQuestSimulated()
        {
            Menu.SetChecked(QuestSimulatedItem, Mode == QuestSimulated);
            return true;
        }

        [MenuItem(QuestDeviceItem, priority = 3)]
        static void SetQuestDevice() => Set(QuestDevice);

        [MenuItem(QuestDeviceItem, true)]
        static bool ValidateQuestDevice()
        {
            Menu.SetChecked(QuestDeviceItem, Mode == QuestDevice);
            return true;
        }

        // ---- Batch entry points (used by CI and the validation harnesses) ---

        public static void SetAutoBatch() => SessionState.SetString(BCaTPlatform.EditorOverrideKey, Auto);
        public static void SetDesktopBatch() => SessionState.SetString(BCaTPlatform.EditorOverrideKey, Desktop);
        public static void SetQuestSimulatedBatch() => SessionState.SetString(BCaTPlatform.EditorOverrideKey, QuestSimulated);
    }
}
