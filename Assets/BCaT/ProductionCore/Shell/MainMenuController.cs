using BCaT.Production.Interaction;
using BCaT.Production.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BCaT.Production.Shell
{
    /// <summary>
    /// Desktop main menu, hosted by the lightweight MainMenuScene (scene 0 of
    /// desktop builds). Builds its UI at runtime through the UiFactory:
    /// Begin Experience / Settings / Accessibility / Credits / Quit.
    /// In kiosk mode the menu is skipped entirely — the experience begins
    /// immediately and quit is reserved for the administrator chord.
    /// On the Quest configuration the menu scene is bypassed as well (Quest
    /// boots straight into the house, preserving existing behavior).
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        GameObject childPanel;

        void Start()
        {
            // The shell menu is a desktop feature; Quest goes straight in.
            if (PlatformCapabilities.IsQuestConfiguration || PlatformCapabilities.IsXRActive ||
                ApplicationModeService.IsKiosk)
            {
                BeginExperience();
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            InteractionState.Block(this, InteractionBlockReason.Menu);
            BuildUi();
        }

        void BuildUi()
        {
            var canvas = UiFactory.CreateOverlayCanvas("BCaT_MainMenu", 30000);
            canvas.transform.SetParent(transform, false);

            UiFactory.CreateFullScreenPanel(canvas.transform, "Backdrop");
            var panel = UiFactory.CreateCenterPanel(canvas.transform, "Panel", new Vector2(640, 700));
            var column = UiFactory.CreateColumn(panel, "Column", 16f);

            UiFactory.CreateLabel(column, Application.productName, 40f);
            UiFactory.CreateLabel(column, Application.companyName, 22f);

            var begin = UiFactory.CreateButton(column, "Begin Experience", BeginExperience, 28f);
            UiFactory.CreateButton(column, "Settings", () =>
            {
                childPanel = SettingsMenuController.Open(() => childPanel = null);
            });
            UiFactory.CreateButton(column, "Accessibility", () =>
            {
                // Opens the settings panel directly on the accessibility tab.
                childPanel = SettingsMenuController.Open(() => childPanel = null, initialTab: 99);
            });
            UiFactory.CreateButton(column, "Credits", OpenCredits);
            UiFactory.CreateButton(column, "Quit", () =>
            {
                childPanel = UiFactory.CreateConfirmDialog("Quit the application?", "Quit",
                    onConfirm: PauseMenuController.QuitApplication,
                    onCancel: () => childPanel = null);
            });

            UiFactory.SelectForKeyboard(begin);
        }

        void Update()
        {
            // Escape closes an open child panel; from the root menu it does nothing.
            if (childPanel == null && FocusedUiInput.CancelPressed)
            {
                // no-op by design: quitting requires the explicit button + confirm
            }
        }

        void OpenCredits()
        {
            var canvas = UiFactory.CreateOverlayCanvas("BCaT_Credits", 31000);
            childPanel = canvas.gameObject;
            var panel = UiFactory.CreateCenterPanel(canvas.transform, "Panel", new Vector2(820, 560));
            var column = UiFactory.CreateColumn(panel, "Column", 18f);
            UiFactory.CreateLabel(column, Application.productName, 30f);
            UiFactory.CreateLabel(column,
                $"{Application.companyName}\nVersion {Application.version}", 22f);
            UiFactory.CreateLabel(column,
                "Full credits and acknowledgements are provided with the\n" +
                "institutional documentation for this installation.", 20f);
            var close = UiFactory.CreateButton(column, "Close", () =>
            {
                Destroy(canvas.gameObject);
                childPanel = null;
            });
            UiFactory.SelectForKeyboard(close);
        }

        void BeginExperience()
        {
            InteractionState.Unblock(this);
            // Use the shared transition lifecycle so the heavy main scene loads
            // behind the existing loading screen.
            SceneTransitionState.RequestTransition(
                ResetService.MainSceneName,
                ResetService.MainEntranceSpawnId,
                SceneManager.GetActiveScene().name);
            SceneManager.LoadSceneAsync(ResetService.LoadingSceneName, LoadSceneMode.Single);
        }

        void OnDestroy()
        {
            InteractionState.Unblock(this);
        }
    }
}
