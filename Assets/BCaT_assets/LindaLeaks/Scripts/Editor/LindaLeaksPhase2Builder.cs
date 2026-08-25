using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

// Rebuilds the Linda Leaks exhibit prefabs to the reviewed interaction model:
//   Artifact = the interaction target (selectable / interactable, no floating prompt).
//   Plaque   = a visual guide only: project title/type, description, creator credit,
//              and the interaction hint embedded in its own text (never floating).
// Three self-contained "exhibit grouping" prefabs are produced, each pairing one
// interactable artifact with its own plaque. Everything stays under Assets/BCaT_assets/LindaLeaks.
public static class LindaLeaksPhase2Builder
{
    private const string Root = "Assets/BCaT_assets/LindaLeaks";
    private const string PrefabRoot = Root + "/Prefabs";
    private const string MaterialRoot = Root + "/Materials";
    private const string TextureRoot = Root + "/Textures";
    private const string UIRoot = Root + "/UI";
    private const string ScenePath = "Assets/BH_XR_MainScene.unity";
    private const string MapHubUrl = "https://maphub.net/Linda_Leaks_Archiving_Project/linda-leaks-housing-co-ops";
    private const string LindaLeaksWebsiteUrl = "https://www.honoringlindaleaks.com/";
    private const string VideoAssetPath = Root + "/Linda_Leaks_CHOF_720p.mp4";
    private const string CameraModelPath = Root + "/Models/LL_AntiqueCamera.glb";
    private const string AlbumModelPath = Root + "/Models/LL_PhotoAlbum.glb";

    private const string CameraPrefab = PrefabRoot + "/LindaLeaks_Exhibit_VintageCamera.prefab";
    private const string AlbumPrefab = PrefabRoot + "/LindaLeaks_Exhibit_PhotoAlbum.prefab";
    private const string MapPrefab = PrefabRoot + "/LindaLeaks_Exhibit_HousingMap.prefab";

    // Model rescale + recenter, derived from the GLB bounding boxes so the tabletop
    // artifacts read at believable real-world sizes and sit centered on their base.
    // Camera GLB size 2.415 x 1.324 x 1.864 m at scale 1 -> ~0.41 m wide (antique tabletop camera).
    private const float CameraScale = 0.17f;
    private static readonly Vector3 CameraModelPos = new Vector3(0.0368f, 0.1122f, -0.1525f);
    private static readonly Vector3 CameraColliderSize = new Vector3(0.411f, 0.225f, 0.317f);
    private static readonly Vector3 CameraColliderCenter = new Vector3(0f, 0.1126f, 0f);

    // Album GLB size 3.637 x 2.681 x 3.317 m at scale 1 (with a large origin offset) -> ~0.36 m wide.
    private const float AlbumScale = 0.10f;
    private static readonly Vector3 AlbumModelPos = new Vector3(-0.2389f, 0.0818f, 0.5982f);
    private static readonly Vector3 AlbumColliderSize = new Vector3(0.364f, 0.268f, 0.332f);
    private static readonly Vector3 AlbumColliderCenter = new Vector3(0f, 0.134f, 0f);

    [MenuItem("BCaT/Linda Leaks/Build Phase 2 Prefabs")]
    public static void Build()
    {
        EnsureFolders();
        AssetDatabase.ImportAsset(Root, ImportAssetOptions.ImportRecursive);
        ConfigureImagesAsSprites();

        // Bespoke Linda Leaks palette (BCaT mood board).
        Material deepPlum = CreateMaterial("LL_DeepPlum", new Color(0.20f, 0.12f, 0.24f));
        Material roseClay = CreateMaterial("LL_RoseClay", new Color(0.58f, 0.29f, 0.31f));
        Material warmPaper = CreateMaterial("LL_WarmPaper", new Color(0.86f, 0.78f, 0.64f));
        Material mossGreen = CreateMaterial("LL_MossGreen", new Color(0.24f, 0.34f, 0.25f));
        Material brass = CreateMaterial("LL_Brass", new Color(0.78f, 0.57f, 0.25f), 0.15f, 0.45f);
        RenderTexture videoTexture = CreateRenderTexture();

        GameObject cameraExhibit = BuildVintageCameraExhibit(deepPlum, warmPaper, roseClay, brass, videoTexture);
        GameObject albumExhibit = BuildPhotoAlbumExhibit(deepPlum, warmPaper, roseClay, brass);
        GameObject mapExhibit = BuildHousingMapExhibit(deepPlum, warmPaper, roseClay, brass, mossGreen);

        SavePrefab(cameraExhibit, CameraPrefab);
        SavePrefab(albumExhibit, AlbumPrefab);
        SavePrefab(mapExhibit, MapPrefab);

        Object.DestroyImmediate(cameraExhibit);
        Object.DestroyImmediate(albumExhibit);
        Object.DestroyImmediate(mapExhibit);

        DeleteLegacyPrefabs();
        PlaceReviewInstances();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Linda Leaks Phase 2 exhibit groupings rebuilt and review instances placed in the front yard.");
    }

