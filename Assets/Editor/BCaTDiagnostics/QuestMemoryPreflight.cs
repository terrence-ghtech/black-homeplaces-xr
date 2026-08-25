using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace BCaT.EditorTools.Diagnostics
{
    /// <summary>
    /// Quest Release memory preflight: answers "how much RAM will this exact
    /// build need, what is using it, and will it OOM" without launching it.
    ///
    /// Observation only — it opens the main scene read-only, walks the asset
    /// dependency graph, measures the imported Android representation of every
    /// dependency, and writes a text + JSON report. Nothing is imported,
    /// reimported, moved, saved or configured.
    ///
    ///   Menu:  BCaT > Diagnostics > Quest Release Memory Preflight
    ///   Batch: -executeMethod BCaT.EditorTools.Diagnostics.QuestMemoryPreflight.Run
    ///          (run with -buildTarget Android so the imported representations
    ///           the tool measures are the Android ones)
    /// </summary>
    public static class QuestMemoryPreflight
    {
        const double Mb = QuestMemoryModel.Mb;

        // ---- collected state ------------------------------------------------

        static readonly Dictionary<string, AssetRecord> Records = new Dictionary<string, AssetRecord>();
        static readonly List<string> Notes = new List<string>();
        static readonly List<string> ConfigLines = new List<string>();
        static readonly Dictionary<string, long> SharedAssetOwners = new Dictionary<string, long>();

        static long profilerTextureCrossCheck;
        static long analyticTextureTotal;
        static long profilerMeshCrossCheck;
        static long analyticMeshTotal;

        static int materialCount;
        static int shaderCount;
        static int skippedTypeCount;
        static readonly Dictionary<string, int> SkippedTypes = new Dictionary<string, int>();

        // Scene facts
        static int sceneRootCount;
        static int sceneObjectCount;
        static int scenePrefabInstanceCount;
        static int videoPlayerCount;
        static int videoPlayerPlayOnAwakeCount;
        static int videoPlayersWithoutTargetTexture;
        static int realtimeProbeCount;
        static long realtimeProbeBytes;
        static int bakedProbeCount;
        static int audioSourcePlayOnAwakeCount;
        static readonly List<string> TerrainBreakdown = new List<string>();
        static long terrainBytes;

        // Attribution
        static readonly Dictionary<string, long> RootAttribution = new Dictionary<string, long>();
        static readonly Dictionary<string, long> PrefabAttribution = new Dictionary<string, long>();
        static readonly Dictionary<string, int> PrefabInstanceCounts = new Dictionary<string, int>();
        static readonly Dictionary<string, List<string>> PrefabTopAssets = new Dictionary<string, List<string>>();

        // Runtime (non-asset) allocations
        static long eyeBufferBytes;
        static string eyeBufferDetail = "-";
        static float renderScale = 1f;
        static int msaaSamples = 1;
        static string urpAssetName = "(none)";
        static bool questTierFound;
        static readonly List<string> QualityTierLines = new List<string>();

        // APK accounting
        static string apkPath;
        static long apkBytes;
        static long apkDataUnity3dCompressed;
        static long apkDataUnity3dUncompressed;
        static long apkStreamingAssetsBytes;
        static long apkAddressablesBytes;
        static long apkNativeLibBytes;
        static long apkResourceFileBytes;
        static string apkNote = "";

        [MenuItem("BCaT/Diagnostics/Quest Release Memory Preflight")]
        public static void Run()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            Reset();

            Debug.Log("[QuestPreflight] Phase 1: reading the Quest Release configuration.");
            CollectConfiguration();

            Debug.Log("[QuestPreflight] Phase 2/3: walking the main-scene dependency graph.");
            CollectSceneDependencies();

            Debug.Log("[QuestPreflight] Phase 4: opening the main scene for object and prefab attribution.");
            CollectSceneObjects();

            Debug.Log("[QuestPreflight] Phase 3: XR render targets.");
            eyeBufferBytes = QuestMemoryModel.EyeBufferBytes(renderScale, msaaSamples, out eyeBufferDetail);

            Debug.Log("[QuestPreflight] Phase 9: build artifact accounting.");
            CollectApk();

            Debug.Log("[QuestPreflight] Writing reports.");
            string textReport = BuildTextReport(stopwatch.Elapsed);
            string jsonReport = BuildJsonReport();

            Directory.CreateDirectory(QuestPreflightConfig.OutputDirectory);
            string textPath = Path.Combine(QuestPreflightConfig.OutputDirectory, QuestPreflightConfig.TextReportName);
            string jsonPath = Path.Combine(QuestPreflightConfig.OutputDirectory, QuestPreflightConfig.JsonReportName);
            File.WriteAllText(textPath, textReport);
            File.WriteAllText(jsonPath, jsonReport);

            Debug.Log($"[QuestPreflight] Done in {stopwatch.Elapsed.TotalSeconds:0.0}s.\n{textReport}");
            Debug.Log($"[QuestPreflight] Reports: {textPath} and {jsonPath}");
        }

        static void Reset()
        {
            Records.Clear();
            Notes.Clear();
            ConfigLines.Clear();
            SharedAssetOwners.Clear();
            SkippedTypes.Clear();
            RootAttribution.Clear();
            PrefabAttribution.Clear();
            PrefabInstanceCounts.Clear();
            PrefabTopAssets.Clear();
            TerrainBreakdown.Clear();
            QualityTierLines.Clear();
            questTierFound = false;
            profilerTextureCrossCheck = analyticTextureTotal = 0;
            profilerMeshCrossCheck = analyticMeshTotal = 0;
            materialCount = shaderCount = skippedTypeCount = 0;
            sceneRootCount = sceneObjectCount = scenePrefabInstanceCount = 0;
            videoPlayerCount = videoPlayerPlayOnAwakeCount = videoPlayersWithoutTargetTexture = 0;
            realtimeProbeCount = bakedProbeCount = audioSourcePlayOnAwakeCount = 0;
            realtimeProbeBytes = terrainBytes = eyeBufferBytes = 0;
            apkPath = null;
            apkBytes = apkDataUnity3dCompressed = apkDataUnity3dUncompressed = 0;
            apkStreamingAssetsBytes = apkAddressablesBytes = apkNativeLibBytes = apkResourceFileBytes = 0;
            apkNote = "";
        }

        // ================= PHASE 1: configuration ============================

        static void CollectConfiguration()
        {
            void Line(string name, string value, string confidence) =>
                ConfigLines.Add($"{name,-34}{value,-46}{confidence}");

            BuildTarget active = EditorUserBuildSettings.activeBuildTarget;
            Line("Active build target", active.ToString(),
                 active == BuildTarget.Android ? "MEASURED" : "WARNING — not Android");
            if (active != BuildTarget.Android)
                Notes.Add("The active build target is not Android, so the imported representations measured " +
                          "here are another platform's. Re-run with -buildTarget Android for Android-accurate " +
                          "texture formats and sizes.");

            Line("Android texture compression", EditorUserBuildSettings.androidBuildSubtarget.ToString(), "MEASURED");
            Line("Scripting backend",
                 PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android).ToString(), "MEASURED");
            Line("Managed stripping",
                 PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Android).ToString(), "MEASURED");
            Line("Target architectures", PlayerSettings.Android.targetArchitectures.ToString(), "MEASURED");
            Line("Graphics APIs (Android)",
                 string.Join(", ", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android)), "MEASURED");
            Line("Graphics jobs", PlayerSettings.graphicsJobs.ToString(), "MEASURED");
            Line("GPU skinning", PlayerSettings.gpuSkinning.ToString(), "MEASURED");
            Line("Mip stripping", PlayerSettings.mipStripping.ToString(), "MEASURED");
            Line("Texture compression format",
                 string.Join(", ", PlayerSettings.Android.textureCompressionFormats), "MEASURED");

            Line("Quality level (active)",
                 $"{QualitySettings.names[QualitySettings.GetQualityLevel()]} " +
                 $"({QualitySettings.GetQualityLevel() + 1}/{QualitySettings.names.Length})", "MEASURED");
            Line("Global mipmap limit", QualitySettings.globalTextureMipmapLimit.ToString(), "MEASURED");
            Line("Anisotropic filtering", QualitySettings.anisotropicFiltering.ToString(), "MEASURED");
            // Streaming and the mipmap limit are per quality LEVEL, and the API
            // only exposes the active one — switching levels would write to
            // QualitySettings.asset, which this tool must not do.
            Notes.Add("Texture streaming and the global mipmap limit are reported for the editor's ACTIVE " +
                      "quality tier, because Unity exposes them only for the active level and switching " +
                      "levels would modify QualitySettings.asset. Verify the '" +
                      QuestPreflightConfig.QuestQualityTierName + "' tier's streamingMipmapsActive and " +
                      "globalTextureMipmapLimit directly in ProjectSettings/QualitySettings.asset.");
            Line("Texture streaming", QualitySettings.streamingMipmapsActive
                 ? $"ON, budget {QualitySettings.streamingMipmapsMemoryBudget:0} MB"
                 : "OFF — every mip of every referenced texture is resident", "MEASURED");
            Line("Anti-aliasing (quality)", QualitySettings.antiAliasing.ToString(), "MEASURED");
            Line("Shadow resolution", QualitySettings.shadowResolution.ToString(), "MEASURED");
            Line("Async upload buffer",
                 $"{QualitySettings.asyncUploadBufferSize} MB, persistent={QualitySettings.asyncUploadPersistentBuffer}",
                 "MEASURED");
            Line("Skin weights", QualitySettings.skinWeights.ToString(), "MEASURED");
            Line("VSync / target frame rate",
                 $"vSync={QualitySettings.vSyncCount} targetFrameRate={Application.targetFrameRate}", "MEASURED");

            msaaSamples = Mathf.Max(1, QualitySettings.antiAliasing);
            ReadQualityTiers();
            Line($"Quality tier modelled ('{QuestPreflightConfig.QuestQualityTierName}')",
                 questTierFound ? urpAssetName : $"{urpAssetName} — NO '{QuestPreflightConfig.QuestQualityTierName}' TIER FOUND",
                 questTierFound ? "MEASURED" : "FALLBACK — active tier used");
            Line("URP render scale (Quest tier)", renderScale.ToString("0.00", CultureInfo.InvariantCulture), "MEASURED");
            Line("URP MSAA samples (Quest tier)", msaaSamples.ToString(), "MEASURED");
            foreach (string tier in QualityTierLines)
                Line("  tier", tier, "MEASURED");

            Line("XR loaders (Android)", ReadXrLoaders(), "MEASURED");
            Line("Eye buffer model",
                 $"{QuestPreflightConfig.QuestEyeWidth}x{QuestPreflightConfig.QuestEyeHeight} per eye @1.0",
                 "ASSUMED");

            var audioConfig = AudioSettings.GetConfiguration();
            Line("Audio config",
                 $"{audioConfig.sampleRate} Hz, {audioConfig.speakerMode}, buffer {audioConfig.dspBufferSize}, " +
                 $"{audioConfig.numRealVoices} real / {audioConfig.numVirtualVoices} virtual", "MEASURED");

            Line("StreamingAssets on disk", $"{FolderBytes("Assets/StreamingAssets") / Mb:N0} MB", "MEASURED");
            Line("Addressables groups", ReadAddressablesSummary(), "MEASURED");
            Line("Scenes in build",
                 string.Join(", ", EditorBuildSettings.scenes.Where(s => s.enabled)
                     .Select(s => Path.GetFileNameWithoutExtension(s.path))), "MEASURED");
        }

        /// <summary>
        /// Reads every quality tier's render pipeline asset WITHOUT switching the
        /// active level (switching would write to QualitySettings.asset, and this
        /// tool changes nothing), then models the eye buffer from the tier the
        /// Quest player actually selects at runtime.
        /// </summary>
        static void ReadQualityTiers()
        {
            string[] names = QualitySettings.names;
            for (int level = 0; level < names.Length; level++)
            {
                ScriptableObject pipeline = null;
                try { pipeline = QualitySettings.GetRenderPipelineAssetAt(level) as ScriptableObject; }
                catch (Exception) { }

                ReadPipelineValues(pipeline, out string assetName, out float scale, out int msaa);
                QualityTierLines.Add($"{names[level],-18} urp={assetName,-28} scale={scale:0.00} msaa={msaa}");

                if (names[level] != QuestPreflightConfig.QuestQualityTierName)
                    continue;

                questTierFound = true;
                urpAssetName = assetName;
                renderScale = scale;
                msaaSamples = msaa;
            }

            if (questTierFound)
                return;

            // No Quest tier: fall back to whatever is active and say so.
            ScriptableObject active = QualitySettings.renderPipeline as ScriptableObject
                                      ?? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline as ScriptableObject;
            ReadPipelineValues(active, out urpAssetName, out renderScale, out int activeMsaa);
            msaaSamples = activeMsaa;
            Notes.Add($"No '{QuestPreflightConfig.QuestQualityTierName}' quality tier exists, so the eye-buffer " +
                      "model used the editor's active tier. Its render scale and MSAA may not be the ones the " +
                      "Quest player runs with.");
        }

        static void ReadPipelineValues(ScriptableObject pipeline, out string assetName, out float scale, out int msaa)
        {
            assetName = "(built-in pipeline)";
            scale = 1f;
            msaa = Mathf.Max(1, QualitySettings.antiAliasing);
            if (pipeline == null)
                return;

            assetName = pipeline.name;

            // Reflection keeps this tool independent of the URP assembly.
            Type type = pipeline.GetType();
            PropertyInfo scaleProperty = type.GetProperty("renderScale", BindingFlags.Public | BindingFlags.Instance);
            if (scaleProperty != null && scaleProperty.PropertyType == typeof(float))
                scale = (float)scaleProperty.GetValue(pipeline);

            PropertyInfo msaaProperty = type.GetProperty("msaaSampleCount", BindingFlags.Public | BindingFlags.Instance);
            if (msaaProperty != null && msaaProperty.PropertyType == typeof(int))
                msaa = Mathf.Max(1, (int)msaaProperty.GetValue(pipeline));
        }

        static string ReadXrLoaders()
        {
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject XRGeneralSettings"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var loaded = AssetDatabase.LoadAllAssetsAtPath(path)
                        .Where(o => o != null && o.GetType().Name.Contains("XRManagerSettings"))
                        .Select(o => o.name)
                        .ToArray();
                    if (loaded.Length > 0)
                        return $"{string.Join(", ", loaded)} (from {Path.GetFileName(path)})";
                }
            }
            catch (Exception e)
            {
                return $"not read ({e.GetType().Name})";
            }
            return "not read";
        }

        static string ReadAddressablesSummary()
        {
            string[] groups = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/AddressableAssetsData" });
            return groups.Length == 0
                ? "none"
                : $"{groups.Length} Addressables assets under Assets/AddressableAssetsData " +
                  "(BlackKitchen_MemoryScene is the only remote scene; not resident at main-house startup)";
        }

        static long FolderBytes(string folder)
        {
            if (!Directory.Exists(folder))
                return 0;
            long total = 0;
            foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;
                try { total += new FileInfo(file).Length; } catch (Exception) { }
            }
            return total;
        }

        // ================= PHASE 2/3: dependency accounting ==================

        static void CollectSceneDependencies()
        {
            string[] dependencies = AssetDatabase.GetDependencies(QuestPreflightConfig.MainScenePath, true);
            Notes.Add($"Dependency graph of {QuestPreflightConfig.MainScenePath}: {dependencies.Length} asset files.");

            int processed = 0;
            foreach (string path in dependencies)
            {
                processed++;
                if (processed % 200 == 0)
                    Debug.Log($"[QuestPreflight] {processed}/{dependencies.Length} dependency files measured.");

                if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".inputactions", StringComparison.OrdinalIgnoreCase))
                    continue;

                UnityEngine.Object[] objects;
                try
                {
                    objects = AssetDatabase.LoadAllAssetsAtPath(path);
                }
                catch (Exception e)
                {
                    Notes.Add($"Could not load '{path}': {e.GetType().Name}");
                    continue;
                }

                foreach (UnityEngine.Object obj in objects)
                    Measure(obj, path);

                if (processed % QuestPreflightConfig.UnloadEveryNAssets == 0)
                {
                    // Keep the tool's own footprint bounded: only numbers and
                    // strings are retained, so releasing the cache is safe.
                    EditorUtility.UnloadUnusedAssetsImmediate();
                    GC.Collect();
                }
            }

            EditorUtility.UnloadUnusedAssetsImmediate();
            GC.Collect();
        }

        /// <summary>
        /// One asset, measured once. Identity is guid:localFileId, so a texture
        /// referenced by forty materials is counted a single time.
        /// </summary>
        static AssetRecord Measure(UnityEngine.Object obj, string path)
        {
            if (obj == null)
                return null;

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out long localId))
                return null;

            string key = guid + ":" + localId;
            if (Records.TryGetValue(key, out AssetRecord existing))
                return existing;

            var record = new AssetRecord
            {
                Key = key,
                Guid = guid,
                Path = path,
                Name = obj.name,
                Type = obj.GetType().Name,
                Category = QuestPreflightConfig.CategoryFor(path),
                Confidence = "CALCULATED",
            };

            switch (obj)
            {
                case Texture texture:
                {
                    long bytes = QuestMemoryModel.TextureBytes(texture, out string detail, out bool readable);
                    record.GpuBytes = bytes;
                    record.CpuBytes = readable ? bytes : 0;
                    record.Detail = detail;
                    analyticTextureTotal += record.TotalBytes;
                    profilerTextureCrossCheck += SafeProfilerSize(obj);
                    break;
                }

                case Mesh mesh:
                {
                    long gpu = QuestMemoryModel.MeshBytes(mesh, out long cpu, out string detail);
                    record.GpuBytes = gpu;
                    record.CpuBytes = cpu;
                    record.Detail = detail;
                    analyticMeshTotal += record.TotalBytes;
                    profilerMeshCrossCheck += SafeProfilerSize(obj);
                    break;
                }

                case AudioClip clip:
                {
                    record.CpuBytes = QuestMemoryModel.AudioBytes(clip, path, out string detail, out string confidence);
                    record.Detail = detail;
                    record.Confidence = confidence;
                    break;
                }

                case TerrainData terrain:
                {
                    long bytes = QuestMemoryModel.TerrainBytes(terrain, out string detail, out List<string> breakdown);
                    record.CpuBytes = bytes;
                    record.Detail = detail;
                    record.Category = "Terrain";
                    terrainBytes += bytes;
                    TerrainBreakdown.AddRange(breakdown.Select(b => $"{obj.name}: {b}"));
                    break;
                }

                case VideoClip video:
                {
                    // A VideoClip is streamed by the platform decoder; the clip
                    // itself is not resident. The decoder working set is counted
                    // once per PREPARED player instead.
                    record.CpuBytes = 0;
                    record.Detail = $"{video.width}x{video.height} {video.frameCount} frames " +
                                    $"@{video.frameRate:0.0}fps — streamed, not resident";
                    record.Confidence = "MEASURED (excluded from resident total by design)";
                    break;
                }

                case Material _:
                    materialCount++;
                    record.Detail = "textures counted once in the texture pass; property block only";
                    record.Confidence = "EXCLUDED (avoids double-counting shared textures)";
                    break;

                case Shader _:
                    shaderCount++;
                    record.Detail = "runtime variant memory is not statically knowable";
                    record.Confidence = "EXCLUDED (see confidence table)";
                    break;

                case AnimationClip _:
                case Font _:
                case TextAsset _:
                case ScriptableObject _:
                {
                    long bytes = SafeProfilerSize(obj);
                    record.CpuBytes = bytes;
                    record.Detail = "Profiler.GetRuntimeMemorySizeLong in the editor";
                    record.Confidence = bytes > 0 ? "MEASURED (editor runtime size)" : "ESTIMATED";
                    break;
                }

                default:
                    skippedTypeCount++;
                    SkippedTypes.TryGetValue(record.Type, out int count);
                    SkippedTypes[record.Type] = count + 1;
                    record.Detail = "no runtime-size model for this type";
                    record.Confidence = "EXCLUDED";
                    break;
            }

            Records[key] = record;
            return record;
        }

        static long SafeProfilerSize(UnityEngine.Object obj)
        {
            try { return Profiler.GetRuntimeMemorySizeLong(obj); }
            catch (Exception) { return 0; }
        }

        // ================= PHASE 4: scene objects and attribution ============

        static void CollectSceneObjects()
        {
            Scene scene;
            try
            {
                scene = EditorSceneManager.OpenScene(QuestPreflightConfig.MainScenePath, OpenSceneMode.Single);
            }
            catch (Exception e)
            {
                Notes.Add($"Could not open {QuestPreflightConfig.MainScenePath} for object attribution: {e.Message}. " +
                          "Asset accounting is unaffected; per-object attribution is missing.");
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            sceneRootCount = roots.Length;

            var prefabPaths = new Dictionary<string, int>();

            foreach (GameObject root in roots)
            {
                Transform[] all = root.GetComponentsInChildren<Transform>(true);
                sceneObjectCount += all.Length;

                foreach (Transform transform in all)
                {
                    GameObject go = transform.gameObject;

                    if (PrefabUtility.IsAnyPrefabInstanceRoot(go))
                    {
                        scenePrefabInstanceCount++;
                        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                        if (!string.IsNullOrEmpty(prefabPath))
                        {
                            prefabPaths.TryGetValue(prefabPath, out int count);
                            prefabPaths[prefabPath] = count + 1;
                        }
                    }
                }

                foreach (VideoPlayer player in root.GetComponentsInChildren<VideoPlayer>(true))
                {
                    videoPlayerCount++;
                    if (player.playOnAwake)
                        videoPlayerPlayOnAwakeCount++;
                    if (player.targetTexture == null)
                        videoPlayersWithoutTargetTexture++;
                }

                foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
                    if (source.playOnAwake)
                        audioSourcePlayOnAwakeCount++;

                foreach (ReflectionProbe probe in root.GetComponentsInChildren<ReflectionProbe>(true))
                {
                    if (probe.mode == UnityEngine.Rendering.ReflectionProbeMode.Realtime)
                    {
                        realtimeProbeCount++;
                        // Cube colour target, 6 faces with mips, RGBAHalf when HDR.
                        int resolution = Mathf.Max(16, probe.resolution);
                        int bpp = probe.hdr ? 8 : 4;
                        long faceBytes = 0;
                        int size = resolution;
                        while (size >= 1)
                        {
                            faceBytes += (long)size * size * bpp;
                            if (size == 1) break;
                            size /= 2;
                        }
                        realtimeProbeBytes += faceBytes * 6;
                    }
                    else
                    {
                        bakedProbeCount++;
                    }
                }
            }

            // Per-root attribution (inclusive: a shared asset is charged to
            // every root that pulls it in, which is stated in the report).
            foreach (GameObject root in roots)
            {
                long bytes = AttributableBytes(EditorUtility.CollectDependencies(new UnityEngine.Object[] { root }));
                if (bytes > 0)
                    RootAttribution[root.name] = bytes;
            }

            // Per-prefab attribution, computed once per prefab ASSET from its
            // own dependency graph rather than per instance.
            foreach (KeyValuePair<string, int> entry in prefabPaths)
            {
                PrefabInstanceCounts[entry.Key] = entry.Value;
                var contributions = new List<AssetRecord>();
                long total = 0;
                foreach (string dependency in AssetDatabase.GetDependencies(entry.Key, true))
                {
                    foreach (AssetRecord record in Records.Values.Where(r => r.Path == dependency && r.TotalBytes > 0))
                    {
                        total += record.TotalBytes;
                        contributions.Add(record);
                    }
                }
                if (total <= 0)
                    continue;

                PrefabAttribution[entry.Key] = total;
                PrefabTopAssets[entry.Key] = contributions
                    .OrderByDescending(r => r.TotalBytes)
                    .Take(3)
                    .Select(r => $"{r.Type,-12} {Path.GetFileName(r.Path),-46} {r.TotalBytes / Mb,8:N1} MB")
                    .ToList();
            }

            Notes.Add("The main scene was opened read-only for object attribution and was never saved.");
        }

        static long AttributableBytes(UnityEngine.Object[] objects)
        {
            var seen = new HashSet<string>();
            long total = 0;
            foreach (UnityEngine.Object obj in objects)
            {
                if (obj == null)
                    continue;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out long localId))
                    continue;
                string key = guid + ":" + localId;
                if (!seen.Add(key))
                    continue;
                if (Records.TryGetValue(key, out AssetRecord record))
                    total += record.TotalBytes;
            }
            return total;
        }

        // ================= PHASE 9: APK accounting ===========================

        static void CollectApk()
        {
            apkPath = QuestPreflightConfig.ApkCandidates.FirstOrDefault(File.Exists);
            if (apkPath == null)
            {
                apkNote = "No Quest APK found at " + string.Join(", ", QuestPreflightConfig.ApkCandidates) +
                          " — storage accounting skipped.";
                return;
            }

            apkBytes = new FileInfo(apkPath).Length;
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(apkPath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string name = entry.FullName;
                        if (name.EndsWith("data.unity3d", StringComparison.OrdinalIgnoreCase))
                        {
                            apkDataUnity3dCompressed += entry.CompressedLength;
                            apkDataUnity3dUncompressed += entry.Length;
                        }
                        else if (name.Contains("/aa/") || name.Contains("assets/aa"))
                            apkAddressablesBytes += entry.Length;
                        else if (name.StartsWith("lib/", StringComparison.OrdinalIgnoreCase))
                            apkNativeLibBytes += entry.Length;
                        else if (name.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) ||
                                 name.EndsWith(".resource", StringComparison.OrdinalIgnoreCase) ||
                                 name.Contains("sharedassets"))
                            apkResourceFileBytes += entry.Length;
                        else if (name.StartsWith("assets/", StringComparison.OrdinalIgnoreCase) &&
                                 !name.Contains("bin/Data"))
                            apkStreamingAssetsBytes += entry.Length;
                    }
                }
            }
            catch (Exception e)
            {
                apkNote = $"APK opened but entries could not be read ({e.GetType().Name}: {e.Message}); " +
                          "only the file size is reported.";
            }
        }

        // ================= totals ============================================

        sealed class Totals
        {
            public long Textures, Cubemaps, RenderTextures, Meshes, Audio, Terrain, Lighting, Animation, Other;
            public long TextureCpu, MeshCpu;
            public int TextureCount, MeshCount, AudioCount, RenderTextureCount, CubemapCount;
            public long SceneOverhead;
            public long AssetResident;
            public long RuntimeAllocations;
            public long VideoWorkingSet;
            public long Resident;
            public double HeadroomLow, HeadroomHigh;          // formula model
            public double PeakLow, PeakHigh;                  // formula model
            public double CalibratedLow, CalibratedHigh;      // trace-calibrated
            public double CalPeakLow, CalPeakHigh;
            public string Verdict;
        }

        static Totals ComputeTotals()
        {
            var totals = new Totals();

            foreach (AssetRecord record in Records.Values)
            {
                bool lighting = record.Category == QuestPreflightConfig.LightingCategory;
                switch (record.Type)
                {
                    case "Texture2D":
                    case "Texture2DArray":
                    case "Texture3D":
                        if (lighting) totals.Lighting += record.TotalBytes;
                        else totals.Textures += record.TotalBytes;
                        totals.TextureCpu += record.CpuBytes;
                        totals.TextureCount++;
                        break;
                    case "Cubemap":
                    case "CubemapArray":
                        if (lighting) totals.Lighting += record.TotalBytes;
                        else totals.Cubemaps += record.TotalBytes;
                        totals.CubemapCount++;
                        break;
                    case "RenderTexture":
                        totals.RenderTextures += record.TotalBytes;
                        totals.RenderTextureCount++;
                        break;
                    case "Mesh":
                        totals.Meshes += record.TotalBytes;
                        totals.MeshCpu += record.CpuBytes;
                        totals.MeshCount++;
                        break;
                    case "AudioClip":
                        totals.Audio += record.TotalBytes;
                        totals.AudioCount++;
                        break;
                    case "TerrainData":
                        totals.Terrain += record.TotalBytes;
                        break;
                    case "AnimationClip":
                        totals.Animation += record.TotalBytes;
                        break;
                    default:
                        totals.Other += record.TotalBytes;
                        break;
                }
            }

            totals.SceneOverhead = (long)(sceneObjectCount * QuestPreflightConfig.PerSceneObjectKB * 1024);

            // Video decoder working set: only players that prepare at startup.
            totals.VideoWorkingSet = (long)(videoPlayerPlayOnAwakeCount *
                                            QuestPreflightConfig.PreparedVideoPlayerMB * Mb);

            // Runtime (non-asset) allocations.
            long runtimeVideoRt = (long)videoPlayersWithoutTargetTexture *
                                  QuestPreflightConfig.DefaultVideoRenderTextureWidth *
                                  QuestPreflightConfig.DefaultVideoRenderTextureHeight * 4;
            totals.RuntimeAllocations = eyeBufferBytes + realtimeProbeBytes + runtimeVideoRt;

            totals.AssetResident = totals.Textures + totals.Cubemaps + totals.RenderTextures + totals.Meshes +
                                   totals.Audio + totals.Terrain + totals.Lighting + totals.Animation +
                                   totals.Other + totals.SceneOverhead;

            totals.Resident = totals.AssetResident + totals.RuntimeAllocations + totals.VideoWorkingSet +
                              (long)(QuestPreflightConfig.BaselineRuntimeMB * Mb);

            // ---- PHASE 6: transient load headroom -------------------------
            double uploadBase = (totals.Textures + totals.Cubemaps + totals.Meshes) / Mb;
            double transientLow = uploadBase * QuestPreflightConfig.TransientUploadCopyLowPct;
            double transientHigh = uploadBase * QuestPreflightConfig.TransientUploadCopyHighPct;

            double serializedBase = apkDataUnity3dUncompressed > 0
                ? apkDataUnity3dUncompressed / Mb
                : QuestPreflightConfig.SerializedReadBufferFloorMB /
                  QuestPreflightConfig.SerializedReadBufferHighPct;
            double serializedLow = Math.Max(QuestPreflightConfig.SerializedReadBufferFloorMB,
                serializedBase * QuestPreflightConfig.SerializedReadBufferLowPct);
            double serializedHigh = Math.Max(QuestPreflightConfig.SerializedReadBufferFloorMB,
                serializedBase * QuestPreflightConfig.SerializedReadBufferHighPct);

            double residentMb = totals.Resident / Mb;
            double allocatorLow = residentMb * QuestPreflightConfig.AllocatorReserveLowPct;
            double allocatorHigh = residentMb * QuestPreflightConfig.AllocatorReserveHighPct;

            totals.HeadroomLow = transientLow + serializedLow + allocatorLow;
            totals.HeadroomHigh = transientHigh + serializedHigh + allocatorHigh;

            totals.PeakLow = residentMb + totals.HeadroomLow;
            totals.PeakHigh = residentMb + totals.HeadroomHigh;

            // Trace-calibrated band: the formula model under-predicted this
            // build's real transient overhead by 2-4x, so the verdict is taken
            // from the measured multiple instead. See CalibratedTransientSource.
            totals.CalibratedLow = residentMb * QuestPreflightConfig.CalibratedTransientFactorLow;
            totals.CalibratedHigh = residentMb * QuestPreflightConfig.CalibratedTransientFactorHigh;
            totals.CalPeakLow = residentMb + totals.CalibratedLow;
            totals.CalPeakHigh = residentMb + totals.CalibratedHigh;

            if (totals.CalPeakHigh < QuestPreflightConfig.SafeBudgetMB)
                totals.Verdict = "PASS";
            else if (totals.CalPeakHigh < QuestPreflightConfig.CriticalThresholdMB)
                totals.Verdict = "WARNING";
            else if (totals.CalPeakLow < QuestPreflightConfig.CriticalThresholdMB)
                totals.Verdict = "CRITICAL";
            else
                totals.Verdict = "PREDICTED OOM";

            return totals;
        }

        // ================= text report =======================================

        static string BuildTextReport(TimeSpan elapsed)
        {
            Totals totals = ComputeTotals();
            var sb = new StringBuilder();

            void Rule() => sb.AppendLine("============================================================================");
            void Thin() => sb.AppendLine("----------------------------------------------------------------------------");
            void Row(string label, double mb) =>
                sb.AppendLine($"{label,-46}{mb,14:N0} MB");
            void RowCount(string label, double mb, string suffix) =>
                sb.AppendLine($"{label,-46}{mb,14:N0} MB   {suffix}");

            Rule();
            sb.AppendLine("BCaT QUEST RELEASE PREFLIGHT");
            Rule();
            sb.AppendLine($"Generated              {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Unity                  {Application.unityVersion}");
            sb.AppendLine($"Main scene             {QuestPreflightConfig.MainScenePath}");
            sb.AppendLine($"Active build target    {EditorUserBuildSettings.activeBuildTarget}");
            sb.AppendLine($"Analysis time          {elapsed.TotalSeconds:0.0}s");
            sb.AppendLine();

            sb.AppendLine($"APK size               {(apkPath != null ? $"{apkBytes / Mb:N0} MB   ({apkPath})" : "not built")}");
            sb.AppendLine();

            Rule();
            sb.AppendLine("ESTIMATED RUNTIME MEMORY");
            Rule();
            Row("Baseline Unity/IL2CPP/OpenXR/Meta runtime", QuestPreflightConfig.BaselineRuntimeMB);
            RowCount("Textures", totals.Textures / Mb, $"{totals.TextureCount} textures");
            RowCount("Cubemaps", totals.Cubemaps / Mb, $"{totals.CubemapCount} cubemaps");
            RowCount("Meshes", totals.Meshes / Mb, $"{totals.MeshCount} meshes");
            RowCount("Audio", totals.Audio / Mb, $"{totals.AudioCount} clips");
            RowCount("Terrain (heightmap/splat/detail/trees)", totals.Terrain / Mb, "layer textures counted above");
            Row("Lighting / probes (baked assets)", totals.Lighting / Mb);
            RowCount("Render textures (authored assets)", totals.RenderTextures / Mb, $"{totals.RenderTextureCount} assets");
            Row("Animation / fonts / other assets", (totals.Animation + totals.Other) / Mb);
            Row("Scene / serialized object overhead", totals.SceneOverhead / Mb);
            Thin();
            Row("Resident scene assets subtotal", totals.AssetResident / Mb);
            sb.AppendLine();
            sb.AppendLine("Runtime/system allocations (not scene assets):");
            Row("  XR eye buffers + swapchain", eyeBufferBytes / Mb);
            Row("  Realtime reflection probes", realtimeProbeBytes / Mb);
            Row("  Runtime video render textures", (totals.RuntimeAllocations - eyeBufferBytes - realtimeProbeBytes) / Mb);
            Row("  Video / decoder working set", totals.VideoWorkingSet / Mb);
            Thin();
            Row("PREDICTED RESIDENT TOTAL", totals.Resident / Mb);
            sb.AppendLine();
            sb.AppendLine("Temporary load headroom — two models, both reported:");
            sb.AppendLine($"{"  (a) formula model (upload+deserialise+alloc)",-46}{totals.HeadroomLow,14:N0} - {totals.HeadroomHigh:N0} MB");
            sb.AppendLine($"{"  (b) trace-calibrated (x resident)",-46}{totals.CalibratedLow,14:N0} - {totals.CalibratedHigh:N0} MB");
            Thin();
            sb.AppendLine($"{"PREDICTED PEAK (a) formula model",-46}{totals.PeakLow,14:N0} - {totals.PeakHigh:N0} MB");
            sb.AppendLine($"{"PREDICTED PEAK (b) trace-calibrated",-46}{totals.CalPeakLow,14:N0} - {totals.CalPeakHigh:N0} MB");
            sb.AppendLine();
            sb.AppendLine("The formula model is reported for transparency but is NOT the verdict: it");
            sb.AppendLine("under-predicted this build's real transient overhead by 2-4x. Verdict uses (b).");
            sb.AppendLine();
            Row("QUEST SAFE BUDGET", QuestPreflightConfig.SafeBudgetMB);
            Row("WARNING THRESHOLD", QuestPreflightConfig.WarningThresholdMB);
            Row("CRITICAL THRESHOLD (lowest observed kill)", QuestPreflightConfig.CriticalThresholdMB);
            sb.AppendLine();
            sb.AppendLine($"RESULT: {totals.Verdict}");
            sb.AppendLine(VerdictExplanation(totals));
            sb.AppendLine();

            // ---- categories ----
            Rule();
            sb.AppendLine($"MEMORY BY BCaT CONTENT CATEGORY (top {QuestPreflightConfig.TopOwnerCount})");
            Rule();
            foreach (var group in Records.Values
                         .Where(r => r.TotalBytes > 0)
                         .GroupBy(r => r.Category)
                         .Select(g => new { Category = g.Key, Bytes = g.Sum(r => r.TotalBytes), Count = g.Count() })
                         .OrderByDescending(g => g.Bytes)
                         .Take(QuestPreflightConfig.TopOwnerCount))
                sb.AppendLine($"{group.Category,-46}{group.Bytes / Mb,14:N0} MB   {group.Count} assets");
            sb.AppendLine();

            // ---- by type ----
            Rule();
            sb.AppendLine("MEMORY BY ASSET TYPE");
            Rule();
            foreach (var group in Records.Values
                         .Where(r => r.TotalBytes > 0)
                         .GroupBy(r => r.Type)
                         .Select(g => new { Type = g.Key, Bytes = g.Sum(r => r.TotalBytes), Count = g.Count() })
                         .OrderByDescending(g => g.Bytes))
                sb.AppendLine($"{group.Type,-46}{group.Bytes / Mb,14:N0} MB   {group.Count} assets");
            sb.AppendLine();

            // ---- top assets ----
            Rule();
            sb.AppendLine($"TOP {QuestPreflightConfig.TopAssetCount} INDIVIDUAL ASSETS BY PREDICTED RESIDENT MEMORY");
            Rule();
            sb.AppendLine($"{"MB",8}  {"Type",-14} {"Category",-28} Asset");
            foreach (AssetRecord record in Records.Values
                         .OrderByDescending(r => r.TotalBytes)
                         .Take(QuestPreflightConfig.TopAssetCount))
            {
                sb.AppendLine($"{record.TotalBytes / Mb,8:N1}  {record.Type,-14} {Truncate(record.Category, 28),-28} {record.Path}");
                sb.AppendLine($"{"",8}  {"",-14} {"",-28} {record.Detail}");
            }
            sb.AppendLine();

            // ---- prefabs ----
            Rule();
            sb.AppendLine($"TOP {QuestPreflightConfig.TopPrefabCount} PREFABS BY ATTRIBUTABLE DEPENDENCIES");
            Rule();
            sb.AppendLine("Inclusive: an asset shared by several prefabs is charged to each of them, so these");
            sb.AppendLine("figures show what each prefab pulls in, not a partition of the total.");
            sb.AppendLine();
            foreach (KeyValuePair<string, long> entry in PrefabAttribution
                         .OrderByDescending(e => e.Value)
                         .Take(QuestPreflightConfig.TopPrefabCount))
            {
                PrefabInstanceCounts.TryGetValue(entry.Key, out int instances);
                sb.AppendLine($"{entry.Value / Mb,8:N1} MB  x{instances,-4} {entry.Key}");
                if (PrefabTopAssets.TryGetValue(entry.Key, out List<string> top))
                    foreach (string line in top)
                        sb.AppendLine($"            -> {line}");
            }
            sb.AppendLine();

            // ---- scene roots ----
            Rule();
            sb.AppendLine($"TOP {QuestPreflightConfig.TopOwnerCount} SCENE ROOT OBJECTS BY ATTRIBUTABLE DEPENDENCIES");
            Rule();
            foreach (KeyValuePair<string, long> entry in RootAttribution
                         .OrderByDescending(e => e.Value)
                         .Take(QuestPreflightConfig.TopOwnerCount))
                sb.AppendLine($"{entry.Value / Mb,10:N1} MB  {entry.Key}");
            sb.AppendLine();

            // ---- scene facts ----
            Rule();
            sb.AppendLine("SCENE COMPOSITION");
            Rule();
            sb.AppendLine($"Root objects                  {sceneRootCount}");
            sb.AppendLine($"Total GameObjects             {sceneObjectCount:N0}");
            sb.AppendLine($"Prefab instances              {scenePrefabInstanceCount:N0} ({PrefabInstanceCounts.Count} distinct prefabs)");
            sb.AppendLine($"VideoPlayers                  {videoPlayerCount} (playOnAwake {videoPlayerPlayOnAwakeCount}, no target texture {videoPlayersWithoutTargetTexture})");
            sb.AppendLine($"AudioSources with playOnAwake {audioSourcePlayOnAwakeCount}");
            sb.AppendLine($"Reflection probes              baked {bakedProbeCount}, realtime {realtimeProbeCount}");
            sb.AppendLine();
            if (TerrainBreakdown.Count > 0)
            {
                sb.AppendLine("Terrain detail:");
                foreach (string line in TerrainBreakdown)
                    sb.AppendLine("  " + line);
                sb.AppendLine();
            }
            sb.AppendLine("XR render targets:");
            sb.AppendLine("  " + eyeBufferDetail);
            sb.AppendLine();

            // ---- configuration ----
            Rule();
            sb.AppendLine("QUEST RELEASE CONFIGURATION (PHASE 1)");
            Rule();
            sb.AppendLine($"{"Setting",-34}{"Value",-46}Confidence");
            Thin();
            foreach (string line in ConfigLines)
                sb.AppendLine(line);
            sb.AppendLine();

            // ---- APK ----
            Rule();
            sb.AppendLine("BUILD ARTIFACT ACCOUNTING (PHASE 9) — STORAGE, NOT RAM");
            Rule();
            if (apkPath == null)
            {
                sb.AppendLine(apkNote);
            }
            else
            {
                sb.AppendLine($"{"APK file",-46}{apkBytes / Mb,14:N0} MB");
                sb.AppendLine($"{"  data.unity3d (compressed in APK)",-46}{apkDataUnity3dCompressed / Mb,14:N0} MB");
                sb.AppendLine($"{"  data.unity3d (uncompressed)",-46}{apkDataUnity3dUncompressed / Mb,14:N0} MB");
                sb.AppendLine($"{"  .resS / .resource / sharedassets",-46}{apkResourceFileBytes / Mb,14:N0} MB");
                sb.AppendLine($"{"  StreamingAssets",-46}{apkStreamingAssetsBytes / Mb,14:N0} MB");
                sb.AppendLine($"{"  Addressables (aa/)",-46}{apkAddressablesBytes / Mb,14:N0} MB");
                sb.AppendLine($"{"  native libraries (lib/)",-46}{apkNativeLibBytes / Mb,14:N0} MB");
                if (!string.IsNullOrEmpty(apkNote))
                    sb.AppendLine(apkNote);
                sb.AppendLine();
                sb.AppendLine("APK bytes are storage. They become RAM only where the model above says so:");
                sb.AppendLine("StreamingAssets video is streamed by the decoder, Addressables content for the");
                sb.AppendLine("Black Kitchen is not resident at main-house startup, and native libraries are");
                sb.AppendLine("inside the measured baseline.");
            }
            sb.AppendLine();

            // ---- confidence ----
            Rule();
            sb.AppendLine("CONFIDENCE");
            Rule();
            sb.AppendLine($"Texture memory        HIGH      block-compression aware, per mip, from the imported Android format");
            sb.AppendLine($"Cubemap memory        HIGH      same model x6 faces");
            sb.AppendLine($"Mesh memory           HIGH      real per-stream vertex strides and index counts");
            sb.AppendLine($"                                (gap: bindposes/boneWeights of skinned meshes are not counted)");
            sb.AppendLine($"Terrain memory        MEDIUM    heightmap/splat/detail/trees calculated; runtime patch buffers not modelled");
            sb.AppendLine($"Audio memory          MEDIUM    exact for PCM/DecompressOnLoad; Vorbis in-memory size is a bitrate model");
            sb.AppendLine($"XR eye buffers        MEDIUM    formula is exact, per-eye resolution is an assumption");
            sb.AppendLine($"Realtime probes       MEDIUM    cube + mip chain calculated from probe resolution");
            sb.AppendLine($"Video working set     LOW       platform decoder allocation is not observable from the editor");
            sb.AppendLine($"Scene overhead        LOW       per-object constant x object count");
            sb.AppendLine($"Load transient RAM    LOW       modelled band, not measurable through public APIs");
            sb.AppendLine($"Shader variants       EXCLUDED  {shaderCount} shaders; runtime variant memory is not statically knowable");
            sb.AppendLine($"Materials             EXCLUDED  {materialCount} materials; their textures are counted once above");
            sb.AppendLine();

            // ---- validation ----
            Rule();
            sb.AppendLine("VALIDATION");
            Rule();
            sb.AppendLine($"De-duplication          every asset counted once by guid:localFileID — {Records.Count:N0} unique assets");
            sb.AppendLine($"Shared textures         materials contribute 0 bytes; textures are charged once regardless of reuse");
            sb.AppendLine($"Prefab duplication      prefab dependencies measured once per prefab ASSET, not per instance");
            double textureRatio = profilerTextureCrossCheck > 0
                ? analyticTextureTotal / (double)profilerTextureCrossCheck : 0;
            double meshRatio = profilerMeshCrossCheck > 0
                ? analyticMeshTotal / (double)profilerMeshCrossCheck : 0;
            sb.AppendLine($"Texture cross-check     analytic {analyticTextureTotal / Mb:N0} MB vs " +
                          $"Profiler {profilerTextureCrossCheck / Mb:N0} MB (ratio {textureRatio:0.00})");
            sb.AppendLine($"Mesh cross-check        analytic {analyticMeshTotal / Mb:N0} MB vs " +
                          $"Profiler {profilerMeshCrossCheck / Mb:N0} MB (ratio {meshRatio:0.00})");
            sb.AppendLine($"Read/Write duplication  CPU copies counted only for readable assets: " +
                          $"textures {totals.TextureCpu / Mb:N0} MB, meshes {totals.MeshCpu / Mb:N0} MB");
            sb.AppendLine($"Streaming audio         charged {QuestPreflightConfig.StreamingClipBufferMB:0.0} MB per clip, not the clip size");
            sb.AppendLine($"Video clips             {Records.Values.Count(r => r.Type == "VideoClip")} VideoClips excluded from resident total (streamed)");
            sb.AppendLine($"Baseline               {QuestPreflightConfig.BaselineRuntimeMB} MB — {QuestPreflightConfig.BaselineSource}");
            sb.AppendLine($"Thresholds             {QuestPreflightConfig.ThresholdSource}");
            sb.AppendLine();
            sb.AppendLine("Cross-check against the captured device trace: the Quest 3 Release run sat at");
            sb.AppendLine("~0.6 GB before the main-scene request and was killed in the 4.7-5.0+ GB band");
            sb.AppendLine("while BH_XR_MainScene was loading. A predicted peak above the safe budget is");
            sb.AppendLine("therefore consistent with the observed failure rather than an independent guess.");
            sb.AppendLine();

            var duplicates = Records.Values
                .Where(r => r.TotalBytes >= 1024 * 1024)
                .GroupBy(r => r.Type + "|" + r.Name + "|" + r.TotalBytes)
                .Where(g => g.Select(r => r.Path).Distinct().Count() > 1)
                .Select(g => new
                {
                    Sample = g.First(),
                    Copies = g.Select(r => r.Path).Distinct().Count(),
                    Wasted = g.First().TotalBytes * (g.Select(r => r.Path).Distinct().Count() - 1),
                })
                .OrderByDescending(d => d.Wasted)
                .ToList();

            if (duplicates.Count > 0)
            {
                sb.AppendLine($"Duplicate content       {duplicates.Count} assets appear at more than one path with " +
                              $"identical name/type/size — {duplicates.Sum(d => d.Wasted) / Mb:N0} MB of the total is " +
                              "redundant copies, each counted separately because each is genuinely resident:");
                foreach (var duplicate in duplicates.Take(10))
                    sb.AppendLine($"  x{duplicate.Copies}  {duplicate.Sample.TotalBytes / Mb,8:N1} MB each  " +
                                  $"{duplicate.Sample.Type} '{duplicate.Sample.Name}'  (e.g. {duplicate.Sample.Path})");
                sb.AppendLine();
            }

            if (SkippedTypes.Count > 0)
            {
                sb.AppendLine("Types with no runtime-size model (excluded from the total):");
                foreach (KeyValuePair<string, int> entry in SkippedTypes.OrderByDescending(e => e.Value).Take(15))
                    sb.AppendLine($"  {entry.Key,-40}{entry.Value}");
                sb.AppendLine();
            }

            if (Notes.Count > 0)
            {
                Rule();
                sb.AppendLine("NOTES");
                Rule();
                foreach (string note in Notes)
                    sb.AppendLine("- " + note);
            }

            return sb.ToString();
        }

        static string VerdictExplanation(Totals totals)
        {
            switch (totals.Verdict)
            {
                case "PASS":
                    return $"        Even the high end of the calibrated peak ({totals.CalPeakHigh:N0} MB) stays under " +
                           $"the safe budget ({QuestPreflightConfig.SafeBudgetMB} MB).";
                case "WARNING":
                    return $"        The calibrated peak ({totals.CalPeakLow:N0}-{totals.CalPeakHigh:N0} MB) exceeds the " +
                           $"safe budget ({QuestPreflightConfig.SafeBudgetMB} MB) but stays below the lowest observed " +
                           $"kill ({QuestPreflightConfig.CriticalThresholdMB} MB).";
                case "CRITICAL":
                    return $"        The calibrated peak ({totals.CalPeakLow:N0}-{totals.CalPeakHigh:N0} MB) straddles the " +
                           $"lowest observed kill ({QuestPreflightConfig.ObservedKillLowMB} MB). OOM is likely, and this " +
                           "is consistent with the device trace for this build.";
                default:
                    return $"        Even the low end of the calibrated peak ({totals.CalPeakLow:N0} MB) exceeds the lowest " +
                           $"observed kill ({QuestPreflightConfig.ObservedKillLowMB} MB); lowmemorykiller is expected " +
                           "during the main-scene load.";
            }
        }

        static string Truncate(string value, int length) =>
            string.IsNullOrEmpty(value) ? string.Empty :
            value.Length <= length ? value : value.Substring(0, length - 1) + "~";

        // ================= JSON report =======================================

        static string BuildJsonReport()
        {
            Totals totals = ComputeTotals();
            var sb = new StringBuilder();

            sb.AppendLine("{");
            sb.AppendLine($"  \"generated\": \"{DateTime.Now:yyyy-MM-ddTHH:mm:ss}\",");
            sb.AppendLine($"  \"unityVersion\": {Json(Application.unityVersion)},");
            sb.AppendLine($"  \"mainScene\": {Json(QuestPreflightConfig.MainScenePath)},");
            sb.AppendLine($"  \"activeBuildTarget\": {Json(EditorUserBuildSettings.activeBuildTarget.ToString())},");
            sb.AppendLine("  \"assumptions\": {");
            sb.AppendLine($"    \"baselineRuntimeMB\": {QuestPreflightConfig.BaselineRuntimeMB},");
            sb.AppendLine($"    \"baselineSource\": {Json(QuestPreflightConfig.BaselineSource)},");
            sb.AppendLine($"    \"baselineConfidence\": {Json(QuestPreflightConfig.BaselineConfidence)},");
            sb.AppendLine($"    \"safeBudgetMB\": {QuestPreflightConfig.SafeBudgetMB},");
            sb.AppendLine($"    \"warningThresholdMB\": {QuestPreflightConfig.WarningThresholdMB},");
            sb.AppendLine($"    \"criticalThresholdMB\": {QuestPreflightConfig.CriticalThresholdMB},");
            sb.AppendLine($"    \"thresholdSource\": {Json(QuestPreflightConfig.ThresholdSource)},");
            sb.AppendLine($"    \"eyeWidth\": {QuestPreflightConfig.QuestEyeWidth},");
            sb.AppendLine($"    \"eyeHeight\": {QuestPreflightConfig.QuestEyeHeight},");
            sb.AppendLine($"    \"eyeBufferSource\": {Json(QuestPreflightConfig.EyeBufferSource)},");
            sb.AppendLine($"    \"streamingClipBufferMB\": {QuestPreflightConfig.StreamingClipBufferMB},");
            sb.AppendLine($"    \"preparedVideoPlayerMB\": {QuestPreflightConfig.PreparedVideoPlayerMB},");
            sb.AppendLine($"    \"perSceneObjectKB\": {QuestPreflightConfig.PerSceneObjectKB}");
            sb.AppendLine("  },");
            sb.AppendLine("  \"prediction\": {");
            sb.AppendLine($"    \"assetResidentMB\": {totals.AssetResident / Mb:0.0},");
            sb.AppendLine($"    \"runtimeAllocationsMB\": {totals.RuntimeAllocations / Mb:0.0},");
            sb.AppendLine($"    \"videoWorkingSetMB\": {totals.VideoWorkingSet / Mb:0.0},");
            sb.AppendLine($"    \"baselineMB\": {QuestPreflightConfig.BaselineRuntimeMB},");
            sb.AppendLine($"    \"residentTotalMB\": {totals.Resident / Mb:0.0},");
            sb.AppendLine($"    \"formulaHeadroomLowMB\": {totals.HeadroomLow:0.0},");
            sb.AppendLine($"    \"formulaHeadroomHighMB\": {totals.HeadroomHigh:0.0},");
            sb.AppendLine($"    \"formulaPeakLowMB\": {totals.PeakLow:0.0},");
            sb.AppendLine($"    \"formulaPeakHighMB\": {totals.PeakHigh:0.0},");
            sb.AppendLine($"    \"calibratedHeadroomLowMB\": {totals.CalibratedLow:0.0},");
            sb.AppendLine($"    \"calibratedHeadroomHighMB\": {totals.CalibratedHigh:0.0},");
            sb.AppendLine($"    \"peakLowMB\": {totals.CalPeakLow:0.0},");
            sb.AppendLine($"    \"peakHighMB\": {totals.CalPeakHigh:0.0},");
            sb.AppendLine($"    \"calibratedTransientSource\": {Json(QuestPreflightConfig.CalibratedTransientSource)},");
            sb.AppendLine($"    \"verdict\": {Json(totals.Verdict)}");
            sb.AppendLine("  },");
            sb.AppendLine("  \"byType\": {");
            AppendMap(sb, Records.Values.Where(r => r.TotalBytes > 0)
                .GroupBy(r => r.Type)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalBytes) / Mb));
            sb.AppendLine("  },");
            sb.AppendLine("  \"byCategory\": {");
            AppendMap(sb, Records.Values.Where(r => r.TotalBytes > 0)
                .GroupBy(r => r.Category)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalBytes) / Mb));
            sb.AppendLine("  },");
            sb.AppendLine("  \"scene\": {");
            sb.AppendLine($"    \"rootObjects\": {sceneRootCount},");
            sb.AppendLine($"    \"gameObjects\": {sceneObjectCount},");
            sb.AppendLine($"    \"prefabInstances\": {scenePrefabInstanceCount},");
            sb.AppendLine($"    \"distinctPrefabs\": {PrefabInstanceCounts.Count},");
            sb.AppendLine($"    \"videoPlayers\": {videoPlayerCount},");
            sb.AppendLine($"    \"videoPlayersPlayOnAwake\": {videoPlayerPlayOnAwakeCount},");
            sb.AppendLine($"    \"audioSourcesPlayOnAwake\": {audioSourcePlayOnAwakeCount},");
            sb.AppendLine($"    \"reflectionProbesBaked\": {bakedProbeCount},");
            sb.AppendLine($"    \"reflectionProbesRealtime\": {realtimeProbeCount},");
            sb.AppendLine($"    \"eyeBufferMB\": {eyeBufferBytes / Mb:0.0},");
            sb.AppendLine($"    \"eyeBufferDetail\": {Json(eyeBufferDetail)}");
            sb.AppendLine("  },");
            sb.AppendLine("  \"apk\": {");
            sb.AppendLine($"    \"path\": {Json(apkPath ?? "")},");
            sb.AppendLine($"    \"fileMB\": {apkBytes / Mb:0.0},");
            sb.AppendLine($"    \"dataUnity3dCompressedMB\": {apkDataUnity3dCompressed / Mb:0.0},");
            sb.AppendLine($"    \"dataUnity3dUncompressedMB\": {apkDataUnity3dUncompressed / Mb:0.0},");
            sb.AppendLine($"    \"resourceFilesMB\": {apkResourceFileBytes / Mb:0.0},");
            sb.AppendLine($"    \"streamingAssetsMB\": {apkStreamingAssetsBytes / Mb:0.0},");
            sb.AppendLine($"    \"addressablesMB\": {apkAddressablesBytes / Mb:0.0},");
            sb.AppendLine($"    \"nativeLibsMB\": {apkNativeLibBytes / Mb:0.0},");
            sb.AppendLine($"    \"note\": {Json(apkNote)}");
            sb.AppendLine("  },");
            sb.AppendLine("  \"prefabs\": [");
            var prefabList = PrefabAttribution.OrderByDescending(e => e.Value).ToList();
            for (int i = 0; i < prefabList.Count; i++)
            {
                PrefabInstanceCounts.TryGetValue(prefabList[i].Key, out int instances);
                sb.Append($"    {{ \"path\": {Json(prefabList[i].Key)}, \"instances\": {instances}, " +
                          $"\"attributableMB\": {prefabList[i].Value / Mb:0.0} }}");
                sb.AppendLine(i < prefabList.Count - 1 ? "," : string.Empty);
            }
            sb.AppendLine("  ],");
            sb.AppendLine("  \"assets\": [");
            var assets = Records.Values.OrderByDescending(r => r.TotalBytes).ToList();
            for (int i = 0; i < assets.Count; i++)
            {
                AssetRecord r = assets[i];
                sb.Append($"    {{ \"path\": {Json(r.Path)}, \"guid\": {Json(r.Guid)}, \"name\": {Json(r.Name)}, " +
                          $"\"type\": {Json(r.Type)}, \"category\": {Json(r.Category)}, " +
                          $"\"cpuMB\": {r.CpuBytes / Mb:0.000}, \"gpuMB\": {r.GpuBytes / Mb:0.000}, " +
                          $"\"totalMB\": {r.TotalBytes / Mb:0.000}, \"detail\": {Json(r.Detail)}, " +
                          $"\"confidence\": {Json(r.Confidence)} }}");
                sb.AppendLine(i < assets.Count - 1 ? "," : string.Empty);
            }
            sb.AppendLine("  ],");
            sb.AppendLine("  \"notes\": [");
            for (int i = 0; i < Notes.Count; i++)
                sb.AppendLine($"    {Json(Notes[i])}{(i < Notes.Count - 1 ? "," : string.Empty)}");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        static void AppendMap(StringBuilder sb, Dictionary<string, double> map)
        {
            var ordered = map.OrderByDescending(e => e.Value).ToList();
            for (int i = 0; i < ordered.Count; i++)
                sb.AppendLine($"    {Json(ordered[i].Key)}: {ordered[i].Value:0.0}" +
                              (i < ordered.Count - 1 ? "," : string.Empty));
        }

        static string Json(string value)
        {
            if (value == null)
                return "\"\"";
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
