using BCaT.Production.Settings;
using BCaT.Production.Shell;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BCaT.Production
{
    /// <summary>
    /// Application bootstrap: creates the single persistent BCaT_AppServices
    /// object on startup (no scene edits required) hosting the shared services —
    /// platform rig activation, interaction router, desktop shell (pause menu,
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

            services.AddComponent<PlatformRigActivator>();
            services.AddComponent<Interaction.InteractionRouter>();

            if (!PlatformCapabilities.IsQuestConfiguration)
            {
                services.AddComponent<PauseMenuController>();
                services.AddComponent<CrosshairController>();
            }

            if (ApplicationModeService.IsKiosk && PlatformCapabilities.SupportsKioskMode)
                services.AddComponent<Kiosk.KioskController>();

            services.AddComponent<Access.SubtitleService>();

            SceneManager.sceneLoaded += OnSceneLoaded;

            // Apply persisted settings once the first scene is up.
            SettingsManager.ApplyAll();
            ResetService.CaptureSceneEntryPose(SceneManager.GetActiveScene());
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