    // ---- Exhibit groupings ---------------------------------------------------

    private static GameObject BuildVintageCameraExhibit(Material plum, Material paper, Material rose, Material brass, RenderTexture videoTexture)
    {
        GameObject root = new GameObject("LindaLeaks_Exhibit_VintageCamera");

        // Artifact: the antique camera is the interaction target.
        GameObject artifact = new GameObject("Artifact_VintageCamera");
        artifact.transform.SetParent(root.transform, false);
        AddModel(artifact.transform, CameraModelPath, "LL_AntiqueCamera_Model", CameraModelPos, Quaternion.Euler(0, 180, 0), Vector3.one * CameraScale);

        BoxCollider collider = artifact.AddComponent<BoxCollider>();
        collider.size = CameraColliderSize;
        collider.center = CameraColliderCenter;
        AddKinematicBody(artifact);

        // Video popup panel (opened on interaction) — faces the viewer (+Z-forward canvas, no mirroring).
        GameObject panelRoot = new GameObject("LL_HallOfFameVideoPanel");
        panelRoot.transform.SetParent(artifact.transform, false);
        panelRoot.transform.localPosition = new Vector3(0f, 1.6f, -0.6f);
        panelRoot.SetActive(false);

        Canvas canvas = CreateWorldCanvas("Canvas", panelRoot.transform, new Vector2(900, 560), 0.0045f);
        Image backdrop = CreateUIImage("Backdrop", canvas.transform, new Vector2(900, 560), plum.color);
        RawImage rawImage = CreateRawImage("Video", backdrop.transform, new Vector2(800, 450), new Vector2(0, 35), videoTexture);
        CreateUIText("Caption", backdrop.transform, "Linda Leaks — Cooperative Hall of Fame", new Vector2(0, -215), new Vector2(760, 50), 24, paper.color, FontStyles.Bold);
        Button closeButton = CreateButton("CloseButton", backdrop.transform, "Close", new Vector2(320, -235), new Vector2(160, 52), brass.color, Color.black);

        VideoPlayer videoPlayer = artifact.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = AssetDatabase.LoadAssetAtPath<VideoClip>(VideoAssetPath);

        AudioSource audioSource = artifact.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        videoPlayer.SetTargetAudioSource(0, audioSource);

        LindaLeaksVideoPopUp popup = artifact.AddComponent<LindaLeaksVideoPopUp>();
        SetPrivate(popup, "popupRoot", panelRoot);
        SetPrivate(popup, "popupCanvas", canvas);
        SetPrivate(popup, "videoImage", rawImage);
        SetPrivate(popup, "videoPlayer", videoPlayer);
        SetPrivate(popup, "videoAudioSource", audioSource);
        SetPrivate(popup, "videoClip", AssetDatabase.LoadAssetAtPath<VideoClip>(VideoAssetPath));
        SetPrivate(popup, "videoFileName", "Linda_Leaks_CHOF_720p.mp4");
        SetPrivate(popup, "interactionDistance", 5f);

        AddButtonCall(closeButton, popup, nameof(LindaLeaksVideoPopUp.ClosePopUp));
        AddXRSimpleInteractable(artifact, popup, nameof(LindaLeaksVideoPopUp.OpenPopUp));

        // Plaque: guide + description + credit + embedded interaction hint (tabletop placard).
        BuildPlaque(root.transform, "Plaque_VintageCamera", new Vector3(0.5f, 0.16f, 0f),
            "Cooperative Hall of Fame",
            "Film / Oral History",
            "A short film honoring the organizers, tenants, and neighbors who built Black-led cooperative housing. Play the film to hear this community history.",
            "Linda Leaks Archiving Project",
            plum, paper, rose, brass);

        return root;
    }

