using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BCaT.Production.Interaction;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the five reusable Adinkra symbol exhibit prefabs and stages them in a
/// row in the front yard for review. Prefabs and the staging group are outputs:
/// edit this builder and re-run the menu items rather than hand-editing them.
///
/// The prefabs carry only exhibit content (model, narration, modal). Review
/// plinths belong to the staging pass, so the prefabs stay droppable anywhere in
/// the house once final placement is decided.
/// </summary>
public static class AdinkraSymbolExhibitBuilder
{
    private const string ScenePath = "Assets/BH_XR_MainScene.unity";
    private const string ExhibitRoot = "Assets/BCaT/Exhibits/Adinkra";
    private const string PrefabRoot = ExhibitRoot + "/Prefabs";
    private const string MaterialRoot = ExhibitRoot + "/Materials";
    private const string AssetRoot = "Assets/BCaT_assets/Adinkra";

    private const string StagingRootName = "AdinkraSymbols_Test";

    /// <summary>Largest model dimension in metres after normalization.</summary>
    private const float SymbolTargetSize = 0.55f;

    /// <summary>Minimum box-collider extent so every symbol is easy to focus.</summary>
    private const float MinColliderSize = 0.34f;

    private const float PlinthHeight = 1.0f;
    private static readonly Vector3 PlinthFootprint = new Vector3(0.5f, PlinthHeight, 0.5f);

    // Front yard staging row. The player arrives at (167.91, 5.86, 130.61)
    // facing +Z, so the row sits a few metres ahead, centred on the arrival
    // point, inside the porch fence (x 157.1 - 178.1) and north of
    // Boundary_Front (z 130.01). Symbols face -Z, back toward the visitor.
    private const float RowCenterX = 167.9f;
    private const float RowZ = 134f;
    private const float RowSpacing = 4f;
    private const float RowFacingYaw = 180f;

    private sealed class SymbolDefinition
    {
        public string SceneObjectName;
        public string PrefabName;
        public string Title;
        public string Meaning;
        public string ModelPath;
        public string NarrationPath;
        public string NarrationMediaId;
        public string WebsiteUrl = "";
        public string WebsiteButtonLabel = "Visit Website";
        public bool ShowVideoSection;
        public string VideoPlaceholderNote = "Video coming soon.";
    }

    private static readonly SymbolDefinition[] Symbols =
    {
        new SymbolDefinition
        {
            SceneObjectName = "Sankofa",
            PrefabName = "Adinkra_Sankofa",
            Title = "Sankofa",
            Meaning = "“Go back and get it” — learning from the past to build a better future.",
            ModelPath = AssetRoot + "/Sankofa (Main Symbol)/Sankofa_Essence.glb",
            NarrationPath = AssetRoot + "/Sankofa (Main Symbol)/sankofa.mp3",
            NarrationMediaId = "adinkra_sankofa",
            WebsiteUrl = "https://ghanahomespace.com/",
            WebsiteButtonLabel = "Visit Ghana Home Space",
            ShowVideoSection = true,
            VideoPlaceholderNote = "Reserved for a future Sankofa video.",
        },
        new SymbolDefinition
        {
            SceneObjectName = "GyeNyame",
            PrefabName = "Adinkra_GyeNyame",
            Title = "Gye Nyame",
            Meaning = "God’s omnipotence, protection, and divine presence.",
            ModelPath = AssetRoot + "/Gye Nyame/gye_nyame.glb",
            NarrationPath = AssetRoot + "/Gye Nyame/gye-nyame.mp3",
            NarrationMediaId = "adinkra_gye_nyame",
        },
        new SymbolDefinition
        {
            SceneObjectName = "Adinkrahene",
            PrefabName = "Adinkra_Adinkrahene",
            Title = "Adinkrahene",
            Meaning = "Leadership, greatness, charisma, and responsibility.",
            ModelPath = AssetRoot + "/Adinkrahene/Adinkrahene.glb",
            NarrationPath = AssetRoot + "/Adinkrahene/adinkrahene.mp3",
            NarrationMediaId = "adinkra_adinkrahene",
        },
        new SymbolDefinition
        {
            SceneObjectName = "Funtunfunefu",
            PrefabName = "Adinkra_Funtunfunefu",
            Title = "Funtunfunefu Denkyemfunefu",
            Meaning = "Unity in diversity, cooperation, and shared destiny.",
            ModelPath = AssetRoot + "/Funtunfunefu Denkyemfunefu/Funtunfunefu_Denkyemfunefu.glb",
            NarrationPath = AssetRoot + "/Funtunfunefu Denkyemfunefu/funtumfunefu-denkyemfunefu.mp3",
            NarrationMediaId = "adinkra_funtunfunefu_denkyemfunefu",
        },
        new SymbolDefinition
        {
            SceneObjectName = "Nsaa",
            PrefabName = "Adinkra_Nsaa",
            Title = "Nsaa",
            Meaning = "Excellence, authenticity, quality craftsmanship, and attention to detail.",
            ModelPath = AssetRoot + "/Nsaa/Nsaa.glb",
            NarrationPath = AssetRoot + "/Nsaa/nsaa.mp3",
            NarrationMediaId = "adinkra_nsaa",
        },
    };

