using System;
using System.IO;
using System.Text;
using BCaT.Production.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the four Kitchen Scholars (Azsaneé Truss &amp; Staci Jones) framed
/// collage prefabs and stages them as a temporary review row in the front yard.
/// Prefabs and the staging group are outputs: edit this builder and re-run the
/// menu items rather than hand-editing them.
///
/// The prefabs carry only exhibit content (framed artwork, narration, the
/// interaction controller) with the artwork face centred on the prefab origin,
/// so each piece can later be dropped flat onto the kitchen wall exactly as the
/// collaborators intend. The review wall panels belong to the staging pass.
///
/// Front-yard staging is intentionally temporary (TEMP_KitchenScholars_FrontYard)
/// for in-game review before final kitchen placement.
/// </summary>
public static class KitchenScholarsExhibitBuilder
{
    private const string ScenePath = "Assets/BH_XR_MainScene.unity";
    private const string ExhibitRoot = "Assets/BCaT/Exhibits/KitchenScholars";
    private const string PrefabRoot = ExhibitRoot + "/Prefabs";
    private const string MaterialRoot = ExhibitRoot + "/Materials";
    private const string MediaRoot = "Assets/BCaT_assets/KitchenScholars";

    private const string StagingRootName = "TEMP_KitchenScholars_FrontYard";

    /// <summary>Largest artwork dimension in metres after normalization.</summary>
    private const float ArtTargetSize = 1.25f;

    private const float FrameBorder = 0.10f;
    private const float FrameDepth = 0.045f;

    /// <summary>Minimum collider extent so every piece is easy to focus/aim at.</summary>
    private const float MinColliderSize = 0.34f;

    // Front yard review row. The player arrives at (167.91, 5.86, 130.61)
    // facing +Z, so the row sits a few metres ahead, centred on the arrival
    // point, inside the porch fence (x 157.1 - 178.1), north of Boundary_Front
    // (z 130.01) and south of the off-limits flower beds (z 145+). This is the
    // same review band the Adinkra symbols used before their final placement
    // (their old row is no longer staged here). The artwork face is authored
    // toward -Z, back toward the arriving visitor, so the slots need no yaw.
    private const float RowCenterX = 167.9f;
    private const float RowZ = 134f;
    private const float RowSpacing = 4f;

    /// <summary>Artwork centre height above the highest slot ground (eye level).</summary>
    private const float ArtCenterHeight = 1.5f;

    private const float PanelWidth = 2.0f;
    private const float PanelDepth = 0.12f;
    private const float PanelHeadroom = 0.25f;

    private sealed class PieceDefinition
    {
        public string SceneObjectName;
        public string PrefabName;
        public string Title;
        public string TexturePath;
        public string AudioPath;
        public string NarrationMediaId;
    }

    // Order mirrors the transcripts in the collaborators' "(Loose)
    // Instructions" document; they state any order is acceptable. Pairing is
    // by the matching Drive artwork/audio filenames.
    private static readonly PieceDefinition[] Pieces =
    {
        new PieceDefinition
        {
            SceneObjectName = "MyGrandmothersRecipes",
            PrefabName = "KitchenScholars_MyGrandmothersRecipes",
            Title = "My Grandmother's Recipes",
            TexturePath = MediaRoot + "/MyGrandmothersRecipes.png",
            AudioPath = MediaRoot + "/MyGrandmothersRecipes.mp3",
            NarrationMediaId = "kitchenscholars_my_grandmothers_recipes",
        },
        new PieceDefinition
        {
            SceneObjectName = "MyAuntPatsHouse",
            PrefabName = "KitchenScholars_MyAuntPatsHouse",
            Title = "My Aunt Pat's House",
            TexturePath = MediaRoot + "/MyAuntPatsHouse.png",
            AudioPath = MediaRoot + "/MyAuntPatsHouse.mp3",
            NarrationMediaId = "kitchenscholars_my_aunt_pats_house",
        },
        new PieceDefinition
        {
            SceneObjectName = "RenovatedKitchen",
            PrefabName = "KitchenScholars_RenovatedKitchen",
            Title = "Renovated Kitchen",
            TexturePath = MediaRoot + "/RenovatedKitchen.png",
            AudioPath = MediaRoot + "/RenovatedKitchen.mp3",
            NarrationMediaId = "kitchenscholars_renovated_kitchen",
        },
        new PieceDefinition
        {
            SceneObjectName = "AncestorCriticalFabulation",
            PrefabName = "KitchenScholars_AncestorCriticalFabulation",
            Title = "Ancestor Critical Fabulation",
            TexturePath = MediaRoot + "/AncestorCriticalFabulation.png",
            AudioPath = MediaRoot + "/AncestorCriticalFabulation.mp3",
            NarrationMediaId = "kitchenscholars_ancestor_critical_fabulation",
        },
    };

