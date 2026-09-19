using System;
using System.Collections.Generic;
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

public static class HouseGroundsExhibitSetup
{
    const string ScenePath = "Assets/BH_XR_MainScene.unity";
    const string ExhibitPath = "Assets/BCaT/Exhibits/HouseGrounds";
    const string ImagesPath = ExhibitPath + "/Images";
    const string ExcavationImagesPath = ImagesPath + "/Excavation";
    const string PrefabPath = ExhibitPath + "/Prefabs/HouseGroundsExhibit.prefab";
    const string InstanceName = "HouseGroundsExhibit";
    const string HouseRecordingFile = "HouseGrounds/HouseRecording.mp4";
    const string ExcavationVideoFile = "HouseGrounds/Excavation.mp4";
    const string PresentationUrl = "https://www.canva.com/design/DAG6kMXrqkc/B9F8m6TC16ezAEZ4S7_CpQ/view?utm_content=DAG6kMXrqkc&utm_campaign=designshare&utm_medium=link2&utm_source=uniquelinks&utlId=hef23e83ebd";

    static readonly string[] Captions =
    {
        "Isidrio “Victor” Radamez Ortega Peralta, Local Interlocutor and barber in a Field Work Site in Sabaneta de Yasica.jpg",
        "Isidrio “Victor” Radamez Ortega Peralta, local interlocutor and barber, beside team member Diana Peña Bastalla_.jpg",
        "BCAT Archeology team including Pedro Jose de Paula Tolentino, Osvaldo Suarez, Ruth Pion, Diana Peña Bastalla, Sophia Monegro, Elizabeth Milagros Alvarez and current tenants that live across from the historic Kingsley home_2.jpg",
        "BCAT Archeology team including Pedro Jose de Paula Tolentino, Osvaldo Suarez, Ruth Pion, Diana Peña Bastalla, Sophia Monegro, Elizabeth Milagros Alvarez and current tenants that live across from the historic Kingsley home_1.jpg",
        "BCAT Archeology team including Pedro Jose de Paula Tolentino, Osvaldo Suarez, Ruth Pion, Diana Peña Bastalla, Sophia Monegro, Elizabeth Milagros Alvarez.jpg",
        "Adjacent above ground well next to Kingsley historic home now in Sabaneta de Yasica.jpg",
        "Archeological remains of Kingsley historic home now in Sabaneta de Yasica_1.jpg",
        "The back steps of the Kingsley 19th century historic home now in Sabaneta de Yasica_3.jpg",
        "The back steps of the Kingsley 19th century historic home now in Sabaneta de Yasica_2.jpg",
        "The back steps of the Kingsley 19th century historic home now in Sabaneta de Yasica_1.jpg",
        "Ruth Pion, archeology technician, within the Archeology team at Sabaneta de Yasica_2.jpg",
        "Ruth Pion, archeology technician, at Sabaneta de Yasica_1.jpg",
        "Elizabeth Milagros Alvarez at Sabaneta de Yasica_2.jpg",
        "Elizabeth Milagros Alvarez at Sabaneta de Yasica_1.jpg",
        "Elizabeth Milagros Alvarez and Ruth Pion, archeology technician, within the Archeology team at Sabaneta de Yasica_1.jpg",
        "Ruth Pion, archeology technician, within the Archeology team at Sabaneta de Yasica_1.jpg",
        "Diana Peña Bastalla, Archeology doctoral student and Osvaldo Saurez, Geographer within the Archeology team at Sabaneta de Yasica_1.jpg",
        "Osvaldo, Geographer within the Archeology team at Sabaneta de Yasica_1.jpg",
        "Archeology Site at Sabaneta de Yasica_3.jpg",
        "Archeology Site at Sabaneta de Yasica_2.jpg",
        "Archeology site at Sabaneta de Yasica.jpg",
        "Sophia Monegro presenting at The Caribbean Digital (TCD) XII conference_5.jpg",
        "Sophia Monegro and Elizabeth Milagros Alvarez presenting at The Caribbean Digital (TCD) XII conference_4.jpg",
        "Sophia Monegro presenting at The Caribbean Digital (TCD) XII conference_4.jpg",
        "Sophia Monegro presenting at The Caribbean Digital (TCD) XII conference_3.jpg",
        "Sophia Monegro and Elizabeth Milagros Alvarez Presenting at The Caribbean Digital (TCD) XII conference_3.jpg",
        "Sophia Monegro and Elizabeth Milagros Alvarez presenting at The Caribbean Digital (TCD) XII conference_2.jpg",
        "Sophia Monegro Presenting at The Caribbean Digital (TCD) XII conference_1.jpg",
        "Sophia Monegro and Elizabeth Milagros Alvarez presenting at The Caribbean Digital (TCD) XII conference_1.jpg",
        "Sophia Monegro Presenting at The Caribbean Digital (TCD) XII conference_.jpg"
    };