    private static GameObject BuildPhotoAlbumExhibit(Material plum, Material paper, Material rose, Material brass)
    {
        GameObject root = new GameObject("LindaLeaks_Exhibit_PhotoAlbum");

        GameObject artifact = new GameObject("Artifact_PhotoAlbum");
        artifact.transform.SetParent(root.transform, false);
        AddModel(artifact.transform, AlbumModelPath, "LL_PhotoAlbum_Model", AlbumModelPos, Quaternion.identity, Vector3.one * AlbumScale);

        BoxCollider collider = artifact.AddComponent<BoxCollider>();
        collider.size = AlbumColliderSize;
        collider.center = AlbumColliderCenter;
        AddKinematicBody(artifact);

        GameObject panelRoot = new GameObject("LL_PhotoAlbumPanel");
        panelRoot.transform.SetParent(artifact.transform, false);
        panelRoot.transform.localPosition = new Vector3(0f, 1.6f, -0.6f);
        panelRoot.SetActive(false);

        Canvas canvas = CreateWorldCanvas("Canvas", panelRoot.transform, new Vector2(1100, 680), 0.004f);
        Image backdrop = CreateUIImage("Backdrop", canvas.transform, new Vector2(1100, 680), plum.color);
        Image photo = CreateUIImage("Photo", backdrop.transform, new Vector2(460, 360), new Color(0.96f, 0.90f, 0.78f), new Vector2(-260, 80));
        TMP_Text title = CreateUIText("Title", backdrop.transform, "Housing Co-op Archive", new Vector2(270, 220), new Vector2(470, 90), 34, paper.color, FontStyles.Bold);
        TMP_Text caption = CreateUIText("Caption", backdrop.transform, "", new Vector2(270, 45), new Vector2(470, 230), 22, paper.color, FontStyles.Normal);
        TMP_Text description = CreateUIText("ProjectDescription", backdrop.transform, "Archival photographs of cooperative housing, community organizing, and everyday neighborhood life.", new Vector2(0, -230), new Vector2(920, 110), 22, paper.color, FontStyles.Normal);
        Button previous = CreateButton("PreviousButton", backdrop.transform, "Previous", new Vector2(-270, -300), new Vector2(180, 52), brass.color, Color.black);
        Button next = CreateButton("NextButton", backdrop.transform, "Next", new Vector2(-60, -300), new Vector2(160, 52), brass.color, Color.black);
        Button close = CreateButton("CloseButton", backdrop.transform, "Close", new Vector2(180, -300), new Vector2(160, 52), brass.color, Color.black);
        Button website = CreateButton("WebsiteButton", backdrop.transform, "Visit HonoringLindaLeaks.com", new Vector2(390, -300), new Vector2(300, 52), brass.color, Color.black);

        LindaLeaksPhotoAlbumController album = artifact.AddComponent<LindaLeaksPhotoAlbumController>();
        SetPrivate(album, "albumRoot", panelRoot);
        SetPrivate(album, "albumCanvas", canvas);
        SetPrivate(album, "photoImage", photo);
        SetPrivate(album, "titleText", title);
        SetPrivate(album, "captionText", caption);
        SetPrivate(album, "projectDescriptionText", description);
        SetPrivate(album, "projectDescription", "Archival photographs of cooperative housing, community organizing, and everyday neighborhood life.");
        SetPrivate(album, "photos", BuildPhotoEntries());
        SetPrivate(album, "externalWebsiteUrl", LindaLeaksWebsiteUrl);

        LindaLeaksPanelOpener opener = artifact.AddComponent<LindaLeaksPanelOpener>();
        SetPrivate(opener, "target", 1);
        SetPrivate(opener, "photoAlbum", album);
        SetPrivate(opener, "interactionDistance", 5f);

        AddButtonCall(previous, album, nameof(LindaLeaksPhotoAlbumController.Previous));
        AddButtonCall(next, album, nameof(LindaLeaksPhotoAlbumController.Next));
        AddButtonCall(close, album, nameof(LindaLeaksPhotoAlbumController.CloseAlbum));
        AddButtonCall(website, album, nameof(LindaLeaksPhotoAlbumController.OpenExternalWebsite));
        AddXRSimpleInteractable(artifact, opener, nameof(LindaLeaksPanelOpener.Open));

        BuildPlaque(root.transform, "Plaque_PhotoAlbum", new Vector3(0.5f, 0.16f, 0f),
            "Housing Co-op Archive",
            "Photo Archive · 9 Images",
            "A gallery of archival photographs documenting cooperative housing, organizers, and community life. Open the album to page through the collection.",
            "Linda Leaks Archiving Project",
            plum, paper, rose, brass);

        return root;
    }

