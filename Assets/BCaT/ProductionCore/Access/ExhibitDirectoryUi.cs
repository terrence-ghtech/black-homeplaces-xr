using System;
using BCaT.Production.Shell;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BCaT.Production.Access
{
    /// <summary>
    /// Visitor-facing exhibit directory. The curatorial titles and room names are
    /// intentionally authored here rather than inferred from scene objects. This
    /// keeps Unity, prefab, and component identifiers out of the visitor UI.
    /// </summary>
    public static class ExhibitDirectoryUi
    {
        sealed class Room
        {
            public readonly string name;
            public readonly string[] projectTitles;

            public Room(string name, params string[] projectTitles)
            {
                this.name = name;
                this.projectTitles = projectTitles;
            }
        }

        // This is curatorial display data, not a runtime inventory. Add or move
        // projects here when the visitor-facing exhibition plan changes.
        static readonly Room[] Directory =
        {
            new Room("Front Yard",
                "Black Homeplace as a Blueprint for Privacy Law",
                "Black Homeplace Project Overview",
                "Smile: Subject(ed) to Recognition"),
            new Room("Hallway",
                "Adinkrahene",
                "Gye Nyame",
                "Sankofa",
                "Nine Night and Good Mourning",
                "The Black Family Museum & Archive"),
            new Room("Living Room",
                "Black Parlors",
                "Duppy Know Who Fi Frighten",
                "Deja Vudu Radio",
                "Rhythm and Rope"),
            new Room("Sewing Room",
                "You Don’t Know About Style, My Darling",
                "In My Sisters Room",
                "Deja Vudu Radio",
                "Rhythm and Rope"),
            new Room("Dining Room",
                "Funtunfunefu Denkyemfunefu",
                "Cooperative Hall of Fame",
                "Linda Leaks housing Co-op map",
                "Rhythm and Rope"),
            new Room("Main Kitchen",
                "Such Lovely Gravy",
                "Ancestor Critical Fabulation",
                "My Aunt Pat's House",
                "Renovated Kitchen",
                "My Grandma's Recipes",
                "Homed: Recipes for Survival"),
            new Room("Upstairs",
                "And That Is the Truth – You Know What I’m Meaning",
                "Housing Co-op Archive",
                "Smile: Subject(ed) to Recognition",
                "Research Papers",
                "Home is where the art is",
                "sugars.flute.loops",
                "Black Homeplaces Community Mural",
                "Nsaa"),
            new Room("Backyard",
                "The Kingsley Women’s home",
                "BTMMP: Telling the Story of Murals",
                "My Grandma’s Garden"),
            new Room("Black Kitchen",
                "Explore the Black Kitchen")
        };

        public static GameObject Open(Action onClose, Action closePauseMenu = null)
        {
            var canvas = UiFactory.CreateOverlayCanvas("BCaT_ExhibitDirectory", 31600);
            var panel = UiFactory.CreateCenterPanel(canvas.transform, "Panel", new Vector2(1000, 860));
            var column = UiFactory.CreateColumn(panel, "Column", 10f);

            UiFactory.CreateLabel(column, "EXHIBIT DIRECTORY", 32f);
            UiFactory.CreateLabel(column,
                SceneManager.GetActiveScene().name == SceneTransitionState.BlackKitchenSceneName
                    ? "You are in: the Black Kitchen"
                    : "You are in: the main house", 20f);

            // Keep the directory usable when its authored contents exceed the panel.
            var viewport = UiFactory.CreateRect(column, "Viewport");
            viewport.sizeDelta = new Vector2(0, 520);
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.35f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            var content = UiFactory.CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.padding = new RectOffset(18, 18, 12, 12);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 8;

            PopulateContent(content);

            var footer = UiFactory.CreateRect(column, "Footer");
            footer.sizeDelta = new Vector2(0, 70);
            var footerLayout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 20;
            footerLayout.childControlWidth = true;
            footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = true;

            UiFactory.CreateButton(footer, "Return to Main Entrance", () =>
            {
                UnityEngine.Object.Destroy(canvas.gameObject);
                onClose?.Invoke();
                closePauseMenu?.Invoke();
                ResetService.ReturnToMainEntrance();
            });
            var close = UiFactory.CreateButton(footer, "Close", () =>
            {
                UnityEngine.Object.Destroy(canvas.gameObject);
                onClose?.Invoke();
            });
            UiFactory.SelectForKeyboard(close);

            return canvas.gameObject;
        }

        /// <summary>
        /// Populates an existing scroll-content transform with the curated,
        /// visitor-facing room and project list. The desktop overlay and Quest
        /// in-headset menu therefore share one source of truth.
        /// </summary>
        public static void PopulateContent(Transform content)
        {
            if (content == null)
                return;

            foreach (var room in Directory)
            {
                CreateRoomHeading(content, room.name);
                foreach (var projectTitle in room.projectTitles)
                    UiFactory.CreateLabel(content, projectTitle, 21f, TMPro.TextAlignmentOptions.Center);
            }
        }

        static void CreateRoomHeading(Transform parent, string roomName)
        {
            var heading = UiFactory.CreateRect(parent, "Room_" + roomName.Replace(" ", string.Empty));
            float height = 48f * UiFactory.TextScale;
            heading.sizeDelta = new Vector2(0, height);
            var layout = heading.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            heading.gameObject.AddComponent<Image>().color = new Color(0.28f, 0.23f, 0.16f, 0.9f);

            var label = UiFactory.CreateLabel(heading, roomName, 25f, TMPro.TextAlignmentOptions.Center);
            label.fontStyle = TMPro.FontStyles.Bold;
            label.color = UiFactory.HighContrast ? Color.white : new Color(1f, 0.9f, 0.55f, 1f);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }
    }
}
