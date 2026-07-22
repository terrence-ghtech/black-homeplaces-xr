using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class BackyardFlowerbedImagePopupSetup
{
    private const string ScenePath = "Assets/BH_XR_MainScene.unity";
    private const string FlowerbedPath = "_SceneContent/Home/Exterior/Backyard/flowerbed";
    private const string PopupRootName = "MyGrandmasGardenImagePopup";
    private const string TexturePath = "Assets/BCaT_assets/Meshell_Sturgis/My Grandma's Garden.png";
    private const float PopupDistance = 1.65f;
    private static readonly Vector2 PopupSize = new Vector2(999f, 1111f);
    private static readonly Vector2 GardenImageSize = new Vector2(680f, 1111f);
    private static readonly Vector2 GardenImagePosition = new Vector2(0f, -144f);

    [MenuItem("BCaT/Meshell/Setup Backyard Flowerbed Image Popup")]
    public static void Setup()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject flowerbed = GameObject.Find(FlowerbedPath);
        if (flowerbed == null)
            throw new System.InvalidOperationException($"Missing flowerbed at {FlowerbedPath}");

        Texture2D gardenTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (gardenTexture == null)
            throw new System.InvalidOperationException($"Missing texture at {TexturePath}");

        ConfigureTextureImport(TexturePath);
        SimpleImagePopupController controller = CreateOrUpdatePopup(flowerbed.transform.parent, gardenTexture);
        TMP_Text promptText = CreateOrUpdatePrompt(flowerbed.transform);
        ConfigureFlowerbedInteraction(flowerbed, controller, promptText);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Backyard flowerbed image popup setup complete.");
    }

    private static void ConfigureTextureImport(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 4096;
        importer.SaveAndReimport();
    }

    private static SimpleImagePopupController CreateOrUpdatePopup(Transform parent, Texture2D texture)
    {
        GameObject holder = GameObject.Find(PopupRootName);
        if (holder == null)
        {
            holder = new GameObject(PopupRootName);
            Undo.RegisterCreatedObjectUndo(holder, "Create flowerbed image popup");
        }

        holder.transform.SetParent(parent, false);
        holder.transform.localPosition = Vector3.zero;
        holder.transform.localRotation = Quaternion.identity;
        holder.transform.localScale = Vector3.one;

        SimpleImagePopupController controller = GetOrAdd<SimpleImagePopupController>(holder);

        GameObject popupRoot = GetOrCreateChild(holder.transform, "PopupRoot");
        SetLayerRecursive(popupRoot, LayerMask.NameToLayer("UI"));

        Canvas canvas = GetOrAdd<Canvas>(popupRoot);
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;
        GetOrAdd<GraphicRaycaster>(popupRoot);
        AddTrackedDeviceGraphicRaycasterIfAvailable(popupRoot);

        RectTransform canvasRect = GetOrAdd<RectTransform>(popupRoot);
        canvasRect.sizeDelta = PopupSize;
        popupRoot.transform.localScale = Vector3.one * 0.001f;

        GameObject panelObject = GetOrCreateChild(popupRoot.transform, "BackgroundPanel");
        Image panel = GetOrAdd<Image>(panelObject);
        panel.color = new Color(0.04f, 0.045f, 0.05f, 0.88f);
        RectTransform panelRect = GetOrAdd<RectTransform>(panelObject);
        Anchor(panelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, PopupSize);

        TMP_Text title = CreateOrUpdateText(panelObject.transform, "Title", "My Grandma's Garden", 36f, TextAlignmentOptions.Left);
        RectTransform titleRect = title.rectTransform;
        Anchor(titleRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(48f, -90f), new Vector2(-110f, 48f));

        GameObject imageObject = GetOrCreateChild(panelObject.transform, "GardenImage");
        Image popupImage = GetOrAdd<Image>(imageObject);
        popupImage.color = Color.white;
        popupImage.preserveAspect = true;
        RectTransform imageRect = GetOrAdd<RectTransform>(imageObject);
        Anchor(imageRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), GardenImagePosition, GardenImageSize);
        imageRect.localRotation = Quaternion.Euler(0f, 0f, -90f);

        Button closeButton = CreateOrUpdateCloseButton(panelObject.transform);

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("imageTexture").objectReferenceValue = texture;
        so.FindProperty("title").stringValue = "My Grandma's Garden";
        so.FindProperty("popupRoot").objectReferenceValue = popupRoot;
        so.FindProperty("popupCanvas").objectReferenceValue = canvas;
        so.FindProperty("image").objectReferenceValue = popupImage;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.FindProperty("openDistanceFromCamera").floatValue = PopupDistance;
        so.ApplyModifiedPropertiesWithoutUndo();

        popupRoot.SetActive(false);
        canvas.enabled = false;
        return controller;
    }

    private static TMP_Text CreateOrUpdatePrompt(Transform flowerbed)
    {
        GameObject promptCanvasObject = GetOrCreateChild(flowerbed, "MyGrandmasGardenPrompt");
        SetLayerRecursive(promptCanvasObject, LayerMask.NameToLayer("UI"));

        Canvas canvas = GetOrAdd<Canvas>(promptCanvasObject);
        canvas.renderMode = RenderMode.WorldSpace;
        GetOrAdd<GraphicRaycaster>(promptCanvasObject);
        AddTrackedDeviceGraphicRaycasterIfAvailable(promptCanvasObject);

        RectTransform canvasRect = GetOrAdd<RectTransform>(promptCanvasObject);
        canvasRect.sizeDelta = new Vector2(520f, 72f);
        promptCanvasObject.transform.localPosition = new Vector3(0f, 1.35f, 0f);
        promptCanvasObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        promptCanvasObject.transform.localScale = Vector3.one * 0.003f;

        GameObject textObject = GetOrCreateChild(promptCanvasObject.transform, "PromptText");
        TMP_Text text = GetOrAdd<TextMeshProUGUI>(textObject);
        text.text = "Press E to view My Grandma's Garden.";
        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        RectTransform textRect = text.rectTransform;
        Stretch(textRect, Vector2.zero, Vector2.zero);
        return text;
    }

    private static void ConfigureFlowerbedInteraction(GameObject flowerbed, SimpleImagePopupController controller, TMP_Text promptText)
    {
        BoxCollider collider = GetOrAdd<BoxCollider>(flowerbed);
        Bounds bounds = CalculateChildRendererBounds(flowerbed);
        collider.center = flowerbed.transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = flowerbed.transform.InverseTransformVector(bounds.size);
        collider.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        collider.isTrigger = false;

        Rigidbody rigidbody = GetOrAdd<Rigidbody>(flowerbed);
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;

        SimpleImagePopupInteractor interactor = GetOrAdd<SimpleImagePopupInteractor>(flowerbed);
        SerializedObject interactorSo = new SerializedObject(interactor);
        interactorSo.FindProperty("popup").objectReferenceValue = controller;
        interactorSo.FindProperty("playerCamera").objectReferenceValue = Camera.main;
        interactorSo.FindProperty("interactionDistance").floatValue = 4f;
        interactorSo.FindProperty("promptText").objectReferenceValue = promptText;
        interactorSo.FindProperty("desktopPrompt").stringValue = "Press E to view My Grandma's Garden.";
        interactorSo.FindProperty("xrPrompt").stringValue = "Interact to view My Grandma's Garden.";
        interactorSo.ApplyModifiedPropertiesWithoutUndo();

        XRSimpleInteractable xrInteractable = GetOrAdd<XRSimpleInteractable>(flowerbed);
        xrInteractable.colliders.Clear();
        xrInteractable.colliders.Add(collider);
        for (int i = xrInteractable.selectEntered.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(xrInteractable.selectEntered, i);

        UnityAction<SelectEnterEventArgs> openAction = interactor.OpenFromXR;
        UnityEventTools.AddPersistentListener(xrInteractable.selectEntered, openAction);
        EditorUtility.SetDirty(xrInteractable);
    }

    private static Bounds CalculateChildRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 size = bounds.size;
        size.x = Mathf.Max(size.x, 0.5f);
        size.y = Mathf.Max(size.y, 0.5f);
        size.z = Mathf.Max(size.z, 0.5f);
        bounds.size = size;
        return bounds;
    }

    private static Button CreateOrUpdateCloseButton(Transform parent)
    {
        GameObject buttonObject = GetOrCreateChild(parent, "CloseButton");
        Image background = GetOrAdd<Image>(buttonObject);
        background.color = new Color(0.12f, 0.13f, 0.15f, 0.95f);

        Button button = GetOrAdd<Button>(buttonObject);
        RectTransform rect = GetOrAdd<RectTransform>(buttonObject);
        Anchor(rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-54f, -54f), new Vector2(56f, 56f));

        TMP_Text label = CreateOrUpdateText(buttonObject.transform, "Label", "X", 32f, TextAlignmentOptions.Center);
        Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
        return button;
    }

    private static TMP_Text CreateOrUpdateText(Transform parent, string name, string value, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = GetOrCreateChild(parent, name);
        TMP_Text text = GetOrAdd<TextMeshProUGUI>(textObject);
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        return text;
    }

    private static GameObject GetOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing.gameObject;

        GameObject child = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(child, $"Create {name}");
        child.transform.SetParent(parent, false);
        return child;
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void Stretch(RectTransform rectTransform, Vector2 offsetMin, Vector2 offsetMax)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
    }

    private static void Anchor(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;
    }

    private static void SetLayerRecursive(GameObject gameObject, int layer)
    {
        if (layer < 0)
            return;

        gameObject.layer = layer;
        foreach (Transform child in gameObject.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    private static void AddTrackedDeviceGraphicRaycasterIfAvailable(GameObject gameObject)
    {
        System.Type type = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
        if (type != null && gameObject.GetComponent(type) == null)
            gameObject.AddComponent(type);
    }
}