    private static GameObject BuildHousingMapExhibit(Material plum, Material paper, Material rose, Material brass, Material moss)
    {
        GameObject root = new GameObject("LindaLeaks_Exhibit_HousingMap");

        // Artifact: the framed map is the interaction target (wall / display object ~1.0 m wide).
        GameObject artifact = new GameObject("Artifact_HousingMap");
        artifact.transform.SetParent(root.transform, false);
        artifact.transform.localPosition = new Vector3(0f, 1.4f, 0f);

        GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frame.name = "MapFrame";
        frame.transform.SetParent(artifact.transform, false);
        frame.transform.localScale = new Vector3(1.0f, 0.7f, 0.04f);
        frame.GetComponent<Renderer>().sharedMaterial = moss;

        GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
        surface.name = "MapSurface";
        surface.transform.SetParent(artifact.transform, false);
        surface.transform.localPosition = new Vector3(0f, 0f, -0.025f);
        surface.transform.localScale = new Vector3(0.86f, 0.56f, 0.02f);
        surface.GetComponent<Renderer>().sharedMaterial = paper;

        // Framed artwork title (part of the piece, reads correctly, not a floating prompt).
        Canvas canvas = CreateWorldCanvas("Canvas", artifact.transform, new Vector2(620, 380), 0.0013f);
        canvas.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        CreateUIText("MapTitle", canvas.transform, "Linda Leaks\nHousing Co-ops", new Vector2(0, 0), new Vector2(560, 300), 46, plum.color, FontStyles.Bold);

        BoxCollider collider = artifact.AddComponent<BoxCollider>();
        collider.size = new Vector3(1.05f, 0.75f, 0.14f);
        AddKinematicBody(artifact);

        InteractableLinkLauncher link = artifact.AddComponent<InteractableLinkLauncher>();
        SetPrivate(link, "targetUrl", MapHubUrl);
        SetPrivate(link, "interactDistance", 5f);
        AddXRSimpleInteractable(artifact, link, nameof(InteractableLinkLauncher.OpenLink));

        BuildPlaque(root.transform, "Plaque_HousingMap", new Vector3(0.85f, 1.4f, 0f),
            "Linda Leaks Housing Co-ops Map",
            "Interactive Archive",
            "An interactive map charting Black cooperative housing sites and their stories. Open the map to trace the places and relationships in the archive.",
            "maphub.net · Linda Leaks Archiving Project",
            plum, paper, rose, brass);

        return root;
    }

