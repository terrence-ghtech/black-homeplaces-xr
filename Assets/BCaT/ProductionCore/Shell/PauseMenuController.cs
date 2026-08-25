using BCaT.Production.Interaction;
using BCaT.Production.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BCaT.Production.Shell
{
    /// <summary>
    /// Desktop pause menu (Escape). Suppresses player movement and world
    /// interaction, unlocks the cursor, and offers Resume / Settings / Exhibit
    /// Directory / Credits / Return to Main Entrance / Quit to Main Menu /
    /// Quit Application. Media state is intentionally preserved while paused
    /// (audio narration continues; leaving via Return/Quit stops it through the
    /// media registry). Escape is owned by focused exhibit interfaces first —
    /// the pause menu only opens when no modal blocker is active.
    /// </summary>
    public sealed class PauseMenuController : MonoBehaviour
    {
        public const string MainMenuSceneName = "MainMenuScene";

        GameObject menuRoot;
        GameObject childPanel;
        bool open;

        void Update()
        {
            if (PlatformCapabilities.IsQuestConfiguration || PlatformCapabilities.IsXRActive)
                return;

            string scene = SceneManager.GetActiveScene().name;
            if (scene == MainMenuSceneName || scene == ResetService.LoadingSceneName)
                return;

            if (!FocusedUiInput.CancelPressed)
                return;

            if (open)
            {
                // A child panel (settings/directory/confirm) owns Escape first.
                if (childPanel == null)
                    Close();
                return;
            }

            // Focused exhibit interfaces (readers, slideshows, exit modal) own
            // Escape, and so does any other menu layer that is already up
            // (opening onboarding, kiosk reset overlay). The pause menu's own
            // Menu block is not in play here — when it is open the branch
            // above handles Escape before this check is reached.
            if (InteractionState.HasReason(InteractionBlockReason.Modal) ||
                InteractionState.HasReason(InteractionBlockReason.Media) ||
                InteractionState.HasReason(InteractionBlockReason.Menu))
                return;
            if (SceneTransitionState.IsTransitionInProgress)
                return;

            OpenMenu();
        }

        public void OpenMenu()
        {
            if (open) return;
            open = true;

            InteractionState.Block(this, InteractionBlockReason.Menu, Close);
            PlayerControlGate.Suspend(this);

            var canvas = UiFactory.CreateOverlayCanvas("BCaT_PauseMenu", 30500);
            menuRoot = canvas.gameObject;

            var panel = UiFactory.CreateCenterPanel(canvas.transform, "Panel", new Vector2(560, 720));
            var column = UiFactory.CreateColumn(panel, "Column", 14f);

            UiFactory.CreateLabel(column, Application.productName, 30f);

            var resume = UiFactory.CreateButton(column, "Resume", Close);
            UiFactory.CreateButton(column, "Settings", () =>
            {
                childPanel = SettingsMenuController.Open(() => childPanel = null);
            });
            UiFactory.CreateButton(column, "Exhibit Directory", () =>
            {
                childPanel = Access.ExhibitDirectoryUi.Open(() => childPanel = null, Close);
            });
            UiFactory.CreateButton(column, "Credits", () =>
            {
                childPanel = MainMenuController.OpenCreditsPanel(() => childPanel = null);
            });
            UiFactory.CreateButton(column, "Return to Main Entrance", () =>
            {
                Close();
                ResetService.ReturnToMainEntrance();
            });

            if (!ApplicationModeService.IsKiosk)
            {
                UiFactory.CreateButton(column, "Quit to Main Menu", () =>
                {
                    childPanel = UiFactory.CreateConfirmDialog(
                        "Return to the main menu?", "Quit to Menu",
                        onConfirm: () =>
                        {
                            childPanel = null;
                            Close();
                            QuitToMainMenu();
                        },
                        onCancel: () => childPanel = null);
                });

                UiFactory.CreateButton(column, "Quit Application", () =>
                {
                    childPanel = UiFactory.CreateConfirmDialog(
                        "Quit the application?", "Quit",
                        onConfirm: () =>
                        {
                            childPanel = null;
                            QuitApplication();
                        },
                        onCancel: () => childPanel = null);
                });
            }

            UiFactory.SelectForKeyboard(resume);
        }

        public void Close()
        {
            if (!open) return;
            open = false;

            if (menuRoot != null) Destroy(menuRoot);
            if (childPanel != null) Destroy(childPanel);
            menuRoot = null;
            childPanel = null;

            InteractionState.Unblock(this);
            PlayerControlGate.Resume(this);
            SettingsManager.Save();
        }

        void QuitToMainMenu()
        {
            Media.MediaPlaybackRegistry.StopAll();
            InteractionState.ForceCloseAll();
            PlayerControlGate.ForceResumeAll();
            SceneManager.LoadSceneAsync(MainMenuSceneName, LoadSceneMode.Single);
        }

        public static void QuitApplication()
        {
            Media.MediaPlaybackRegistry.StopAll();
            SettingsManager.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnDestroy()
        {
            if (open)
            {
                InteractionState.Unblock(this);
                PlayerControlGate.Resume(this);
            }
        }
    }
}
