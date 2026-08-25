using BCaT.Production.Settings;
using BCaT.Production.Shell;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BCaT.Production
{
    /// <summary>
    /// Application bootstrap: creates the single persistent BCaT_AppServices
    /// object on startup (no scene edits required) hosting the shared services —
    /// interaction router, desktop shell (pause menu,
    /// crosshair), kiosk controller, and subtitle service — and applies the
    /// persisted settings. This is the only DontDestroyOnLoad object in the
    /// project; everything else continues to use the established static-state
    /// pattern.
    /// </summary>
    public static class BCaTAppBootstrap
    {
        static GameObject services;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            if (services != null)
                return;

            services = new GameObject("BCaT_AppServices");
            Object.DontDestroyOnLoad(services);

            Debug.Log($"[BCaT] Bootstrap: {PlatformCapabilities.Describe()}");

            services.AddComponent<Interaction.InteractionRouter>();
            services.AddComponent<Interaction.XRInteractionPromptHoverBridge>();
            services.AddComponent<LegacyInteractionPromptSuppressor>();

            // Service composition follows the active platform PROFILE rather
            // than the build define, so an Editor Quest session composes the
            // same services a Quest device does. Composing from the build
            // define made simulated Quest sessions grow a desktop pause menu
            // and crosshair, which then pulled in a second EventSystem.
            if (BCaTPlatform.ShowsAppShell)
            {
                services.AddComponent<PauseMenuController>();
                services.AddComponent<CrosshairController>();
            }

            services.AddComponent<OpeningOnboardingController>();

            if (ApplicationModeService.IsKiosk && BCaTPlatform.AllowsKioskMode)
                services.AddComponent<Kiosk.KioskController>();

            services.AddComponent<Access.SubtitleService>();

            if (BCaTPlatform.IsQuest)
            {
                // Safety net for controllers that stay invisible after a missed
                // tracking-acquired event.
                services.AddComponent<XrControllerVisibilityGuard>();
            }

            AssertAddressablesMatchPlatform();

            SceneManager.sceneLoaded += OnSceneLoaded;

            // Apply persisted settings once the first scene is up.
            SettingsManager.ApplyAll();
            ResetService.CaptureSceneEntryPose(SceneManager.GetActiveScene());
        }

        /// <summary>
        /// A player built against another platform's Addressables content fails
        /// only later, deep inside the Black Kitchen portal, as an opaque
        /// download timeout. Check the runtime path at startup instead, so the
        /// cause is named in the first lines of the log. Development builds only:
        /// this is a build-configuration mistake, not a runtime condition to
        /// recover from.
        /// </summary>
        static void AssertAddressablesMatchPlatform()
        {
            if (!Debug.isDebugBuild)
                return;

            string runtimePath;
            try
            {
                runtimePath = UnityEngine.AddressableAssets.Addressables.RuntimePath;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BCaT] Could not read the Addressables runtime path: {e.Message}");
                return;
            }

            string expected = BCaTPlatform.ExpectedAddressablesPlatformFolder;
            Debug.Log($"[BCaT] Addressables runtime path: {runtimePath} " +
                      $"(expecting a '{expected}' bundle folder beneath it).");

            if (expected == null)
                return;

            // The platform folder is a SUBFOLDER of the runtime path, not part of
            // it. On Quest the runtime path is a jar: URL inside the APK, which
            // no file API can enumerate — and the build pipeline already
            // validates the APK's aa/Android contents far more thoroughly, so
            // there is nothing useful to add here.
            if (!System.IO.Directory.Exists(runtimePath))
                return;

            var present = new System.Collections.Generic.List<string>();
            foreach (string folder in BCaTPlatform.KnownAddressablesPlatformFolders)
                if (System.IO.Directory.Exists(System.IO.Path.Combine(runtimePath, folder)))
                    present.Add(folder);

            if (present.Count == 0)
                return; // No local bundle folders at all: nothing to mismatch.

            if (!present.Contains(expected))
            {
                Debug.LogError($"[BCaT] Addressables content mismatch: expected a '{expected}' " +
                               $"bundle folder under '{runtimePath}' but found only " +
                               $"[{string.Join(", ", present)}]. This player was built against " +
                               "another platform's Addressables content; remote scenes will fail " +
                               "to load.");
            }
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single)
                return;

            // New scene, new rigs/terrains/cameras: re-apply and re-capture.
            Diagnostics.MemTrace.Mark("BOOTSTRAP_SCENE_PASS_BEGIN", $"scene={scene.name}"); // BCAT_MEMTRACE
            ResetService.CaptureSceneEntryPose(scene);
            PlayerControlGate.Reapply();
            SettingsManager.ApplyAll();
            Diagnostics.MemTrace.Mark("BOOTSTRAP_SCENE_PASS_END", $"scene={scene.name}"); // BCAT_MEMTRACE
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            services = null;
        }
    }
}
