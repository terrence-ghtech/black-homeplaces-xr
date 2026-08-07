using System;
using UnityEngine;
using UnityEngine.XR;

namespace BCaT.Production
{
    /// <summary>
    /// The single platform authority.
    ///
    /// Everything that needs to know which platform is running asks this class,
    /// and this class is the only place allowed to touch the raw platform APIs
    /// (build defines, XRSettings, XR Management). That matters because the
    /// project previously had two decision primitives
    /// (InteractionPromptText.IsXRActive and ScenePlatformRigSelector.ShouldUseXR)
    /// and nine call sites that bypassed the capability wrapper entirely.
    ///
    /// Resolution precedence, highest first:
    ///   1. Editor Platform Test Mode override  (development only)
    ///   2. -bcatPlatform=Desktop|Quest         (command line)
    ///   3. Build define                        (the Quest player is always Quest)
    ///   4. XR device probe                     (an XR device is initialized)
    ///   5. Desktop
    ///
    /// Latching: an answer of Quest, or any answer from a forced source
    /// (1-3), is final. A Desktop answer that came from the device probe stays
    /// provisional and may promote to Quest exactly once, which is precisely
    /// the behavior the previous IsXRActive() polling had — XR Management can
    /// report an inactive device for the first frames after load. The platform
    /// can therefore never move Quest → Desktop, and the one permitted
    /// promotion is logged.
    ///
    /// Timing: resolution needs no scene, so the answer is available from the
    /// first Awake of the first scene. That is what lets ScenePlatformBinding
    /// activate the correct rig branch before any rig component runs.
    /// </summary>
    public static class BCaTPlatform
    {
        public const string CommandLinePrefix = "-bcatPlatform=";

#if UNITY_EDITOR
        /// <summary>
        /// SessionState key for the Editor override. SessionState survives
        /// domain reloads and resets when the editor restarts, which is the
        /// right lifetime for a test mode.
        /// </summary>
        public const string EditorOverrideKey = "BCaT.PlatformTestMode";
#endif

        static BCaTPlatformId? resolved;
        static BCaTPlatformSource resolvedSource;
        static bool latched;
        static bool forcedEvaluated;
        static BCaTPlatformId? forced;
        static BCaTPlatformSource forcedSource;
        static BCaTPlatformProfile desktopProfile;
        static BCaTPlatformProfile questProfile;

        /// <summary>Raised once, the first time the platform is resolved.</summary>
        public static event Action<BCaTPlatformId> PlatformResolved;

        // ---- Resolved platform ---------------------------------------------

        public static BCaTPlatformId Current
        {
            get
            {
                if (latched)
                    return resolved.Value;

                BCaTPlatformId value = Resolve(out BCaTPlatformSource source);
                bool first = !resolved.HasValue;
                bool promoted = !first && resolved.Value != value;

                resolved = value;
                resolvedSource = source;

                // Only a probe-derived Desktop answer stays provisional: a late
                // XR initialization may promote it to Quest exactly once, which
                // is the behavior the previous per-call IsXRActive() had.
                latched = source != BCaTPlatformSource.Default;

                if (first)
                {
                    Debug.Log($"[BCaTPlatform] Resolved platform: {value} (source={source}). {Describe()}");
                    PlatformResolved?.Invoke(value);
                }
                else if (promoted)
                {
                    Debug.Log($"[BCaTPlatform] Platform promoted Desktop → {value} (source={source}) " +
                              "after XR initialization completed.");
                    PlatformResolved?.Invoke(value);
                }

                return value;
            }
        }

        public static BCaTPlatformSource Source
        {
            get
            {
                _ = Current;
                return resolvedSource;
            }
        }

        public static bool IsQuest => Current == BCaTPlatformId.Quest;

        public static bool IsDesktop => Current == BCaTPlatformId.Desktop;

        // ---- Profile --------------------------------------------------------

        public static BCaTPlatformProfile Profile => ProfileFor(Current);

