using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Final Adinkra symbol surface treatment.
///
/// All five symbol meshes get one shared glossy black URP Lit material (the GLBs
/// carry no UVs, so no texture can map onto them). Sankofa additionally gets a
/// single flat artwork overlay: a Quad child of Sankofa_Essence_Model textured
/// with a transparent PNG derived from sankofa.jpg by removing only the exterior
/// white background — interior enclosed white regions and all coloured artwork
/// are preserved.
///
/// The other four symbols' reference JPG/PNGs are never used as textures.
///
/// Meshes, UVs, colliders, transforms of existing objects, interaction scripts,
/// labels, modal content and audio are not touched. The Quad's auto-added
/// MeshCollider is removed so collider setup is unchanged.
/// </summary>
public static class AdinkraSymbolMaterialAssigner
{
    private const string AssetRoot = "Assets/BCaT_assets/Adinkra";
    private const string PrefabRoot = "Assets/BCaT/Exhibits/Adinkra/Prefabs";
    private const string MaterialRoot = "Assets/BCaT/Exhibits/Adinkra/Materials";

    private const string GlossyBlackPath = MaterialRoot + "/Adinkra_GlossyBlack.mat";
    private const string OverlayMaterialPath = MaterialRoot + "/Sankofa_Artwork_Unlit.mat";

    private const string SankofaSourceImage = AssetRoot + "/Sankofa (Main Symbol)/sankofa.jpg";
    private const string SankofaTransparentPng = AssetRoot + "/Sankofa (Main Symbol)/sankofa_transparent.png";
    private const string SankofaPrefab = PrefabRoot + "/Adinkra_Sankofa.prefab";
    private const string SankofaModelObject = "Sankofa_Essence_Model";
    private const string OverlayObjectName = "Sankofa_Artwork_Front";

    private const string UrpLitShaderGuid = "933532a4fcc9baf4fa0491de14d08ed7";

    // Exterior-background classification: a pixel counts as removable background
    // only if it is both very light and nearly neutral, so saturated artwork
    // colours (yellow, red, green) are never mistaken for background.
    private const float BackgroundLuminance = 0.85f;
    private const float BackgroundNeutrality = 0.12f;

    /// <summary>Gap between the mesh front face and the overlay, in world metres.</summary>
    private const float OverlayOffsetMetres = 0.002f;

    private static readonly (string symbol, string modelObject, string prefab)[] AllSymbols =
    {
        ("Sankofa", "Sankofa_Essence_Model", PrefabRoot + "/Adinkra_Sankofa.prefab"),
        ("Gye Nyame", "gye_nyame_Model", PrefabRoot + "/Adinkra_GyeNyame.prefab"),
        ("Adinkrahene", "Adinkrahene_Model", PrefabRoot + "/Adinkra_Adinkrahene.prefab"),
        ("Funtunfunefu Denkyemfunefu", "Funtunfunefu_Denkyemfunefu_Model",
            PrefabRoot + "/Adinkra_Funtunfunefu.prefab"),
        ("Nsaa", "Nsaa_Model", PrefabRoot + "/Adinkra_Nsaa.prefab"),
    };