    // Plaque: title, project type, description (with embedded interaction hint), and creator credit.
    // The board is centered on the plaque's local origin; the caller positions the plaque.
    private static void BuildPlaque(Transform parent, string name, Vector3 localPosition,
        string title, string type, string description, string credit,
        Material board, Material paper, Material accent, Material brass)
    {
        GameObject plaque = new GameObject(name);
        plaque.transform.SetParent(parent, false);
        plaque.transform.localPosition = localPosition;

        GameObject backplate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backplate.name = "Plaque_Backplate";
        backplate.transform.SetParent(plaque.transform, false);
        backplate.transform.localScale = new Vector3(0.5f, 0.32f, 0.02f);
        backplate.GetComponent<Renderer>().sharedMaterial = board;

        GameObject accentBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        accentBar.name = "Plaque_AccentBar";
        accentBar.transform.SetParent(plaque.transform, false);
        accentBar.transform.localPosition = new Vector3(0f, -0.135f, -0.011f);
        accentBar.transform.localScale = new Vector3(0.47f, 0.02f, 0.008f);
        accentBar.GetComponent<Renderer>().sharedMaterial = brass;

        // World canvas at identity rotation, placed on the readable (-Z) face — text reads correctly
        // for a viewer looking toward +Z (the mirroring fix: no 180° flip on the canvas).
        Canvas canvas = CreateWorldCanvas("Canvas", plaque.transform, new Vector2(700, 440), 0.00065f);
        canvas.transform.localPosition = new Vector3(0f, 0f, -0.012f);
        CreateUIText("ProjectTitle", canvas.transform, title, new Vector2(0, 150), new Vector2(660, 95), 42, paper.color, FontStyles.Bold);
        CreateUIText("ProjectType", canvas.transform, type, new Vector2(0, 80), new Vector2(660, 45), 24, brass.color, FontStyles.Italic);
        CreateUIText("Description", canvas.transform, description, new Vector2(0, -30), new Vector2(640, 200), 26, paper.color, FontStyles.Normal);
        CreateUIText("Credit", canvas.transform, credit, new Vector2(0, -195), new Vector2(660, 50), 22, accent.color, FontStyles.Bold);
    }

    // ---- Review placement ----------------------------------------------------

    private static void PlaceReviewInstances()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        foreach (string legacy in new[] { "LindaLeaks_Phase2_ReviewOnly", "LindaLeaks_Review_FrontYard" })
        {
            GameObject existing = GameObject.Find(legacy);
            if (existing != null)
                Object.DestroyImmediate(existing);
        }

        GameObject parent = new GameObject("LindaLeaks_Review_FrontYard");

        // Front yard, south of the house (house front faces -Z; spawn ~(168,123)); exhibits face the
        // approaching player. Tabletop exhibits get a review plinth; the wall map hangs at eye level.
        (string prefab, float x, float z, bool tabletop)[] layout =
        {
            (CameraPrefab, 163f, 132f, true),
            (AlbumPrefab, 166f, 132f, true),
            (MapPrefab, 169f, 132f, false),
        };

        foreach (var item in layout)
        {
            float groundY = SampleGroundY(item.x, item.z, 6f);
            float baseY = groundY;

            if (item.tabletop)
            {
                const float plinthHeight = 0.9f;
                GameObject plinth = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plinth.name = "ReviewPlinth";
                plinth.transform.SetParent(parent.transform, true);
                plinth.transform.localScale = new Vector3(0.9f, plinthHeight, 0.7f);
                plinth.transform.position = new Vector3(item.x + 0.25f, groundY + plinthHeight * 0.5f, item.z);
                Material plinthMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot + "/LL_WarmPaper.mat");
                if (plinthMat != null)
                    plinth.GetComponent<Renderer>().sharedMaterial = plinthMat;
                baseY = groundY + plinthHeight;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(item.prefab);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = prefab.name + "_Preview";
            instance.transform.SetParent(parent.transform, true);
            instance.transform.position = new Vector3(item.x, baseY, item.z);
            instance.transform.rotation = Quaternion.identity;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static float SampleGroundY(float x, float z, float fallback)
    {
        Ray ray = new Ray(new Vector3(x, 500f, z), Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return fallback;
    }

    private static void DeleteLegacyPrefabs()
    {
        foreach (string legacy in new[]
        {
            PrefabRoot + "/LindaLeaks_Camera_HallOfFameVideo.prefab",
            PrefabRoot + "/LindaLeaks_PhotoAlbum_Gallery.prefab",
            PrefabRoot + "/LindaLeaks_MapHub_Frame.prefab",
            PrefabRoot + "/LindaLeaks_ProjectPlacard.prefab",
        })
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(legacy) != null)
                AssetDatabase.DeleteAsset(legacy);
        }
    }

    // ---- Shared builders -----------------------------------------------------

    private static void EnsureFolders()
    {
        foreach (string folder in new[] { PrefabRoot, MaterialRoot, TextureRoot, UIRoot, Root + "/Models", Root + "/Images" })
        {
            if (!AssetDatabase.IsValidFolder(folder))
                Directory.CreateDirectory(folder);
        }
    }