        public static BCaTPlatformProfile ProfileFor(BCaTPlatformId id)
        {
            if (id == BCaTPlatformId.Quest)
            {
                if (questProfile == null)
                    questProfile = LoadOrFallback(BCaTPlatformProfile.QuestResourceName, id);
                return questProfile;
            }

            if (desktopProfile == null)
                desktopProfile = LoadOrFallback(BCaTPlatformProfile.DesktopResourceName, id);
            return desktopProfile;
        }

        static BCaTPlatformProfile LoadOrFallback(string resourceName, BCaTPlatformId id)
        {
            BCaTPlatformProfile asset = Resources.Load<BCaTPlatformProfile>(resourceName);
            if (asset != null)
                return asset;

            Debug.LogWarning($"[BCaTPlatform] Platform profile '{resourceName}' not found in Resources; " +
                             "using the built-in fallback profile. Behavior is unchanged, but the " +
                             "profile asset should be restored.");
            return BCaTPlatformProfile.CreateFallback(id);
        }

        // ---- Convenience capability queries ---------------------------------
        // These read the profile so a platform difference is a data change.

        public static bool UseXRPrompts => Profile.usesXRPrompts;
        public static bool SupportsKeyboardMouse => Profile.supportsKeyboardMouse;
        public static bool ShowsAppShell => Profile.showsAppShell;
        public static bool AllowsKioskMode => Profile.allowsKioskMode;
        public static bool OwnsSwapchain => Profile.ownsSwapchain;
        public static bool QualityIsFixed => Profile.qualityIsFixed;
        public static ScenePlayerRig.RigKind RigKind => Profile.rigKind;
        public static BCaTPromptStyle PromptStyle => Profile.promptStyle;
        public static BCaTUiInputModuleKind UiInputModule => Profile.uiInputModule;
        public static BCaTInputProviderKind InputProvider => Profile.inputProvider;
        public static BCaTMediaSourcePolicy MediaSourcePolicy => Profile.mediaSourcePolicy;
        public static bool VerboseTransitionDiagnostics => Profile.verboseTransitionDiagnostics;

        // ---- Raw probes (the only sanctioned platform API access) ------------

        /// <summary>
        /// True when this binary is the Meta Quest player. Quest is the only
        /// supported Android target, so Android implies Quest. Unlike
        /// <see cref="IsQuest"/> this is a statement about the *binary*, not the
        /// resolved platform, so it stays false under an Editor simulation.
        /// </summary>
        public static bool IsQuestPlayerBinary =>
#if UNITY_ANDROID && !UNITY_EDITOR
            true;
#else
            false;
#endif

        /// <summary>
        /// True when the Editor is in "Quest XR (Simulated)" test mode and the
        /// XR Device Simulator should therefore run. False in players and in
        /// every other mode — a real headset must not compete with simulated
        /// devices, and desktop must never have the simulator consuming input.
        /// </summary>
        public static bool WantsEditorDeviceSimulator
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.SessionState.GetString(EditorOverrideKey, "Auto") == "QuestSimulated";
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// True when XR Management reports a live XR device. This is the probe
        /// the resolver uses; it is false under Editor Quest simulation, which
        /// is exactly why the Editor override outranks it.
        /// </summary>
        public static bool ProbeXRDevice()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#elif UNITY_EDITOR
            // XR Management can keep a loader initialized while the desktop rig
            // is the intended path, so only a genuinely active device counts.
            return XRSettings.isDeviceActive;
#elif UNITY_ANDROID
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

        // ---- Resolution ------------------------------------------------------