    [MenuItem("BCaT/House Grounds/Build And Attach To Backyard Well")]
    public static void BuildAndAttach()
    {
        AssetDatabase.Refresh();
        ConfigureTextureImports();
        AssetDatabase.ImportAsset("Assets/StreamingAssets/HouseGrounds/HouseRecording.mp4", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/StreamingAssets/HouseGrounds/Excavation.mp4", ImportAssetOptions.ForceUpdate);

        GameObject prefab = BuildPrefab();
        ConfigurePackagedMedia();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform well = FindBackyardWell(scene);
        if (well == null)
            throw new InvalidOperationException("Could not identify the backyard 'well' object in " + ScenePath);

        Transform existing = well.Find(InstanceName);
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = InstanceName;
        instance.transform.SetParent(well, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[HouseGroundsSetup] Built the prefab and attached one instance to the backyard well.");
    }

    [MenuItem("BCaT/House Grounds/Validate")]
    public static void Validate()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            throw new InvalidOperationException("House Grounds prefab is missing.");

        HouseGroundsExhibitController controller = prefab.GetComponent<HouseGroundsExhibitController>();
        if (controller == null)
            throw new InvalidOperationException("House Grounds prefab has no controller.");
        if (controller.Priority <= 0)
            throw new InvalidOperationException(
                "House Grounds must outrank an active proximity-only exhibit during Desktop router selection.");

        SerializedObject controllerData = new(controller);
        SerializedProperty items = controllerData.FindProperty("excavationItems");
        if (items.arraySize != Captions.Length + 1)
            throw new InvalidOperationException($"Expected {Captions.Length + 1} excavation entries, found {items.arraySize}.");
        for (int i = 0; i < Captions.Length; i++)
            if (items.GetArrayElementAtIndex(i).FindPropertyRelative("image").objectReferenceValue == null)
                throw new InvalidOperationException($"Excavation image {i + 1:00} is not serialized.");
        if (!items.GetArrayElementAtIndex(Captions.Length).FindPropertyRelative("isVideo").boolValue)
            throw new InvalidOperationException("The final excavation carousel item is not configured as video.");

        if (controllerData.FindProperty("houseInProgressImage").objectReferenceValue == null ||
            controllerData.FindProperty("videoPlayer").objectReferenceValue == null ||
            controllerData.FindProperty("presentationButton").objectReferenceValue == null)
            throw new InvalidOperationException("House image, video player, or presentation button is not wired.");

        Type xrType = Type.GetType("UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable, Unity.XR.Interaction.Toolkit")
            ?? Type.GetType("UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable, Unity.XR.Interaction.Toolkit");
        if (xrType == null || prefab.GetComponent(xrType) == null)
            throw new InvalidOperationException("House Grounds prefab has no XRSimpleInteractable.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform well = FindBackyardWell(scene);
        Transform instance = well != null ? well.Find(InstanceName) : null;
        if (instance == null)
            throw new InvalidOperationException("House Grounds instance is not attached beneath the backyard well.");

        HouseGroundsExhibitController sceneController = instance.GetComponent<HouseGroundsExhibitController>();
        BoxCollider sceneCollider = instance.GetComponent<BoxCollider>();
        if (sceneController == null || sceneCollider == null || !sceneCollider.enabled || !sceneCollider.isTrigger)
            throw new InvalidOperationException("House Grounds scene instance is missing its enabled trigger interaction target.");
        if (string.IsNullOrWhiteSpace(sceneController.GetPrompt(false)))
            throw new InvalidOperationException("House Grounds has no Desktop universal interaction prompt.");
        if (sceneController.Priority <= 0)
            throw new InvalidOperationException("House Grounds must retain Desktop routing precedence at the well.");

        if (!File.Exists("Assets/StreamingAssets/HouseGrounds/HouseRecording.mp4") ||
            !File.Exists("Assets/StreamingAssets/HouseGrounds/Excavation.mp4"))
            throw new FileNotFoundException("One or both packaged House Grounds videos are missing.");

        RemoteMediaConfig config = AssetDatabase.LoadAssetAtPath<RemoteMediaConfig>("Assets/Resources/RemoteMediaConfig.asset");
        if (config == null || !config.IsPackaged(HouseRecordingFile) || !config.IsPackaged(ExcavationVideoFile))
            throw new InvalidOperationException("House Grounds videos are not registered as packaged media.");

        Debug.Log($"[HouseGroundsValidation] PASS — 3 tabs, {Captions.Length} images, 1 carousel video, Desktop/XR routing, Canva link, and well attachment are wired.");
    }

    [MenuItem("BCaT/House Grounds/Fix Media Title Layout")]
    public static void FixMediaTitleLayout()
    {
        GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            RectTransform title = prefab.transform.Find("HouseGroundsPopup/Background/MediaTitle") as RectTransform;
            RectTransform mediaFrame = prefab.transform.Find("HouseGroundsPopup/Background/MediaFrame") as RectTransform;
            TMP_Text titleText = title != null ? title.GetComponent<TMP_Text>() : null;
            if (title == null || mediaFrame == null || titleText == null)
                throw new InvalidOperationException("House Grounds popup title or media frame is missing.");

            // Reserve a dedicated, two-line title band below the tabs. This is
            // shared world-space popup geometry for Desktop and Quest alike.
            title.anchoredPosition = new Vector2(0f, 286f);
            title.sizeDelta = new Vector2(1120f, 58f);
            titleText.fontSize = 22f;
            titleText.textWrappingMode = TextWrappingModes.Normal;
            titleText.overflowMode = TextOverflowModes.Ellipsis;

            mediaFrame.anchoredPosition = new Vector2(0f, -5f);
            mediaFrame.sizeDelta = new Vector2(1160f, 470f);

            PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefab);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[HouseGroundsSetup] Fixed the shared media-title layout without changing content or interaction behavior.");
    }

    static void ConfigureTextureImports()
    {
        ConfigureTexture(ImagesPath + "/HouseInProgress.png", true);
        for (int i = 0; i < Captions.Length; i++)
            ConfigureTexture($"{ExcavationImagesPath}/{i + 1:00}.jpg", false);
    }

    static void ConfigureTexture(string path, bool alpha)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new FileNotFoundException("House Grounds image missing or not importable: " + path);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = alpha;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Compressed;

        TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
        android.name = "Android";
        android.overridden = true;
        android.maxTextureSize = 2048;
        android.format = TextureImporterFormat.ASTC_6x6;
        android.compressionQuality = 75;
        importer.SetPlatformTextureSettings(android);
        importer.SaveAndReimport();
    }