    [MenuItem("BCaT/Adinkra/Assign Symbol Materials")]
    public static void AssignMaterials()
    {
        var report = new StringBuilder("ADINKRA_TREATMENT_BEGIN\n");

        Material glossyBlack = EnsureGlossyBlack(report);
        if (glossyBlack == null)
        {
            Debug.LogError(report + "\nAborted: glossy black material unavailable.");
            return;
        }

        Texture2D overlayTexture = BuildSankofaTransparentPng(report);
        Material overlayMaterial = overlayTexture != null
            ? EnsureOverlayMaterial(overlayTexture, report)
            : null;

        foreach ((string symbol, string modelObject, string prefabPath) in AllSymbols)
        {
            report.AppendLine($"\n[{symbol}]");
            bool wantsOverlay = symbol == "Sankofa" && overlayMaterial != null;
            ApplyToPrefab(prefabPath, modelObject, glossyBlack,
                wantsOverlay ? overlayMaterial : null, report);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        report.AppendLine("\nADINKRA_TREATMENT_END");
        Debug.Log(report.ToString());
    }

    // ---- Shared glossy black (URP Lit) -----------------------------------

    private static Material EnsureGlossyBlack(StringBuilder report)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                        ?? AssetDatabase.LoadAssetAtPath<Shader>(
                            AssetDatabase.GUIDToAssetPath(UrpLitShaderGuid));
        if (shader == null)
        {
            report.AppendLine("URP Lit shader could not be resolved.");
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(GlossyBlackPath);
        bool created = material == null;
        if (created)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GlossyBlackPath));
            material = new Material(shader) { name = "Adinkra_GlossyBlack" };
            AssetDatabase.CreateAsset(material, GlossyBlackPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        ColorUtility.TryParseHtmlString("#050505", out Color black);
        material.SetColor("_BaseColor", black);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", black);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 0.8f);
        if (material.HasProperty("_WorkflowMode"))
            material.SetFloat("_WorkflowMode", 1f);   // Metallic workflow
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 0f);        // Opaque

        // Specular highlights + environment reflections ON (URP uses "off" keywords).
        if (material.HasProperty("_SpecularHighlights"))
            material.SetFloat("_SpecularHighlights", 1f);
        if (material.HasProperty("_EnvironmentReflections"))
            material.SetFloat("_EnvironmentReflections", 1f);
        material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
        material.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");