        static BCaTPlatformId Resolve(out BCaTPlatformSource source)
        {
            // Forced sources cannot change within a session, so evaluate them
            // once: this getter is read per frame and GetCommandLineArgs()
            // allocates.
            if (!forcedEvaluated)
            {
                forcedEvaluated = true;
                forced = null;

#if UNITY_EDITOR
                if (TryGetEditorOverride(out BCaTPlatformId overridden))
                {
                    forced = overridden;
                    forcedSource = BCaTPlatformSource.EditorOverride;
                }
                else
#endif
                if (TryGetCommandLineOverride(out BCaTPlatformId fromArgs))
                {
                    forced = fromArgs;
                    forcedSource = BCaTPlatformSource.CommandLine;
                }
                else if (IsQuestPlayerBinary)
                {
                    forced = BCaTPlatformId.Quest;
                    forcedSource = BCaTPlatformSource.BuildDefine;
                }
            }

            if (forced.HasValue)
            {
                source = forcedSource;
                return forced.Value;
            }

            if (ProbeXRDevice())
            {
                source = BCaTPlatformSource.XRDevice;
                return BCaTPlatformId.Quest;
            }

            source = BCaTPlatformSource.Default;
            return BCaTPlatformId.Desktop;
        }

        static bool TryGetCommandLineOverride(out BCaTPlatformId id)
        {
            id = BCaTPlatformId.Desktop;
            try
            {
                foreach (string arg in Environment.GetCommandLineArgs())
                {
                    if (arg == null || !arg.StartsWith(CommandLinePrefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string value = arg.Substring(CommandLinePrefix.Length);
                    if (Enum.TryParse(value, ignoreCase: true, out BCaTPlatformId parsed))
                    {
                        id = parsed;
                        return true;
                    }

                    Debug.LogWarning($"[BCaTPlatform] Unrecognized {CommandLinePrefix} value '{value}'. " +
                                     "Expected Desktop or Quest.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BCaTPlatform] Could not read command line arguments: {e.Message}");
            }

            return false;
        }

#if UNITY_EDITOR
        static bool TryGetEditorOverride(out BCaTPlatformId id)
        {
            id = BCaTPlatformId.Desktop;
            string value = UnityEditor.SessionState.GetString(EditorOverrideKey, string.Empty);
            if (string.IsNullOrEmpty(value) || value == "Auto")
                return false;

            // "QuestSimulated" and "QuestDevice" both resolve to the Quest
            // platform; they differ only in whether the device simulator runs.
            if (value.StartsWith("Quest", StringComparison.OrdinalIgnoreCase))
            {
                id = BCaTPlatformId.Quest;
                return true;
            }

            if (value.StartsWith("Desktop", StringComparison.OrdinalIgnoreCase))
            {
                id = BCaTPlatformId.Desktop;
                return true;
            }

            return false;
        }
#endif

        public static string Describe()
        {
            // Read the cached value when present: Describe() is called from
            // inside the first-resolve log, and going through Current there
            // would re-enter the resolver.
            BCaTPlatformId id = resolved ?? Current;
            return $"platform={id}, source={resolvedSource}, unityPlatform={Application.platform}, " +
                   $"questBinary={IsQuestPlayerBinary}, xrDevice={ProbeXRDevice()}, " +
                   $"profile={ProfileFor(id).displayName}, mode={ApplicationModeService.Mode}, " +
                   $"quality={QualityTierName()}";
        }

        static string QualityTierName()
        {
            string[] names = QualitySettings.names;
            int level = QualitySettings.GetQualityLevel();
            return level >= 0 && level < names.Length ? names[level] : "Unknown";
        }

        /// <summary>
        /// Resolve before any scene object awakens so ScenePlatformBinding can
        /// answer during Awake, and so the startup line appears before any
        /// platform-dependent log.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResolveAtStartup() => _ = Current;

        /// <summary>Domain-reload hygiene: statics survive disabled domain reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            resolved = null;
            resolvedSource = BCaTPlatformSource.Default;
            latched = false;
            forcedEvaluated = false;
            forced = null;
            forcedSource = BCaTPlatformSource.Default;
            desktopProfile = null;
            questProfile = null;
            PlatformResolved = null;
        }
    }
}
