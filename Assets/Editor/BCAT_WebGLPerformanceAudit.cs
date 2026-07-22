using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public static class BCAT_WebGLPerformanceAudit
{
    private const string MenuPath = "BCaT/Audit/WebGL Performance Report";
    private const string OutputDirectory = "Assets/BCAT_AuditReports";
    private const int TopTextureCount = 20;
    private const int TopMediaCount = 20;

    [MenuItem(MenuPath)]
    public static void GenerateReport()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            Debug.LogError("BCAT WebGL Performance Audit: No active scene is loaded.");
            return;
        }

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string outputFile = Path.Combine(OutputDirectory, $"WebGL_Performance_Audit_{timestamp}.txt");
        string fullOutputPath = Path.GetFullPath(outputFile);

        Directory.CreateDirectory(OutputDirectory);

        try
        {
            string report = BuildReport(activeScene, fullOutputPath);
            File.WriteAllText(fullOutputPath, report, Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log($"BCAT WebGL Performance Audit report written to {outputFile}");
            EditorUtility.RevealInFinder(fullOutputPath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"BCAT WebGL Performance Audit failed: {ex}");
        }
    }

    private static string BuildReport(Scene activeScene, string fullOutputPath)
    {
        GameObject[] sceneRoots = activeScene.GetRootGameObjects();
        List<GameObject> allGameObjects = GetAllSceneGameObjects(sceneRoots);

        Terrain[] terrains = allGameObjects
            .Select(go => go.GetComponent<Terrain>())
            .Where(component => component != null)
            .ToArray();

        Renderer[] renderers = allGameObjects
            .SelectMany(go => go.GetComponents<Renderer>())
            .ToArray();

        MeshRenderer[] meshRenderers = allGameObjects
            .SelectMany(go => go.GetComponents<MeshRenderer>())
            .ToArray();

        SkinnedMeshRenderer[] skinnedMeshRenderers = allGameObjects
            .SelectMany(go => go.GetComponents<SkinnedMeshRenderer>())
            .ToArray();

        MeshFilter[] meshFilters = allGameObjects
            .SelectMany(go => go.GetComponents<MeshFilter>())
            .ToArray();

        Light[] lights = allGameObjects
            .SelectMany(go => go.GetComponents<Light>())
            .ToArray();

        ReflectionProbe[] reflectionProbes = allGameObjects
            .SelectMany(go => go.GetComponents<ReflectionProbe>())
            .ToArray();

        LightProbeGroup[] lightProbeGroups = allGameObjects
            .SelectMany(go => go.GetComponents<LightProbeGroup>())
            .ToArray();

        ParticleSystem[] particleSystems = allGameObjects
            .SelectMany(go => go.GetComponents<ParticleSystem>())
            .ToArray();

        Canvas[] canvases = allGameObjects
            .SelectMany(go => go.GetComponents<Canvas>())
            .ToArray();

        AudioSource[] audioSources = allGameObjects
            .SelectMany(go => go.GetComponents<AudioSource>())
            .ToArray();

        VideoPlayer[] videoPlayers = allGameObjects
            .SelectMany(go => go.GetComponents<VideoPlayer>())
            .ToArray();

        Collider[] colliders = allGameObjects
            .SelectMany(go => go.GetComponents<Collider>())
            .ToArray();

        List<Material> sceneMaterials = GetSceneMaterials(renderers);
        TriangleStats triangleStats = CalculateTriangleStats(meshFilters, skinnedMeshRenderers);
        MissingScriptStats missingScriptStats = GetMissingScriptStats(allGameObjects);
        WebGLSettingsSnapshot webGlSettings = GetWebGLSettingsSnapshot();
        QualitySettingsSnapshot qualitySettings = GetQualitySettingsSnapshot();
        List<AssetSizeEntry> largestTextures = GetLargestProjectAssets(TextureExtensions, TopTextureCount);
        List<AssetSizeEntry> largestMediaAssets = GetLargestProjectAssets(MediaAndModelExtensions, TopMediaCount);

        var builder = new StringBuilder(32 * 1024);
        AppendLine(builder, "BCAT WebGL Performance Audit");
        AppendLine(builder, $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        AppendLine(builder, $"Report Path: {fullOutputPath}");
        AppendLine(builder);

        AppendLine(builder, "Scene");
        AppendLine(builder, $"Active Scene Name: {activeScene.name}");
        AppendLine(builder, $"Active Scene Path: {activeScene.path}");
        AppendLine(builder, $"Root Object Count: {sceneRoots.Length}");
        AppendLine(builder, $"Total GameObject Count: {allGameObjects.Count}");
        AppendLine(builder);

        AppendLine(builder, "Terrain");
        AppendLine(builder, $"Terrain Count: {terrains.Length}");
        for (int i = 0; i < terrains.Length; i++)
        {
            AppendTerrainSection(builder, terrains[i], i);
        }
        AppendLine(builder);

        AppendLine(builder, "Scene Component Counts");
        AppendLine(builder, $"Renderer Count: {renderers.Length}");
        AppendLine(builder, $"MeshRenderer Count: {meshRenderers.Length}");
        AppendLine(builder, $"SkinnedMeshRenderer Count: {skinnedMeshRenderers.Length}");
        AppendLine(builder, $"MeshFilter Count: {meshFilters.Length}");
        AppendLine(builder, $"Approximate Triangle Count From Readable Meshes: {triangleStats.ReadableTriangleCount}");
        AppendLine(builder, $"Readable Meshes Used For Triangle Count: {triangleStats.ReadableMeshCount}");
        AppendLine(builder, $"Non-Readable Meshes Skipped For Triangle Count: {triangleStats.NonReadableMeshCount}");
        AppendLine(builder);

        AppendLine(builder, "Materials And Shaders");
        AppendLine(builder, $"Material Count: {sceneMaterials.Count}");
        AppendLine(builder, $"Unique Shader Count: {sceneMaterials.Select(material => material.shader).Where(shader => shader != null).Distinct().Count()}");
        AppendLine(builder, $"Transparent Material Count: {sceneMaterials.Count(IsTransparentMaterial)}");
        AppendLine(builder, $"Alpha-Clipped Material Count: {sceneMaterials.Count(IsAlphaClippedMaterial)}");
        AppendLine(builder, $"Materials With GPU Instancing Enabled: {sceneMaterials.Count(material => material != null && material.enableInstancing)}");
        AppendLine(builder, $"Materials With GPU Instancing Disabled: {sceneMaterials.Count(material => material != null && !material.enableInstancing)}");
        AppendLine(builder);

        AppendLine(builder, "Lighting And FX");
        foreach (IGrouping<LightType, Light> grouping in lights.GroupBy(light => light.type).OrderBy(group => group.Key.ToString()))
        {
            AppendLine(builder, $"Light Count ({grouping.Key}): {grouping.Count()}");
        }
        AppendLine(builder, $"Total Light Count: {lights.Length}");
        AppendLine(builder, $"Reflection Probe Count: {reflectionProbes.Length}");
        AppendLine(builder, $"Light Probe Group Count: {lightProbeGroups.Length}");
        AppendLine(builder, $"Particle System Count: {particleSystems.Length}");
        AppendLine(builder);

        AppendLine(builder, "Colliders");
        foreach (IGrouping<string, Collider> grouping in colliders.GroupBy(collider => collider.GetType().Name).OrderBy(group => group.Key))
        {
            AppendLine(builder, $"Collider Count ({grouping.Key}): {grouping.Count()}");
        }
        AppendLine(builder, $"Total Collider Count: {colliders.Length}");
        AppendLine(builder);

        AppendLine(builder, "UI And Media");
        AppendLine(builder, $"Canvas Count: {canvases.Length}");
        AppendLine(builder, $"AudioSource Count: {audioSources.Length}");
        AppendLine(builder, $"VideoPlayer Count: {videoPlayers.Length}");
        AppendLine(builder);

        AppendLine(builder, "Missing Scripts");
        AppendLine(builder, $"GameObjects With Missing Scripts: {missingScriptStats.GameObjectsWithMissingScripts}");
        AppendLine(builder, $"Total Missing Script Slots: {missingScriptStats.TotalMissingScriptSlots}");
        if (missingScriptStats.Paths.Count == 0)
        {
            AppendLine(builder, "No missing scripts found in the active scene.");
        }
        else
        {
            foreach (string path in missingScriptStats.Paths)
            {
                AppendLine(builder, path);
            }
        }
        AppendLine(builder);

        AppendLine(builder, "URP And Quality");
        AppendLine(builder, $"Current WebGL Quality Tier: {qualitySettings.WebGLQualityTierName}");
        AppendLine(builder, $"Current WebGL Quality Tier Index: {qualitySettings.WebGLQualityTierIndex}");
        AppendLine(builder, $"URP Asset Assigned To Active WebGL Quality Tier: {qualitySettings.WebGLUrpAssetName}");
        AppendLine(builder, $"URP Asset Path: {qualitySettings.WebGLUrpAssetPath}");
        AppendLine(builder);

        AppendLine(builder, "WebGL Player Settings");
        AppendLine(builder, $"Color Space: {PlayerSettings.colorSpace}");
        AppendLine(builder, $"MTRendering: {PlayerSettings.GetMobileMTRendering(NamedBuildTarget.WebGL)}");
        AppendLine(builder, $"GPU Skinning: {PlayerSettings.gpuSkinning}");
        AppendLine(builder, $"Graphics Jobs: {PlayerSettings.graphicsJobs}");
        AppendLine(builder, $"Strip Engine Code: {PlayerSettings.stripEngineCode}");
        AppendLine(builder, $"Incremental GC: {PlayerSettings.gcIncremental}");
        AppendLine(builder, $"WebGL Memory Size (legacy MB): {webGlSettings.WebGLMemorySizeMb}");
        AppendLine(builder, $"WebGL Initial Memory Size (MB): {webGlSettings.WebGLInitialMemorySizeMb}");
        AppendLine(builder, $"WebGL Maximum Memory Size (MB): {webGlSettings.WebGLMaximumMemorySizeMb}");
        AppendLine(builder, $"WebGL Memory Growth Mode: {webGlSettings.WebGLMemoryGrowthMode}");
        AppendLine(builder, $"WebGL Memory Linear Growth Step (MB): {webGlSettings.WebGLMemoryLinearGrowthStepMb}");
        AppendLine(builder, $"WebGL Memory Geometric Growth Step: {webGlSettings.WebGLMemoryGeometricGrowthStep}");
        AppendLine(builder, $"WebGL Memory Geometric Growth Cap (MB): {webGlSettings.WebGLMemoryGeometricGrowthCapMb}");
        AppendLine(builder, $"WebGL Data Caching: {webGlSettings.WebGLDataCaching}");
        AppendLine(builder, $"WebGL Compression Format: {webGlSettings.WebGLCompressionFormat}");
        AppendLine(builder, $"WebGL Decompression Fallback: {webGlSettings.WebGLDecompressionFallback}");
        AppendLine(builder, $"WebGL Exception Support: {webGlSettings.WebGLExceptionSupport}");
        AppendLine(builder, $"WebGL Debug Symbols: {webGlSettings.WebGLDebugSymbols}");
        AppendLine(builder, $"WebGL Threads Support: {webGlSettings.WebGLThreadsSupport}");
        AppendLine(builder, $"WebGL Name Files As Hashes: {webGlSettings.WebGLNameFilesAsHashes}");
        AppendLine(builder, $"WebGL Show Diagnostics: {webGlSettings.WebGLShowDiagnostics}");
        AppendLine(builder, $"WebGL Analyze Build Size: {webGlSettings.WebGLAnalyzeBuildSize}");
        AppendLine(builder, $"WebGL Use Embedded Resources: {webGlSettings.WebGLUseEmbeddedResources}");
        AppendLine(builder, $"WebGL Power Preference: {webGlSettings.WebGLPowerPreference}");
        AppendLine(builder);

        AppendLine(builder, "Largest Textures In Project");
        AppendAssetSizeEntries(builder, largestTextures);
        AppendLine(builder);

        AppendLine(builder, "Largest Audio/Video/Model Assets In Project");
        AppendAssetSizeEntries(builder, largestMediaAssets);

        return builder.ToString();
    }

    private static void AppendTerrainSection(StringBuilder builder, Terrain terrain, int index)
    {
        TerrainData terrainData = terrain.terrainData;
        AppendLine(builder, $"Terrain [{index}]");
        AppendLine(builder, $"Name: {terrain.name}");
        AppendLine(builder, $"Scene Path: {GetGameObjectPath(terrain.gameObject)}");

        if (terrainData == null)
        {
            AppendLine(builder, "TerrainData: null");
            return;
        }

        AppendLine(builder, $"Terrain Size: {terrainData.size}");
        AppendLine(builder, $"Detail Resolution: {terrainData.detailResolution}");
        AppendLine(builder, $"Detail Resolution Per Patch: {terrainData.detailResolutionPerPatch}");
        AppendLine(builder, $"Detail Prototype Count: {terrainData.detailPrototypes.Length}");

        long totalDetailInstances = 0;
        for (int layerIndex = 0; layerIndex < terrainData.detailPrototypes.Length; layerIndex++)
        {
            long layerCount = CountDetailLayerInstances(terrainData, layerIndex);
            totalDetailInstances += layerCount;
            string prototypeName = GetDetailPrototypeName(terrainData.detailPrototypes[layerIndex], layerIndex);
            AppendLine(builder, $"Detail Layer [{layerIndex}] {prototypeName}: {layerCount}");
        }

        AppendLine(builder, $"Total Detail Instance Count: {totalDetailInstances}");
        AppendLine(builder, $"Tree Instance Count: {terrainData.treeInstanceCount}");
        AppendLine(builder, $"Tree Prototype Count: {terrainData.treePrototypes.Length}");
        AppendLine(builder, $"Tree Distance: {terrain.treeDistance}");
        AppendLine(builder, $"Detail Distance: {terrain.detailObjectDistance}");
        AppendLine(builder, $"Basemap Distance: {terrain.basemapDistance}");
        AppendLine(builder, $"Pixel Error: {terrain.heightmapPixelError}");
        AppendLine(builder, $"Draw Instanced: {terrain.drawInstanced}");

        TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
        AppendLine(builder, $"Tree Colliders Enabled: {GetTerrainTreeColliderStatus(terrainCollider)}");
    }

    private static long CountDetailLayerInstances(TerrainData terrainData, int layerIndex)
    {
        int resolution = terrainData.detailResolution;
        if (resolution <= 0)
        {
            return 0;
        }

        int[,] detailLayer = terrainData.GetDetailLayer(0, 0, resolution, resolution, layerIndex);
        long total = 0;
        int width = detailLayer.GetLength(0);
        int height = detailLayer.GetLength(1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                total += detailLayer[x, y];
            }
        }

        return total;
    }

    private static string GetDetailPrototypeName(DetailPrototype prototype, int layerIndex)
    {
        if (prototype.prototype != null)
        {
            return prototype.prototype.name;
        }

        if (prototype.prototypeTexture != null)
        {
            return prototype.prototypeTexture.name;
        }

        return $"Prototype_{layerIndex}";
    }

    private static TriangleStats CalculateTriangleStats(IEnumerable<MeshFilter> meshFilters, IEnumerable<SkinnedMeshRenderer> skinnedMeshRenderers)
    {
        long readableTriangleCount = 0;
        int readableMeshCount = 0;
        int nonReadableMeshCount = 0;

        foreach (Mesh mesh in meshFilters.Select(filter => filter.sharedMesh).Concat(skinnedMeshRenderers.Select(renderer => renderer.sharedMesh)))
        {
            if (mesh == null)
            {
                continue;
            }

            if (!mesh.isReadable)
            {
                nonReadableMeshCount++;
                continue;
            }

            readableTriangleCount += GetTriangleCount(mesh);
            readableMeshCount++;
        }

        return new TriangleStats(readableTriangleCount, readableMeshCount, nonReadableMeshCount);
    }

    private static long GetTriangleCount(Mesh mesh)
    {
        long triangleCount = 0;
        int subMeshCount = mesh.subMeshCount;
        for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
        {
            try
            {
                if (mesh.GetTopology(subMeshIndex) == MeshTopology.Triangles)
                {
                    triangleCount += (long)mesh.GetIndexCount(subMeshIndex) / 3L;
                }
            }
            catch
            {
                // Keep the audit read-only and resilient if a mesh cannot report topology.
            }
        }

        return triangleCount;
    }

    private static List<Material> GetSceneMaterials(IEnumerable<Renderer> renderers)
    {
        return renderers
            .Where(renderer => renderer != null)
            .SelectMany(renderer => renderer.sharedMaterials)
            .Where(material => material != null)
            .Distinct()
            .OrderBy(material => material.name)
            .ToList();
    }

    private static bool IsTransparentMaterial(Material material)
    {
        if (material == null)
        {
            return false;
        }

        if (material.renderQueue >= (int)RenderQueue.Transparent)
        {
            return true;
        }

        if (material.HasProperty("_Surface") && material.GetFloat("_Surface") >= 1f)
        {
            return true;
        }

        string renderType = material.GetTag("RenderType", false, string.Empty);
        return renderType.IndexOf("Transparent", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsAlphaClippedMaterial(Material material)
    {
        if (material == null)
        {
            return false;
        }

        if (material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") >= 0.5f)
        {
            return true;
        }

        if (material.IsKeywordEnabled("_ALPHATEST_ON"))
        {
            return true;
        }

        string renderType = material.GetTag("RenderType", false, string.Empty);
        return renderType.IndexOf("TransparentCutout", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static MissingScriptStats GetMissingScriptStats(IEnumerable<GameObject> gameObjects)
    {
        int gameObjectsWithMissingScripts = 0;
        int totalMissingScriptSlots = 0;
        var paths = new List<string>();

        foreach (GameObject gameObject in gameObjects)
        {
            int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
            if (missingCount <= 0)
            {
                continue;
            }

            gameObjectsWithMissingScripts++;
            totalMissingScriptSlots += missingCount;
            paths.Add($"{GetGameObjectPath(gameObject)} (missing scripts: {missingCount})");
        }

        return new MissingScriptStats(gameObjectsWithMissingScripts, totalMissingScriptSlots, paths);
    }

    private static QualitySettingsSnapshot GetQualitySettingsSnapshot()
    {
        int webGlTierIndex = GetWebGLQualityTierIndexFromProjectSettings();
        string tierName = webGlTierIndex >= 0 && webGlTierIndex < QualitySettings.names.Length
            ? QualitySettings.names[webGlTierIndex]
            : "<unknown>";

        RenderPipelineAsset pipelineAsset = QualitySettings.GetRenderPipelineAssetAt(webGlTierIndex);
        string assetName = pipelineAsset != null ? pipelineAsset.name : "<none>";
        string assetPath = pipelineAsset != null ? AssetDatabase.GetAssetPath(pipelineAsset) : "<none>";

        return new QualitySettingsSnapshot(webGlTierIndex, tierName, assetName, assetPath);
    }

    private static WebGLSettingsSnapshot GetWebGLSettingsSnapshot()
    {
        string projectSettingsText = ReadProjectSettingsAsset("ProjectSettings/ProjectSettings.asset");

        return new WebGLSettingsSnapshot(
            GetYamlInt(projectSettingsText, "webGLMemorySize"),
            GetYamlInt(projectSettingsText, "webGLInitialMemorySize"),
            GetYamlInt(projectSettingsText, "webGLMaximumMemorySize"),
            GetYamlInt(projectSettingsText, "webGLMemoryGrowthMode"),
            GetYamlInt(projectSettingsText, "webGLMemoryLinearGrowthStep"),
            GetYamlFloat(projectSettingsText, "webGLMemoryGeometricGrowthStep"),
            GetYamlInt(projectSettingsText, "webGLMemoryGeometricGrowthCap"),
            GetYamlBool(projectSettingsText, "webGLDataCaching"),
            GetYamlInt(projectSettingsText, "webGLCompressionFormat"),
            GetYamlBool(projectSettingsText, "webGLDecompressionFallback"),
            GetYamlInt(projectSettingsText, "webGLExceptionSupport"),
            GetYamlBool(projectSettingsText, "webGLDebugSymbols"),
            GetYamlBool(projectSettingsText, "webGLThreadsSupport"),
            GetYamlBool(projectSettingsText, "webGLNameFilesAsHashes"),
            GetYamlBool(projectSettingsText, "webGLShowDiagnostics"),
            GetYamlBool(projectSettingsText, "webGLAnalyzeBuildSize"),
            GetYamlBool(projectSettingsText, "webGLUseEmbeddedResources"),
            GetYamlInt(projectSettingsText, "webGLPowerPreference"));
    }

    private static List<AssetSizeEntry> GetLargestProjectAssets(HashSet<string> extensions, int count)
    {
        var results = new List<AssetSizeEntry>();
        string assetsRoot = Path.GetFullPath("Assets");

        foreach (string filePath in Directory.EnumerateFiles(assetsRoot, "*", SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(extension) || !extensions.Contains(extension.ToLowerInvariant()))
            {
                continue;
            }

            var info = new FileInfo(filePath);
            string relativePath = ToAssetPath(filePath);
            results.Add(new AssetSizeEntry(relativePath, info.Length));
        }

        return results
            .OrderByDescending(entry => entry.SizeBytes)
            .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .Take(count)
            .ToList();
    }

    private static List<GameObject> GetAllSceneGameObjects(IEnumerable<GameObject> sceneRoots)
    {
        var result = new List<GameObject>();
        foreach (GameObject root in sceneRoots)
        {
            CollectGameObjectsRecursive(root.transform, result);
        }

        return result;
    }

    private static void CollectGameObjectsRecursive(Transform current, List<GameObject> output)
    {
        output.Add(current.gameObject);
        for (int i = 0; i < current.childCount; i++)
        {
            CollectGameObjectsRecursive(current.GetChild(i), output);
        }
    }

    private static string GetGameObjectPath(GameObject gameObject)
    {
        var stack = new Stack<string>();
        Transform current = gameObject.transform;
        while (current != null)
        {
            stack.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", stack);
    }

    private static string ToAssetPath(string fullPath)
    {
        string normalizedFullPath = fullPath.Replace('\\', '/');
        string normalizedProjectPath = Path.GetFullPath(".").Replace('\\', '/');
        if (normalizedFullPath.StartsWith(normalizedProjectPath, StringComparison.OrdinalIgnoreCase))
        {
            string relative = normalizedFullPath.Substring(normalizedProjectPath.Length).TrimStart('/');
            return relative;
        }

        return normalizedFullPath;
    }

    private static bool GetTerrainTreeColliderStatus(TerrainCollider terrainCollider)
    {
        if (terrainCollider == null)
        {
            return false;
        }

        SerializedObject serializedObject = new SerializedObject(terrainCollider);
        SerializedProperty property = serializedObject.FindProperty("m_EnableTreeColliders");
        return property != null && property.boolValue;
    }

    private static int GetWebGLQualityTierIndexFromProjectSettings()
    {
        string qualitySettingsText = ReadProjectSettingsAsset("ProjectSettings/QualitySettings.asset");
        Match match = Regex.Match(qualitySettingsText, @"^\s*WebGL:\s*(\d+)\s*$", RegexOptions.Multiline);
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : -1;
    }

    private static string ReadProjectSettingsAsset(string relativePath)
    {
        string fullPath = Path.GetFullPath(relativePath);
        return File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty;
    }

    private static int GetYamlInt(string yamlText, string propertyName)
    {
        Match match = Regex.Match(yamlText, @"^\s*" + Regex.Escape(propertyName) + @":\s*(-?\d+)\s*$", RegexOptions.Multiline);
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
    }

    private static float GetYamlFloat(string yamlText, string propertyName)
    {
        Match match = Regex.Match(yamlText, @"^\s*" + Regex.Escape(propertyName) + @":\s*(-?\d+(?:\.\d+)?)\s*$", RegexOptions.Multiline);
        return match.Success ? float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0f;
    }

    private static bool GetYamlBool(string yamlText, string propertyName)
    {
        return GetYamlInt(yamlText, propertyName) != 0;
    }

    private static void AppendAssetSizeEntries(StringBuilder builder, IEnumerable<AssetSizeEntry> entries)
    {
        foreach (AssetSizeEntry entry in entries)
        {
            AppendLine(builder, $"{FormatBytes(entry.SizeBytes)} | {entry.Path}");
        }
    }

    private static string FormatBytes(long sizeBytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = sizeBytes;
        int unitIndex = 0;
        while (size >= 1024d && unitIndex < units.Length - 1)
        {
            size /= 1024d;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }

    private static void AppendLine(StringBuilder builder, string value = "")
    {
        builder.AppendLine(value);
    }

    private readonly struct TriangleStats
    {
        public TriangleStats(long readableTriangleCount, int readableMeshCount, int nonReadableMeshCount)
        {
            ReadableTriangleCount = readableTriangleCount;
            ReadableMeshCount = readableMeshCount;
            NonReadableMeshCount = nonReadableMeshCount;
        }

        public long ReadableTriangleCount { get; }
        public int ReadableMeshCount { get; }
        public int NonReadableMeshCount { get; }
    }

    private readonly struct MissingScriptStats
    {
        public MissingScriptStats(int gameObjectsWithMissingScripts, int totalMissingScriptSlots, List<string> paths)
        {
            GameObjectsWithMissingScripts = gameObjectsWithMissingScripts;
            TotalMissingScriptSlots = totalMissingScriptSlots;
            Paths = paths;
        }

        public int GameObjectsWithMissingScripts { get; }
        public int TotalMissingScriptSlots { get; }
        public List<string> Paths { get; }
    }

    private readonly struct QualitySettingsSnapshot
    {
        public QualitySettingsSnapshot(int webGlQualityTierIndex, string webGlQualityTierName, string webGlUrpAssetName, string webGlUrpAssetPath)
        {
            WebGLQualityTierIndex = webGlQualityTierIndex;
            WebGLQualityTierName = webGlQualityTierName;
            WebGLUrpAssetName = webGlUrpAssetName;
            WebGLUrpAssetPath = webGlUrpAssetPath;
        }

        public int WebGLQualityTierIndex { get; }
        public string WebGLQualityTierName { get; }
        public string WebGLUrpAssetName { get; }
        public string WebGLUrpAssetPath { get; }
    }

    private readonly struct WebGLSettingsSnapshot
    {
        public WebGLSettingsSnapshot(
            int webGLMemorySizeMb,
            int webGLInitialMemorySizeMb,
            int webGLMaximumMemorySizeMb,
            int webGLMemoryGrowthMode,
            int webGLMemoryLinearGrowthStepMb,
            float webGLMemoryGeometricGrowthStep,
            int webGLMemoryGeometricGrowthCapMb,
            bool webGLDataCaching,
            int webGLCompressionFormat,
            bool webGLDecompressionFallback,
            int webGLExceptionSupport,
            bool webGLDebugSymbols,
            bool webGLThreadsSupport,
            bool webGLNameFilesAsHashes,
            bool webGLShowDiagnostics,
            bool webGLAnalyzeBuildSize,
            bool webGLUseEmbeddedResources,
            int webGLPowerPreference)
        {
            WebGLMemorySizeMb = webGLMemorySizeMb;
            WebGLInitialMemorySizeMb = webGLInitialMemorySizeMb;
            WebGLMaximumMemorySizeMb = webGLMaximumMemorySizeMb;
            WebGLMemoryGrowthMode = webGLMemoryGrowthMode;
            WebGLMemoryLinearGrowthStepMb = webGLMemoryLinearGrowthStepMb;
            WebGLMemoryGeometricGrowthStep = webGLMemoryGeometricGrowthStep;
            WebGLMemoryGeometricGrowthCapMb = webGLMemoryGeometricGrowthCapMb;
            WebGLDataCaching = webGLDataCaching;
            WebGLCompressionFormat = webGLCompressionFormat;
            WebGLDecompressionFallback = webGLDecompressionFallback;
            WebGLExceptionSupport = webGLExceptionSupport;
            WebGLDebugSymbols = webGLDebugSymbols;
            WebGLThreadsSupport = webGLThreadsSupport;
            WebGLNameFilesAsHashes = webGLNameFilesAsHashes;
            WebGLShowDiagnostics = webGLShowDiagnostics;
            WebGLAnalyzeBuildSize = webGLAnalyzeBuildSize;
            WebGLUseEmbeddedResources = webGLUseEmbeddedResources;
            WebGLPowerPreference = webGLPowerPreference;
        }

        public int WebGLMemorySizeMb { get; }
        public int WebGLInitialMemorySizeMb { get; }
        public int WebGLMaximumMemorySizeMb { get; }
        public int WebGLMemoryGrowthMode { get; }
        public int WebGLMemoryLinearGrowthStepMb { get; }
        public float WebGLMemoryGeometricGrowthStep { get; }
        public int WebGLMemoryGeometricGrowthCapMb { get; }
        public bool WebGLDataCaching { get; }
        public int WebGLCompressionFormat { get; }
        public bool WebGLDecompressionFallback { get; }
        public int WebGLExceptionSupport { get; }
        public bool WebGLDebugSymbols { get; }
        public bool WebGLThreadsSupport { get; }
        public bool WebGLNameFilesAsHashes { get; }
        public bool WebGLShowDiagnostics { get; }
        public bool WebGLAnalyzeBuildSize { get; }
        public bool WebGLUseEmbeddedResources { get; }
        public int WebGLPowerPreference { get; }
    }

    private readonly struct AssetSizeEntry
    {
        public AssetSizeEntry(string path, long sizeBytes)
        {
            Path = path;
            SizeBytes = sizeBytes;
        }

        public string Path { get; }
        public long SizeBytes { get; }
    }

    private static readonly HashSet<string> TextureExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".psd", ".exr", ".hdr", ".bmp", ".gif", ".ktx2"
    };

    private static readonly HashSet<string> MediaAndModelExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".mp3", ".ogg", ".aiff", ".aif", ".flac", ".mp4", ".mov", ".avi", ".webm", ".m4v",
        ".fbx", ".obj", ".blend", ".glb", ".gltf", ".dae", ".3ds", ".dxf"
    };
}