    static GameObject BuildPrefab()
    {
        GameObject root = new(InstanceName);
        try
        {
            BoxCollider interactionCollider = root.AddComponent<BoxCollider>();
            interactionCollider.center = new Vector3(0f, 1.05f, 0f);
            interactionCollider.size = new Vector3(2.2f, 2.1f, 2.2f);
            interactionCollider.isTrigger = true;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            HouseGroundsExhibitController controller = root.AddComponent<HouseGroundsExhibitController>();
            GameObject popup = CreatePopup(root.transform, out PopupReferences ui);

            VideoPlayer player = popup.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = false;
            player.renderMode = VideoRenderMode.RenderTexture;
            AudioSource audio = popup.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.loop = false;
            audio.spatialBlend = 0f;

            ConfigureController(controller, root.transform, popup, ui, player, audio);
            AddXrSelect(root, interactionCollider, controller);
            SetLayerRecursive(popup, LayerMask.NameToLayer("UI"));
            popup.SetActive(false);

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    sealed class PopupReferences
    {
        public Canvas canvas;
        public Image image;
        public RawImage video;
        public AspectRatioFitter imageAspect;
        public AspectRatioFitter videoAspect;
        public TMP_Text title;
        public TMP_Text caption;
        public TMP_Text counter;
        public Button recordingTab;
        public Button excavationTab;
        public Button houseImageTab;
        public Button previous;
        public Button next;
        public Button presentation;
        public Button close;
    }

    static GameObject CreatePopup(Transform parent, out PopupReferences ui)
    {
        ui = new PopupReferences();
        GameObject root = new("HouseGroundsPopup", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        root.transform.SetParent(parent, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(1280f, 940f);
        root.transform.localScale = Vector3.one * 0.00135f;

        ui.canvas = root.GetComponent<Canvas>();
        ui.canvas.renderMode = RenderMode.WorldSpace;
        ui.canvas.overrideSorting = true;
        ui.canvas.sortingOrder = 130;
        root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 12f;
        AddTrackedDeviceGraphicRaycaster(root);

        Image background = UiImage(root.transform, "Background", new Color(0.025f, 0.025f, 0.03f, 1f),
            Vector2.zero, rootRect.sizeDelta);
        Text(background.transform, "ExhibitHeading", new Vector2(-510f, 420f), new Vector2(980f, 52f),
            "House and Grounds", 38f, TextAlignmentOptions.Left);
        ui.close = Button(background.transform, "CloseButton", new Vector2(585f, 418f), new Vector2(64f, 54f), "X", 30f);

        ui.recordingTab = TabButton(background.transform, "RecordingTab", new Vector2(-340f, 348f), "HOUSE RECORDING");
        ui.excavationTab = TabButton(background.transform, "ExcavationTab", new Vector2(0f, 348f), "EXCAVATION");
        ui.houseImageTab = TabButton(background.transform, "HouseImageTab", new Vector2(340f, 348f), "HOUSE IMAGE");

        Image mediaFrame = UiImage(background.transform, "MediaFrame", new Color(0.008f, 0.008f, 0.01f, 1f),
            new Vector2(0f, -5f), new Vector2(1160f, 470f));

        GameObject imageObject = new("ImageDisplay", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(AspectRatioFitter));
        imageObject.transform.SetParent(mediaFrame.transform, false);
        Stretch(imageObject.GetComponent<RectTransform>(), new Vector2(14f, 14f));
        ui.image = imageObject.GetComponent<Image>();
        ui.image.color = Color.white;
        ui.image.preserveAspect = true;
        ui.image.raycastTarget = false;
        ui.imageAspect = imageObject.GetComponent<AspectRatioFitter>();
        ui.imageAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        GameObject videoObject = new("VideoDisplay", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(RawImage), typeof(AspectRatioFitter));
        videoObject.transform.SetParent(mediaFrame.transform, false);
        Stretch(videoObject.GetComponent<RectTransform>(), new Vector2(14f, 14f));
        ui.video = videoObject.GetComponent<RawImage>();
        ui.video.color = Color.white;
        ui.video.raycastTarget = false;
        ui.videoAspect = videoObject.GetComponent<AspectRatioFitter>();
        ui.videoAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        ui.title = Text(background.transform, "MediaTitle", new Vector2(0f, 286f), new Vector2(1120f, 58f),
            string.Empty, 22f, TextAlignmentOptions.Center);
        ui.title.overflowMode = TextOverflowModes.Ellipsis;
        ui.caption = Text(background.transform, "Caption", new Vector2(0f, -286f), new Vector2(910f, 72f),
            string.Empty, 20f, TextAlignmentOptions.Center);
        ui.caption.overflowMode = TextOverflowModes.Ellipsis;

        ui.previous = Button(background.transform, "PreviousButton", new Vector2(-545f, -300f), new Vector2(72f, 58f), "<", 34f);
        ui.next = Button(background.transform, "NextButton", new Vector2(545f, -300f), new Vector2(72f, 58f), ">", 34f);
        ui.counter = Text(background.transform, "Counter", new Vector2(475f, -360f), new Vector2(180f, 38f),
            string.Empty, 22f, TextAlignmentOptions.Center);

        ui.presentation = Button(background.transform, "PresentationButton", new Vector2(0f, -410f),
            new Vector2(430f, 62f), "VIEW CANVA PRESENTATION", 22f);
        return root;
    }

    static Button TabButton(Transform parent, string name, Vector2 position, string label)
    {
        Button button = Button(parent, name, position, new Vector2(310f, 58f), label, 21f);
        Outline outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.75f, 0.52f, 1f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);
        outline.enabled = false;
        return button;
    }

    static void ConfigureController(HouseGroundsExhibitController controller, Transform interaction,
        GameObject popup, PopupReferences ui, VideoPlayer player, AudioSource audio)
    {
        SerializedObject so = new(controller);
        so.FindProperty("focusPoint").objectReferenceValue = interaction;
        so.FindProperty("colliderRoot").objectReferenceValue = interaction;
        so.FindProperty("interactionDistance").floatValue = 4.5f;
        so.FindProperty("maxViewAngle").floatValue = 20f;

        SerializedProperty prompt = so.FindProperty("prompt");
        prompt.FindPropertyRelative("desktopPrompt").stringValue = "";
        prompt.FindPropertyRelative("xrPrompt").stringValue = "";
        prompt.FindPropertyRelative("verb").enumValueIndex = (int)SharedInteractionVerb.View;
        prompt.FindPropertyRelative("objectName").stringValue = "House and Grounds";

        so.FindProperty("popupRoot").objectReferenceValue = popup;
        so.FindProperty("popupCanvas").objectReferenceValue = ui.canvas;
        so.FindProperty("imageDisplay").objectReferenceValue = ui.image;
        so.FindProperty("videoDisplay").objectReferenceValue = ui.video;
        so.FindProperty("imageAspect").objectReferenceValue = ui.imageAspect;
        so.FindProperty("videoAspect").objectReferenceValue = ui.videoAspect;
        so.FindProperty("titleText").objectReferenceValue = ui.title;
        so.FindProperty("captionText").objectReferenceValue = ui.caption;
        so.FindProperty("counterText").objectReferenceValue = ui.counter;
        so.FindProperty("recordingTabButton").objectReferenceValue = ui.recordingTab;
        so.FindProperty("excavationTabButton").objectReferenceValue = ui.excavationTab;
        so.FindProperty("houseImageTabButton").objectReferenceValue = ui.houseImageTab;
        so.FindProperty("previousButton").objectReferenceValue = ui.previous;
        so.FindProperty("nextButton").objectReferenceValue = ui.next;
        so.FindProperty("presentationButton").objectReferenceValue = ui.presentation;
        so.FindProperty("closeButton").objectReferenceValue = ui.close;
        so.FindProperty("openDistanceFromCamera").floatValue = 1.75f;
        so.FindProperty("houseRecordingFileName").stringValue = HouseRecordingFile;
        so.FindProperty("houseInProgressImage").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>(ImagesPath + "/HouseInProgress.png");
        so.FindProperty("presentationUrl").stringValue = PresentationUrl;
        so.FindProperty("videoPlayer").objectReferenceValue = player;
        so.FindProperty("videoAudioSource").objectReferenceValue = audio;
        so.FindProperty("prepareTimeoutSeconds").floatValue = 20f;

        SerializedProperty items = so.FindProperty("excavationItems");
        items.arraySize = Captions.Length + 1;
        for (int i = 0; i < Captions.Length; i++)
        {
            SerializedProperty item = items.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("isVideo").boolValue = false;
            item.FindPropertyRelative("displayName").stringValue = Path.GetFileNameWithoutExtension(Captions[i]);
            item.FindPropertyRelative("caption").stringValue = string.Empty;
            item.FindPropertyRelative("image").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>($"{ExcavationImagesPath}/{i + 1:00}.jpg");
            item.FindPropertyRelative("videoFileName").stringValue = string.Empty;
        }

        SerializedProperty videoItem = items.GetArrayElementAtIndex(Captions.Length);
        videoItem.FindPropertyRelative("isVideo").boolValue = true;
        videoItem.FindPropertyRelative("displayName").stringValue =
            "Sophia Monegro sweeping at archeology site at Sabaneta de Yasica_1";
        videoItem.FindPropertyRelative("caption").stringValue = "Video";
        videoItem.FindPropertyRelative("image").objectReferenceValue = null;
        videoItem.FindPropertyRelative("videoFileName").stringValue = ExcavationVideoFile;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void ConfigurePackagedMedia()
    {
        RemoteMediaConfig config = AssetDatabase.LoadAssetAtPath<RemoteMediaConfig>("Assets/Resources/RemoteMediaConfig.asset");
        if (config == null)
            throw new FileNotFoundException("Assets/Resources/RemoteMediaConfig.asset is missing.");

        SerializedObject so = new(config);
        SerializedProperty names = so.FindProperty("packagedFileNames");
        var ordered = new List<string>();
        HashSet<string> values = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < names.arraySize; i++)
        {
            string value = names.GetArrayElementAtIndex(i).stringValue;
            if (!string.IsNullOrWhiteSpace(value) && values.Add(value))
                ordered.Add(value);
        }
        if (values.Add(HouseRecordingFile)) ordered.Add(HouseRecordingFile);
        if (values.Add(ExcavationVideoFile)) ordered.Add(ExcavationVideoFile);
        names.arraySize = ordered.Count;
        for (int i = 0; i < ordered.Count; i++)
            names.GetArrayElementAtIndex(i).stringValue = ordered[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
    }

    static Transform FindBackyardWell(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == "well" && candidate.Find("Env_Well_01") != null)
                    return candidate;

        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == "well")
                    return candidate;
        return null;
    }

    static Image UiImage(Transform parent, string name, Color color, Vector2 position, Vector2 size)
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

    static TMP_Text Text(Transform parent, string name, Vector2 position, Vector2 size,
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

    static Button Button(Transform parent, string name, Vector2 position, Vector2 size, string label, float labelSize)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = go.GetComponent<Image>();
        image.color = new Color(0.12f, 0.12f, 0.14f, 1f);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        Text(go.transform, "Label", Vector2.zero, size, label, labelSize, TextAlignmentOptions.Center);
        return button;
    }

    static void Stretch(RectTransform rect, Vector2 padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = padding;
        rect.offsetMax = -padding;
    }

    static void AddTrackedDeviceGraphicRaycaster(GameObject target)
    {
        Type type = Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
        if (type != null && target.GetComponent(type) == null)
            target.AddComponent(type);
    }

    static void AddXrSelect(GameObject target, Collider collider, HouseGroundsExhibitController controller)
    {
        Type type = Type.GetType("UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable, Unity.XR.Interaction.Toolkit")
            ?? Type.GetType("UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable, Unity.XR.Interaction.Toolkit");
        if (type == null)
            throw new InvalidOperationException("XRSimpleInteractable is unavailable.");

        Component interactable = target.AddComponent(type);
        object colliderList = type.GetProperty("colliders")?.GetValue(interactable);
        colliderList?.GetType().GetMethod("Add")?.Invoke(colliderList, new object[] { collider });

        XrSelectSurface surface = target.AddComponent<XrSelectSurface>();
        SerializedObject surfaceData = new(surface);
        surfaceData.FindProperty("padding").floatValue = 0.02f;
        surfaceData.FindProperty("forwardsTo").stringValue = controller.GetType().Name;
        surfaceData.ApplyModifiedPropertiesWithoutUndo();

        object selectEntered = type.GetProperty("selectEntered")?.GetValue(interactable)
            ?? type.GetField("selectEntered")?.GetValue(interactable)
            ?? type.GetField("m_SelectEntered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(interactable);
        if (selectEntered is not UnityEventBase unityEvent)
            throw new InvalidOperationException("Could not access XRSimpleInteractable.selectEntered.");
        UnityEventTools.AddVoidPersistentListener(unityEvent, controller.OnXRSelect);
        EditorUtility.SetDirty(interactable);
    }

    static void SetLayerRecursive(GameObject root, int layer)
    {
        if (layer < 0) return;
        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
