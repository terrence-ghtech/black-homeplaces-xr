using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BCaT.Production.Shell
{
    /// <summary>
    /// Desktop pause-menu page describing the Black Homeplaces project.
    /// It is a child of the pause menu, so opening and closing it deliberately
    /// leaves the pause/menu interaction block in place.
    /// </summary>
    public static class AboutBlackHomeplacesUi
    {
        public const string BodyCopy =
            "Black Homeplaces is an inter-institutional, collaborative research project centring diasporic placemaking. Bringing together 30 collaborators and 17 projects from across the United States, South and Central America, the Caribbean, the United Kingdom, and West Africa, Black Homeplaces explores overlaps in Black domestic experiences across the diaspora.\n\n" +
            "Multimodal outputs including video games, soundscapes, oral histories, artwork, storymaps and 3D scanned artefacts are housed within this virtual reality (VR) installation of an archetypical Black home. The eclectic furnishings of the house reflect tension between notions of home as personal, familial and intimate; versus generic experience characterised by shared cultural objects and inherited rituals—such as glass cabinet display cases, religious iconography and plastic-covered sofas. The Black home is a site of memory, migration histories, and Afro-futurity that collapses a critically fabulated past and an imagined future.\n\n" +
            "Black Homeplaces prioritises a collaborative, counter-institutional archival praxis. We offer the home as a living archive, reflecting historic and ongoing flows of migration that locate ‘home’ with respect to where we are now, and where we have come from.\n\n" +
            "This software has been developed with endless thanks to GHTech Inc.";

        public static GameObject Open(Action onClose)
        {
            var canvas = UiFactory.CreateOverlayCanvas("BCaT_AboutBlackHomeplaces", 31600);
            var panel = UiFactory.CreateCenterPanel(canvas.transform, "Panel", new Vector2(1000, 900));
            var column = UiFactory.CreateColumn(panel, "Column", 14f);

            UiFactory.CreateLabel(column, "About Black Homeplaces", 32f);

            var viewport = UiFactory.CreateRect(column, "Viewport");
            viewport.sizeDelta = new Vector2(0f, 650f);
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            var content = UiFactory.CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;

            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(28, 28, 22, 22);
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            var body = UiFactory.CreateLabel(content, BodyCopy, 22f, TextAlignmentOptions.TopLeft);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.overflowMode = TextOverflowModes.Overflow;
            var bodyLayout = body.GetComponent<LayoutElement>();
            bodyLayout.minHeight = -1f;
            bodyLayout.preferredHeight = -1f;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 8f;

            var close = UiFactory.CreateButton(column, "Close", () =>
            {
                UnityEngine.Object.Destroy(canvas.gameObject);
                onClose?.Invoke();
            });
            UiFactory.SelectForKeyboard(close);

            return canvas.gameObject;
        }
    }
}
