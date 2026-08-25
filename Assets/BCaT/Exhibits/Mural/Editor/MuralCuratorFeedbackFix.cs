using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MuralCuratorFeedbackFix
{
    private const string ScenePath = "Assets/BH_XR_MainScene.unity";
    private const string ImagesPath = "Assets/BCaT/Exhibits/Mural/Images";
    private const string WallTexturePath = ImagesPath + "/wall_mural_cropped.png";
    private const string MuralMaterialPath = "Assets/BCaT/Exhibits/Mural/Materials/MuralFinished_Unlit.mat";
    private const string VideoFileName = "Mural/11_mural_process_video.mp4";

    private readonly struct ImageItem
    {
        public readonly string Title;
        public readonly string Path;

        public ImageItem(string title, string path)
        {
            Title = title;
            Path = path;
        }
    }

    private static readonly ImageItem[] OrderedItems =
    {
        new("Initial wall sketch", ImagesPath + "/01_initial_wall_sketch.png"),
        new("Early draft", ImagesPath + "/02_early_draft.jpg"),
        new("Early coloured draft", ImagesPath + "/03_early_coloured_draft.png"),
        new("In progress - early wall blocking", ImagesPath + "/04_in_progress_early_wall_blocking.png"),
        new("In progress - mid process wall", ImagesPath + "/04_mid_process_wall.jpg"),
        new("Baby my love", ""),
        new("Participation prompts - What represents home to you?", ImagesPath + "/07_participation_prompts_home.png"),
        new("Participant prompts and canvases", ImagesPath + "/08_participant_prompts_and_canvases.png"),
        new("Maïa Walcott’s mural", ImagesPath + "/09_maia_walcotts_mural.png"),
        new("Side table and lamp", ImagesPath + "/10_side_table_and_lamp.png"),
        new("The good china", ImagesPath + "/11_the_good_china.png"),
        new("Glass fish and decorative flowers", ImagesPath + "/12_glass_fish_decorative_flowers.png"),
        new("Bar cart and window", ImagesPath + "/13_bar_cart_and_window.png"),
        new("Finished mural", ImagesPath + "/14_finished_mural.jpg"),
        new("The BCaT Lab", ImagesPath + "/15_the_bcat_lab.png"),
        new("From start to finish", ImagesPath + "/16_from_start_to_finish.png"),
    };

    [MenuItem("BCaT/Mural/Apply Curator Feedback Fix")]
    public static void Apply()
    {
        ConfigureTextureImports();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        EditorSceneManager.OpenScene(ScenePath);
        MuralExhibitController controller = Object.FindFirstObjectByType<MuralExhibitController>();
        if (controller == null)
            throw new MissingReferenceException("MuralExhibitController not found in " + ScenePath);

        ConfigureController(controller);
        ConfigureWallPanel(controller);
        ConfigureGalleryLayout(controller);

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        EditorSceneManager.SaveScene(controller.gameObject.scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MuralCuratorFeedbackFix] Applied mural curator feedback scene/data updates.");
    }

    private static void ConfigureTextureImports()
    {
        foreach (ImageItem item in OrderedItems)
        {
            if (!string.IsNullOrEmpty(item.Path))
                ConfigureTextureImport(item.Path);
        }

        ConfigureTextureImport(WallTexturePath);
    }

    private static void ConfigureTextureImport(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Missing mural image asset", path);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            throw new InvalidDataException("Mural asset is not a texture: " + path);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.alphaIsTransparency = Path.GetExtension(path).ToLowerInvariant() == ".png";
        importer.maxTextureSize = 8192;

        ApplyPlatform(importer, "DefaultTexturePlatform");
        ApplyPlatform(importer, "Standalone");
        ApplyPlatform(importer, "Android");
        ApplyPlatform(importer, "WebGL");

        importer.SaveAndReimport();
    }

    private static void ApplyPlatform(TextureImporter importer, string buildTarget)
    {
        TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(buildTarget);
        settings.name = buildTarget;
        settings.overridden = buildTarget != "DefaultTexturePlatform";
        settings.maxTextureSize = 8192;
        settings.resizeAlgorithm = TextureResizeAlgorithm.Mitchell;
        settings.textureCompression = TextureImporterCompression.Compressed;
        importer.SetPlatformTextureSettings(settings);
    }

    private static void ConfigureController(MuralExhibitController controller)
    {
        SerializedObject so = new(controller);

        SerializedProperty prompt = so.FindProperty("prompt");
        prompt.FindPropertyRelative("desktopPrompt").stringValue = "Press E to view mural";
        prompt.FindPropertyRelative("xrPrompt").stringValue = "View — Black Homeplaces Community Mural";
        prompt.FindPropertyRelative("verb").enumValueIndex = 2;
        prompt.FindPropertyRelative("objectName").stringValue = "Black Homeplaces Community Mural";

        SerializedProperty items = so.FindProperty("items");
        items.arraySize = OrderedItems.Length;
        for (int i = 0; i < OrderedItems.Length; i++)
        {
            SerializedProperty item = items.GetArrayElementAtIndex(i);
            bool isVideo = string.IsNullOrEmpty(OrderedItems[i].Path);
            item.FindPropertyRelative("type").enumValueIndex = isVideo ? 1 : 0;
            item.FindPropertyRelative("displayName").stringValue = OrderedItems[i].Title;
            item.FindPropertyRelative("caption").stringValue = string.Empty;
            item.FindPropertyRelative("image").objectReferenceValue = isVideo
                ? null
                : AssetDatabase.LoadAssetAtPath<Sprite>(OrderedItems[i].Path);
            item.FindPropertyRelative("videoFileName").stringValue = isVideo ? VideoFileName : string.Empty;
            item.FindPropertyRelative("videoUrlOverride").stringValue = string.Empty;
            item.FindPropertyRelative("editorPreviewClip").objectReferenceValue = null;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureWallPanel(MuralExhibitController controller)
    {
        Texture2D wallTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(WallTexturePath);
        if (wallTexture == null)
            throw new FileNotFoundException("Missing cropped mural wall texture", WallTexturePath);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MuralMaterialPath);
        if (material == null)
            throw new FileNotFoundException("Missing mural wall material", MuralMaterialPath);

        material.SetTexture("_BaseMap", wallTexture);
        material.SetTexture("_MainTex", wallTexture);
        EditorUtility.SetDirty(material);

        Transform panel = controller.transform.Find("MuralPanel_FinishedImage_Interactable");
        if (panel == null)
            throw new MissingReferenceException("Mural wall panel not found below MuralExhibit.");

        float width = panel.localScale.x;
        float aspect = (float)wallTexture.width / wallTexture.height;
        panel.localScale = new Vector3(width, width / aspect, panel.localScale.z);
        EditorUtility.SetDirty(panel);
    }

    private static void ConfigureGalleryLayout(MuralExhibitController controller)
    {
        foreach (TMP_Text text in controller.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name == "Title")
            {
                text.alignment = TextAlignmentOptions.Center;
                text.enableAutoSizing = false;
                text.fontSize = 28f;
                text.fontSizeMin = 28f;
                text.fontSizeMax = 28f;
                text.lineSpacing = -8f;
                text.textWrappingMode = TextWrappingModes.Normal;
                text.overflowMode = TextOverflowModes.Overflow;
                RectTransform rect = text.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, 378f);
                rect.sizeDelta = new Vector2(720f, 58f);
                EditorUtility.SetDirty(text);
            }
        }

        foreach (RectTransform rect in controller.GetComponentsInChildren<RectTransform>(true))
        {
            switch (rect.name)
            {
                case "MediaFrame":
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = new Vector2(0f, 25f);
                    rect.sizeDelta = new Vector2(1160f, 640f);
                    EditorUtility.SetDirty(rect);
                    break;
                case "ImageDisplay":
                case "VideoDisplay":
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(-8f, -8f);
                    EditorUtility.SetDirty(rect);
                    break;
                case "PreviousButton":
                    ConfigureButtonRect(rect, new Vector2(-500f, -350f), new Vector2(72f, 72f));
                    break;
                case "NextButton":
                    ConfigureButtonRect(rect, new Vector2(500f, -350f), new Vector2(72f, 72f));
                    break;
                case "CloseButton":
                    ConfigureButtonRect(rect, new Vector2(586f, 374f), new Vector2(58f, 58f));
                    break;
            }
        }

        foreach (AspectRatioFitter fitter in controller.GetComponentsInChildren<AspectRatioFitter>(true))
        {
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            EditorUtility.SetDirty(fitter);
        }
    }

    private static void ConfigureButtonRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        EditorUtility.SetDirty(rect);
    }
}
