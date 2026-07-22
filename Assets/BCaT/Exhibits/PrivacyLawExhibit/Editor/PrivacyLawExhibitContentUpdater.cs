using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PrivacyLawExhibitContentUpdater
{
    private const string RootName = "PrivacyLawExhibit_ROOT";
    private const string ScenePath = "Assets/BH_XR_MainScene.unity";
    private const string PrefabPath = "Assets/BCaT/Exhibits/PrivacyLawExhibit/Prefabs/PrivacyLawExhibit.prefab";
    private const string TexturePath = "Assets/BCaT/Exhibits/PrivacyLawExhibit/Textures/";

    private const string ExhibitTitleLine1 = "Inside, Outside, and In Between";
    private const string ExhibitTitleLine2 = "Black Homeplace as a Blueprint for Privacy Law";
    private const string AuthorCredit = "Nina-Simone Edwards";

    private const string Paragraph1 = "The current American legal structure facilitates 2 zones: zone 1 for the outside and zone 2 for the inside. Zone 1 is always open for police and other law enforcement officers: officers may use their own vision to see things in plain view and may even surveil outside and around the home. Getting to zone 2 is a bit harder: to be allowed into zone 2, there must be judicial approval, where police officers justify their need to enter the zone. These 2 zones are a limiting binary structure.";
    private const string Paragraph2 = "This blueprint is primarily used to reorient the conception of privacy and the homeplace. Recognizing that the home is more than just a home, but for many Black Americans, a homeplace that serves as a place of refuge, belonging, and a brave space for cultivating resistance, calls for a critical reorientation of what privacy means in the homeplace. Privacy should not be conceptualized as a binary that divides experience into “inside” and “outside.” Although the distinction between inside and outside is crucial, there are many zones of privacy within the homeplace that must be considered.";
    private const string Paragraph3 = "When these distinct zones are ignored, privacy is violated on multiple levels. In Figure 3, the footprints represent more than a single intrusion. The first pair marks the breach the law recognizes, but the others reveal the deeper wounds—those that constrict a person’s ability to feel safe, to create, and to heal within their own home. Even after the physical intrusion ends, the figure shows a lingering haze that settles over the space. The loss of privacy leaves the home stripped of safety and belonging, and the homeowner is left to rebuild from what remains.";
    private const string Paragraph4 = "For Black Americans, these intrusions cut deeper. In a world that so often perceives Black people as “other,” as lesser, or as a suspect before a neighbor, the homeplace becomes a refuge. It is where one can breathe, develop, create oneself, and craft a life shielded from prying eyes. So many Black people are forced to code-switch to survive: to adjust their speech, posture, or presence to meet the expectations of those who call the police for nonexistent infractions or judge based on long-held stereotypes. These paradigms, rooted deeply in the foundation of the United States, shape the daily realities of Black life. They can constrict in some moments and outright restrict in others.";
    private const string Paragraph5 = "The homeplace is the space where Black people can simply be–where memories are curated in the front room, where privacy is found in a bedroom, where living, without performance, is allowed. Intrusions into these multiple zones of privacy abruptly shatter that possibility.";

    [MenuItem("BCaT/Exhibits/Update Privacy Law Exhibit Content")]
    public static void UpdateContent()
    {
        ConfigureTexture("PrivacyLaw_Figure01.png");
        ConfigureTexture("PrivacyLaw_Figure02.png");
        ConfigureTexture("PrivacyLaw_Figure03.jpg");
        ConfigureTexture("PrivacyLaw_SourceBlueprint.png");

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        ApplyContent(prefabRoot);
        PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            Debug.LogError($"Could not find existing scene root: {RootName}");
            return;
        }

        PrefabUtility.RevertPrefabInstance(root, InteractionMode.AutomatedAction);
        ApplyContent(root);
        PrefabUtility.RecordPrefabInstancePropertyModifications(root);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Updated Privacy Law Exhibit content in prefab and existing scene instance.");
    }

    private static void ApplyContent(GameObject root)
    {
        Sprite figure01 = LoadSprite("PrivacyLaw_Figure01.png");
        Sprite figure02 = LoadSprite("PrivacyLaw_Figure02.png");
        Sprite figure03 = LoadSprite("PrivacyLaw_Figure03.jpg");

        Transform expanded = root.transform.Find("ExpandedExhibit");
        ConfigureExpandedCanvas(expanded);
        Transform header = expanded.Find("Header");
        ClearChildren(header);
        TMP_Text title1 = CreateText(header, "ExhibitTitle", ExhibitTitleLine1, new Vector2(20f, 22f), new Vector2(760f, 34f), 24, TextAlignmentOptions.Left);
        title1.fontStyle = FontStyles.Bold;
        CreateText(header, "ExhibitSubtitle", ExhibitTitleLine2, new Vector2(20f, -8f), new Vector2(760f, 28f), 19, TextAlignmentOptions.Left);
        TMP_Text author = CreateText(header, "AuthorCredit", AuthorCredit, new Vector2(20f, -36f), new Vector2(760f, 24f), 16, TextAlignmentOptions.Left);
        author.color = new Color(0.71f, 0.88f, 1f, 1f);

        ConfigurePage(root.transform.Find("ExpandedExhibit/ContentArea/Page_01"), "The 2 Legally Recognized Zones", figure01, "Figure 1. The 2 legally recognized zones", Paragraph1, false);
        ConfigurePage(root.transform.Find("ExpandedExhibit/ContentArea/Page_02"), "The Family’s Three-Bedroom Home", figure02, "Figure 2. The family’s three bedroom home", Paragraph2, false);
        ScrollRect page03Scroll = ConfigurePage(root.transform.Find("ExpandedExhibit/ContentArea/Page_03"), "The Haze", figure03, "Figure 3. The haze", Paragraph3 + "\n\n" + Paragraph4 + "\n\n" + Paragraph5, true);

        PrivacyLawExhibitController controller = root.GetComponentInChildren<PrivacyLawExhibitController>(true);
        if (controller != null)
        {
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("page03ScrollRect").objectReferenceValue = page03Scroll;
            so.FindProperty("expandedCanvas").objectReferenceValue = expanded.GetComponent<Canvas>();
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        ConfigureRaycastTargets(root);
    }

    private static ScrollRect ConfigurePage(Transform page, string title, Sprite figure, string caption, string body, bool scrollBody)
    {
        ClearChildren(page);
        RectTransform pageRect = page.GetComponent<RectTransform>();
        pageRect.anchoredPosition = new Vector2(205f, -6f);
        pageRect.sizeDelta = new Vector2(850f, 560f);

        TMP_Text pageTitle = CreateText(page, "PageTitle", title, new Vector2(0f, 255f), new Vector2(820f, 38f), 24, TextAlignmentOptions.Left);
        pageTitle.fontStyle = FontStyles.Bold;

        Image image = CreateImage(page, "Figure", new Vector2(0f, 108f), new Vector2(800f, 270f), new Color(1f, 1f, 1f, 1f));
        image.sprite = figure;
        image.preserveAspect = true;
        image.type = Image.Type.Simple;

        TMP_Text cap = CreateText(page, "FigureCaption", caption, new Vector2(0f, -45f), new Vector2(800f, 28f), 16, TextAlignmentOptions.Left);
        cap.color = new Color(0.71f, 0.88f, 1f, 1f);

        if (!scrollBody)
        {
            TMP_Text paragraph = CreateText(page, "BodyText", body, new Vector2(0f, -168f), new Vector2(800f, 205f), 18, TextAlignmentOptions.TopLeft);
            paragraph.textWrappingMode = TextWrappingModes.Normal;
            paragraph.lineSpacing = 7f;
            return null;
        }

        ScrollRect scroll = CreateScrollArea(page, body);
        scroll.verticalNormalizedPosition = 1f;
        return scroll;
    }

    private static ScrollRect CreateScrollArea(Transform page, string body)
    {
        Image viewportImage = CreateImage(page, "Page03TextScroll", new Vector2(0f, -170f), new Vector2(800f, 210f), new Color(0.01f, 0.07f, 0.12f, 0.56f));
        Mask mask = viewportImage.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        GameObject content = new GameObject("ScrollContent", typeof(RectTransform));
        content.transform.SetParent(viewportImage.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = new Vector2(0f, -8f);
        contentRect.sizeDelta = new Vector2(-28f, 520f);

        TMP_Text paragraph = CreateText(content.transform, "BodyText", body, new Vector2(0f, -230f), new Vector2(748f, 500f), 17, TextAlignmentOptions.TopLeft);
        paragraph.textWrappingMode = TextWrappingModes.Normal;
        paragraph.lineSpacing = 6f;

        Image scrollbarBg = CreateImage(page, "Page03Scrollbar", new Vector2(414f, -170f), new Vector2(14f, 210f), new Color(0.03f, 0.16f, 0.25f, 0.72f));
        Scrollbar scrollbar = scrollbarBg.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        Image handle = CreateImage(scrollbarBg.transform, "Handle", Vector2.zero, new Vector2(12f, 60f), new Color(0.43f, 0.78f, 1f, 0.86f));
        scrollbar.handleRect = handle.rectTransform;
        scrollbar.targetGraphic = handle;

        ScrollRect scroll = viewportImage.gameObject.AddComponent<ScrollRect>();
        scroll.content = contentRect;
        scroll.viewport = viewportImage.rectTransform;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scroll.verticalNormalizedPosition = 1f;
        return scroll;
    }

    private static void ConfigureExpandedCanvas(Transform expanded)
    {
        Canvas canvas = expanded.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
        }

        expanded.localScale = Vector3.one * 0.00135f;

        if (expanded.GetComponent<GraphicRaycaster>() == null)
            expanded.gameObject.AddComponent<GraphicRaycaster>();

        AddComponentIfAvailable(expanded.gameObject, "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster");

        CanvasGroup group = expanded.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.interactable = true;
            group.blocksRaycasts = true;
        }
    }

    private static void ConfigureRaycastTargets(GameObject root)
    {
        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;

        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            if (button.targetGraphic != null)
                button.targetGraphic.raycastTarget = true;
            Image image = button.GetComponent<Image>();
            if (image != null)
                image.raycastTarget = true;
        }

        foreach (Scrollbar scrollbar in root.GetComponentsInChildren<Scrollbar>(true))
        {
            if (scrollbar.targetGraphic != null)
                scrollbar.targetGraphic.raycastTarget = true;
            Image image = scrollbar.GetComponent<Image>();
            if (image != null)
                image.raycastTarget = true;
            if (scrollbar.handleRect != null)
            {
                Image handle = scrollbar.handleRect.GetComponent<Image>();
                if (handle != null)
                    handle.raycastTarget = true;
            }
        }

        foreach (ScrollRect scrollRect in root.GetComponentsInChildren<ScrollRect>(true))
        {
            if (scrollRect.viewport != null)
            {
                Graphic viewportGraphic = scrollRect.viewport.GetComponent<Graphic>();
                if (viewportGraphic != null)
                    viewportGraphic.raycastTarget = true;
            }
        }
    }

    private static Image CreateImage(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateText(Transform parent, string name, string text, Vector2 pos, Vector2 size, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        TMP_Text tmp = go.GetComponent<TMP_Text>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = new Color(0.91f, 0.97f, 1f, 1f);
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.enableAutoSizing = false;
        return tmp;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }

    private static Sprite LoadSprite(string fileName)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturePath + fileName);
        if (sprite == null)
            Debug.LogError($"Missing Privacy Law sprite: {TexturePath + fileName}");
        return sprite;
    }

    private static void ConfigureTexture(string fileName)
    {
        string path = TexturePath + fileName;
        if (!File.Exists(path))
        {
            Debug.LogError($"Missing Privacy Law texture: {path}");
            return;
        }

        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = fileName.EndsWith(".png");
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static Component AddComponentIfAvailable(GameObject target, string fullTypeName)
    {
        if (target.GetComponent(fullTypeName) != null)
            return target.GetComponent(fullTypeName);

        foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type type = assembly.GetType(fullTypeName);
            if (type == null || !typeof(Component).IsAssignableFrom(type))
                continue;

            Component existing = target.GetComponent(type);
            return existing != null ? existing : target.AddComponent(type);
        }

        return null;
    }
}
