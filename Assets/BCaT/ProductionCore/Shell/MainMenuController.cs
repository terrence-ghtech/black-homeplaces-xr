using BCaT.Production.Interaction;
using BCaT.Production.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BCaT.Production.Shell
{
    /// <summary>
    /// Desktop main menu, hosted by the lightweight MainMenuScene (scene 0 of
    /// desktop builds). Builds its UI at runtime through the UiFactory:
    /// Begin Experience / Settings / Credits / Quit.
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

            ConfigureSingleLine(UiFactory.CreateLabel(column, Application.productName, 40f));
            ConfigureSingleLine(UiFactory.CreateLabel(column, Application.companyName, 22f));

            var begin = UiFactory.CreateButton(column, "Begin Experience", BeginExperience, 28f);
            UiFactory.CreateButton(column, "Settings", () =>
            {
                childPanel = SettingsMenuController.Open(() => childPanel = null);
            });
            UiFactory.CreateButton(column, "Credits", () =>
            {
                childPanel = OpenCreditsPanel(() => childPanel = null);
            });
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

        /// <summary>Single attribution source shared by desktop and Quest menus.</summary>
        public const string CreditsAttributionText =
            "Creative Commons Attributions\n\n" +
            "\"Sewing Machine\" by Karatecake69\n" +
            "https://skfb.ly/6Vsx9\n" +
            "Licensed under Creative Commons Attribution 4.0\n" +
            "http://creativecommons.org/licenses/by/4.0/\n\n" +
            "\"Needle and Thread\" by kel\n" +
            "https://skfb.ly/pAIMs\n\n" +
            "\"Old Sewing Box\" by Diego-M84\n" +
            "https://skfb.ly/oIrtZ\n\n" +
            "\"QUILT BED\" by rerwandi\n" +
            "https://sketchfab.com/3d-models/bed-352f853f03194e9dac551e2c8afe7d1a\n\n" +
            "\"Pillow & Quilt\" by Kiu\n" +
            "https://sketchfab.com/3d-models/pillow-quilt-13b51cb8742e49dca344cfd4bceb4eaf\n\n" +
            "\"Dishwasher\"\n" +
            "https://sketchfab.com/3d-models/dishwasher-7a4d8ed6ece248a596ec4dfb5741e7cc\n\n" +
            "\"Kitchen Island\"\n" +
            "https://sketchfab.com/3d-models/modern-scandinavian-kitchen-island-a9738f4e651b4779acdddcfbb89516f6\n\n" +
            "\"Fan\"\n" +
            "https://sketchfab.com/3d-models/zedah-prevalent-52-inch-luxury-fan-4946786960914c2e9342c02f0ad3d89d\n\n" +
            "\"Rug\"\n" +
            "https://sketchfab.com/3d-models/rug-f4a9c92c9e194abca7f84b8f1e4f8d2e\n\n" +
            "\"Medieval Stone Well\"\n" +
            "https://sketchfab.com/3d-models/medieval-stone-well-1771719e771f407a8c00f1542900aea8\n\n" +
            "\"Garden Flower Vegetation\"\n" +
            "https://sketchfab.com/3d-models/garden-flower-vegetation-683e2043a99d4bddb750098a934e3533\n\n" +
            "\"Tropical Plant\"\n" +
            "https://sketchfab.com/3d-models/tropical-plant-7365d3fc0eb148028c751364dd172952\n\n" +
            "\"Tropical Plant\"\n" +
            "https://sketchfab.com/3d-models/tropical-plant-3ee280726f1f496e9b2377d43b4cbb2d\n\n" +
            "\"Orange Flowers\"\n" +
            "https://sketchfab.com/3d-models/flower-0fa50cf622f44f2ba59eff6c11cb8fbd\n\n" +
            "\"Japanese Red Bridge\"\n" +
            "https://sketchfab.com/3d-models/japanese-red-bridge-e6ec1981fb14462495d1aa75bd1131ac\n\n" +
            "\"Photo Album\"\n" +
            "https://sketchfab.com/3d-models/photo-album-0c2ea994c2c44b6caa2a1e6e5d3b2d55\n\n" +
            "\"Antique Camera\"\n" +
            "https://sketchfab.com/3d-models/antique-camera-1f187dbc50024d1982e9d87e44aecff7\n\n" +
            "\"Cake Display\"\n" +
            "https://sketchfab.com/3d-models/cake-display-d62b42a8943a48f0bc0ffe9481b885dc";

        /// <summary>Shared credits panel, also used by the pause menu.</summary>
        public static GameObject OpenCreditsPanel(System.Action onClose)
        {
            var canvas = UiFactory.CreateOverlayCanvas("BCaT_Credits", 31000);
            var panel = UiFactory.CreateCenterPanel(canvas.transform, "Panel", new Vector2(820, 700));
            var column = UiFactory.CreateColumn(panel, "Column", 12f);
            // Let this column honor the preferred heights below.  The stock menu
            // columns are intentionally fixed-height, which is unsuitable for a
            // scroll viewport and wrapped attribution text.
            column.GetComponent<VerticalLayoutGroup>().childControlHeight = true;
            ConfigureSingleLine(UiFactory.CreateLabel(column, Application.productName, 30f));
            var productDetails = UiFactory.CreateLabel(column,
                $"{Application.companyName}\nVersion {Application.version}", 22f);
            var detailsLayout = productDetails.GetComponent<LayoutElement>();
            float detailsHeight = 22f * UiFactory.TextScale * 3.2f;
            detailsLayout.minHeight = detailsHeight;
            detailsLayout.preferredHeight = detailsHeight;

            // The attribution list is deliberately scrollable: its TMP label needs to
            // report its wrapped preferred height instead of inheriting CreateLabel's
            // one-line default.  Without this content hierarchy, it draws through the
            // other column children when the list grows beyond the panel.
            var viewport = UiFactory.CreateRect(column, "CreditsViewport");
            var viewportLayout = viewport.gameObject.AddComponent<LayoutElement>();
            viewportLayout.minHeight = 1f;
            viewportLayout.preferredHeight = 390f;
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            var content = UiFactory.CreateRect(viewport, "CreditsContent");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;

            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(24, 24, 20, 20);
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            var credits = UiFactory.CreateLabel(content, CreditsAttributionText, 20f,
                TMPro.TextAlignmentOptions.TopLeft);
            credits.textWrappingMode = TMPro.TextWrappingModes.Normal;
            credits.overflowMode = TMPro.TextOverflowModes.Overflow;
            var creditsLayout = credits.GetComponent<LayoutElement>();
            creditsLayout.minHeight = -1f;
            creditsLayout.preferredHeight = -1f;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 8f;

            var close = UiFactory.CreateButton(column, "Close", () =>
            {
                Destroy(canvas.gameObject);
                onClose?.Invoke();
            });
            UiFactory.SelectForKeyboard(close);
            return canvas.gameObject;
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
            Diagnostics.MemTrace.Mark("MAIN_SCENE_REQUESTED_FROM_MENU", // BCAT_MEMTRACE
                $"dest={ResetService.MainSceneName}");
            SceneManager.LoadSceneAsync(ResetService.LoadingSceneName, LoadSceneMode.Single);
        }

        static void ConfigureSingleLine(TMPro.TMP_Text label)
        {
            if (label == null)
                return;

            label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            label.overflowMode = TMPro.TextOverflowModes.Ellipsis;
        }

        void OnDestroy()
        {
            InteractionState.Unblock(this);
        }
    }
}
