using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BCaT.Production.Shell
{
    /// <summary>
    /// Shared opening guidance for the first-landing overlay shown immediately
    /// after visitors enter the house.
    /// </summary>
    public static class OpeningInstructionsUi
    {
        const string Title = "Before You Explore";
        const string Intro = "A few tips before you move through the house.";

        static readonly Color Accent = new Color(0.86f, 0.68f, 0.32f, 1f);
        static readonly Color IconBack = new Color(0.02f, 0.018f, 0.014f, 0.65f);
        static readonly Color Divider = new Color(0.95f, 0.92f, 0.82f, 0.34f);
        static readonly Color MutedText = new Color(0.95f, 0.93f, 0.88f, 0.82f);

        static readonly InstructionItem[] DesktopControls =
        {
            new InstructionItem("WASD", "Move", "Use W, A, S, D or the arrow keys to move.", "OpeningOnboardingIcons/onboarding_move"),
            new InstructionItem("Mouse", "Look Around", "Move the mouse to look around.", "OpeningOnboardingIcons/onboarding_look"),
            new InstructionItem("E", "Interact", "Aim at an exhibit, then press E.", "OpeningOnboardingIcons/onboarding_interact"),
            new InstructionItem("Esc", "Return to Main Entrance", "Press Esc and choose Return to Main Entrance.", "OpeningOnboardingIcons/onboarding_return")
        };

        static readonly InstructionItem[] DesktopGuidance =
        {
            new InstructionItem("Nav", "Navigation", "Move through the house much like a first-person exhibit or game space."),
            new InstructionItem("Audio", "Audio", "Some exhibit audio is spatial, so voices or sounds may change as you get closer.")
        };

        static readonly InstructionItem[] QuestControls =
        {
            new InstructionItem("L", "Move", "Use the left thumbstick to move."),
            new InstructionItem("Head", "Look Around", "Turn your head to look. Use the right thumbstick to turn."),
            new InstructionItem("Grip", "Interact", "Point at an exhibit with a controller ray, then press grip to select."),
            InstructionItem.Spacer()
        };

        static readonly InstructionItem[] QuestGuidance =
        {
            new InstructionItem("Nav", "Navigation", "Walk through the house with smooth stick movement and natural head look."),
            new InstructionItem("UI", "Panels", "For menus and panels, point at the UI and press trigger."),
            new InstructionItem("Audio", "Audio", "Some exhibit audio is spatial, so voices or sounds may change as you get closer.")
        };

        public static RectTransform AddTo(Transform parent, bool quest, float fontSize = 20f, float preferredHeight = 500f)
        {
            float scaledHeight = preferredHeight * UiFactory.TextScale;
            InstructionItem[] controls = quest ? QuestControls : DesktopControls;
            InstructionItem[] guidance = quest ? QuestGuidance : DesktopGuidance;

            var root = UiFactory.CreateRect(parent, quest ? "OpeningInstructions_Quest" : "OpeningInstructions_Desktop");
            root.anchorMin = new Vector2(0f, 0.5f);
            root.anchorMax = new Vector2(1f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(0f, scaledHeight);

            var layout = root.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = scaledHeight;
            layout.preferredHeight = scaledHeight;
            layout.flexibleHeight = 0f;

            var stack = root.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.spacing = 16f;
            stack.childAlignment = TextAnchor.UpperLeft;
            stack.childControlWidth = true;
            stack.childControlHeight = false;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;

            var title = UiFactory.CreateLabel(root, Title, fontSize + 12f, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;

            var intro = UiFactory.CreateLabel(root, Intro, fontSize, TextAlignmentOptions.Left);
            intro.color = MutedText;
            intro.textWrappingMode = TextWrappingModes.Normal;

            var columns = UiFactory.CreateRect(root, "InstructionColumns");
            var columnsLayout = columns.gameObject.AddComponent<LayoutElement>();
            columnsLayout.minHeight = scaledHeight - 120f * UiFactory.TextScale;
            columnsLayout.preferredHeight = columnsLayout.minHeight;

            var horizontal = columns.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 28f;
            horizontal.childAlignment = TextAnchor.UpperCenter;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = true;
            horizontal.childForceExpandHeight = false;

            BuildColumn(columns, controls, fontSize, 560f, 1.15f, addAudioBodyGap: false);
            BuildDivider(columns, columnsLayout.preferredHeight);
            BuildColumn(columns, guidance, fontSize, 390f, 0.86f, addAudioBodyGap: !quest);

            return root;
        }

        static void BuildColumn(Transform parent, InstructionItem[] items, float fontSize,
            float preferredWidth, float widthWeight, bool addAudioBodyGap)
        {
            var column = UiFactory.CreateRect(parent, "InstructionColumn");
            var layout = column.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = preferredWidth;
            layout.flexibleWidth = widthWeight;
            layout.minWidth = preferredWidth * 0.72f;

            var stack = column.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.spacing = 17f;
            stack.childAlignment = TextAnchor.UpperLeft;
            stack.childControlWidth = true;
            stack.childControlHeight = false;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;

            foreach (var item in items)
                BuildItem(column, item, fontSize, addAudioBodyGap && item.Heading == "Audio");
        }

        static void BuildDivider(Transform parent, float height)
        {
            var divider = UiFactory.CreateRect(parent, "Divider");
            var layout = divider.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 2f;
            layout.preferredWidth = 2f;
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleWidth = 0f;

            var image = divider.gameObject.AddComponent<Image>();
            image.color = Divider;
            image.raycastTarget = false;
        }

        static void BuildItem(Transform parent, InstructionItem item, float fontSize, bool addBodyGap)
        {
            var row = UiFactory.CreateRect(parent, "Item_" + Sanitize(item.Heading));
            var rowLayout = row.gameObject.AddComponent<LayoutElement>();
            rowLayout.minHeight = 84f * UiFactory.TextScale;
            rowLayout.preferredHeight = rowLayout.minHeight;

            if (item.IsSpacer)
                return;

            var horizontal = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 18f;
            horizontal.childAlignment = TextAnchor.UpperLeft;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = false;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = false;

            BuildIcon(row, item.Icon, item.IconSpriteResource, fontSize);

            var textColumn = UiFactory.CreateRect(row, "Text");
            var textLayout = textColumn.gameObject.AddComponent<LayoutElement>();
            textLayout.preferredWidth = 420f;
            textLayout.flexibleWidth = 1f;
            textLayout.minWidth = 220f;

            var vertical = textColumn.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = addBodyGap ? 15f : 3f;
            vertical.childAlignment = TextAnchor.UpperLeft;
            vertical.childControlWidth = true;
            vertical.childControlHeight = false;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            var heading = UiFactory.CreateLabel(textColumn, item.Heading, fontSize + 2f, TextAlignmentOptions.Left);
            heading.fontStyle = FontStyles.Bold;

            var body = UiFactory.CreateLabel(textColumn, item.Body, fontSize - 1f, TextAlignmentOptions.Left);
            body.color = MutedText;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.lineSpacing = 2f;

            var bodyLayout = body.GetComponent<LayoutElement>();
            bodyLayout.minHeight = 42f * UiFactory.TextScale;
            bodyLayout.preferredHeight = bodyLayout.minHeight;
        }

        static void BuildIcon(Transform parent, string text, string spriteResource, float fontSize)
        {
            var icon = UiFactory.CreateRect(parent, "Icon_" + Sanitize(text));
            var layout = icon.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 58f;
            layout.preferredWidth = 58f;
            layout.minHeight = 58f;
            layout.preferredHeight = 58f;

            var image = icon.gameObject.AddComponent<Image>();
            image.color = IconBack;
            image.raycastTarget = false;

            if (!string.IsNullOrEmpty(spriteResource))
            {
                Sprite sprite = Resources.Load<Sprite>(spriteResource);
                if (sprite != null)
                {
                    var artworkRect = UiFactory.CreateRect(icon, "Artwork");
                    artworkRect.anchorMin = new Vector2(0.5f, 0.5f);
                    artworkRect.anchorMax = new Vector2(0.5f, 0.5f);
                    artworkRect.pivot = new Vector2(0.5f, 0.5f);
                    artworkRect.sizeDelta = IconArtworkSize(text);

                    var artwork = artworkRect.gameObject.AddComponent<Image>();
                    artwork.sprite = sprite;
                    artwork.color = UiFactory.TextColor;
                    artwork.preserveAspect = true;
                    artwork.raycastTarget = false;
                    return;
                }
            }

            var label = UiFactory.CreateLabel(icon, text, Mathf.Min(fontSize, 18f), TextAlignmentOptions.Center);
            label.color = Accent;
            label.fontStyle = FontStyles.Bold;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.enableAutoSizing = true;
            label.fontSizeMin = 8f;
            label.fontSizeMax = Mathf.Min(fontSize, 18f);
        }

        static Vector2 IconArtworkSize(string text)
        {
            if (text == "Mouse")
                return new Vector2(36f, 36f);
            if (text == "WASD")
                return new Vector2(40f, 40f);
            return new Vector2(38f, 38f);
        }

        static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Blank";

            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]))
                    chars[i] = '_';
            return new string(chars);
        }

        readonly struct InstructionItem
        {
            public readonly string Icon;
            public readonly string Heading;
            public readonly string Body;
            public readonly string IconSpriteResource;
            public readonly bool IsSpacer;

            public static InstructionItem Spacer() =>
                new InstructionItem(string.Empty, string.Empty, string.Empty, null, true);

            public InstructionItem(string icon, string heading, string body, string iconSpriteResource = null,
                bool isSpacer = false)
            {
                Icon = icon;
                Heading = heading;
                Body = body;
                IconSpriteResource = iconSpriteResource;
                IsSpacer = isSpacer;
            }
        }
    }
}