    private static void ConfigureImagesAsSprites()
    {
        foreach (string path in Directory.GetFiles(Root + "/Images"))
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
                continue;

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }

    private static Material CreateMaterial(string name, Color color, float metallic = 0f, float smoothness = 0.35f)
    {
        string path = $"{MaterialRoot}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static RenderTexture CreateRenderTexture()
    {
        string path = $"{UIRoot}/LL_HallOfFameVideo_RenderTexture.renderTexture";
        RenderTexture texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
        if (texture == null)
        {
            texture = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32)
            {
                name = "LL_HallOfFameVideo_RenderTexture"
            };
            AssetDatabase.CreateAsset(texture, path);
        }

        return texture;
    }

    private static void AddKinematicBody(GameObject go)
    {
        Rigidbody rigidbody = go.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
    }

    private static GameObject AddModel(Transform parent, string assetPath, string name, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null)
            return null;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        instance.name = name;
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = position;
        instance.transform.localRotation = rotation;
        instance.transform.localScale = scale;
        return instance;
    }

    private static Canvas CreateWorldCanvas(string name, Transform parent, Vector2 size, float scale)
    {
        GameObject canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.layer = 5;
        canvasObject.transform.SetParent(parent, false);
        canvasObject.transform.localScale = Vector3.one * scale;
        RectTransform rect = canvasObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
        canvasObject.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 1;
        return canvas;
    }

    private static Image CreateUIImage(string name, Transform parent, Vector2 size, Color color)
    {
        return CreateUIImage(name, parent, size, color, Vector2.zero);
    }

    private static Image CreateUIImage(string name, Transform parent, Vector2 size, Color color, Vector2 position)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.layer = 5;
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static RawImage CreateRawImage(string name, Transform parent, Vector2 size, Vector2 position, Texture texture)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        imageObject.layer = 5;
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        RawImage image = imageObject.GetComponent<RawImage>();
        image.texture = texture;
        image.color = Color.white;
        return image;
    }

    private static TMP_Text CreateUIText(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize, Color color, FontStyles style)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = 5;
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        TMP_Text label = textObject.GetComponent<TMP_Text>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = true;
        return label;
    }

    private static Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2 size, Color color, Color textColor)
    {
        Image image = CreateUIImage(name, parent, size, color, position);
        Button button = image.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        button.colors = colors;
        CreateUIText("Label", image.transform, label, Vector2.zero, size, 22, textColor, FontStyles.Bold);
        return button;
    }

    private static List<PhotoEntry> BuildPhotoEntries()
    {
        // Canonical 9-image archival co-op housing set.
        string[] paths =
        {
            Root + "/Images/LL_CoopHousing_01.jpg",
            Root + "/Images/LL_CoopHousing_02.jpg",
            Root + "/Images/LL_CoopHousing_03.jpg",
            Root + "/Images/LL_CoopHousing_04.jpg",
            Root + "/Images/LL_CoopHousing_05.jpg",
            Root + "/Images/LL_CoopHousing_06.png",
            Root + "/Images/LL_CoopHousing_07.jpeg",
            Root + "/Images/LL_CoopHousing_08.jpg",
            Root + "/Images/LL_CoopHousing_09.jpg"
        };

        (string title, string caption)[] metadata =
        {
            (
                "T Street Collective — Cheryl Boykins & Linda Leaks (1984)",
                "Cheryl Boykins (left) and Linda Leaks (right) sitting on the steps at 1333 T Street NW, Washington, DC, July 1984."
            ),
            (
                "T Street Collective — Black Women's Self Help Collective Meeting (1983)",
                "Audrey Sartin (back left), Margaret Carey (back middle), Ajowa Ifateyo (back right), Rosa Brunson (front left), Shepsara Raari (Deborah Berry) (front middle), and Faye Herbert (front right) at a May 1983 Black Women's Self Help Collective meeting at the T Street Collective House. A map of Africa and a photograph of Malcolm X are visible in the background."
            ),
            (
                "T Street Collective — Community Dancing (1983)",
                "Community celebration following a Black Women's Self Help Collective gathering, August 1983. Pictured are Faye Williams, TiaJuana Malone, Lianne Rozzell, Ajowa Ifateyo, possibly Cheryl Boykins, and S. Marquita Sykes."
            ),
            (
                "Southern Homes & Gardens — Residents Organize for Ownership",
                "The Southern Homes and Gardens Task Force during its campaign for resident ownership. Residents worked with public officials, organized demonstrations, and advocated for cooperative homeownership with pro bono support from Covington & Burling attorneys."
            ),
            (
                "Southern Homes & Gardens — Community Celebration",
                "Residents celebrate the successful establishment of Southern Homes and Gardens Cooperative, including Bella Tinus, Yvonne Timmer, Joan Thinar, Phyllis Thompson, James Morse, Thelma Ariett, Sara Atkinson, Mr. Bittles, Angela London, and Donny Simpson."
            ),
            (
                "Ella Jo Baker Cooperative — Groundbreaking Ceremony (2002)",
                "Groundbreaking ceremony for the Ella Jo Baker Intentional Community Cooperative, featuring Linda Leaks, cooperative members, community partners, Manna Inc., DC officials, and housing advocates."
            ),
            (
                "Ella Jo Baker Cooperative — University Place Home (2003)",
                "2548 University Place NW following renovation as part of the Ella Jo Baker Intentional Community Cooperative."
            ),
            (
                "Ella Jo Baker Cooperative — Before Rehabilitation",
                "2521 University Place NW before rehabilitation, with Ajowa Ifateyo standing on the porch. A Department of Housing and Community Development sign is visible in the basement window."
            ),
            (
                "Ella Jo Baker Cooperative — Board Meeting Gathering (2004)",
                "Linda Leaks, Ajowa Ifateyo, S. Marquita Sykes, Parisa Norouzi, Beverly Cannon, and Robin Williams Ashton gathered around the cooperative's \"red book\" of governance documents, circa 2004."
            )
        };

        var entries = new List<PhotoEntry>();
        for (int i = 0; i < paths.Length; i++)
        {
            entries.Add(new PhotoEntry
            {
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]),
                title = metadata[i].title,
                caption = metadata[i].caption
            });
        }

        return entries;
    }

    private static void AddButtonCall(Button button, Object target, string methodName)
    {
        UnityAction action = System.Delegate.CreateDelegate(typeof(UnityAction), target, methodName) as UnityAction;
        UnityEventTools.AddPersistentListener(button.onClick, action);
        EditorUtility.SetDirty(button);
    }

    private static void AddXRSimpleInteractable(GameObject root, Object target, string methodName)
    {
        System.Type type = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable, Unity.XR.Interaction.Toolkit");
        if (type == null)
            return;

        Component interactable = root.AddComponent(type);
        FieldInfo selectEnteredField = type.GetField("m_SelectEntered", BindingFlags.Instance | BindingFlags.NonPublic);
        object selectEntered = selectEnteredField?.GetValue(interactable);
        if (selectEntered == null)
            return;

        MethodInfo targetMethod = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        if (targetMethod == null)
            return;

        SerializedObject serializedObject = new SerializedObject(interactable);
        SerializedProperty calls = serializedObject.FindProperty("m_SelectEntered.m_PersistentCalls.m_Calls");
        if (calls == null)
            return;

        int index = calls.arraySize;
        calls.InsertArrayElementAtIndex(index);
        SerializedProperty call = calls.GetArrayElementAtIndex(index);
        call.FindPropertyRelative("m_Target").objectReferenceValue = target;
        call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = $"{target.GetType().FullName}, Assembly-CSharp";
        call.FindPropertyRelative("m_MethodName").stringValue = methodName;
        call.FindPropertyRelative("m_Mode").intValue = 1;
        call.FindPropertyRelative("m_CallState").intValue = 2;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(interactable);
    }

    private static void SetPrivate(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null && field.FieldType.IsEnum && value is int intValue)
            value = System.Enum.ToObject(field.FieldType, intValue);

        field?.SetValue(target, value);
        if (target is Object unityObject)
            EditorUtility.SetDirty(unityObject);
    }

    private static void SavePrefab(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
    }
}
