using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PrivacyLawExhibitSceneBuilder
{
    private const string RootName = "PrivacyLawExhibit_ROOT";
    private const string BasePath = "Assets/BCaT/Exhibits/PrivacyLawExhibit";
    private const string PrefabPath = BasePath + "/Prefabs/PrivacyLawExhibit.prefab";
    private const string ScenePath = "Assets/BH_XR_MainScene.unity";
    private const string Paragraph1 = "The current American legal structure facilitates 2 zones: zone 1 for the outside and zone 2 for the inside. Zone 1 is always open for police and other law enforcement officers: officers may use their own vision to see things in plain view and may even surveil outside and around the home. Getting to zone 2 is a bit harder: to be allowed into zone 2, there must be judicial approval, where police officers justify their need to enter the zone. These 2 zones are a limiting binary structure.";
    private const string Paragraph2 = "This blueprint is primarily used to reorient the conception of privacy and the homeplace. Recognizing that the home is more than just a home, but for many Black Americans, a homeplace that serves as a place of refuge, belonging, and a brave space for cultivating resistance, calls for a critical reorientation of what privacy means in the homeplace. Privacy should not be conceptualized as a binary that divides experience into “inside” and “outside.” Although the distinction between inside and outside is crucial, there are many zones of privacy within the homeplace that must be considered.";
    private const string Paragraph3 = "When these distinct zones are ignored, privacy is violated on multiple levels. In Figure 3, the footprints represent more than a single intrusion. The first pair marks the breach the law recognizes, but the others reveal the deeper wounds—those that constrict a person’s ability to feel safe, to create, and to heal within their own home. Even after the physical intrusion ends, the figure shows a lingering haze that settles over the space. The loss of privacy leaves the home stripped of safety and belonging, and the homeowner is left to rebuild from what remains.";
    private const string Paragraph4 = "For Black Americans, these intrusions cut deeper. In a world that so often perceives Black people as “other,” as lesser, or as a suspect before a neighbor, the homeplace becomes a refuge. It is where one can breathe, develop, create oneself, and craft a life shielded from prying eyes. So many Black people are forced to code-switch to survive: to adjust their speech, posture, or presence to meet the expectations of those who call the police for nonexistent infractions or judge based on long-held stereotypes. These paradigms, rooted deeply in the foundation of the United States, shape the daily realities of Black life. They can constrict in some moments and outright restrict in others.";
    private const string Paragraph5 = "The homeplace is the space where Black people can simply be–where memories are curated in the front room, where privacy is found in a bedroom, where living, without performance, is allowed. Intrusions into these multiple zones of privacy abruptly shatter that possibility.";

    [MenuItem("BCaT/Exhibits/Build Privacy Law Exhibit")]
    public static void Build()
    {
        Directory.CreateDirectory(BasePath + "/Materials");
        Directory.CreateDirectory(BasePath + "/Textures");
        Directory.CreateDirectory(BasePath + "/Prefabs");
        Directory.CreateDirectory(BasePath + "/Documentation");

        Material line = CreateMaterial("PrivacyLaw_HologramLine", new Color(0.43f, 0.78f, 1f, 0.82f), true);
        Material panel = CreateMaterial("PrivacyLaw_Panel", new Color(0.02f, 0.17f, 0.30f, 0.34f), true);
        Material dimPanel = CreateMaterial("PrivacyLaw_DarkPanel", new Color(0.01f, 0.05f, 0.10f, 0.82f), true);

        Sprite figure01 = AssetDatabase.LoadAssetAtPath<Sprite>(BasePath + "/Textures/PrivacyLaw_Figure01.png");
        Sprite figure02 = AssetDatabase.LoadAssetAtPath<Sprite>(BasePath + "/Textures/PrivacyLaw_Figure02.png");
        Sprite figure03 = AssetDatabase.LoadAssetAtPath<Sprite>(BasePath + "/Textures/PrivacyLaw_Figure03.jpg");

        GameObject prefabRoot = CreateExhibit(line, panel, dimPanel, figure01, figure02, figure03);
        PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        Object.DestroyImmediate(prefabRoot);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = RootName;
        instance.transform.SetPositionAndRotation(new Vector3(-0.75f, 0.05f, -3.2f), Quaternion.Euler(0f, 180f, 0f));
        instance.transform.localScale = Vector3.one;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.Refresh();
        Debug.Log($"Built {RootName}, prefab saved to {PrefabPath}, scene updated at {ScenePath}.");
    }

    private static GameObject CreateExhibit(Material line, Material panel, Material dimPanel, Sprite figure01, Sprite figure02, Sprite figure03)
    {
        GameObject root = new GameObject(RootName);

        GameObject trigger = Child(root.transform, "ProximityTrigger");
        BoxCollider triggerCollider = trigger.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector3(3.8f, 2.4f, 3.2f);
        triggerCollider.center = new Vector3(0f, 1.1f, 0f);

        GameObject idle = Child(root.transform, "IdleHologram");
        CanvasGroup idleGroup = idle.AddComponent<CanvasGroup>();
        GameObject core = Child(idle.transform, "HologramCore");
        GameObject blueprint = Quad(core.transform, "BlueprintPanel", panel, new Vector3(0f, 1.15f, 0f), new Vector3(0f, 0f, -8f), new Vector3(1.2f, 0.72f, 1f));
        Child(blueprint.transform, "BlueprintHouseGraphic");
        DrawFloorPlan(blueprint.transform, line);
        Transform ring01 = Ring(idle.transform, "OrbitRing_01", line, 0.92f, 0.34f, 1.02f, 0f);
        Transform ring02 = Ring(idle.transform, "OrbitRing_02", line, 1.05f, 0.39f, 1.12f, 15f);
        Transform ring03 = Ring(idle.transform, "OrbitRing_03", line, 0.78f, 0.29f, 0.92f, -18f);
        CreateDataLines(idle.transform, line);
        GameObject ground = Child(idle.transform, "GroundProjection");
        Ring(ground.transform, "GroundRing_01", line, 0.78f, 0.78f, 0.03f, 0f);
        Ring(ground.transform, "GroundRing_02", line, 0.48f, 0.48f, 0.035f, 0f);
        GameObject anim = Child(idle.transform, "HologramAnimation");

        GameObject prompt = CreatePrompt(root.transform);
        TMP_Text promptText = prompt.transform.Find("PromptText").GetComponent<TMP_Text>();

        GameObject expanded = Child(root.transform, "ExpandedExhibit");
        CanvasGroup expandedGroup = expanded.AddComponent<CanvasGroup>();
        Canvas canvas = expanded.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        expanded.AddComponent<GraphicRaycaster>();
        AddComponentIfAvailable(expanded, "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster");
        RectTransform cr = expanded.GetComponent<RectTransform>();
        cr.sizeDelta = new Vector2(1400f, 760f);
        expanded.transform.localPosition = new Vector3(0f, 1.55f, 0.18f);
        expanded.transform.localRotation = Quaternion.identity;
        expanded.transform.localScale = Vector3.one * 0.00135f;

        Image mainPanel = UiImage(expanded.transform, "MainPanel", new Color(0.01f, 0.06f, 0.12f, 0.86f), new Vector2(0f, 0f), new Vector2(1360f, 720f));
        mainPanel.material = dimPanel;
        RectTransform header = UiGroup(expanded.transform, "Header", new Vector2(275f, 308f), new Vector2(780f, 80f));
        Text(header, "1", new Vector2(-355f, 10f), new Vector2(42f, 42f), 22, TextAlignmentOptions.Center);
        TMP_Text title = Text(header, "The 2 Legally Recognized Zones", new Vector2(45f, 10f), new Vector2(690f, 48f), 26, TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;

        GameObject nav = Child(expanded.transform, "PageNavigation");
        RectTransform navRt = nav.AddComponent<RectTransform>();
        navRt.anchoredPosition = new Vector2(-475f, 85f);
        navRt.sizeDelta = new Vector2(300f, 420f);
        Image[] navBgs = new Image[3];
        Button b1 = NavButton(nav.transform, "PageButton_01", "1", "The 2 Legally\nRecognized Zones", 130f, out navBgs[0]);
        Button b2 = NavButton(nav.transform, "PageButton_02", "2", "The Family's\nThree-Bedroom Home", 25f, out navBgs[1]);
        Button b3 = NavButton(nav.transform, "PageButton_03", "3", "The Haze", -80f, out navBgs[2]);

        GameObject content = Child(expanded.transform, "ContentArea");
        GameObject page01 = Page(content.transform, "Page_01", "The 2 Legally Recognized Zones", figure01, Paragraph1);
        GameObject page02 = Page(content.transform, "Page_02", "The Family’s Three-Bedroom Home", figure02, Paragraph2);
        GameObject page03 = Page(content.transform, "Page_03", "The Haze", figure03, Paragraph3 + "\n\n" + Paragraph4 + "\n\n" + Paragraph5);

        Button prev = SmallButton(expanded.transform, "PreviousButton", "<", new Vector2(-185f, -288f), new Vector2(58f, 58f));
        TMP_Text indicator = Text(expanded.transform, "PageIndicator", new Vector2(0f, -288f), new Vector2(130f, 48f), 26, TextAlignmentOptions.Center);
        Button next = SmallButton(expanded.transform, "NextButton", ">", new Vector2(185f, -288f), new Vector2(58f, 58f));
        Button close = SmallButton(expanded.transform, "CloseButton", "Close Exhibit", new Vector2(505f, -288f), new Vector2(210f, 58f));
        Button closeIcon = SmallButton(expanded.transform, "CloseIconButton", "X", new Vector2(642f, 320f), new Vector2(46f, 46f));

        GameObject controllerObject = Child(root.transform, "PrivacyLawExhibitController");
        PrivacyLawExhibitController controller = controllerObject.AddComponent<PrivacyLawExhibitController>();
        controller.Configure(idle, expanded, prompt, idleGroup, expandedGroup, canvas, page01, page02, page03, null, indicator, b1, b2, b3, prev, next, close, closeIcon, navBgs, anim.transform, blueprint.transform, ring01, ring02, ring03, promptText);
        PrivacyLawProximityRelay relay = trigger.AddComponent<PrivacyLawProximityRelay>();
        SerializedObject relaySo = new SerializedObject(relay);
        relaySo.FindProperty("controller").objectReferenceValue = controller;
        relaySo.ApplyModifiedPropertiesWithoutUndo();

        Component xr = AddComponentIfAvailable(trigger, "UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable");
        WireXrSelect(xr, controller);

        EnsureEventSystem();
        return root;
    }

    private static GameObject CreatePrompt(Transform parent)
    {
        GameObject prompt = Child(parent, "InteractionPrompt");
        Canvas canvas = prompt.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        prompt.AddComponent<GraphicRaycaster>();
        RectTransform rt = prompt.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(520f, 96f);
        prompt.transform.localPosition = new Vector3(0f, 0.55f, -0.22f);
        prompt.transform.localScale = Vector3.one * 0.0016f;
        UiImage(prompt.transform, "PromptBackground", new Color(0.01f, 0.08f, 0.15f, 0.82f), Vector2.zero, new Vector2(500f, 80f));
        Text(prompt.transform, "PromptIcon", new Vector2(-205f, 0f), new Vector2(52f, 52f), 28, TextAlignmentOptions.Center).text = "X";
        TMP_Text label = Text(prompt.transform, "PromptText", new Vector2(40f, 0f), new Vector2(390f, 58f), 24, TextAlignmentOptions.Left);
        label.text = "Press E to Examine Privacy Exhibit";
        return prompt;
    }

    private static GameObject Page(Transform parent, string name, string title, Sprite figure, string body)
    {
        GameObject page = Child(parent, name);
        RectTransform rt = page.AddComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(205f, 20f);
        rt.sizeDelta = new Vector2(820f, 520f);
        Image fig = UiImage(page.transform, "Figure", new Color(0.04f, 0.25f, 0.40f, 0.65f), new Vector2(0f, 125f), new Vector2(760f, 245f));
        fig.sprite = figure;
        fig.preserveAspect = true;
        Text(page.transform, title, new Vector2(0f, 275f), new Vector2(760f, 54f), 24, TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;
        TMP_Text paragraph = Text(page.transform, body, new Vector2(0f, -115f), new Vector2(760f, 210f), 21, TextAlignmentOptions.TopLeft);
        paragraph.textWrappingMode = TextWrappingModes.Normal;
        paragraph.lineSpacing = 9f;
        return page;
    }

    private static void DrawFloorPlan(Transform parent, Material mat)
    {
        Vector3[] pts = { new(-0.42f, -0.18f, -0.01f), new(0.42f, -0.18f, -0.01f), new(0.42f, 0.18f, -0.01f), new(-0.42f, 0.18f, -0.01f), new(-0.42f, -0.18f, -0.01f) };
        Polyline(parent, "BlueprintOutline", mat, pts, 0.01f);
        Polyline(parent, "BlueprintRooms", mat, new[] { new Vector3(-0.12f, -0.18f, -0.01f), new Vector3(-0.12f, 0.18f, -0.01f), new Vector3(0.18f, 0.18f, -0.01f), new Vector3(0.18f, -0.18f, -0.01f), new Vector3(0.18f, 0f, -0.01f), new Vector3(-0.42f, 0f, -0.01f) }, 0.008f);
    }

    private static void CreateDataLines(Transform parent, Material mat)
    {
        GameObject holder = Child(parent, "VerticalDataLines");
        for (int i = 0; i < 10; i++)
        {
            float x = -0.7f + i * 0.155f;
            Polyline(holder.transform, "DataLine_" + (i + 1).ToString("00"), mat, new[] { new Vector3(x, 1.75f, 0.02f), new Vector3(x, 0.75f + (i % 3) * 0.1f, 0.02f) }, 0.004f);
        }
    }

    private static Transform Ring(Transform parent, string name, Material mat, float rx, float rz, float y, float tilt)
    {
        Vector3[] points = new Vector3[73];
        for (int i = 0; i < points.Length; i++)
        {
            float a = Mathf.PI * 2f * i / (points.Length - 1);
            points[i] = new Vector3(Mathf.Cos(a) * rx, y, Mathf.Sin(a) * rz);
        }
        LineRenderer lr = Polyline(parent, name, mat, points, 0.008f);
        lr.transform.localRotation = Quaternion.Euler(tilt, 0f, 0f);
        return lr.transform;
    }

    private static LineRenderer Polyline(Transform parent, string name, Material mat, Vector3[] points, float width)
    {
        GameObject go = Child(parent, name);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.material = mat;
        lr.positionCount = points.Length;
        lr.SetPositions(points);
        lr.startWidth = width;
        lr.endWidth = width;
        lr.numCapVertices = 3;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    private static Button NavButton(Transform parent, string name, string badge, string label, float y, out Image bg)
    {
        Button b = SmallButton(parent, name, "", new Vector2(0f, y), new Vector2(260f, 78f));
        bg = b.GetComponent<Image>();
        Text(b.transform, badge, new Vector2(-100f, 0f), new Vector2(42f, 42f), 24, TextAlignmentOptions.Center);
        Text(b.transform, label, new Vector2(28f, 0f), new Vector2(160f, 54f), 16, TextAlignmentOptions.Left);
        return b;
    }

    private static Button SmallButton(Transform parent, string name, string text, Vector2 pos, Vector2 size)
    {
        Image img = UiImage(parent, name, new Color(0.02f, 0.18f, 0.30f, 0.66f), pos, size);
        Button button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        if (!string.IsNullOrEmpty(text))
            Text(img.transform, text, Vector2.zero, size, 24, TextAlignmentOptions.Center);
        return button;
    }

    private static TMP_Text Text(Transform parent, string text, Vector2 pos, Vector2 size, int fontSize, TextAlignmentOptions align)
    {
        GameObject go = Child(parent, text.Length > 28 ? "Text" : text);
        TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = new Color(0.91f, 0.97f, 1f, 1f);
        tmp.alignment = align;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return tmp;
    }

    private static RectTransform UiGroup(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        GameObject go = Child(parent, name);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static Image UiImage(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
    {
        GameObject go = Child(parent, name);
        Image img = go.AddComponent<Image>();
        img.color = color;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return img;
    }

    private static GameObject Quad(Transform parent, string name, Material mat, Vector3 pos, Vector3 euler, Vector3 scale)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go;
    }

    private static GameObject Child(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Material CreateMaterial(string name, Color color, bool transparent)
    {
        string path = $"{BasePath}/Materials/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_EmissionColor", new Color(color.r, color.g, color.b, 1f) * 1.5f);
        if (transparent)
        {
            mat.SetFloat("_Surface", 1f);
            mat.renderQueue = 3000;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Sprite CreateMaintenanceSprite(string name)
    {
        string path = $"{BasePath}/Textures/{name}.png";
        if (!File.Exists(path))
        {
            Texture2D tex = new Texture2D(512, 256, TextureFormat.RGBA32, false);
            Color bg = new Color(0.02f, 0.13f, 0.23f, 0.92f);
            Color line = new Color(0.43f, 0.78f, 1f, 1f);
            for (int y = 0; y < tex.height; y++)
            for (int x = 0; x < tex.width; x++)
                tex.SetPixel(x, y, (x % 32 == 0 || y % 32 == 0 || x < 3 || y < 3 || x > tex.width - 4 || y > tex.height - 4) ? line : bg);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
        }
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
            return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    private static Component AddComponentIfAvailable(GameObject target, string fullTypeName)
    {
        foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type type = assembly.GetType(fullTypeName);
            if (type == null || !typeof(Component).IsAssignableFrom(type))
                continue;

            return target.AddComponent(type);
        }

        Debug.LogWarning($"Optional component not found for Privacy Law Exhibit: {fullTypeName}");
        return null;
    }

    private static void WireXrSelect(Component xrInteractable, PrivacyLawExhibitController controller)
    {
        if (xrInteractable == null || controller == null)
            return;

        System.Type type = xrInteractable.GetType();
        object selectEntered =
            type.GetProperty("selectEntered")?.GetValue(xrInteractable)
            ?? type.GetField("selectEntered")?.GetValue(xrInteractable);

        if (selectEntered is UnityEventBase unityEvent)
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(unityEvent, controller.OpenFromXR);
    }
}
