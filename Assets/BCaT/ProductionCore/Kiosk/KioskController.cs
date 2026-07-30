using BCaT.Production.Interaction;
using BCaT.Production.Media;
using BCaT.Production.Settings;
using BCaT.Production.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BCaT.Production.Kiosk
{
    /// <summary>
    /// Kiosk mode services (desktop installations only):
    ///  • enforces fullscreen and the administrator's fixed quality tier,
    ///  • tracks visitor activity (keyboard, mouse movement/buttons, interactions),
    ///  • defers the inactivity reset while intentional long-form media plays
    ///    (configurable via KioskConfiguration.allowResetDuringMedia),
    ///  • runs the full reset sequence through the shared lifecycle systems:
    ///    block input → stop media → close interfaces → return to entrance →
    ///    restore cursor/controls/prompts,
    ///  • hidden administrator chords: hold Ctrl+Shift+Q to quit,
    ///    Ctrl+Shift+F10 for the settings panel. No credentials are stored.
    /// </summary>
    public sealed class KioskController : MonoBehaviour
    {
        float idleSeconds;
        float adminHoldSeconds;
        Vector2 lastMousePosition;
        bool resetInProgress;
        GameObject adminPanel;

        KioskConfiguration Config => ApplicationModeService.Kiosk;

        void Start()
        {
            Debug.Log($"[Kiosk] Active. timeout={Config.inactivityTimeoutSeconds}s, " +
                      $"fixedQuality='{Config.fixedQualityTier}', " +
                      $"adminExit={Config.allowAdminExit}, adminSettings={Config.allowAdminSettings}");

            // Fullscreen and fixed tier are asserted through the normal apply path.
            SettingsManager.ApplyAll();
        }

        void Update()
        {
            TrackActivity();
            HandleAdminChords();
            HandleInactivity();
        }

        void TrackActivity()
        {
            bool activity = false;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.isPressed)
                activity = true;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 pos = mouse.position.ReadValue();
                if ((pos - lastMousePosition).sqrMagnitude > 4f)
                    activity = true;
                lastMousePosition = pos;

                if (mouse.leftButton.isPressed || mouse.rightButton.isPressed)
                    activity = true;
            }

            // Menu/modal interaction and media interaction count as activity.
            if (InteractionState.HasReason(InteractionBlockReason.Menu) ||
                InteractionState.HasReason(InteractionBlockReason.Modal))
                idleSeconds = Mathf.Min(idleSeconds, 1f);

            if (activity)
                idleSeconds = 0f;
            else
                idleSeconds += Time.unscaledDeltaTime;
        }

        void HandleInactivity()
        {
            if (resetInProgress) return;
            float timeout = Config.inactivityTimeoutSeconds;
            if (timeout <= 0f) return;

            // Intentional long-form media defers the reset unless configured otherwise.
            if (!Config.allowResetDuringMedia && MediaPlaybackRegistry.IsAnyMediaPlaying)
            {
                idleSeconds = Mathf.Min(idleSeconds, timeout * 0.5f);
                return;
            }

            if (idleSeconds >= timeout)
                StartCoroutine(ResetSequence());
        }

        /// <summary>
        /// The kiosk reset sequence (shared lifecycle systems, not a bare teleport).
        /// </summary>
        System.Collections.IEnumerator ResetSequence()
        {
            resetInProgress = true;
            idleSeconds = 0f;
            Debug.Log("[Kiosk] Inactivity reset starting.");

            // 1) Block new input.
            InteractionState.Block(this, InteractionBlockReason.PlayerControl);
            PlayerControlGate.Suspend(this);

            // 2) Fade/notice (brief, honest reset screen).
            var notice = UiFactory.CreateOverlayCanvas("BCaT_KioskReset", 32700);
            UiFactory.CreateFullScreenPanel(notice.transform, "Backdrop");
            UiFactory.CreateLabel(notice.transform, "Resetting the exhibit…", 30f)
                .rectTransform.anchoredPosition = Vector2.zero;
            yield return null;

            // 3) Stop all active media; 4) close all exhibit interfaces.
            MediaPlaybackRegistry.StopAll();
            InteractionState.ForceCloseAll();

            // 5–9) Release exhibit resources / unload non-core scenes / return to
            // entrance: ReturnToMainEntrance runs the shared transition lifecycle
            // (media stop, audio exit preparation, loading scene, spawn restore).
            InteractionState.Unblock(this);
            PlayerControlGate.Resume(this);
            ResetService.ReturnToMainEntrance();

            // Wait for any transition to finish before declaring done.
            float safety = 60f;
            while (SceneTransitionState.IsTransitionInProgress && safety > 0f)
            {
                safety -= Time.unscaledDeltaTime;
                yield return null;
            }

            // 10–12) Restore camera/cursor/prompts and clear temporary state.
            PlayerControlGate.ForceResumeAll();
            if (notice != null)
                Destroy(notice.gameObject);

            Debug.Log("[Kiosk] Inactivity reset complete.");
            resetInProgress = false;
        }

        void HandleAdminChords()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool ctrlShift = (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed) &&
                             (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);

            // Hold Ctrl+Shift+Q to quit (administrator exit sequence).
            if (Config.allowAdminExit && ctrlShift && keyboard.qKey.isPressed)
            {
                adminHoldSeconds += Time.unscaledDeltaTime;
                if (adminHoldSeconds >= Config.adminChordHoldSeconds)
                {
                    Debug.Log("[Kiosk] Administrator exit chord accepted.");
                    PauseMenuController.QuitApplication();
                }
            }
            else
            {
                adminHoldSeconds = 0f;
            }

            // Ctrl+Shift+F10 opens the full (unrestricted) settings panel.
            if (Config.allowAdminSettings && ctrlShift &&
                keyboard.f10Key.wasPressedThisFrame && adminPanel == null)
            {
                Debug.Log("[Kiosk] Administrator settings chord accepted.");
                InteractionState.Block(this, InteractionBlockReason.Menu);
                PlayerControlGate.Suspend(this);
                adminPanel = AdminSettingsPanel();
            }
        }

        GameObject AdminSettingsPanel()
        {
            // The standard settings UI, but with kiosk restrictions bypassed by
            // temporarily treating the session as standard for this panel.
            return SettingsMenuControllerUnrestricted(() =>
            {
                adminPanel = null;
                InteractionState.Unblock(this);
                PlayerControlGate.Resume(this);
            });
        }

        static GameObject SettingsMenuControllerUnrestricted(System.Action onClose)
        {
            // SettingsMenuController consults ApplicationModeService.IsKiosk;
            // for the admin panel we build the unrestricted variant directly.
            return AdminSettingsMenu.Open(onClose);
        }
    }

    /// <summary>Unrestricted settings panel for kiosk administrators.</summary>
    static class AdminSettingsMenu
    {
        public static GameObject Open(System.Action onClose)
        {
            var canvas = UiFactory.CreateOverlayCanvas("BCaT_AdminSettings", 32600);
            var panel = UiFactory.CreateCenterPanel(canvas.transform, "Panel", new Vector2(760, 420));
            var column = UiFactory.CreateColumn(panel, "Column", 14f);
            UiFactory.CreateLabel(column, "Administrator", 30f);
            UiFactory.CreateLabel(column,
                $"Mode file: {ApplicationModeService.ModeFilePath}\n" +
                $"Kiosk config: {ApplicationModeService.KioskConfigPath}\n" +
                $"Settings: {SettingsManager.SettingsPath}\n" +
                $"Log: {Application.consoleLogPath}", 18f);

            UiFactory.CreateButton(column, "Open Full Settings", () =>
            {
                SettingsMenuControllerBypass(onClose, canvas);
            });
            UiFactory.CreateButton(column, "Quit Application", PauseMenuController.QuitApplication);
            var close = UiFactory.CreateButton(column, "Close", () =>
            {
                Object.Destroy(canvas.gameObject);
                onClose?.Invoke();
            });
            UiFactory.SelectForKeyboard(close);
            return canvas.gameObject;
        }

        static void SettingsMenuControllerBypass(System.Action onClose, Canvas adminCanvas)
        {
            Object.Destroy(adminCanvas.gameObject);
            // Kiosk restriction in SettingsMenuController keys off the mode;
            // administrators get the standard full panel via this entry point.
            SettingsMenuController.OpenUnrestricted(onClose);
        }
    }
}