    [MenuItem("BCaT/Kitchen Scholars/Build Artwork Prefabs")]
    public static void BuildPrefabs()
    {
        EnsureFolders();
        AssetDatabase.ImportAsset(MediaRoot, ImportAssetOptions.ImportRecursive);
        ConfigureMediaImportSettings();

        var log = new StringBuilder("[KitchenScholars] Prefab build\n");
        foreach (PieceDefinition piece in Pieces)
        {
            GameObject built = BuildArtworkExhibit(piece, log);
            string path = $"{PrefabRoot}/{piece.PrefabName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(built, path);
            UnityEngine.Object.DestroyImmediate(built);
            log.AppendLine($"  saved {path}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(log.ToString());
    }

    [MenuItem("BCaT/Kitchen Scholars/Stage In Front Yard (TEMP Review)")]
    public static void StageFrontYard()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject existing = GameObject.Find(StagingRootName);
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing);

        Material panelMaterial = CreateMaterial("KitchenScholars_WallPanel", new Color(0.34f, 0.32f, 0.30f), 0f, 0.15f);
        GameObject stagingRoot = new GameObject(StagingRootName);
        stagingRoot.transform.position = Vector3.zero;

        var log = new StringBuilder($"[KitchenScholars] Front yard staging ({StagingRootName})\n");
        float startX = RowCenterX - RowSpacing * (Pieces.Length - 1) * 0.5f;

        // The front-yard grass slopes ~5.37 m at the fence down to ~4.86 m by
        // the walkway. Sample every slot, then level every artwork centre (and
        // panel top) to the highest slot so the row reads as one gallery wall;
        // each panel stretches down to its own ground so nothing floats.
        var grounds = new float[Pieces.Length];
        float maxGround = float.NegativeInfinity;
        for (int i = 0; i < Pieces.Length; i++)
        {
            grounds[i] = SampleGroundY(startX + RowSpacing * i, RowZ, 4.86f);
            maxGround = Mathf.Max(maxGround, grounds[i]);
        }

        float artCenterY = maxGround + ArtCenterHeight;
        float maxArtHalfHeight = 0f;
        foreach (PieceDefinition piece in Pieces)
            maxArtHalfHeight = Mathf.Max(maxArtHalfHeight, GetArtSize(piece).y * 0.5f);
        float panelTopY = artCenterY + maxArtHalfHeight + PanelHeadroom;

        for (int i = 0; i < Pieces.Length; i++)
        {
            PieceDefinition piece = Pieces[i];
            float x = startX + RowSpacing * i;
            float groundY = grounds[i];
            float panelHeight = panelTopY - groundY;

            GameObject slot = new GameObject(piece.SceneObjectName);
            slot.transform.SetParent(stagingRoot.transform, false);
            slot.transform.SetPositionAndRotation(new Vector3(x, groundY, RowZ), Quaternion.identity);

            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "ReviewWallPanel";
            panel.transform.SetParent(slot.transform, false);
            panel.transform.localScale = new Vector3(PanelWidth, panelHeight, PanelDepth);
            // Panel front face sits just behind the artwork frame's back.
            panel.transform.localPosition = new Vector3(0f, panelHeight * 0.5f, PanelDepth * 0.5f + FrameDepth + 0.005f);
            Renderer panelRenderer = panel.GetComponent<Renderer>();
            panelRenderer.sharedMaterial = panelMaterial;
            panelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/{piece.PrefabName}.prefab");
            if (prefab == null)
                throw new FileNotFoundException(
                    $"Missing {piece.PrefabName}.prefab — run BCaT/Kitchen Scholars/Build Artwork Prefabs first.");

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = piece.PrefabName;
            instance.transform.SetParent(slot.transform, false);
            instance.transform.localPosition = new Vector3(0f, artCenterY - groundY, 0f);
            instance.transform.localRotation = Quaternion.identity;

            log.AppendLine($"  {piece.SceneObjectName,-28} x={x:F2} z={RowZ:F2} ground={groundY:F2} " +
                           $"artCenterY={artCenterY:F2} panelHeight={panelHeight:F2} facing=-Z");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(log.ToString());
    }

    [MenuItem("BCaT/Kitchen Scholars/Build And Stage Everything")]
    public static void BuildAndStage()
    {
        BuildPrefabs();
        StageFrontYard();
        CaptureReviewScreenshot();
    }

    /// <summary>
    /// Renders a review screenshot from the visitor's arrival side into
    /// Library/KitchenScholarsFrontYard.png. Two frames are rendered and the
    /// first discarded (batch mode's first frame precedes shader warm-up).
    /// </summary>
    [MenuItem("BCaT/Kitchen Scholars/Capture Front Yard Screenshot")]
    public static void CaptureReviewScreenshot()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var cameraGo = new GameObject("KitchenScholars_ReviewCamera");
        try
        {
            var camera = cameraGo.AddComponent<Camera>();
            camera.transform.position = new Vector3(RowCenterX, 6.6f, RowZ - 6.5f);
            camera.transform.LookAt(new Vector3(RowCenterX, 6.2f, RowZ));
            camera.fieldOfView = 65f;

            var rt = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = rt;
            camera.Render(); // warm-up frame; discard
            camera.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;
            rt.Release();

            string path = Path.Combine("Library", "KitchenScholarsFrontYard.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(rt);
            Debug.Log($"[KitchenScholars] Review screenshot written to {path}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cameraGo);
        }
    }

    // ---- Media import settings -------------------------------------------

    /// <summary>
    /// Quest-conscious import settings for the optimized media. Textures cap at
    /// 2048 with mipmaps (the sources are 2000 px collages viewed up close);
    /// narration clips stream as Vorbis so the 1-3 minute recordings never sit
    /// decompressed in memory.
    /// </summary>
    private static void ConfigureMediaImportSettings()
    {
        foreach (PieceDefinition piece in Pieces)
        {
            var textureImporter = AssetImporter.GetAtPath(piece.TexturePath) as TextureImporter;
            if (textureImporter == null)
                throw new FileNotFoundException("Missing Kitchen Scholars texture: " + piece.TexturePath);

            textureImporter.textureType = TextureImporterType.Default;
            textureImporter.sRGBTexture = true;
            textureImporter.alphaSource = TextureImporterAlphaSource.None;
            textureImporter.mipmapEnabled = true;
            textureImporter.wrapMode = TextureWrapMode.Clamp;
            textureImporter.anisoLevel = 4;
            textureImporter.maxTextureSize = 2048;
            textureImporter.textureCompression = TextureImporterCompression.Compressed;
            // The collages are 1600x2000 / 1545x2000 / 2000x1545; the default
            // ToNearest POT rescale stretched them square (2048x2048) and
            // distorted the artwork. ASTC on Quest and desktop GPUs handle
            // NPOT textures with mipmaps, so keep the native pixels.
            textureImporter.npotScale = TextureImporterNPOTScale.None;
            textureImporter.SaveAndReimport();

            var audioImporter = AssetImporter.GetAtPath(piece.AudioPath) as AudioImporter;
            if (audioImporter == null)
                throw new FileNotFoundException("Missing Kitchen Scholars narration: " + piece.AudioPath);

            AudioImporterSampleSettings settings = audioImporter.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            audioImporter.defaultSampleSettings = settings;
            audioImporter.forceToMono = false; // sources are already mono
            audioImporter.loadInBackground = true;
            audioImporter.SaveAndReimport();
        }
    }

    // ---- Exhibit construction -------------------------------------------

    private static GameObject BuildArtworkExhibit(PieceDefinition piece, StringBuilder log)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(piece.TexturePath);
        if (texture == null)
            throw new FileNotFoundException("Missing Kitchen Scholars texture: " + piece.TexturePath);

        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(piece.AudioPath);
        if (clip == null)
            throw new FileNotFoundException("Missing Kitchen Scholars narration: " + piece.AudioPath);

        Vector2 artSize = GetArtSize(piece);
        log.AppendLine($"  {piece.Title}: texture {texture.width}x{texture.height} px -> " +
                       $"{artSize.x:F3} x {artSize.y:F3} m, narration '{clip.name}' {clip.length:F1}s");

        GameObject root = new GameObject(piece.PrefabName);

        // The framed artwork is the interaction target. Its face is centred on
        // the prefab origin and looks toward -Z, so final kitchen placement is
        // "origin on the wall, -Z into the room".
        GameObject target = new GameObject("Artwork_" + piece.SceneObjectName);
        target.transform.SetParent(root.transform, false);

        Material artMaterial = CreateArtMaterial(piece, texture);
        GameObject art = GameObject.CreatePrimitive(PrimitiveType.Quad);
        art.name = "Collage";
        UnityEngine.Object.DestroyImmediate(art.GetComponent<Collider>());
        art.transform.SetParent(target.transform, false);
        art.transform.localScale = new Vector3(artSize.x, artSize.y, 1f);
        OrientQuadTowardNegativeZ(art);
        Renderer artRenderer = art.GetComponent<Renderer>();
        artRenderer.sharedMaterial = artMaterial;
        artRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Material frameMaterial = CreateMaterial("KitchenScholars_Frame", new Color(0.16f, 0.12f, 0.10f), 0f, 0.35f);
        GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frame.name = "Frame";
        UnityEngine.Object.DestroyImmediate(frame.GetComponent<Collider>());
        frame.transform.SetParent(target.transform, false);
        frame.transform.localScale = new Vector3(artSize.x + FrameBorder, artSize.y + FrameBorder, FrameDepth);
        // Frame body sits behind the artwork face (+Z), leaving the collage a
        // few millimetres proud of the frame front.
        frame.transform.localPosition = new Vector3(0f, 0f, FrameDepth * 0.5f + 0.003f);
        frame.GetComponent<Renderer>().sharedMaterial = frameMaterial;

        var collider = target.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0f, FrameDepth * 0.5f);
        collider.size = new Vector3(
            Mathf.Max(artSize.x + FrameBorder + 0.04f, MinColliderSize),
            Mathf.Max(artSize.y + FrameBorder + 0.04f, MinColliderSize),
            Mathf.Max(FrameDepth + 0.06f, 0.12f));

        var body = target.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        AudioSource narrationSource = root.AddComponent<AudioSource>();
        narrationSource.playOnAwake = false;
        narrationSource.loop = false;
        narrationSource.volume = 0.9f;
        // 2D for intelligibility, matching the other narration exhibits; the
        // proximity stop rule already scopes the audio to the artwork.
        narrationSource.spatialBlend = 0f;

        var controller = root.AddComponent<KitchenScholarsArtwork>();
        ConfigureController(controller, piece, art.transform, target.transform, narrationSource, clip);

        // Quest reachability: XrSelectSurface mirrors the box collider as an
        // XRI aim surface at runtime on Quest and disables itself on desktop.
        var surface = target.AddComponent<XrSelectSurface>();
        var surfaceSo = new SerializedObject(surface);
        surfaceSo.FindProperty("padding").floatValue = 0.02f;
        surfaceSo.FindProperty("forwardsTo").stringValue = piece.Title;
        surfaceSo.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    private static void ConfigureController(KitchenScholarsArtwork controller, PieceDefinition piece,
        Transform artworkRoot, Transform target, AudioSource narrationSource, AudioClip clip)
    {
        var so = new SerializedObject(controller);
        so.FindProperty("pieceTitle").stringValue = piece.Title;
        so.FindProperty("artworkRoot").objectReferenceValue = artworkRoot;

        so.FindProperty("narrationClip").objectReferenceValue = clip;
        so.FindProperty("narrationSource").objectReferenceValue = narrationSource;
        so.FindProperty("narrationVolume").floatValue = 0.9f;
        so.FindProperty("narrationMediaId").stringValue = piece.NarrationMediaId;

        so.FindProperty("focusPoint").objectReferenceValue = target;
        so.FindProperty("colliderRoot").objectReferenceValue = target;
        so.FindProperty("interactionDistance").floatValue = 3.5f;
        so.FindProperty("narrationStopDistance").floatValue = 5f;
        so.FindProperty("maxViewAngle").floatValue = 25f;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Artwork size in metres, largest dimension normalized. The aspect comes
    /// from the source PNG's IHDR header rather than the imported texture, so
    /// quad proportions never depend on import-time rescaling.
    /// </summary>
    private static Vector2 GetArtSize(PieceDefinition piece)
    {
        if (!TryReadPngDimensions(piece.TexturePath, out int width, out int height))
            return new Vector2(ArtTargetSize, ArtTargetSize);

        float aspect = (float)width / height;
        return aspect >= 1f
            ? new Vector2(ArtTargetSize, ArtTargetSize / aspect)
            : new Vector2(ArtTargetSize * aspect, ArtTargetSize);
    }

    /// <summary>PNG IHDR: width/height are big-endian uints at byte offsets 16 and 20.</summary>
    private static bool TryReadPngDimensions(string assetPath, out int width, out int height)
    {
        width = 0;
        height = 0;
        try
        {
            byte[] header = new byte[24];
            using (FileStream stream = File.OpenRead(assetPath))
            {
                if (stream.Read(header, 0, header.Length) < header.Length)
                    return false;
            }

            if (header[12] != (byte)'I' || header[13] != (byte)'H' || header[14] != (byte)'D' || header[15] != (byte)'R')
                return false;

            width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
            return width > 0 && height > 0;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Rotates the quad so its rendered face looks toward the parent's -Z.
    /// Resolved from the mesh's own normals rather than assuming the primitive's
    /// facing convention.
    /// </summary>
    private static void OrientQuadTowardNegativeZ(GameObject quad)
    {
        var meshFilter = quad.GetComponent<MeshFilter>();
        Vector3 normal = Vector3.back;
        if (meshFilter != null && meshFilter.sharedMesh != null &&
            meshFilter.sharedMesh.normals != null && meshFilter.sharedMesh.normals.Length > 0)
            normal = meshFilter.sharedMesh.normals[0];

        if (Vector3.Dot(normal, Vector3.back) < 0f)
            quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
    }

    // ---- Assets helpers ---------------------------------------------------

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

    private static Material CreateArtMaterial(PieceDefinition piece, Texture2D texture)
    {
        string path = $"{MaterialRoot}/KitchenScholars_{piece.SceneObjectName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = "KitchenScholars_" + piece.SceneObjectName };
            AssetDatabase.CreateAsset(material, path);
        }

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.05f);

        EditorUtility.SetDirty(material);
        return material;
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
        RaycastHit[] hits = Physics.RaycastAll(
            new Ray(new Vector3(x, 12f, z), Vector3.down), 40f, ~0, QueryTriggerInteraction.Ignore);

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