    [MenuItem("BCaT/Adinkra/Build Symbol Prefabs")]
    public static void BuildPrefabs()
    {
        EnsureFolders();
        AssetDatabase.ImportAsset(AssetRoot, ImportAssetOptions.ImportRecursive);

        var log = new StringBuilder("[Adinkra] Prefab build\n");
        foreach (SymbolDefinition symbol in Symbols)
        {
            GameObject built = BuildSymbolExhibit(symbol, log);
            string path = $"{PrefabRoot}/{symbol.PrefabName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(built, path);
            UnityEngine.Object.DestroyImmediate(built);
            log.AppendLine($"  saved {path}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(log.ToString());
    }

    [MenuItem("BCaT/Adinkra/Stage Symbols In Front Yard")]
    public static void StageFrontYard()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject existing = GameObject.Find(StagingRootName);
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing);

        Material plinthMaterial = CreateMaterial("Adinkra_Plinth", new Color(0.30f, 0.27f, 0.26f), 0f, 0.2f);
        GameObject stagingRoot = new GameObject(StagingRootName);
        stagingRoot.transform.position = Vector3.zero;

        var log = new StringBuilder($"[Adinkra] Front yard staging ({StagingRootName})\n");
        float startX = RowCenterX - RowSpacing * (Symbols.Length - 1) * 0.5f;

        // The front-yard grass slopes from ~5.37 m at the fence down to ~4.86 m
        // across the middle walkway, so following the ground per slot would sag
        // the row by half a metre. Sample every slot first and level all plinth
        // tops to the highest one; each plinth then stretches down to its own
        // ground so nothing floats.
        var grounds = new float[Symbols.Length];
        float levelTopY = float.NegativeInfinity;
        for (int i = 0; i < Symbols.Length; i++)
        {
            grounds[i] = SampleGroundY(startX + RowSpacing * i, RowZ, 4.86f);
            levelTopY = Mathf.Max(levelTopY, grounds[i] + PlinthHeight);
        }

        for (int i = 0; i < Symbols.Length; i++)
        {
            SymbolDefinition symbol = Symbols[i];
            float x = startX + RowSpacing * i;
            float groundY = grounds[i];
            float plinthHeight = levelTopY - groundY;

            GameObject slot = new GameObject(symbol.SceneObjectName);
            slot.transform.SetParent(stagingRoot.transform, false);
            slot.transform.SetPositionAndRotation(
                new Vector3(x, groundY, RowZ), Quaternion.Euler(0f, RowFacingYaw, 0f));

            GameObject plinth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plinth.name = "ReviewPlinth";
            plinth.transform.SetParent(slot.transform, false);
            plinth.transform.localScale = new Vector3(PlinthFootprint.x, plinthHeight, PlinthFootprint.z);
            plinth.transform.localPosition = new Vector3(0f, plinthHeight * 0.5f, 0f);
            Renderer plinthRenderer = plinth.GetComponent<Renderer>();
            plinthRenderer.sharedMaterial = plinthMaterial;
            plinthRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/{symbol.PrefabName}.prefab");
            if (prefab == null)
                throw new FileNotFoundException(
                    $"Missing {symbol.PrefabName}.prefab — run BCaT/Adinkra/Build Symbol Prefabs first.");

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = symbol.PrefabName;
            instance.transform.SetParent(slot.transform, false);
            instance.transform.localPosition = new Vector3(0f, plinthHeight, 0f);
            instance.transform.localRotation = Quaternion.identity;

            log.AppendLine($"  {symbol.SceneObjectName,-14} x={x:F2} z={RowZ:F2} ground={groundY:F2} " +
                           $"plinthHeight={plinthHeight:F2} symbolBaseY={levelTopY:F2} yaw={RowFacingYaw}");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(log.ToString());
    }

    [MenuItem("BCaT/Adinkra/Build And Stage Everything")]
    public static void BuildAndStage()
    {
        BuildPrefabs();
        StageFrontYard();
    }

    // ---- Exhibit construction -------------------------------------------

    private static GameObject BuildSymbolExhibit(SymbolDefinition symbol, StringBuilder log)
    {
        GameObject root = new GameObject(symbol.PrefabName);

        // The symbol model is the interaction target, matching the Linda Leaks
        // artifacts and the mural panel.
        GameObject target = new GameObject("Symbol_" + symbol.SceneObjectName);
        target.transform.SetParent(root.transform, false);

        GameObject model = AddNormalizedModel(target.transform, symbol.ModelPath, out Bounds scaledBounds, out Vector3 nativeSize);
        log.AppendLine($"  {symbol.Title}: native size {nativeSize.x:F3} x {nativeSize.y:F3} x {nativeSize.z:F3} m, " +
                       $"scaled to {scaledBounds.size.x:F3} x {scaledBounds.size.y:F3} x {scaledBounds.size.z:F3} m");

        BoxCollider collider = target.AddComponent<BoxCollider>();
        collider.center = scaledBounds.center;
        collider.size = new Vector3(
            Mathf.Max(scaledBounds.size.x, MinColliderSize),
            Mathf.Max(scaledBounds.size.y, MinColliderSize),
            Mathf.Max(scaledBounds.size.z, MinColliderSize));

        Rigidbody body = target.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        AudioSource narrationSource = root.AddComponent<AudioSource>();
        narrationSource.playOnAwake = false;
        narrationSource.loop = false;
        narrationSource.volume = 0.9f;
        // The narration plays while the visitor is inside the focused modal, so
        // it is 2D for intelligibility rather than a room-placed spatial source.
        narrationSource.spatialBlend = 0f;

        ModalParts modal = BuildModal(root.transform, symbol, scaledBounds.max.y);

        AdinkraSymbolExhibit controller = root.AddComponent<AdinkraSymbolExhibit>();
        ConfigureController(controller, symbol, model, target.transform, narrationSource, modal);

        AddXrSelect(target, controller);
        return root;
    }

    private static void ConfigureController(AdinkraSymbolExhibit controller, SymbolDefinition symbol,
        GameObject model, Transform target, AudioSource narrationSource, ModalParts modal)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(symbol.NarrationPath);
        if (clip == null)
            throw new FileNotFoundException("Missing Adinkra narration clip: " + symbol.NarrationPath);

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("symbolName").stringValue = symbol.Title;
        so.FindProperty("meaning").stringValue = symbol.Meaning;
        so.FindProperty("modelRoot").objectReferenceValue = model != null ? model.transform : null;

        so.FindProperty("narrationClip").objectReferenceValue = clip;
        so.FindProperty("narrationSource").objectReferenceValue = narrationSource;
        so.FindProperty("narrationVolume").floatValue = 0.9f;
        so.FindProperty("narrationMediaId").stringValue = symbol.NarrationMediaId;

        so.FindProperty("websiteUrl").stringValue = symbol.WebsiteUrl;
        so.FindProperty("websiteButtonLabel").stringValue = symbol.WebsiteButtonLabel;

        so.FindProperty("showVideoSection").boolValue = symbol.ShowVideoSection;
        so.FindProperty("futureVideoFileName").stringValue = "";
        so.FindProperty("videoPlaceholderNote").stringValue = symbol.VideoPlaceholderNote;

        so.FindProperty("focusPoint").objectReferenceValue = target;
        so.FindProperty("colliderRoot").objectReferenceValue = target;
        so.FindProperty("interactionDistance").floatValue = 3.5f;
        so.FindProperty("maxViewAngle").floatValue = 18f;
        so.FindProperty("worldPromptText").objectReferenceValue = null;

        SerializedProperty prompt = so.FindProperty("prompt");
        prompt.FindPropertyRelative("desktopPrompt").stringValue = "Press E to Examine Symbol";
        prompt.FindPropertyRelative("xrPrompt").stringValue = "Interact to Examine Symbol";
        prompt.FindPropertyRelative("verb").enumValueIndex = (int)SharedInteractionVerb.View;
        prompt.FindPropertyRelative("objectName").stringValue = symbol.Title;

        so.FindProperty("modalRoot").objectReferenceValue = modal.Root;
        so.FindProperty("modalCanvas").objectReferenceValue = modal.Canvas;
        so.FindProperty("titleText").objectReferenceValue = modal.Title;
        so.FindProperty("meaningText").objectReferenceValue = modal.Meaning;
        so.FindProperty("narrationButton").objectReferenceValue = modal.NarrationButton;
        so.FindProperty("narrationButtonLabel").objectReferenceValue = modal.NarrationButtonLabel;
        so.FindProperty("narrationStatusText").objectReferenceValue = modal.NarrationStatus;
        so.FindProperty("videoSection").objectReferenceValue = modal.VideoSection;
        so.FindProperty("videoPlaceholderText").objectReferenceValue = modal.VideoPlaceholder;
        so.FindProperty("websiteButton").objectReferenceValue = modal.WebsiteButton;
        so.FindProperty("websiteButtonLabelText").objectReferenceValue = modal.WebsiteButtonLabel;
        so.FindProperty("closeButton").objectReferenceValue = modal.CloseButton;
        so.FindProperty("openDistanceFromCamera").floatValue = 1.6f;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---- Model ----------------------------------------------------------

    /// <summary>
    /// Instantiates the GLB and normalizes it so its largest dimension is
    /// <see cref="SymbolTargetSize"/>, centred horizontally with its base at the
    /// parent origin (so it sits on a plinth, shelf or table).
    /// </summary>
    private static GameObject AddNormalizedModel(Transform parent, string assetPath,
        out Bounds scaledBounds, out Vector3 nativeSize)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null)
            throw new FileNotFoundException("Missing Adinkra model: " + assetPath);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        instance.name = Path.GetFileNameWithoutExtension(assetPath) + "_Model";
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        if (!TryGetRendererBounds(instance, out Bounds native))
        {
            Debug.LogWarning($"[Adinkra] '{assetPath}' has no renderers; model left unscaled.");
            nativeSize = Vector3.zero;
            scaledBounds = new Bounds(Vector3.zero, Vector3.one * MinColliderSize);
            return instance;
        }

