using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BlackKitchenExperienceBuilder
{
    private const string BasePath = "Assets/BCaT/Exhibits/BlackKitchen";
    private const string MainScenePath = "Assets/BH_XR_MainScene.unity";
    private const string MemoryScenePath = BasePath + "/Scenes/BlackKitchen_MemoryScene.unity";
    private const string ModelPath = BasePath + "/Models/BlackKitchen_ScannedEnvironment.glb";
    private const string PortalRootName = "BlackKitchenPortal_ROOT";
    private const string ExperienceRootName = "BlackKitchenExperience_ROOT";

    [MenuItem("BCaT/Exhibits/Build Black Kitchen Experience")]
    public static void Build()
    {
        EnsureFolders();
        CreateMaterials();
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
        BuildMemoryScene();
        BuildPortalInMainScene();
        EnsureBuildScene();
        WriteDocumentation();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Black Kitchen experience build complete.");
    }

    [MenuItem("BCaT/Exhibits/Repair Black Kitchen Spawn And Grounding")]
    public static void RepairSpawnAndGrounding()
    {
        Scene scene = EditorSceneManager.OpenScene(MemoryScenePath, OpenSceneMode.Single);
        GameObject root = GameObject.Find(ExperienceRootName);
        if (root == null)
        {
            Debug.LogError("Black Kitchen experience root was not found. Run the full Black Kitchen builder before repairing spawn and grounding.");
            return;
        }

        Transform spawn = root.transform.Find("SpawnPoint") ?? Child(root.transform, "SpawnPoint").transform;
        spawn.SetPositionAndRotation(new Vector3(0f, 0.02f, -2.25f), Quaternion.identity);

        Transform bounds = root.transform.Find("NavigationBounds") ?? Child(root.transform, "NavigationBounds").transform;
        Transform safetyFloor = bounds.Find("SpawnSafetyFloor");
        if (safetyFloor == null)
            safetyFloor = Child(bounds, "SpawnSafetyFloor").transform;

        safetyFloor.localPosition = new Vector3(0f, -0.08f, -2.25f);
        safetyFloor.localRotation = Quaternion.identity;
        safetyFloor.localScale = Vector3.one;

        foreach (Renderer renderer in safetyFloor.GetComponentsInChildren<Renderer>(true))
            Object.DestroyImmediate(renderer);
        foreach (MeshFilter meshFilter in safetyFloor.GetComponentsInChildren<MeshFilter>(true))
            Object.DestroyImmediate(meshFilter);

        BoxCollider floorCollider = safetyFloor.GetComponent<BoxCollider>();
        if (floorCollider == null)
            floorCollider = safetyFloor.gameObject.AddComponent<BoxCollider>();
        floorCollider.isTrigger = false;
        floorCollider.center = Vector3.zero;
        floorCollider.size = new Vector3(7f, 0.16f, 6f);

        BlackKitchenExperienceController controller = Object.FindFirstObjectByType<BlackKitchenExperienceController>();
        if (controller != null)
        {
            SerializedObject controllerObject = new SerializedObject(controller);
            SerializedProperty fallThreshold = controllerObject.FindProperty("fallRecoveryYThreshold");
            if (fallThreshold != null)
                fallThreshold.floatValue = -2.5f;
            SerializedProperty fallEnabled = controllerObject.FindProperty("enableFallRecovery");
            if (fallEnabled != null)
                fallEnabled.boolValue = true;
            controllerObject.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Black Kitchen spawn and grounding repair complete.");
    }

    private static void EnsureFolders()
    {
        Directory.CreateDirectory(BasePath + "/Audio");
        Directory.CreateDirectory(BasePath + "/Editor");
        Directory.CreateDirectory(BasePath + "/Materials");
        Directory.CreateDirectory(BasePath + "/Models/Reference");
        Directory.CreateDirectory(BasePath + "/Prefabs");
        Directory.CreateDirectory(BasePath + "/Scenes");
        Directory.CreateDirectory(BasePath + "/Scripts");
        Directory.CreateDirectory(BasePath + "/Textures");
        Directory.CreateDirectory(BasePath + "/Documentation");
    }

    private static void CreateMaterials()
    {
        CreateMaterial("BlackKitchen_DarkFloor", new Color(0.025f, 0.025f, 0.025f, 1f));
        CreateMaterial("BlackKitchen_DarkWall", new Color(0.015f, 0.015f, 0.017f, 1f));
        CreateMaterial("BlackKitchen_PromptPanel", new Color(0.02f, 0.025f, 0.028f, 0.88f));
    }

    private static Material CreateMaterial(string name, Color color)
    {
        string path = BasePath + "/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        return material;
    }

    private static void BuildMemoryScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject root = new GameObject(ExperienceRootName);
        GameObject spawn = Child(root.transform, "SpawnPoint");
        spawn.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, -4.25f), Quaternion.Euler(0f, 0f, 0f));

        GameObject environment = Child(root.transform, "ScannedKitchenEnvironment");
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model != null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            instance.name = "BlackKitchen_ScannedEnvironment";
            instance.transform.SetParent(environment.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            DisableShadows(instance);
        }
        else
        {
            Debug.LogWarning("Black Kitchen GLB did not import as a GameObject yet. Scene keeps ScannedKitchenEnvironment as a slot.");
        }

        GameObject bounds = Child(root.transform, "NavigationBounds");
        CreateFloor(bounds.transform);
        Box(bounds.transform, "Boundary_Back", new Vector3(0f, 1.5f, 3.7f), new Vector3(7f, 3f, 0.25f), true);
        Box(bounds.transform, "Boundary_Front", new Vector3(0f, 1.5f, -5.2f), new Vector3(7f, 3f, 0.25f), true);
        Box(bounds.transform, "Boundary_Left", new Vector3(-3.5f, 1.5f, -0.75f), new Vector3(0.25f, 3f, 8.9f), true);
        Box(bounds.transform, "Boundary_Right", new Vector3(3.5f, 1.5f, -0.75f), new Vector3(0.25f, 3f, 8.9f), true);
        Box(bounds.transform, "Counter_CollisionProxy", new Vector3(0f, 0.65f, -0.4f), new Vector3(2.2f, 1.3f, 0.9f), true);
        Box(bounds.transform, "Appliance_CollisionProxy", new Vector3(-1.9f, 0.7f, 0.9f), new Vector3(1.2f, 1.4f, 1.1f), true);

        BlackKitchenAudioCoordinator coordinator = Child(root.transform, "BlackKitchenAudioCoordinator").AddComponent<BlackKitchenAudioCoordinator>();

        GameObject exit = Child(root.transform, "ExitInterface");
        exit.transform.position = new Vector3(2.55f, 1.15f, -3.75f);
        BoxCollider exitCollider = exit.AddComponent<BoxCollider>();
        exitCollider.isTrigger = true;
        exitCollider.size = new Vector3(1.4f, 1f, 0.2f);
        exit.AddComponent<Rigidbody>().isKinematic = true;
        TMP_Text exitPrompt = CreatePrompt(exit.transform, "ExitPrompt", Vector3.zero, "Press E to Exit Black Kitchen");

        AudioSource exitReflection = AudioSourceObject(root.transform, "ExitReflectionAudio", LoadClip("exit_reflection.mp3"), 0f, 1f, 8f, 0f);
        CreateCredits(root.transform);

        GameObject controllerObject = Child(root.transform, "BlackKitchenExperienceController");
        BlackKitchenExperienceController controller = controllerObject.AddComponent<BlackKitchenExperienceController>();
        SetPrivate(controller, "spawnPoint", spawn.transform);
        SetPrivate(controller, "exitReflectionSource", exitReflection);
        SetPrivate(controller, "exitPromptText", exitPrompt);
        SetPrivate(controller, "exitInteractionRoot", exit.transform);
        SetPrivate(controller, "audioCoordinator", coordinator);

        // The five audio stations and the interaction manager share one implementation
        // with the retrofit menu action.
        BlackKitchenAudioStationBuilder.CreateStationsAndManager(root.transform, coordinator, controller);

        Light light = Child(root.transform, "RestrainedAreaLight").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.45f;
        light.color = new Color(0.86f, 0.84f, 0.78f);
        light.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.09f, 0.09f, 0.095f);

        EditorSceneManager.SaveScene(scene, MemoryScenePath);
    }

    private static void BuildPortalInMainScene()
    {
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        GameObject existing = GameObject.Find(PortalRootName);
        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject root = new GameObject(PortalRootName);
        root.transform.SetPositionAndRotation(new Vector3(43.1f, 1.15f, 35.8f), Quaternion.Euler(0f, -90f, 0f));

        GameObject trigger = Child(root.transform, "KitchenIslandTrigger");
        trigger.transform.localPosition = Vector3.zero;
        BoxCollider triggerCollider = trigger.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector3(3.2f, 2.4f, 1.4f);
        trigger.AddComponent<Rigidbody>().isKinematic = true;

        GameObject interactable = Child(root.transform, "KitchenIslandInteractable");
        interactable.transform.localPosition = Vector3.zero;
        BoxCollider interactableCollider = interactable.AddComponent<BoxCollider>();
        interactableCollider.isTrigger = true;
        interactableCollider.size = new Vector3(2.2f, 1.7f, 1.0f);
        interactable.AddComponent<Rigidbody>().isKinematic = true;

        GameObject promptRoot = Child(root.transform, "InteractionPrompt");
        TMP_Text promptText = CreatePrompt(promptRoot.transform, "PromptCanvas", new Vector3(0f, 0.75f, -0.75f), "Press E to Enter Black Kitchen");

        GameObject returnPoint = Child(root.transform, "ReturnPoint");
        returnPoint.transform.SetPositionAndRotation(new Vector3(42.2f, 1.6f, 34.45f), Quaternion.Euler(0f, -30f, 0f));

        CanvasGroup overlay = CreateOverlay(root.transform);
        GameObject controllerObject = Child(root.transform, "BlackKitchenPortalController");
        BlackKitchenPortalController controller = controllerObject.AddComponent<BlackKitchenPortalController>();
        SetPrivate(controller, "transitionOverlay", overlay);
        SetPrivate(controller, "returnPoint", returnPoint.transform);
        SetPrivate(controller, "promptText", promptText);
        SetPrivate(controller, "interactionRoot", interactable.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void EnsureBuildScene()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        foreach (EditorBuildSettingsScene scene in scenes)
        {
            if (scene.path == MemoryScenePath)
                return;
        }

        System.Array.Resize(ref scenes, scenes.Length + 1);
        scenes[^1] = new EditorBuildSettingsScene(MemoryScenePath, true);
        EditorBuildSettings.scenes = scenes;
    }

    private static void WriteDocumentation()
    {
        string text =
            "Black Kitchen implementation notes\n\n" +
            "point_cloud_scan_REFERENCE_ONLY_DO_NOT_BUILD.ply is stored under Models/Reference as development reference only.\n" +
            "It is not referenced by the memory scene, not under Resources, not under StreamingAssets, and not added to Addressables by this builder.\n" +
            "The scanned GLB is used once in BlackKitchen_MemoryScene and has no full-scan MeshCollider.\n";
        File.WriteAllText(BasePath + "/Documentation/BlackKitchen_ImplementationNotes.txt", text);
    }

    private static void DisableShadows(GameObject root)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private static AudioClip LoadClip(string filename)
    {
        return AssetDatabase.LoadAssetAtPath<AudioClip>(BasePath + "/Audio/" + filename);
    }

    private static AudioSource AudioSourceObject(Transform parent, string name, AudioClip clip, float volume, float minDistance, float maxDistance, float spatialBlend)
    {
        GameObject go = Child(parent, name);
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = false;
        source.volume = volume;
        source.spatialBlend = spatialBlend;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        return source;
    }

    private static void CreateFloor(Transform parent)
    {
        Material floorMaterial = AssetDatabase.LoadAssetAtPath<Material>(BasePath + "/Materials/BlackKitchen_DarkFloor.mat");
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "DarkGroundingPlane";
        floor.transform.SetParent(parent, false);
        floor.transform.localPosition = new Vector3(0f, -0.05f, -0.75f);
        floor.transform.localScale = new Vector3(7f, 0.1f, 9f);
        Renderer renderer = floor.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = floorMaterial;
    }

    private static void Box(Transform parent, string name, Vector3 position, Vector3 size, bool invisible)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localScale = size;
        if (invisible)
        {
            Renderer renderer = box.GetComponent<Renderer>();
            if (renderer != null)
                Object.DestroyImmediate(renderer);
        }
    }

    private static TMP_Text CreatePrompt(Transform parent, string name, Vector3 localPosition, string text)
    {
        GameObject canvasObject = Child(parent, name);
        canvasObject.transform.localPosition = localPosition;
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one * 0.0016f;
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObject.AddComponent<GraphicRaycaster>();
        RectTransform canvasRt = canvasObject.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(560f, 96f);

        GameObject background = Child(canvasObject.transform, "PromptBackground");
        Image image = background.AddComponent<Image>();
        image.color = new Color(0.02f, 0.025f, 0.028f, 0.88f);
        RectTransform bgRt = background.GetComponent<RectTransform>();
        bgRt.anchoredPosition = Vector2.zero;
        bgRt.sizeDelta = new Vector2(540f, 80f);

        TMP_Text label = Text(canvasObject.transform, "PromptText", text, 24, TextAlignmentOptions.Center);
        RectTransform labelRt = label.GetComponent<RectTransform>();
        labelRt.anchoredPosition = Vector2.zero;
        labelRt.sizeDelta = new Vector2(500f, 58f);
        return label;
    }

    private static void CreateCredits(Transform parent)
    {
        GameObject panel = Child(parent, "CreditsPanel");
        panel.transform.position = new Vector3(1.55f, 1.35f, -3.85f);
        panel.transform.rotation = Quaternion.Euler(0f, -18f, 0f);
        panel.transform.localScale = Vector3.one * 0.002f;
        Canvas canvas = panel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(760f, 300f);
        Image bg = Child(panel.transform, "CreditsBackground").AddComponent<Image>();
        bg.color = new Color(0.015f, 0.015f, 0.017f, 0.72f);
        bg.GetComponent<RectTransform>().sizeDelta = new Vector2(740f, 280f);
        TMP_Text text = Text(panel.transform, "CreditsText",
            "Credits\nClarisa James \u2014 Educator; Founder, DIVAS for Social Justice\nIn conversation, 2025\nThis work is presented as a living collaboration rather than a memorial or archive.",
            24, TextAlignmentOptions.MidlineLeft);
        RectTransform textRt = text.GetComponent<RectTransform>();
        textRt.anchoredPosition = Vector2.zero;
        textRt.sizeDelta = new Vector2(680f, 220f);
    }

    private static CanvasGroup CreateOverlay(Transform parent)
    {
        GameObject overlayObject = Child(parent, "TransitionOverlay");
        Canvas canvas = overlayObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        CanvasGroup group = overlayObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        Image image = Child(overlayObject.transform, "BlackFade").AddComponent<Image>();
        image.color = Color.black;
        RectTransform rt = image.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return group;
    }

    private static TMP_Text Text(Transform parent, string name, string value, int size, TextAlignmentOptions alignment)
    {
        GameObject go = Child(parent, name);
        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.color = new Color(0.88f, 0.86f, 0.8f, 1f);
        text.alignment = alignment;
        text.enableWordWrapping = true;
        return text;
    }

    private static GameObject Child(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void SetPrivate(Object target, string fieldName, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(fieldName);
        if (property != null)
        {
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

}
