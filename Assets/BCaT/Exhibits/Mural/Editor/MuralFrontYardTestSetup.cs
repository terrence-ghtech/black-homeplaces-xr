using System;
using System.IO;
using BCaT.Production.Interaction;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public static class MuralFrontYardTestSetup
{
    private const string ScenePath = "Assets/BH_XR_MainScene.unity";
    private const string RootName = "TEST_MuralExhibit_FrontYard";
    private const string ImagesPath = "Assets/BCaT/Exhibits/Mural/Images";
    private const string MaterialPath = "Assets/BCaT/Exhibits/Mural/Materials/MuralFinished_Unlit.mat";
    private const string VideoFileName = "Mural/11_mural_process_video.mp4";

    private static readonly Vector3 RootPosition = new(71.7f, 0f, 63.5f);
    private static readonly Vector3 RootEuler = new(0f, 180f, 0f);
    private static readonly Vector3 PanelLocalPosition = new(0f, 2.15f, 0f);
    private static readonly Vector3 PanelScale = new(5.6f, 3.15f, 0.08f);

    private static readonly string[] ImageNames =
    {
        "01_initial_wall_sketch.png",
        "02_color_maquette.jpg",
        "03_early_wall_blocking.jpg",
        "04_mid_process_wall.jpg",
        "05_participation_prompts.jpg",
        "06_workshop_materials.jpg",
        "07_artist_with_mural.jpg",
        "08_finished_detail_mirror.jpg",
        "09_finished_detail_flowers.jpg",
        "10_finished_mural_full.JPG",
    };

    [MenuItem("BCaT/Mural/Setup Front Yard Test Exhibit")]
    public static void Setup()
    {
        ConfigureTextureImports();
        AssetDatabase.ImportAsset("Assets/StreamingAssets/Mural/11_mural_process_video.mp4", ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing);

        Material muralMaterial = CreateOrUpdateMuralMaterial();
        GameObject root = new(RootName);
        root.transform.SetPositionAndRotation(RootPosition, Quaternion.Euler(RootEuler));
        Transform contentParent = GameObject.Find("_SceneContent")?.transform;
        if (contentParent != null)
            root.transform.SetParent(contentParent, true);

        GameObject panel = CreatePanel(root.transform, muralMaterial);
        MuralExhibitController controller = root.AddComponent<MuralExhibitController>();

        GameObject galleryRoot = CreateGallery(root.transform, out Canvas galleryCanvas, out Image imageDisplay,
            out RawImage videoDisplay, out AspectRatioFitter imageAspect, out AspectRatioFitter videoAspect,
            out TMP_Text titleText, out TMP_Text captionText, out TMP_Text counterText,
            out Button previousButton, out Button nextButton, out Button closeButton);

        VideoPlayer videoPlayer = galleryRoot.AddComponent<VideoPlayer>();
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;

        AudioSource videoAudio = galleryRoot.AddComponent<AudioSource>();
        videoAudio.playOnAwake = false;
        videoAudio.loop = false;

        ConfigureController(controller, panel.transform, panel.transform, galleryRoot, galleryCanvas, imageDisplay,
            videoDisplay, imageAspect, videoAspect, titleText, captionText, counterText, previousButton, nextButton,
            closeButton, videoPlayer, videoAudio);

        AddXrSelect(panel, controller);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[MuralFrontYardTestSetup] Created {RootName} at {RootPosition}, rotation {RootEuler}.");
    }

    private static void ConfigureTextureImports()
    {
        foreach (string name in ImageNames)
        {
            string path = $"{ImagesPath}/{name}";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new FileNotFoundException("Mural image missing or not importable: " + path);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = name.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
            importer.maxTextureSize = name == "10_finished_mural_full.JPG" ? 4096 : 2048;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }
    }

    private static Material CreateOrUpdateMuralMaterial()
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ImagesPath}/10_finished_mural_full.JPG");
        if (texture == null)
            throw new FileNotFoundException("Missing finished mural texture.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Texture")
                ?? Shader.Find("Standard");
            material = new Material(shader) { name = "MuralFinished_Unlit" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);

        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject CreatePanel(Transform parent, Material material)
    {
        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "MuralPanel_FinishedImage_Interactable";
        panel.transform.SetParent(parent, false);
        panel.transform.localPosition = PanelLocalPosition;
        panel.transform.localRotation = Quaternion.identity;
        panel.transform.localScale = PanelScale;

        Renderer renderer = panel.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return panel;
    }

    private static GameObject CreateGallery(
        Transform parent,
        out Canvas canvas,
        out Image imageDisplay,
        out RawImage videoDisplay,
        out AspectRatioFitter imageAspect,
        out AspectRatioFitter videoAspect,
        out TMP_Text titleText,
        out TMP_Text captionText,
        out TMP_Text counterText,
        out Button previousButton,
        out Button nextButton,
        out Button closeButton)
    {
        GameObject root = new("MuralGalleryModal", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        root.transform.SetParent(parent, false);
        root.SetActive(false);

        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 120;
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1280f, 820f);
        root.transform.localScale = Vector3.one * 0.0014f;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 12f;
        AddTrackedDeviceGraphicRaycasterIfAvailable(root);

        Image background = UiImage(root.transform, "Background", new Color(0.025f, 0.025f, 0.027f, 0.96f),
            Vector2.zero, new Vector2(1280f, 820f));

        RectTransform mediaFrame = ChildRect(background.transform, "MediaFrame",
            new Vector2(0f, 45f), new Vector2(1160f, 610f));
        UiImage(mediaFrame, "FrameBackground", new Color(0.02f, 0.02f, 0.022f, 1f),
            Vector2.zero, new Vector2(1160f, 610f));

        GameObject imageObject = new("ImageDisplay", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(AspectRatioFitter));
        imageObject.transform.SetParent(mediaFrame, false);
        Stretch(imageObject.GetComponent<RectTransform>(), new Vector2(18f, 18f));
        imageDisplay = imageObject.GetComponent<Image>();
        imageDisplay.color = Color.white;
        imageDisplay.preserveAspect = true;
        imageDisplay.raycastTarget = false;
        imageAspect = imageObject.GetComponent<AspectRatioFitter>();
        imageAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        imageAspect.aspectRatio = 16f / 9f;

        GameObject videoObject = new("VideoDisplay", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(RawImage), typeof(AspectRatioFitter));
        videoObject.transform.SetParent(mediaFrame, false);
        Stretch(videoObject.GetComponent<RectTransform>(), new Vector2(18f, 18f));
        videoDisplay = videoObject.GetComponent<RawImage>();
        videoDisplay.color = Color.white;
        videoDisplay.raycastTarget = false;
        videoAspect = videoObject.GetComponent<AspectRatioFitter>();
        videoAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        videoAspect.aspectRatio = 16f / 9f;

        titleText = Text(root.transform, "Title", new Vector2(-520f, 365f), new Vector2(760f, 52f),
            "Mural Process", 34, TextAlignmentOptions.Left);
        captionText = Text(root.transform, "Caption", new Vector2(-515f, -342f), new Vector2(760f, 70f),
            string.Empty, 22, TextAlignmentOptions.Left);
        counterText = Text(root.transform, "Counter", new Vector2(470f, -342f), new Vector2(180f, 52f),
            "1 / 11", 28, TextAlignmentOptions.Center);

        previousButton = Button(root.transform, "PreviousButton", new Vector2(-535f, -342f), new Vector2(74f, 54f), "<");
        nextButton = Button(root.transform, "NextButton", new Vector2(535f, -342f), new Vector2(74f, 54f), ">");
        closeButton = Button(root.transform, "CloseButton", new Vector2(585f, 364f), new Vector2(70f, 54f), "X");

        return root;
    }

    private static void ConfigureController(
        MuralExhibitController controller,
        Transform focus,
        Transform colliderRoot,
        GameObject galleryRoot,
        Canvas galleryCanvas,
        Image imageDisplay,
        RawImage videoDisplay,
        AspectRatioFitter imageAspect,
        AspectRatioFitter videoAspect,
        TMP_Text titleText,
        TMP_Text captionText,
        TMP_Text counterText,
        Button previousButton,
        Button nextButton,
        Button closeButton,
        VideoPlayer videoPlayer,
        AudioSource videoAudio)
    {
        SerializedObject so = new(controller);
        so.FindProperty("focusPoint").objectReferenceValue = focus;
        so.FindProperty("colliderRoot").objectReferenceValue = colliderRoot;
        so.FindProperty("interactionDistance").floatValue = 4.5f;
        so.FindProperty("maxViewAngle").floatValue = 18f;
        so.FindProperty("worldPromptText").objectReferenceValue = null;

        SerializedProperty prompt = so.FindProperty("prompt");
        prompt.FindPropertyRelative("desktopPrompt").stringValue = "Press E";
        prompt.FindPropertyRelative("xrPrompt").stringValue = "Interact";
        prompt.FindPropertyRelative("verb").enumValueIndex = (int)SharedInteractionVerb.View;
        prompt.FindPropertyRelative("objectName").stringValue = "mural";

        so.FindProperty("galleryRoot").objectReferenceValue = galleryRoot;
        so.FindProperty("galleryCanvas").objectReferenceValue = galleryCanvas;
        so.FindProperty("imageDisplay").objectReferenceValue = imageDisplay;
        so.FindProperty("videoDisplay").objectReferenceValue = videoDisplay;
        so.FindProperty("imageAspect").objectReferenceValue = imageAspect;
        so.FindProperty("videoAspect").objectReferenceValue = videoAspect;
        so.FindProperty("titleText").objectReferenceValue = titleText;
        so.FindProperty("captionText").objectReferenceValue = captionText;
        so.FindProperty("counterText").objectReferenceValue = counterText;
        so.FindProperty("previousButton").objectReferenceValue = previousButton;
        so.FindProperty("nextButton").objectReferenceValue = nextButton;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.FindProperty("openDistanceFromCamera").floatValue = 1.75f;
        so.FindProperty("videoPlayer").objectReferenceValue = videoPlayer;
        so.FindProperty("videoAudioSource").objectReferenceValue = videoAudio;
        so.FindProperty("prepareTimeoutSeconds").floatValue = 20f;

        SerializedProperty items = so.FindProperty("items");
        items.arraySize = 11;
        for (int i = 0; i < ImageNames.Length; i++)
        {
            SerializedProperty item = items.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("type").enumValueIndex = (int)MuralExhibitController.GalleryItemType.Image;
            item.FindPropertyRelative("displayName").stringValue = Path.GetFileNameWithoutExtension(ImageNames[i]).Replace('_', ' ');
            item.FindPropertyRelative("caption").stringValue = "";
            item.FindPropertyRelative("image").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>($"{ImagesPath}/{ImageNames[i]}");
            item.FindPropertyRelative("videoFileName").stringValue = "";
            item.FindPropertyRelative("videoUrlOverride").stringValue = "";
            item.FindPropertyRelative("editorPreviewClip").objectReferenceValue = null;
        }

        SerializedProperty videoItem = items.GetArrayElementAtIndex(10);
        videoItem.FindPropertyRelative("type").enumValueIndex = (int)MuralExhibitController.GalleryItemType.Video;
        videoItem.FindPropertyRelative("displayName").stringValue = "11 mural process video";
        videoItem.FindPropertyRelative("caption").stringValue = "";
        videoItem.FindPropertyRelative("image").objectReferenceValue = null;
        videoItem.FindPropertyRelative("videoFileName").stringValue = VideoFileName;
        videoItem.FindPropertyRelative("videoUrlOverride").stringValue = "";
        videoItem.FindPropertyRelative("editorPreviewClip").objectReferenceValue = null;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Image UiImage(Transform parent, string name, Color color, Vector2 position, Vector2 size)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static RectTransform ChildRect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static TMP_Text Text(Transform parent, string name, Vector2 position, Vector2 size,
        string value, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static Button Button(Transform parent, string name, Vector2 position, Vector2 size, string label)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Image background = go.GetComponent<Image>();
        background.color = new Color(0.16f, 0.16f, 0.17f, 0.96f);

        Text(go.transform, "Label", Vector2.zero, size, label, 30f, TextAlignmentOptions.Center);
        return go.GetComponent<Button>();
    }

    private static void Stretch(RectTransform rect, Vector2 padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = padding;
        rect.offsetMax = -padding;
    }

    private static void AddTrackedDeviceGraphicRaycasterIfAvailable(GameObject gameObject)
    {
        Type type = Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
        if (type != null && gameObject.GetComponent(type) == null)
            gameObject.AddComponent(type);
    }

    private static void AddXrSelect(GameObject target, MuralExhibitController controller)
    {
        Type type = Type.GetType("UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable, Unity.XR.Interaction.Toolkit")
            ?? Type.GetType("UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable, Unity.XR.Interaction.Toolkit");
        if (type == null)
        {
            Debug.LogWarning("[MuralFrontYardTestSetup] XRSimpleInteractable type unavailable; Quest select not wired.");
            return;
        }

        Component interactable = target.GetComponent(type) ?? target.AddComponent(type);
        object selectEntered = type.GetProperty("selectEntered")?.GetValue(interactable)
            ?? type.GetField("selectEntered")?.GetValue(interactable)
            ?? type.GetField("m_SelectEntered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(interactable);

        if (selectEntered is UnityEventBase unityEvent)
        {
            for (int i = unityEvent.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(unityEvent, i);
            UnityEventTools.AddVoidPersistentListener(unityEvent, controller.OnXRSelect);
            EditorUtility.SetDirty(interactable);
        }
    }
}