        // No base map: the meshes have no UVs, so colour comes from the material.
        material.SetTexture("_BaseMap", null);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);

        report.AppendLine($"shared glossy black : {GlossyBlackPath} ({(created ? "created" : "reused")})");
        report.AppendLine($"                      shader='{material.shader.name}' baseColor=#050505 " +
                          $"metallic={material.GetFloat("_Metallic")} " +
                          $"smoothness={material.GetFloat("_Smoothness")} specular=on envReflections=on");
        return material;
    }

    // ---- Sankofa transparent PNG -----------------------------------------

    /// <summary>
    /// Flood-fills the exterior background inward from the image border and makes
    /// only that region transparent, then tight-crops to the remaining artwork.
    /// White enclosed inside the symbol is never reached by the fill, so it stays.
    /// </summary>
    private static Texture2D BuildSankofaTransparentPng(StringBuilder report)
    {
        string sourceFull = Path.GetFullPath(SankofaSourceImage);
        if (!File.Exists(sourceFull))
        {
            report.AppendLine($"Sankofa source image missing: {SankofaSourceImage}");
            return null;
        }

        // Decode from disk so the result is readable regardless of import settings.
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!source.LoadImage(File.ReadAllBytes(sourceFull)))
        {
            report.AppendLine("Sankofa source image could not be decoded.");
            return null;
        }

        int w = source.width, h = source.height;
        Color32[] pixels = source.GetPixels32();
        bool[] background = new bool[w * h];

        var stack = new Stack<int>();
        void Seed(int index)
        {
            if (!background[index] && IsLightNeutral(pixels[index]))
            {
                background[index] = true;
                stack.Push(index);
            }
        }

        for (int x = 0; x < w; x++)
        {
            Seed(x);                    // bottom row
            Seed((h - 1) * w + x);      // top row
        }
        for (int y = 0; y < h; y++)
        {
            Seed(y * w);                // left column
            Seed(y * w + w - 1);        // right column
        }

        while (stack.Count > 0)
        {
            int index = stack.Pop();
            int x = index % w, y = index / w;
            if (x > 0) Seed(index - 1);
            if (x < w - 1) Seed(index + 1);
            if (y > 0) Seed(index - w);
            if (y < h - 1) Seed(index + w);
        }

        // Grow the transparent region by one pixel to swallow the JPEG fringe
        // that would otherwise survive as a pale outline.
        bool[] grown = (bool[])background.Clone();
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (background[i]) continue;
                bool touches =
                    (x > 0 && background[i - 1]) || (x < w - 1 && background[i + 1]) ||
                    (y > 0 && background[i - w]) || (y < h - 1 && background[i + w]);
                if (touches) grown[i] = true;
            }
        }
        background = grown;

        int cleared = 0;
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (background[i])
                {
                    pixels[i].a = 0;
                    cleared++;
                }
                else
                {
                    pixels[i].a = 255;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (maxX < minX || maxY < minY)
        {
            report.AppendLine("Sankofa background removal produced no opaque pixels; aborted overlay.");
            return null;
        }

        // Tight crop (2 px padding) so the PNG's extents equal the artwork extents,
        // which is what lets the Quad align to the model silhouette.
        const int pad = 2;
        minX = Mathf.Max(0, minX - pad);
        minY = Mathf.Max(0, minY - pad);
        maxX = Mathf.Min(w - 1, maxX + pad);
        maxY = Mathf.Min(h - 1, maxY + pad);
        int cw = maxX - minX + 1, ch = maxY - minY + 1;

        var cropped = new Color32[cw * ch];
        for (int y = 0; y < ch; y++)
            for (int x = 0; x < cw; x++)
                cropped[y * cw + x] = pixels[(minY + y) * w + (minX + x)];

        var output = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
        output.SetPixels32(cropped);
        output.Apply();

        File.WriteAllBytes(Path.GetFullPath(SankofaTransparentPng), output.EncodeToPNG());
        Object.DestroyImmediate(output);
        Object.DestroyImmediate(source);

        AssetDatabase.ImportAsset(SankofaTransparentPng, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(SankofaTransparentPng) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        report.AppendLine($"transparent PNG     : {SankofaTransparentPng}");
        report.AppendLine($"                      source {w}x{h} -> cropped {cw}x{ch}, " +
                          $"{cleared} px cleared ({100f * cleared / (w * h):F1}% of source), " +
                          $"artwork aspect {(float)cw / ch:F3}");

        return AssetDatabase.LoadAssetAtPath<Texture2D>(SankofaTransparentPng);
    }

    private static bool IsLightNeutral(Color32 c)
    {
        float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
        float max = Mathf.Max(r, Mathf.Max(g, b));
        float min = Mathf.Min(r, Mathf.Min(g, b));
        float luminance = 0.2126f * r + 0.7152f * g + 0.0722f * b;
        return luminance >= BackgroundLuminance && (max - min) <= BackgroundNeutrality;
    }

    // ---- Overlay material (URP Unlit cutout) -----------------------------

    private static Material EnsureOverlayMaterial(Texture2D texture, StringBuilder report)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            report.AppendLine("URP Unlit shader could not be resolved; overlay skipped.");
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(OverlayMaterialPath);
        bool created = material == null;
        if (created)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OverlayMaterialPath));
            material = new Material(shader) { name = "Sankofa_Artwork_Unlit" };
            AssetDatabase.CreateAsset(material, OverlayMaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);

        // Opaque + Alpha Clipping: a flat cutout needs no partial transparency,
        // and writing depth avoids sorting artefacts against the mesh 2 mm behind.
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", 0.5f);
        material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back); // Render Face = Front
        material.SetFloat("_ZWrite", 1f);
        material.EnableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "TransparentCutout");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);

        report.AppendLine($"overlay material    : {OverlayMaterialPath} ({(created ? "created" : "reused")})");
        report.AppendLine($"                      shader='{material.shader.name}' surface=Opaque " +
                          $"alphaClip=on cutoff=0.5 renderFace=Front baseColor=white");
        return material;
    }

    // ---- Prefab application ----------------------------------------------

    private static void ApplyToPrefab(string prefabPath, string modelObjectName,
        Material glossyBlack, Material overlayMaterial, StringBuilder report)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        if (contents == null)
        {
            report.AppendLine($"  prefab not found: {prefabPath}");
            return;
        }

        try
        {
            Transform model = FindDeep(contents.transform, modelObjectName);
            if (model == null)
            {
                report.AppendLine($"  '{modelObjectName}' not found in {prefabPath}");
                return;
            }

            // 1. Shared glossy black onto element 0 of every mesh renderer.
            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer.gameObject.name == OverlayObjectName)
                    continue;

                Material[] slots = renderer.sharedMaterials;
                string previous = slots.Length > 0 && slots[0] != null ? slots[0].name : "(none)";
                if (slots.Length == 0)
                    slots = new Material[1];
                slots[0] = glossyBlack;
                renderer.sharedMaterials = slots;
                EditorUtility.SetDirty(renderer);
                report.AppendLine($"  mesh '{renderer.gameObject.name}' element0 " +
                                  $"'{previous}' -> '{glossyBlack.name}'");
            }

            // 2. Sankofa only: the flat artwork overlay.
            if (overlayMaterial != null)
                CreateOverlay(model, renderers, overlayMaterial, report);

            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath, out bool saved);
            report.AppendLine($"  prefab saved: {saved} ({prefabPath})");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void CreateOverlay(Transform model, MeshRenderer[] modelRenderers,
        Material overlayMaterial, StringBuilder report)
    {
        // Replace any overlay from a previous run.
        Transform existing = FindDeep(model, OverlayObjectName);
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        if (!TryGetWorldBounds(modelRenderers, out Bounds world))
        {
            report.AppendLine("  overlay skipped: model bounds unavailable.");
            return;
        }

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = OverlayObjectName;

        // The primitive ships with a MeshCollider; collider setup must not change.
        MeshCollider autoCollider = quad.GetComponent<MeshCollider>();
        if (autoCollider != null)
            Object.DestroyImmediate(autoCollider);

        quad.transform.SetParent(model, false);

        // Work in the model's local space (identity rotation, uniform scale).
        Vector3 localMin = model.InverseTransformPoint(world.min);
        Vector3 localMax = model.InverseTransformPoint(world.max);
        float width = Mathf.Abs(localMax.x - localMin.x);
        float height = Mathf.Abs(localMax.y - localMin.y);
        float frontZ = Mathf.Max(localMin.z, localMax.z);

        float scaleZ = Mathf.Abs(model.lossyScale.z) < 1e-6f ? 1f : Mathf.Abs(model.lossyScale.z);
        float localOffset = OverlayOffsetMetres / scaleZ;

        quad.transform.localPosition = new Vector3(
            (localMin.x + localMax.x) * 0.5f,
            (localMin.y + localMax.y) * 0.5f,
            frontZ + localOffset);

        // Unity's Quad normal points along -Z; turn it to face the model's front (+Z).
        Mesh quadMesh = quad.GetComponent<MeshFilter>().sharedMesh;
        Vector3 meshNormal = quadMesh != null && quadMesh.normals.Length > 0
            ? quadMesh.normals[0]
            : new Vector3(0f, 0f, -1f);
        quad.transform.localRotation = meshNormal.z < 0f
            ? Quaternion.Euler(0f, 180f, 0f)
            : Quaternion.identity;

        quad.transform.localScale = new Vector3(width, height, 1f);

        MeshRenderer quadRenderer = quad.GetComponent<MeshRenderer>();
        quadRenderer.sharedMaterials = new[] { overlayMaterial };
        quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quadRenderer.receiveShadows = false;
        EditorUtility.SetDirty(quadRenderer);

        Vector3 worldNormal = quad.transform.TransformDirection(meshNormal).normalized;
        report.AppendLine($"  overlay '{quad.name}' parent='{model.name}'");
        report.AppendLine($"           localPos={quad.transform.localPosition} " +
                          $"localScale={quad.transform.localScale} " +
                          $"localEuler={quad.transform.localEulerAngles}");
        report.AppendLine($"           model visible size (world) {world.size.x:F4} x {world.size.y:F4} x " +
                          $"{world.size.z:F4} m, mesh aspect {world.size.x / world.size.y:F3}");
        report.AppendLine($"           overlay world offset {OverlayOffsetMetres * 1000f:F1} mm in front; " +
                          $"facing normal (world) {worldNormal}");
        report.AppendLine($"           shadows: cast=Off receive=False; MeshCollider removed");
    }

    private static bool TryGetWorldBounds(MeshRenderer[] renderers, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (MeshRenderer renderer in renderers)
        {
            if (renderer == null || renderer.gameObject.name == OverlayObjectName)
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

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null)
                return found;
        }

        return null;
    }
}