        nativeSize = native.size;
        float largest = Mathf.Max(native.size.x, Mathf.Max(native.size.y, native.size.z));
        float scale = largest > 0.0001f ? SymbolTargetSize / largest : 1f;

        instance.transform.localScale = Vector3.one * scale;
        // Re-measure after scaling, then recentre on the parent origin.
        TryGetRendererBounds(instance, out Bounds scaled);
        Vector3 offset = new Vector3(-scaled.center.x, -scaled.min.y, -scaled.center.z);
        instance.transform.localPosition = offset;

        TryGetRendererBounds(instance, out scaledBounds);
        // Express bounds in the parent's local space for the collider.
        scaledBounds = new Bounds(parent.InverseTransformPoint(scaledBounds.center), scaledBounds.size);
        return instance;
    }

    private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found;
    }

    // ---- Modal ----------------------------------------------------------

    private sealed class ModalParts
    {
        public GameObject Root;
        public Canvas Canvas;
        public TMP_Text Title;
        public TMP_Text Meaning;
        public Button NarrationButton;
        public TMP_Text NarrationButtonLabel;
        public TMP_Text NarrationStatus;
        public GameObject VideoSection;
        public TMP_Text VideoPlaceholder;
        public Button WebsiteButton;
        public TMP_Text WebsiteButtonLabel;
        public Button CloseButton;
    }

    private const float ModalWidth = 1200f;
    private const float ModalPadding = 44f;
    private const float TitleHeight = 84f;
    private const float MeaningHeight = 150f;
    private const float NarrationButtonHeight = 92f;
    private const float StatusHeight = 44f;
    private const float WebsiteButtonHeight = 86f;
    private const float VideoHeaderHeight = 54f;
    private const float VideoFrameHeight = 170f;

    private static readonly Color ModalBackground = new Color(0.025f, 0.025f, 0.027f, 0.96f);
    private static readonly Color PanelFill = new Color(0.08f, 0.075f, 0.09f, 1f);
    private static readonly Color ButtonFill = new Color(0.16f, 0.16f, 0.17f, 0.96f);
    private static readonly Color AccentText = new Color(0.85f, 0.72f, 0.45f, 1f);

    /// <summary>
    /// Builds this exhibit's focused modal using the same construction the
    /// mural gallery modal uses (world-space canvas, dark panel, TMP text,
    /// UI Buttons), so the five symbols read as the same museum interface.
    /// </summary>
    private static ModalParts BuildModal(Transform parent, SymbolDefinition symbol, float symbolTopY)
    {
        var parts = new ModalParts();
        bool hasWebsite = !string.IsNullOrWhiteSpace(symbol.WebsiteUrl);

        // The panel is sized to the sections this symbol actually shows, so a
        // symbol with no website or video does not open a half-empty card.
        float height = ModalPadding * 2f + TitleHeight + 16f + MeaningHeight + 26f +
                       NarrationButtonHeight + 10f + StatusHeight;
        if (hasWebsite)
            height += 22f + WebsiteButtonHeight;
        if (symbol.ShowVideoSection)
            height += 34f + VideoHeaderHeight + 10f + VideoFrameHeight;

        Vector2 modalSize = new Vector2(ModalWidth, height);

        GameObject root = new GameObject("SymbolModal", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(0f, symbolTopY + 0.45f, 0f);
        root.transform.localScale = Vector3.one * 0.0014f;
        root.SetActive(false);
        parts.Root = root;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 120;
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 |
                                          AdditionalCanvasShaderChannels.Normal |
                                          AdditionalCanvasShaderChannels.Tangent;
        root.GetComponent<RectTransform>().sizeDelta = modalSize;
        root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 12f;
        AddTrackedDeviceGraphicRaycasterIfAvailable(root);
        parts.Canvas = canvas;

        Image background = UiImage(root.transform, "Background", ModalBackground, Vector2.zero, modalSize);
        Transform bg = background.transform;

        // Top-down layout cursor: y of the next section's top edge.
        float cursor = height * 0.5f - ModalPadding;

        parts.Title = Text(bg, "TitleText", Row(ref cursor, TitleHeight, 16f), new Vector2(1080f, TitleHeight),
            symbol.Title, 52f, TextAlignmentOptions.Center, AccentText, FontStyles.Bold);

        parts.Meaning = Text(bg, "MeaningText", Row(ref cursor, MeaningHeight, 26f),
            new Vector2(1000f, MeaningHeight), symbol.Meaning, 32f, TextAlignmentOptions.Top, Color.white);

        parts.NarrationButton = Button(bg, "NarrationButton", Row(ref cursor, NarrationButtonHeight, 10f),
            new Vector2(440f, NarrationButtonHeight), "Play Narration", 32f, out parts.NarrationButtonLabel);

        parts.NarrationStatus = Text(bg, "NarrationStatusText", Row(ref cursor, StatusHeight, 22f),
            new Vector2(900f, StatusHeight), string.Empty, 24f, TextAlignmentOptions.Center,
            new Color(0.75f, 0.75f, 0.78f, 1f));

        parts.WebsiteButton = Button(bg, "WebsiteButton",
            hasWebsite ? Row(ref cursor, WebsiteButtonHeight, 34f) : new Vector2(0f, cursor),
            new Vector2(560f, WebsiteButtonHeight), symbol.WebsiteButtonLabel, 28f, out parts.WebsiteButtonLabel);
        parts.WebsiteButton.gameObject.SetActive(hasWebsite);

        // Placeholder section for media attached later. Titled "Video" and
        // intentionally empty; the controller shows it only when enabled.
        float videoSectionHeight = VideoHeaderHeight + 10f + VideoFrameHeight;
        GameObject videoSection = new GameObject("VideoSection", typeof(RectTransform));
        videoSection.transform.SetParent(bg, false);
        RectTransform videoRect = videoSection.GetComponent<RectTransform>();
        videoRect.sizeDelta = new Vector2(1000f, videoSectionHeight);
        videoRect.anchoredPosition = symbol.ShowVideoSection
            ? Row(ref cursor, videoSectionHeight, 0f)
            : new Vector2(0f, cursor);

        float videoTop = videoSectionHeight * 0.5f;
        Text(videoSection.transform, "VideoHeader",
            new Vector2(0f, videoTop - VideoHeaderHeight * 0.5f), new Vector2(1000f, VideoHeaderHeight),
            "Video", 34f, TextAlignmentOptions.Center, AccentText, FontStyles.Bold);
        Vector2 framePosition = new Vector2(0f, videoTop - VideoHeaderHeight - 10f - VideoFrameHeight * 0.5f);
        UiImage(videoSection.transform, "VideoPlaceholderFrame", PanelFill, framePosition,
            new Vector2(880f, VideoFrameHeight));
        parts.VideoPlaceholder = Text(videoSection.transform, "VideoPlaceholderText", framePosition,
            new Vector2(840f, VideoFrameHeight - 30f), symbol.VideoPlaceholderNote, 26f,
            TextAlignmentOptions.Center, new Color(0.66f, 0.66f, 0.70f, 1f));
        videoSection.SetActive(symbol.ShowVideoSection);
        parts.VideoSection = videoSection;

        parts.CloseButton = Button(bg, "CloseButton",
            new Vector2(ModalWidth * 0.5f - 65f, height * 0.5f - 46f), new Vector2(80f, 68f),
            "X", 32f, out _);

        // Applied last so every child inherits the UI layer the modal renders on.
        SetLayerRecursive(root, LayerMask.NameToLayer("UI"));
        return parts;
    }

    /// <summary>
    /// Consumes <paramref name="height"/> from the top-down layout cursor and
    /// returns the centred anchored position for that row.
    /// </summary>
    private static Vector2 Row(ref float cursor, float height, float gapAfter)
    {
        var position = new Vector2(0f, cursor - height * 0.5f);
        cursor -= height + gapAfter;
        return position;
    }

    // ---- UI helpers (mirroring the mural modal builder) ------------------

    private static Image UiImage(Transform parent, string name, Color color, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text Text(Transform parent, string name, Vector2 position, Vector2 size,
        string value, float fontSize, TextAlignmentOptions alignment, Color color,
        FontStyles style = FontStyles.Normal)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.fontStyle = style;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static Button Button(Transform parent, string name, Vector2 position, Vector2 size,
        string label, float fontSize, out TMP_Text labelText)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Image background = go.GetComponent<Image>();
        background.color = ButtonFill;

        labelText = Text(go.transform, "Label", Vector2.zero, size, label, fontSize,
            TextAlignmentOptions.Center, Color.white);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = background;
        return button;
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        if (layer < 0)
            return;

        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    private static void AddTrackedDeviceGraphicRaycasterIfAvailable(GameObject gameObject)
    {
        Type type = Type.GetType(
            "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
        if (type != null && gameObject.GetComponent(type) == null)
            gameObject.AddComponent(type);
    }

    private static void AddXrSelect(GameObject target, AdinkraSymbolExhibit controller)
    {
        Type type = Type.GetType(
                        "UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable, Unity.XR.Interaction.Toolkit")
                    ?? Type.GetType("UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable, Unity.XR.Interaction.Toolkit");
        if (type == null)
        {
            Debug.LogWarning("[Adinkra] XRSimpleInteractable type unavailable; Quest select not wired.");
            return;
        }

        Component interactable = target.GetComponent(type) ?? target.AddComponent(type);
        object selectEntered = type.GetProperty("selectEntered")?.GetValue(interactable)
                               ?? type.GetField("selectEntered")?.GetValue(interactable)
                               ?? type.GetField("m_SelectEntered",
                                       System.Reflection.BindingFlags.Instance |
                                       System.Reflection.BindingFlags.NonPublic)
                                   ?.GetValue(interactable);

        if (selectEntered is UnityEventBase unityEvent)
        {
            for (int i = unityEvent.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(unityEvent, i);
            UnityEventTools.AddVoidPersistentListener(unityEvent, controller.OnXRSelect);
            EditorUtility.SetDirty(interactable);
        }
    }

    // ---- Assets / scene helpers -----------------------------------------

    private static void EnsureFolders()
    {
        foreach (string folder in new[]
                 {
                     ExhibitRoot, ExhibitRoot + "/Scripts", ExhibitRoot + "/Editor", PrefabRoot, MaterialRoot,
                 })
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }
        }
    }

    private static Material CreateMaterial(string name, Color color, float metallic, float smoothness)
    {
        string path = $"{MaterialRoot}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);

        EditorUtility.SetDirty(material);
        return material;
    }

    /// <summary>
    /// Ground height for a staging slot. Starts below Boundary_Top (y 13.28) and
    /// skips the invisible boundary shells so the row lands on the porch ground
    /// rather than on a containment collider.
    /// </summary>
    private static float SampleGroundY(float x, float z, float fallback)
    {
        var hits = new List<RaycastHit>(Physics.RaycastAll(
            new Ray(new Vector3(x, 12f, z), Vector3.down), 40f, ~0, QueryTriggerInteraction.Ignore));

        float best = float.NegativeInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null ||
                hit.collider.name.StartsWith("Boundary", StringComparison.Ordinal) ||
                IsOwnStagingCollider(hit.collider))
                continue;
            if (hit.point.y > best)
                best = hit.point.y;
        }

        return float.IsNegativeInfinity(best) ? fallback : best;
    }

    /// <summary>Never measure the ground against a previous staging pass.</summary>
    private static bool IsOwnStagingCollider(Collider collider)
    {
        for (Transform t = collider.transform; t != null; t = t.parent)
        {
            if (t.name == StagingRootName)
                return true;
        }

        return false;
    }
}
