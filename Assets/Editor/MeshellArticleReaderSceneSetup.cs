using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class MeshellArticleReaderSceneSetup
{
    private const string ScenePath = "Assets/BH_XR_MainScene.unity";
    private const string NotePadsPath = "_SceneContent/ImplementedContributorInstallations/Meshell_Sturgis/NotePads";
    private const string ReaderName = "MeshellArticleReaderPopup";

    [MenuItem("BCaT/Meshell/Setup Article Reader")]
    public static void Setup()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        GameObject notePads = GameObject.Find(NotePadsPath);
        if (notePads == null)
            throw new System.InvalidOperationException($"Missing {NotePadsPath}");

        RemoveComponentByClassName(notePads, "ArticleLinkCollectionLauncher");
        MeshellArticleReaderController reader = CreateOrUpdateReader(notePads.transform);
        ConfigurePrompt(notePads.transform);
        ConfigureNotebookCollection(notePads, reader);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Meshell article reader setup complete.");
    }

    [MenuItem("BCaT/Meshell/Setup Notebook Interaction Only")]
    public static void SetupNotebookInteractionOnly()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        GameObject notePads = GameObject.Find(NotePadsPath);
        if (notePads == null)
            throw new System.InvalidOperationException($"Missing {NotePadsPath}");

        MeshellArticleReaderController reader = notePads.GetComponentInChildren<MeshellArticleReaderController>(true);
        if (reader == null)
            throw new System.InvalidOperationException($"Missing MeshellArticleReaderController under {NotePadsPath}");

        ConfigureNotebookCollection(notePads, reader);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Meshell notebook interaction setup complete.");
    }

    [MenuItem("BCaT/Meshell/Configure Article Page Textures")]
    public static void ConfigureArticlePageTextures()
    {
        ConfigurePageTextures("Assets/BCaT_assets/Meshell_Sturgis/articles/Pages");
        AssetDatabase.SaveAssets();
        Debug.Log("Meshell article page texture imports configured.");
    }

    private static void ConfigurePageTextures(string root)
    {
        foreach (string path in Directory.GetFiles(root, "*.png", SearchOption.AllDirectories))
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }
    }

    private static MeshellArticleReaderController CreateOrUpdateReader(Transform parent)
    {
        Transform existing = parent.Find(ReaderName);
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject, true);

        GameObject root = new GameObject(ReaderName, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        root.layer = LayerMask.NameToLayer("UI");

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.localPosition = Vector3.zero;
        rect.localRotation = Quaternion.Euler(0f, 180f, 0f);
        rect.localScale = Vector3.one * 0.0015f;
        rect.sizeDelta = new Vector2(1240f, 860f);

        Canvas canvas = GetOrAdd<Canvas>(root);
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;
        GetOrAdd<CanvasScaler>(root);
        GetOrAdd<GraphicRaycaster>(root);
        GetOrAdd<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>(root);

        Image background = CreateImage(root.transform, "Background", new Vector2(1240f, 860f), Vector2.zero, new Color(0.055f, 0.05f, 0.045f, 0.97f));
        background.raycastTarget = true;

        RectTransform header = CreatePanel(root.transform, "Header", new Vector2(1120f, 120f), new Vector2(0f, 340f));
        TMP_Text title = CreateText(header, "ArticleTitleText", new Vector2(735f, 56f), new Vector2(-180f, 18f), 34f, TextAlignmentOptions.Left);
        TMP_Text authorYear = CreateText(header, "AuthorYearText", new Vector2(520f, 34f), new Vector2(-287f, -32f), 23f, TextAlignmentOptions.Left);
        Button close = CreateButton(header, "CloseButton", "Close", new Vector2(130f, 48f), new Vector2(495f, 20f));

        RectTransform pageArea = CreatePanel(root.transform, "PageArea", new Vector2(1120f, 580f), new Vector2(0f, 15f));
        Image page = CreateImage(pageArea, "PageImage", new Vector2(444f, 655f), new Vector2(0f, -10f), Color.white);
        page.preserveAspect = false;
        page.raycastTarget = false;
        TMP_Text pageNumber = CreateText(pageArea, "PageNumberText", new Vector2(260f, 36f), new Vector2(0f,-355f), 22f, TextAlignmentOptions.Center);

        RectTransform footer = CreatePanel(root.transform, "Footer", new Vector2(1120f, 100f), new Vector2(0f, -365f));
        Button previousArticle = CreateButton(footer, "PreviousArticleButton", "Previous Article", new Vector2(190f, 50f), new Vector2(-465f, 0f));
        Button nextArticle = CreateButton(footer, "NextArticleButton", "Next Article", new Vector2(170f, 50f), new Vector2(-255f, 0f));
        Button previousPage = CreateButton(footer, "PreviousPageButton", "Previous Page", new Vector2(175f, 50f), new Vector2(245f, 0f));
        Button nextPage = CreateButton(footer, "NextPageButton", "Next Page", new Vector2(155f, 50f), new Vector2(430f, 0f));

        MeshellArticleReaderController controller = GetOrAdd<MeshellArticleReaderController>(root);
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("popupRoot").objectReferenceValue = root;
        so.FindProperty("popupCanvas").objectReferenceValue = canvas;
        so.FindProperty("pageImage").objectReferenceValue = page;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("authorYearText").objectReferenceValue = authorYear;
        so.FindProperty("pageNumberText").objectReferenceValue = pageNumber;
        so.FindProperty("previousPageButton").objectReferenceValue = previousPage;
        so.FindProperty("nextPageButton").objectReferenceValue = nextPage;
        so.FindProperty("previousArticleButton").objectReferenceValue = previousArticle;
        so.FindProperty("nextArticleButton").objectReferenceValue = nextArticle;
        so.FindProperty("closeButton").objectReferenceValue = close;

        SerializedProperty articles = so.FindProperty("articles");
        articles.arraySize = 3;
        SetArticle(articles.GetArrayElementAtIndex(0), "A Tesseract for Art & Scholarship", "Meshell Sturgis", "2021", "Assets/BCaT_assets/Meshell_Sturgis/articles/Pages/Tesseract");
        SetArticle(articles.GetArrayElementAtIndex(1), "Black Refractions: Holed Up and Held Down", "Meshell Sturgis", "2021", "Assets/BCaT_assets/Meshell_Sturgis/articles/Pages/BlackRefractions");
        SetArticle(articles.GetArrayElementAtIndex(2), "Getting Home: With Communication, Love, and Practice", "Meshell Sturgis", "2021", "Assets/BCaT_assets/Meshell_Sturgis/articles/Pages/GettingHome");
        so.ApplyModifiedPropertiesWithoutUndo();

        root.SetActive(false);
        return controller;
    }

    private static void SetArticle(SerializedProperty article, string title, string author, string year, string folder)
    {
        article.FindPropertyRelative("title").stringValue = title;
        article.FindPropertyRelative("author").stringValue = author;
        article.FindPropertyRelative("year").stringValue = year;

        List<Texture2D> pages = new List<Texture2D>();
        foreach (string path in Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly))
            pages.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(path));

        pages.Sort((a, b) => string.CompareOrdinal(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b)));

        SerializedProperty pageList = article.FindPropertyRelative("pages");
        pageList.arraySize = pages.Count;
        for (int i = 0; i < pages.Count; i++)
            pageList.GetArrayElementAtIndex(i).objectReferenceValue = pages[i];
    }

    private static void ConfigurePrompt(Transform notePads)
    {
        PlatformInteractionPrompt prompt = notePads.Find("Canvas/PromptText")?.GetComponent<PlatformInteractionPrompt>();
        if (prompt == null)
            return;

        SerializedObject so = new SerializedObject(prompt);
        so.FindProperty("textAfterVerb").stringValue = " to read this article.";
        so.FindProperty("fullDesktopText").stringValue = "Press E to read this article.";
        so.FindProperty("fullXRText").stringValue = "Interact to read this article.";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureNotebookCollection(GameObject notePads, MeshellArticleReaderController reader)
    {
        RemoveIfPresent<MeshellArticleNotebookInputRouter>(notePads);

        MeshellArticleNotebookOpener opener = GetOrAdd<MeshellArticleNotebookOpener>(notePads);
        SerializedObject openerObject = new SerializedObject(opener);
        openerObject.FindProperty("reader").objectReferenceValue = reader;
        openerObject.ApplyModifiedPropertiesWithoutUndo();

        LindaLeaksPanelOpener panelOpener = GetOrAdd<LindaLeaksPanelOpener>(notePads);
        SerializedObject panelOpenerObject = new SerializedObject(panelOpener);
        panelOpenerObject.FindProperty("target").enumValueIndex = 2;
        panelOpenerObject.FindProperty("videoPopUp").objectReferenceValue = null;
        panelOpenerObject.FindProperty("photoAlbum").objectReferenceValue = null;
        panelOpenerObject.FindProperty("meshellArticleReader").objectReferenceValue = opener;
        panelOpenerObject.FindProperty("playerCamera").objectReferenceValue = null;
        panelOpenerObject.FindProperty("interactionDistance").floatValue = 8f;
        panelOpenerObject.ApplyModifiedPropertiesWithoutUndo();

        // The visible notebooks are paper-thin meshes lying flat on the study table,
        // and their per-child MeshColliders relied on a proxy mesh reference that
        // does not resolve at load time (sharedMesh == null, zero-size bounds).
        // Replace them with one parent BoxCollider covering the stack, matching the
        // proven Linda Leaks photo album structure (parent collider + kinematic body).
        Bounds notebookBounds = default;
        bool hasBounds = false;
        foreach (string childName in new[] { "Notepad", "Notepad (1)", "Notepad (2)" })
        {
            Transform notebook = notePads.transform.Find(childName);
            if (notebook == null)
                continue;

            RemoveIfPresent<MeshellArticleNotebookOpener>(notebook.gameObject);
            RemoveIfPresent<XRSimpleInteractable>(notebook.gameObject);

            MeshCollider childCollider = notebook.GetComponent<MeshCollider>();
            if (childCollider != null)
            {
                PrefabUtility.RevertObjectOverride(childCollider, InteractionMode.AutomatedAction);
                Object.DestroyImmediate(childCollider, true);
            }

            Renderer renderer = notebook.GetComponent<Renderer>();
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                notebookBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                notebookBounds.Encapsulate(renderer.bounds);
            }
        }

        BoxCollider box = GetOrAdd<BoxCollider>(notePads);
        box.isTrigger = true;
        if (hasBounds)
        {
            // Tight fit around the stack with a little aim tolerance above it, kept
            // clear of the security monitor (east) and the wall (south).
            const float padSides = 0.08f;
            const float padBelow = 0.04f;
            const float padAbove = 0.3f;

            Vector3 worldCenter = notebookBounds.center + Vector3.up * ((padAbove - padBelow) * 0.5f);
            Vector3 worldSize = notebookBounds.size + new Vector3(padSides * 2f, padAbove + padBelow, padSides * 2f);
            Vector3 lossyScale = notePads.transform.lossyScale;

            box.center = notePads.transform.InverseTransformPoint(worldCenter);
            box.size = new Vector3(worldSize.x / lossyScale.x, worldSize.y / lossyScale.y, worldSize.z / lossyScale.z);
            Debug.Log($"NotePads BoxCollider center={box.center:F3} size={box.size:F3} (world center={worldCenter:F3} size={worldSize:F3}).");
        }

        Rigidbody body = GetOrAdd<Rigidbody>(notePads);
        body.isKinematic = true;
        body.useGravity = false;

        XRSimpleInteractable interactable = GetOrAdd<XRSimpleInteractable>(notePads);
        SerializedObject interactableObject = new SerializedObject(interactable);
        SerializedProperty colliders = interactableObject.FindProperty("m_Colliders");
        colliders.arraySize = 1;
        colliders.GetArrayElementAtIndex(0).objectReferenceValue = box;
        interactableObject.ApplyModifiedPropertiesWithoutUndo();

        UnityEventTools.RemovePersistentListener(interactable.selectEntered, opener.Open);
        UnityEventTools.RemovePersistentListener(interactable.selectEntered, panelOpener.Open);
        UnityEventTools.AddPersistentListener(interactable.selectEntered, panelOpener.Open);
    }

    private static RectTransform CreatePanel(Transform parent, string name, Vector2 size, Vector2 position)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static Image CreateImage(Transform parent, string name, Vector2 size, Vector2 position, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = CreateChildRect(go, parent, size, position);

        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateText(Transform parent, string name, Vector2 size, Vector2 position, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        CreateChildRect(go, parent, size, position);

        TMP_Text text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.95f, 0.9f, 0.82f, 1f);
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 size, Vector2 position)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        CreateChildRect(go, parent, size, position);

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.18f, 0.13f, 0.1f, 1f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        TMP_Text text = CreateText(go.transform, "Label", size, Vector2.zero, 19f, TextAlignmentOptions.Center);
        text.text = label;
        text.color = Color.white;
        return button;
    }

    private static RectTransform CreateChildRect(GameObject go, Transform parent, Vector2 size, Vector2 position)
    {
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static void RemoveIfPresent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component != null)
            Object.DestroyImmediate(component, true);
    }

    private static void RemoveComponentByClassName(GameObject gameObject, string className)
    {
        foreach (Component component in gameObject.GetComponents<Component>())
        {
            if (component == null || component.GetType().Name == className)
                Object.DestroyImmediate(component, true);
        }
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }
}
