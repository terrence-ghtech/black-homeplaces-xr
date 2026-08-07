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

            if (ApplicationModeService.IsKiosk && BCaTPlatform.AllowsKioskMode)
                services.AddComponent<Kiosk.KioskController>();

            services.AddComponent<Access.SubtitleService>();

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

            Debug.Log($"[BCaT] Addressables runtime path: {runtimePath} (expecting a '{expected}' " +
                      "platform folder).");

            if (expected != null && !runtimePath.Contains(expected))
            {
                Debug.LogError($"[BCaT] Addressables content mismatch: runtime path '{runtimePath}' " +
                               $"does not contain the expected '{expected}' platform folder. This " +
                               "player was built against another platform's Addressables content; " +
                               "remote scenes will fail to load.");
            }
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single)
                return;

            // New scene, new rigs/terrains/cameras: re-apply and re-capture.
            ResetService.CaptureSceneEntryPose(scene);
            PlayerControlGate.Reapply();
            SettingsManager.ApplyAll();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            services = null;
        }
    }
}
