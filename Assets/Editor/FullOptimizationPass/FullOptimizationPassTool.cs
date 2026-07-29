// Full cross-platform optimization pass — batch-mode orchestrator.
// Driven by the audit CSVs in PerformanceAudit/HighROIInvestigation.
// Every production change is logged to PerformanceAudit/FullOptimizationPass.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BCAT.OptimizationPass
{
    public static class FullOptimizationPassTool
    {
        private const string ReportDir = "PerformanceAudit/FullOptimizationPass";
        private const string AuditDir = "PerformanceAudit/HighROIInvestigation";
        private const string MainScenePath = "Assets/BH_XR_MainScene.unity";
        private const string BkScenePath = "Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity";
        private const string MeshOutDir = "Assets/BCaT/OptimizedMeshes";

        private static StringBuilder _errors = new StringBuilder();

        // ================= batch entry points =================

        public static void RunBeforeMetrics()
        {
            try
            {
                Directory.CreateDirectory(ReportDir);
                CollectMetrics("before");
                Debug.Log("[FullOpt] before metrics complete");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Fail("before_metrics", e); }
        }

        public static void RunAfterMetrics()
        {
            try
            {
                Directory.CreateDirectory(ReportDir);
                CollectMetrics("after");
                Debug.Log("[FullOpt] after metrics complete");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Fail("after_metrics", e); }
        }

        public static void RunFullPass()
        {
            Directory.CreateDirectory(ReportDir);
            EnsureFolder(MeshOutDir);
            var simplifiedCache = new Dictionary<int, Mesh[]>(); // srcMesh instanceID -> [lod1, lod2, collision]

            // ---------- main scene ----------
            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            Phase("shadows", () => PhaseShadows(scene));
            Phase("material_dedup", () => PhaseMaterialDedup(scene));
            Phase("terrain", () => PhaseTerrain(scene));
            Phase("lods_top_assets", () => PhaseLodsTopAssets(scene, simplifiedCache));
            Phase("lods_fbx_existing", () => PhaseLodsFromFbxLevels(scene));
            Phase("colliders", () => PhaseColliders(scene, simplifiedCache, isBlackKitchen: false));
            Phase("renderer_reduction", () => PhaseRendererReduction(scene));
            Phase("canvases_main", () => PhaseCanvases(scene));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[FullOpt] main scene saved");

            // ---------- Black Kitchen scene ----------
            var bk = EditorSceneManager.OpenScene(BkScenePath, OpenSceneMode.Single);
            Phase("bk_lods", () => PhaseBlackKitchenLods(bk, simplifiedCache));
            Phase("bk_colliders", () => PhaseColliders(bk, simplifiedCache, isBlackKitchen: true));
            Phase("canvases_bk", () => PhaseCanvases(bk));
            EditorSceneManager.MarkSceneDirty(bk);
            EditorSceneManager.SaveScene(bk);
            Debug.Log("[FullOpt] Black Kitchen scene saved");

            // ---------- asset-level ----------
            Phase("instancing", PhaseInstancing);
            Phase("textures", PhaseTextures);
            Phase("build_refs", PhaseBuildRefs);

            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(ReportDir, "phase_errors.txt"), _errors.ToString());
            Debug.Log("[FullOpt] full pass complete");
            EditorApplication.Exit(0);
        }

        private static void Phase(string name, Action action)
        {
            try
            {
                Debug.Log($"[FullOpt] phase start: {name}");
                action();
                Debug.Log($"[FullOpt] phase done: {name}");
            }
            catch (Exception e)
            {
                _errors.Append("PHASE ").Append(name).Append(" FAILED\n").Append(e).Append("\n\n");
                Debug.LogError($"[FullOpt] phase {name} failed: {e.Message}");
            }
        }

        private static void Fail(string tag, Exception e)
        {
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(Path.Combine(ReportDir, tag + "_exception.txt"), e.ToString());
            EditorApplication.Exit(1);
        }

        // ================= shadows =================

        private static void PhaseShadows(Scene scene)
        {
            var rows = ReadCsv(Path.Combine(AuditDir, "shadow_optimization_candidates.csv"));
            var map = BuildPathMap(scene);
            var log = new List<string> { "scene,object_path,renderer,prev_cast,new_cast,prev_receive,new_receive,mesh_triangles,status" };
            int applied = 0, missing = 0, already = 0;

            foreach (var r in rows)
            {
                if (r["scene"] != MainScenePath) continue;
                if (r["recommended_cast_shadows_after"] != "No") continue;
                if (r["visual_risk"] != "Low") continue;
                bool receiveOff = r["recommended_receive_shadows_after"] == "False";
                string path = r["object_path"];

                if (!map.TryGetValue(path, out var gos)) { missing++; log.Add(Csv(scene.path, path, "-", "-", "-", "-", "-", r["triangle_count"], "NOT_FOUND")); continue; }
                foreach (var go in gos)
                {
                    foreach (var rd in go.GetComponents<Renderer>())
                    {
                        string prevCast = rd.shadowCastingMode.ToString();
                        string prevRecv = rd.receiveShadows.ToString();
                        bool change = rd.shadowCastingMode != ShadowCastingMode.Off || (receiveOff && rd.receiveShadows);
                        if (!change) { already++; continue; }
                        rd.shadowCastingMode = ShadowCastingMode.Off;
                        if (receiveOff) rd.receiveShadows = false;
                        applied++;
                        log.Add(Csv(scene.path, path, rd.GetType().Name, prevCast, "Off", prevRecv, rd.receiveShadows.ToString(), r["triangle_count"], "APPLIED"));
                    }
                }
            }
            log.Add(Csv("SUMMARY", $"applied={applied}", $"alreadyOff={already}", $"missing={missing}", "", "", "", "", ""));
            File.WriteAllLines(Path.Combine(ReportDir, "Shadow_Changes.csv"), log);
        }

        // ================= material dedup =================

        private static void PhaseMaterialDedup(Scene scene)
        {
            var rows = ReadCsv(Path.Combine(AuditDir, "duplicate_material_groups.csv"));
            var log = new List<string> { "group_id,duplicate_material,canonical_material,slots_remapped,instancing_enabled,status" };
            var remap = new Dictionary<Material, Material>();

            foreach (var r in rows)
            {
                if (r["group_type"] != "Exact duplicate" || r["merging_safe"] != "Yes") continue;
                var paths = r["material_asset_paths"].Split('|').Distinct().ToList();
                // only standalone .mat assets can be safely remapped and persisted
                if (paths.Any(p => !p.EndsWith(".mat")) || paths.Count < 2)
                {
                    log.Add(Csv(r["group_id"], string.Join(";", paths.Take(2)), "-", "0", "false", "SKIPPED_embedded_or_single"));
                    continue;
                }
                paths.Sort(StringComparer.Ordinal);
                var canonical = AssetDatabase.LoadAssetAtPath<Material>(paths[0]);
                if (canonical == null) { log.Add(Csv(r["group_id"], paths[0], "-", "0", "false", "SKIPPED_missing_canonical")); continue; }
                if (canonical.shader != null && canonical.shader.name.Contains("Universal Render Pipeline") && !canonical.enableInstancing)
                {
                    canonical.enableInstancing = true;
                    EditorUtility.SetDirty(canonical);
                }
                for (int i = 1; i < paths.Count; i++)
                {
                    var dup = AssetDatabase.LoadAssetAtPath<Material>(paths[i]);
                    if (dup == null || dup == canonical) continue;
                    if (dup.shader != canonical.shader) { log.Add(Csv(r["group_id"], paths[i], paths[0], "0", "false", "SKIPPED_shader_mismatch")); continue; }
                    remap[dup] = canonical;
                    log.Add(Csv(r["group_id"], paths[i], paths[0], "pending", canonical.enableInstancing.ToString(), "REMAP"));
                }
            }

            int slots = 0;
            foreach (var rd in AllComponents<Renderer>(scene))
            {
                var mats = rd.sharedMaterials;
                bool dirty = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && remap.TryGetValue(mats[i], out var canon))
                    {
                        mats[i] = canon;
                        dirty = true;
                        slots++;
                    }
                }
                if (dirty) rd.sharedMaterials = mats;
            }
            log.Add(Csv("SUMMARY", $"duplicates_remapped={remap.Count}", $"renderer_slots_updated={slots}", "", "", ""));
            File.WriteAllLines(Path.Combine(ReportDir, "Material_Changes.csv"), log);
        }

        // ================= GPU instancing =================

        private static void PhaseInstancing()
        {
            var rows = ReadCsv(Path.Combine(AuditDir, "gpu_instancing_candidates.csv"));
            var log = new List<string> { "material,shader,instances_in_scene,prev_enabled,new_enabled,status" };
            var done = new HashSet<string>();
            foreach (var r in rows)
            {
                string matPath = r["material"];
                if (!done.Add(matPath)) continue;
                if (!matPath.EndsWith(".mat"))
                {
                    log.Add(Csv(matPath, r["shader"], r["instance_count"], "-", "-", "SKIPPED_embedded_in_model_asset"));
                    continue;
                }
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) { log.Add(Csv(matPath, r["shader"], r["instance_count"], "-", "-", "MISSING")); continue; }
                bool prev = mat.enableInstancing;
                if (!prev)
                {
                    mat.enableInstancing = true;
                    EditorUtility.SetDirty(mat);
                }
                log.Add(Csv(matPath, r["shader"], r["instance_count"], prev.ToString(), "True", prev ? "ALREADY_ENABLED" : "ENABLED"));
            }
            File.WriteAllLines(Path.Combine(ReportDir, "Instancing_Changes.csv"), log);
        }

        // ================= textures =================

        private static readonly string[] PackPrefixes =
        {
            "Assets/Furniture Mega Pack/",
            "Assets/DevDen Arch Viz Scotland/",
            "Assets/Idyllic Italian Coast Town/",
            "Assets/TerrainSampleAssets/",
            "Assets/Shaded Spectrum/",
            "Assets/HyTeKGames/",
            "Assets/danthaigames/",
            "Assets/Animated Tropical Vegetation/",
            "Assets/Coconut Palm Tree Pack/",
            "Assets/picture-frame/",
            "Assets/SubstanceAssets/",
            "Assets/My_Custom/txtrs/",
            "Assets/Emilulz_Assets/",
            "Assets/LowPolyLivingRoomPack/",
            "Assets/Food Pack-Demo/",
            "Assets/UnityTechnologies/",
            "Assets/StarterAssets/",
        };

        private static void PhaseTextures()
        {
            var log = new List<string> { "asset,dim,default_max_prev,default_max_new,webgl_override,android_override,readwrite_prev,readwrite_new,status" };
            int changed = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    if (!PackPrefixes.Any(pre => p.StartsWith(pre))) continue;
                    var imp = AssetImporter.GetAtPath(p) as TextureImporter;
                    if (imp == null || imp.textureShape != TextureImporterShape.Texture2D) continue;
                    if (imp.textureType != TextureImporterType.Default && imp.textureType != TextureImporterType.NormalMap) continue;
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                    if (tex == null) continue;
                    int dim = Mathf.Max(tex.width, tex.height);

                    bool dirty = false;
                    int prevMax = imp.maxTextureSize;
                    bool prevRW = imp.isReadable;
                    string webglNote = "-", androidNote = "-";

                    if (imp.maxTextureSize > 2048) { imp.maxTextureSize = 2048; dirty = true; }

                    if (dim > 1024)
                    {
                        var web = imp.GetPlatformTextureSettings("WebGL");
                        if (!web.overridden || web.maxTextureSize > 1024)
                        {
                            web.overridden = true;
                            web.maxTextureSize = 1024;
                            web.format = TextureImporterFormat.Automatic;
                            imp.SetPlatformTextureSettings(web);
                            dirty = true;
                            webglNote = "1024";
                        }
                        var droid = imp.GetPlatformTextureSettings("Android");
                        if (!droid.overridden || droid.maxTextureSize > 1024)
                        {
                            droid.overridden = true;
                            droid.maxTextureSize = 1024;
                            droid.format = TextureImporterFormat.Automatic;
                            imp.SetPlatformTextureSettings(droid);
                            dirty = true;
                            androidNote = "1024";
                        }
                    }
                    if (imp.isReadable) { imp.isReadable = false; dirty = true; }
                    if (imp.textureCompression == TextureImporterCompression.Uncompressed)
                    {
                        imp.textureCompression = TextureImporterCompression.Compressed;
                        dirty = true;
                    }
                    if (!dirty) continue;
                    imp.SaveAndReimport();
                    changed++;
                    log.Add(Csv(p, dim.ToString(), prevMax.ToString(), imp.maxTextureSize.ToString(), webglNote, androidNote, prevRW.ToString(), imp.isReadable.ToString(), "APPLIED"));
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            log.Add(Csv("SUMMARY", $"textures_changed={changed}", "", "", "", "", "", "", ""));
            File.WriteAllLines(Path.Combine(ReportDir, "Texture_Import_Changes.csv"), log);
        }

        // ================= terrain =================

        private static void PhaseTerrain(Scene scene)
        {
            var log = new List<string>();
            foreach (var terrain in AllComponents<Terrain>(scene))
            {
                log.Add($"terrain={GetPath(terrain.transform)}");
                log.Add($"pixelError: {terrain.heightmapPixelError} -> 4");
                log.Add($"basemapDistance: {terrain.basemapDistance} -> 90");
                log.Add($"detailObjectDistance: {terrain.detailObjectDistance} -> 18");
                log.Add($"treeDistance: {terrain.treeDistance} -> 90");
                log.Add($"treeBillboardDistance: {terrain.treeBillboardDistance} -> 45");
                log.Add($"treeCrossFadeLength: {terrain.treeCrossFadeLength} -> 8");
                log.Add($"shadowCastingMode: {terrain.shadowCastingMode} (unchanged)");
                log.Add($"drawInstanced: {terrain.drawInstanced} (unchanged)");
                terrain.heightmapPixelError = 4;
                terrain.basemapDistance = 90;
                terrain.detailObjectDistance = 18;
                terrain.treeDistance = 90;
                terrain.treeBillboardDistance = 45;
                terrain.treeCrossFadeLength = 8;
                EditorUtility.SetDirty(terrain);
            }
            log.Add("note: WebGL/Quest use stronger values via Quality-level terrain overrides (see QualitySettings.asset).");
            File.WriteAllLines(Path.Combine(ReportDir, "terrain_changes_data.txt"), log);
        }

        // ================= LOD groups: top assets =================

        private class LodSpec
        {
            public string parentPath;
            public float ratio1, ratio2;
            public float t0, t1, cull;
            public string label;
        }

        private static readonly LodSpec[] TopAssetSpecs =
        {
            new LodSpec { label = "metal_table_asset", parentPath = "_SceneContent/ImplementedContributorInstallations/BTMMP_Workstation_Assembly/static/metal_table_asset", ratio1 = 0.5f, ratio2 = 0.2f, t0 = 0.40f, t1 = 0.15f, cull = 0.02f },
            new LodSpec { label = "RW_PhotoAlbum", parentPath = "_SceneContent/ImplementedContributorInstallations/RI/Photo_Asset/Photo-Album/RW_PhotoAlbum_Model", ratio1 = 0.6f, ratio2 = 0.25f, t0 = 0.35f, t1 = 0.12f, cull = 0.02f },
            new LodSpec { label = "LL_PhotoAlbum_Preview", parentPath = "_SceneContent/ImplementedContributorInstallations/LindaLeaks_Exhibit/PhotoAlbum_Preview/Artifact_PhotoAlbum/LL_PhotoAlbum_Model", ratio1 = 0.6f, ratio2 = 0.25f, t0 = 0.35f, t1 = 0.12f, cull = 0.02f },
            new LodSpec { label = "glass_fish", parentPath = "_SceneContent/ImplementedContributorInstallations/RI/Fish_Asset/glass_fish", ratio1 = 0.5f, ratio2 = 0.2f, t0 = 0.40f, t1 = 0.15f, cull = 0.02f },
            new LodSpec { label = "japanese_red_bridge", parentPath = "_SceneContent/Home/Exterior/Pond/japanese_red_bridge", ratio1 = 0.5f, ratio2 = 0.18f, t0 = 0.50f, t1 = 0.20f, cull = 0.005f },
            new LodSpec { label = "drum", parentPath = "_SceneContent/ImplementedContributorInstallations/9Night/drum", ratio1 = 0.55f, ratio2 = 0.22f, t0 = 0.40f, t1 = 0.15f, cull = 0.02f },
        };

        private static List<string> _lodLog;

        private static void PhaseLodsTopAssets(Scene scene, Dictionary<int, Mesh[]> cache)
        {
            _lodLog = new List<string> { "label,parent_path,renderers,src_tris_total,lod1_tris_total,lod2_tris_total,t0,t1,cull,status" };
            var map = BuildPathMap(scene);
            foreach (var spec in TopAssetSpecs)
            {
                if (!map.TryGetValue(spec.parentPath, out var gos))
                {
                    _lodLog.Add(Csv(spec.label, spec.parentPath, "0", "0", "0", "0", "", "", "", "PARENT_NOT_FOUND"));
                    continue;
                }
                foreach (var go in gos)
                    BuildLodGroup(go, spec, cache, _lodLog);
            }
            File.WriteAllLines(Path.Combine(ReportDir, "LOD_Changes.csv"), _lodLog);
        }

        private static void BuildLodGroup(GameObject parent, LodSpec spec, Dictionary<int, Mesh[]> cache, List<string> log)
        {
            if (parent.GetComponent<LODGroup>() != null || parent.GetComponentInParent<LODGroup>() != null)
            {
                log.Add(Csv(spec.label, GetPath(parent.transform), "0", "0", "0", "0", "", "", "", "SKIPPED_existing_lodgroup"));
                return;
            }
            var renderers = parent.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null)
                .ToList();
            if (renderers.Count == 0)
            {
                log.Add(Csv(spec.label, GetPath(parent.transform), "0", "0", "0", "0", "", "", "", "NO_RENDERERS"));
                return;
            }

            long srcTris = 0, lod1Tris = 0, lod2Tris = 0;
            var lod1Renderers = new List<Renderer>();
            var lod2Renderers = new List<Renderer>();

            foreach (var r in renderers)
            {
                var mf = r.GetComponent<MeshFilter>();
                Mesh src = mf.sharedMesh;
                srcTris += TriCount(src);
                Mesh[] lods = GetOrBuildSimplified(src, spec.ratio1, spec.ratio2, cache);
                if (lods[0] == null && lods[1] == null) continue;
                if (lods[0] != null)
                {
                    lod1Renderers.Add(MakeLodCopy(r, lods[0], "_LOD1"));
                    lod1Tris += TriCount(lods[0]);
                }
                if (lods[1] != null)
                {
                    lod2Renderers.Add(MakeLodCopy(r, lods[1], "_LOD2"));
                    lod2Tris += TriCount(lods[1]);
                }
            }
            if (lod1Renderers.Count == 0)
            {
                log.Add(Csv(spec.label, GetPath(parent.transform), renderers.Count.ToString(), srcTris.ToString(), "0", "0", "", "", "", "SKIPPED_no_simplified_meshes"));
                return;
            }

            var group = parent.AddComponent<LODGroup>();
            var lodsArr = new List<LOD> { new LOD(spec.t0, renderers.Cast<Renderer>().ToArray()) };
            if (lod2Renderers.Count > 0)
            {
                lodsArr.Add(new LOD(spec.t1, lod1Renderers.ToArray()));
                lodsArr.Add(new LOD(spec.cull, lod2Renderers.ToArray()));
            }
            else
            {
                lodsArr.Add(new LOD(spec.cull, lod1Renderers.ToArray()));
            }
            group.SetLODs(lodsArr.ToArray());
            group.fadeMode = LODFadeMode.CrossFade;
            group.animateCrossFading = true;
            group.RecalculateBounds();
            EditorUtility.SetDirty(parent);
            log.Add(Csv(spec.label, GetPath(parent.transform), renderers.Count.ToString(), srcTris.ToString(), lod1Tris.ToString(), lod2Tris.ToString(),
                spec.t0.ToString(CultureInfo.InvariantCulture), spec.t1.ToString(CultureInfo.InvariantCulture), spec.cull.ToString(CultureInfo.InvariantCulture), "APPLIED"));
        }

        private static Mesh[] GetOrBuildSimplified(Mesh src, float ratio1, float ratio2, Dictionary<int, Mesh[]> cache)
        {
            int id = src.GetInstanceID();
            if (cache.TryGetValue(id, out var existing)) return existing;

            var result = new Mesh[3];
            Mesh lod1 = QemMeshSimplifier.Simplify(src, ratio1, out int t1);
            Mesh lod2 = QemMeshSimplifier.Simplify(src, ratio2, out int t2);
            Mesh coll = QemMeshSimplifier.Simplify(src, 0.12f, out int tc);
            string baseName = Sanitize($"{src.name}_{Mathf.Abs(id)}");
            if (lod1 != null) { AssetDatabase.CreateAsset(lod1, $"{MeshOutDir}/{baseName}_LOD1.asset"); result[0] = lod1; }
            if (lod2 != null) { AssetDatabase.CreateAsset(lod2, $"{MeshOutDir}/{baseName}_LOD2.asset"); result[1] = lod2; }
            if (coll != null) { AssetDatabase.CreateAsset(coll, $"{MeshOutDir}/{baseName}_COL.asset"); result[2] = coll; }
            cache[id] = result;
            return result;
        }

        private static Renderer MakeLodCopy(MeshRenderer original, Mesh mesh, string suffix)
        {
            var go = new GameObject(original.gameObject.name + suffix);
            go.transform.SetParent(original.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.layer = original.gameObject.layer;
            GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(original.gameObject));
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = original.sharedMaterials;
            mr.shadowCastingMode = original.shadowCastingMode;
            mr.receiveShadows = original.receiveShadows;
            mr.lightProbeUsage = original.lightProbeUsage;
            mr.reflectionProbeUsage = original.reflectionProbeUsage;
            return mr;
        }

        // ================= LOD groups from FBX-provided LOD meshes =================

        private static void PhaseLodsFromFbxLevels(Scene scene)
        {
            var log = new List<string> { "object_path,mesh,lod_levels_wired,status" };
            int built = 0;
            foreach (var mr in AllComponents<MeshRenderer>(scene).ToList())
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Mesh src = mf.sharedMesh;
                if (!src.name.EndsWith("_LOD0")) continue;
                if (mr.GetComponentInParent<LODGroup>() != null) continue;
                string assetPath = AssetDatabase.GetAssetPath(src);
                if (string.IsNullOrEmpty(assetPath)) continue;

                string stem = src.name.Substring(0, src.name.Length - 1); // "..._LOD"
                var levels = new List<Mesh>();
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                {
                    if (obj is Mesh m && m != src && m.name.StartsWith(stem))
                        levels.Add(m);
                }
                if (levels.Count == 0) continue;
                levels.Sort((x, y) => string.CompareOrdinal(x.name, y.name));

                var group = mr.gameObject.AddComponent<LODGroup>();
                var lods = new List<LOD>();
                var lodRenderers = new List<Renderer> { mr };
                float[] cuts = { 0.30f, 0.10f, 0.03f };
                lods.Add(new LOD(cuts[0], lodRenderers.ToArray()));
                for (int i = 0; i < levels.Count && i < 2; i++)
                {
                    var copy = MakeLodCopy(mr, levels[i], "_L" + (i + 1));
                    float cut = i == levels.Count - 1 || i == 1 ? 0.01f : cuts[i + 1];
                    lods.Add(new LOD(cut, new Renderer[] { copy }));
                }
                group.SetLODs(lods.ToArray());
                group.fadeMode = LODFadeMode.CrossFade;
                group.animateCrossFading = true;
                group.RecalculateBounds();
                built++;
                log.Add(Csv(GetPath(mr.transform), src.name, (lods.Count - 1).ToString(), "APPLIED"));
            }
            log.Add(Csv("SUMMARY", $"lodgroups_built={built}", "", ""));
            File.AppendAllLines(Path.Combine(ReportDir, "LOD_Changes.csv"), log);
        }

        // ================= Black Kitchen LODs =================

        private static void PhaseBlackKitchenLods(Scene scene, Dictionary<int, Mesh[]> cache)
        {
            var log = new List<string> { "object_path,src_tris,lod1_tris,lod2_tris,status" };
            foreach (var mr in AllComponents<MeshRenderer>(scene).ToList())
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                long tris = TriCount(mf.sharedMesh);
                if (tris < 50000) continue;
                if (mr.GetComponentInParent<LODGroup>() != null) continue;

                Mesh[] lods = GetOrBuildSimplified(mf.sharedMesh, 0.5f, 0.2f, cache);
                if (lods[0] == null) { log.Add(Csv(GetPath(mr.transform), tris.ToString(), "0", "0", "SIMPLIFY_FAILED")); continue; }

                var group = mr.gameObject.AddComponent<LODGroup>();
                var lodList = new List<LOD> { new LOD(0.35f, new Renderer[] { mr }) };
                var c1 = MakeLodCopy(mr, lods[0], "_LOD1");
                if (lods[1] != null)
                {
                    var c2 = MakeLodCopy(mr, lods[1], "_LOD2");
                    lodList.Add(new LOD(0.12f, new Renderer[] { c1 }));
                    lodList.Add(new LOD(0.01f, new Renderer[] { c2 }));
                }
                else
                {
                    lodList.Add(new LOD(0.01f, new Renderer[] { c1 }));
                }
                group.SetLODs(lodList.ToArray());
                group.fadeMode = LODFadeMode.CrossFade;
                group.animateCrossFading = true;
                group.RecalculateBounds();
                log.Add(Csv(GetPath(mr.transform), tris.ToString(), TriCount(lods[0]).ToString(), lods[1] != null ? TriCount(lods[1]).ToString() : "0", "APPLIED"));
            }
            File.AppendAllLines(Path.Combine(ReportDir, "LOD_Changes.csv"), log);
        }

        // ================= colliders =================

        private static readonly string[] StructuralHints = { "floor", "stair", "ground", "wall", "roof", "terrain", "bridge", "step", "path", "walk", "ramp" };

        private static void PhaseColliders(Scene scene, Dictionary<int, Mesh[]> cache, bool isBlackKitchen)
        {
            var log = new List<string> { "scene,object_path,prev_collider_tris,action,new_collider,new_tris,status" };
            int threshold = isBlackKitchen ? 10000 : 5000;

            foreach (var mc in AllComponents<MeshCollider>(scene).ToList())
            {
                if (mc.isTrigger || mc.sharedMesh == null) continue;
                long tris = TriCount(mc.sharedMesh);
                if (tris <= threshold) continue;
                string path = GetPath(mc.transform);
                string lower = path.ToLowerInvariant();
                bool structural = StructuralHints.Any(h => lower.Contains(h)) || isBlackKitchen;
                bool interactable = HasInteractableContext(mc.gameObject);

                if (structural || interactable)
                {
                    float ratio = structural ? 0.12f : 0.25f;
                    Mesh[] lods = GetOrBuildSimplified(mc.sharedMesh, 0.5f, 0.2f, cache);
                    Mesh collMesh = structural ? lods[2] : (lods[1] ?? lods[2]);
                    if (collMesh == null)
                    {
                        log.Add(Csv(scene.path, path, tris.ToString(), "simplify", "-", "-", "SIMPLIFY_FAILED_kept_original"));
                        continue;
                    }
                    mc.sharedMesh = collMesh;
                    EditorUtility.SetDirty(mc);
                    log.Add(Csv(scene.path, path, tris.ToString(), "simplified_mesh(ratio~" + ratio + ")", "MeshCollider", TriCount(collMesh).ToString(), "APPLIED"));
                }
                else
                {
                    // decorative: replace with a fitted box
                    var mesh = mc.sharedMesh;
                    var go = mc.gameObject;
                    var box = go.AddComponent<BoxCollider>();
                    box.center = mesh.bounds.center;
                    box.size = mesh.bounds.size;
                    box.material = mc.sharedMaterial;
                    UnityEngine.Object.DestroyImmediate(mc);
                    EditorUtility.SetDirty(go);
                    log.Add(Csv(scene.path, path, tris.ToString(), "box_replacement", "BoxCollider", "12", "APPLIED"));
                }
            }
            string file = Path.Combine(ReportDir, "Collider_Changes.csv");
            if (File.Exists(file)) { log.RemoveAt(0); File.AppendAllLines(file, log); }
            else File.WriteAllLines(file, log);
        }

        private static bool HasInteractableContext(GameObject go)
        {
            var t = go.transform;
            int depth = 0;
            while (t != null && depth < 4)
            {
                foreach (var c in t.GetComponents<Component>())
                {
                    if (c == null) continue;
                    string n = c.GetType().Name;
                    if (n.Contains("Interactable") || n.Contains("Interaction") || n.Contains("PopUp") || n.Contains("Grab"))
                        return true;
                }
                t = t.parent;
                depth++;
            }
            return false;
        }

        // ================= renderer reduction =================

        private static void PhaseRendererReduction(Scene scene)
        {
            var log = new List<string> { "action,group,detail,renderers_before,renderers_after,status" };

            // 1) Combine dense flower clusters (single shared material, plain props only).
            var flowerGroups = new Dictionary<Transform, List<MeshRenderer>>();
            foreach (var mr in AllComponents<MeshRenderer>(scene))
            {
                if (!mr.enabled || mr.sharedMaterials.Length != 1 || mr.sharedMaterial == null) continue;
                if (mr.sharedMaterial.name != "M_DEMOAtlas_LowPolyFlowers") continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                if (mr.GetComponentInParent<LODGroup>() != null) continue;
                if (mr.GetComponents<Component>().Length > 3) continue; // transform+filter+renderer only
                var parent = mr.transform.parent;
                if (parent == null) continue;
                if (!flowerGroups.TryGetValue(parent, out var list)) flowerGroups[parent] = list = new List<MeshRenderer>();
                list.Add(mr);
            }
            foreach (var kv in flowerGroups.Where(kv => kv.Value.Count >= 20))
                CombineGroup(kv.Key, kv.Value, "Flowers_" + kv.Key.name, log, shadowsOff: true);

            // 2) Combine the static drone assembly (77 renderers, one material).
            var pathMap = BuildPathMap(scene);
            pathMap.TryGetValue("_SceneContent/ImplementedContributorInstallations/BTMMP_Workstation_Assembly/static/drone", out var droneMatches);
            var droneParent = droneMatches != null && droneMatches.Count > 0 ? droneMatches[0] : null;
            if (droneParent != null)
            {
                var byMat = new Dictionary<Material, List<MeshRenderer>>();
                foreach (var mr in droneParent.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!mr.enabled || mr.sharedMaterials.Length != 1 || mr.sharedMaterial == null) continue;
                    var mf = mr.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    if (mr.GetComponents<Component>().Length > 3) continue;
                    if (!byMat.TryGetValue(mr.sharedMaterial, out var list)) byMat[mr.sharedMaterial] = list = new List<MeshRenderer>();
                    list.Add(mr);
                }
                foreach (var kv in byMat.Where(kv => kv.Value.Count >= 10))
                    CombineGroup(droneParent.transform, kv.Value, "Drone_" + Sanitize(kv.Key.name), log, shadowsOff: false);
            }
            else log.Add(Csv("combine", "drone", "parent not found", "0", "0", "SKIPPED"));

            // 3) Static flags for repeated architecture (enables static batching + occludee culling).
            int flagged = 0;
            foreach (var mr in AllComponents<MeshRenderer>(scene))
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                string meshPath = AssetDatabase.GetAssetPath(mf.sharedMesh);
                if (string.IsNullOrEmpty(meshPath)) continue;
                bool eligible = meshPath.StartsWith("Assets/Idyllic Italian Coast Town/")
                             || GetPath(mr.transform).StartsWith("_SceneContent/ImplementedContributorInstallations/BTMMP_Workstation_Assembly/static");
                if (!eligible) continue;
                var go = mr.gameObject;
                if (HasInteractableContext(go)) continue;
                if (go.GetComponentInParent<Animator>() != null || go.GetComponent<Rigidbody>() != null) continue;
                var flags = GameObjectUtility.GetStaticEditorFlags(go);
                var want = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic;
                if ((flags & want) == want) continue;
                GameObjectUtility.SetStaticEditorFlags(go, flags | want);
                flagged++;
            }
            log.Add(Csv("static_flags", "IdyllicItalianCoastTown+BTMMP_static", "BatchingStatic|OccludeeStatic|OccluderStatic", flagged.ToString(), flagged.ToString(), "APPLIED"));

            File.WriteAllLines(Path.Combine(ReportDir, "Renderer_Reduction_Changes.csv"), log);
        }

        private static void CombineGroup(Transform groupParent, List<MeshRenderer> renderers, string label, List<string> log, bool shadowsOff)
        {
            var material = renderers[0].sharedMaterial;
            var chunks = new List<List<MeshRenderer>>();
            var current = new List<MeshRenderer>();
            int vertBudget = 0;
            foreach (var mr in renderers)
            {
                int v = mr.GetComponent<MeshFilter>().sharedMesh.vertexCount;
                if (vertBudget + v > 60000 && current.Count > 0)
                {
                    chunks.Add(current);
                    current = new List<MeshRenderer>();
                    vertBudget = 0;
                }
                current.Add(mr);
                vertBudget += v;
            }
            if (current.Count > 0) chunks.Add(current);

            int chunkIdx = 0;
            foreach (var chunk in chunks)
            {
                var combines = new CombineInstance[chunk.Count];
                for (int i = 0; i < chunk.Count; i++)
                {
                    combines[i] = new CombineInstance
                    {
                        mesh = chunk[i].GetComponent<MeshFilter>().sharedMesh,
                        transform = groupParent.worldToLocalMatrix * chunk[i].transform.localToWorldMatrix
                    };
                }
                var combined = new Mesh { name = $"{label}_chunk{chunkIdx}" };
                combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
                combined.CombineMeshes(combines, true, true);
                combined.RecalculateBounds();
                string assetPath = $"{MeshOutDir}/Combined_{Sanitize(label)}_{chunkIdx}.asset";
                AssetDatabase.CreateAsset(combined, assetPath);

                var go = new GameObject($"Combined_{label}_{chunkIdx}");
                go.transform.SetParent(groupParent, false);
                go.layer = chunk[0].gameObject.layer;
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = combined;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = shadowsOff ? ShadowCastingMode.Off : chunk[0].shadowCastingMode;
                mr.receiveShadows = chunk[0].receiveShadows;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic);

                foreach (var src in chunk) src.enabled = false;
                chunkIdx++;
            }
            log.Add(Csv("combine", label, $"chunks={chunks.Count};material={material.name};shadowsOff={shadowsOff}", renderers.Count.ToString(), chunks.Count.ToString(), "APPLIED"));
        }

        // ================= canvases =================

        private static void PhaseCanvases(Scene scene)
        {
            var log = new List<string> { "scene,object_path,render_mode,graphics,raycast_targets,has_graphic_raycaster,active,note" };
            foreach (var canvas in AllComponents<Canvas>(scene))
            {
                var graphics = canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
                int raycastTargets = graphics.Count(g => g.raycastTarget);
                log.Add(Csv(scene.path, GetPath(canvas.transform), canvas.renderMode.ToString(),
                    graphics.Length.ToString(), raycastTargets.ToString(),
                    (canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() != null).ToString(),
                    canvas.gameObject.activeInHierarchy.ToString(), ""));
            }
            // Duplicate EventSystem cleanup (keep the first active one).
            var eventSystems = AllComponents<EventSystem>(scene).Where(e => e.gameObject.activeInHierarchy).ToList();
            for (int i = 1; i < eventSystems.Count; i++)
            {
                eventSystems[i].gameObject.SetActive(false);
                log.Add(Csv(scene.path, GetPath(eventSystems[i].transform), "EventSystem", "-", "-", "-", "False", "DISABLED_duplicate_eventsystem"));
            }
            string file = Path.Combine(ReportDir, "canvas_report.csv");
            if (File.Exists(file)) { log.RemoveAt(0); File.AppendAllLines(file, log); }
            else File.WriteAllLines(file, log);
        }

        // ================= build references =================

        private static void PhaseBuildRefs()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Build reference data (generated)");
            sb.AppendLine("\n## Build Settings scenes");
            foreach (var s in EditorBuildSettings.scenes)
                sb.AppendLine($"- enabled={s.enabled} {s.path}");

            sb.AppendLine("\n## Preloaded assets");
            foreach (var a in PlayerSettings.GetPreloadedAssets())
                sb.AppendLine($"- {(a == null ? "(null)" : AssetDatabase.GetAssetPath(a))}");

            sb.AppendLine("\n## Resources folder contents");
            foreach (string guid in AssetDatabase.FindAssets("", new[] { "Assets/Resources" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(p)) continue;
                long size = 0; try { size = new FileInfo(p).Length; } catch { }
                sb.AppendLine($"- {p} ({size / 1024} KB)");
            }

            sb.AppendLine("\n## StreamingAssets");
            if (Directory.Exists("Assets/StreamingAssets"))
                foreach (var f in Directory.GetFiles("Assets/StreamingAssets", "*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".meta")))
                    sb.AppendLine($"- {f} ({new FileInfo(f).Length / 1024} KB)");
            else sb.AppendLine("- (none)");

            sb.AppendLine("\n## Shader variant collections");
            foreach (string guid in AssetDatabase.FindAssets("t:ShaderVariantCollection", new[] { "Assets" }))
                sb.AppendLine($"- {AssetDatabase.GUIDToAssetPath(guid)}");

            sb.AppendLine("\n## Addressables groups");
            try
            {
                var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
                if (settings != null)
                {
                    foreach (var g in settings.groups.Where(g => g != null))
                    {
                        sb.AppendLine($"- group: {g.Name} entries={g.entries.Count}");
                        foreach (var e in g.entries.Take(50))
                            sb.AppendLine($"    - {e.AssetPath}");
                    }
                }
            }
            catch (Exception e) { sb.AppendLine($"(addressables inspection failed: {e.Message})"); }

            File.WriteAllText(Path.Combine(ReportDir, "build_reference_data.md"), sb.ToString());
        }

        // ================= metrics =================

        private static void CollectMetrics(string tag)
        {
            var lines = new List<string> { "scene,metric,value" };
            foreach (string scenePath in new[] { MainScenePath, BkScenePath })
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                var allRenderers = AllComponents<Renderer>(scene).ToList();
                var lodNonZero = new HashSet<Renderer>();
                int lodGroups = 0;
                foreach (var lg in AllComponents<LODGroup>(scene))
                {
                    lodGroups++;
                    var lods = lg.GetLODs();
                    for (int i = 1; i < lods.Length; i++)
                        foreach (var r in lods[i].renderers)
                            if (r != null) lodNonZero.Add(r);
                }

                var effective = allRenderers.Where(r => r.enabled && r.gameObject.activeInHierarchy && !lodNonZero.Contains(r)).ToList();
                long tris = 0, shadowTris = 0;
                int shadowCasters = 0, slots = 0;
                var mats = new HashSet<Material>();
                foreach (var r in effective)
                {
                    Mesh m = null;
                    if (r is MeshRenderer) { var mf = r.GetComponent<MeshFilter>(); m = mf ? mf.sharedMesh : null; }
                    else if (r is SkinnedMeshRenderer smr) m = smr.sharedMesh;
                    long t = m != null ? TriCount(m) : 0;
                    tris += t;
                    if (r.shadowCastingMode != ShadowCastingMode.Off) { shadowCasters++; shadowTris += t; }
                    foreach (var mat in r.sharedMaterials) { slots++; if (mat != null) mats.Add(mat); }
                }

                long colTris = 0; int cols = 0;
                foreach (var mc in AllComponents<MeshCollider>(scene))
                {
                    if (mc.sharedMesh == null) continue;
                    cols++;
                    colTris += TriCount(mc.sharedMesh);
                }

                int canvases = AllComponents<Canvas>(scene).Count();
                var terrain = AllComponents<Terrain>(scene).FirstOrDefault();

                lines.Add(Csv(scenePath, "renderers_total", allRenderers.Count.ToString()));
                lines.Add(Csv(scenePath, "renderers_effective_lod0", effective.Count.ToString()));
                lines.Add(Csv(scenePath, "triangles_effective_lod0", tris.ToString()));
                lines.Add(Csv(scenePath, "shadow_casters", shadowCasters.ToString()));
                lines.Add(Csv(scenePath, "shadow_caster_triangles", shadowTris.ToString()));
                lines.Add(Csv(scenePath, "unique_materials", mats.Count.ToString()));
                lines.Add(Csv(scenePath, "material_slots", slots.ToString()));
                lines.Add(Csv(scenePath, "lod_groups", lodGroups.ToString()));
                lines.Add(Csv(scenePath, "mesh_colliders", cols.ToString()));
                lines.Add(Csv(scenePath, "mesh_collider_triangles", colTris.ToString()));
                lines.Add(Csv(scenePath, "canvases", canvases.ToString()));
                if (terrain != null)
                    lines.Add(Csv(scenePath, "terrain_pixelError_detail_tree_billboard",
                        $"{terrain.heightmapPixelError}|{terrain.detailObjectDistance}|{terrain.treeDistance}|{terrain.treeBillboardDistance}"));
            }
            File.WriteAllLines(Path.Combine(ReportDir, $"metrics_{tag}.csv"), lines);
        }

        // ================= validation =================

        public static void RunValidationAndAfterMetrics()
        {
            try
            {
                Directory.CreateDirectory(ReportDir);
                ValidateOptimizedMeshes();
                ValidateScenes();
                CollectMetrics("after");
                File.WriteAllText(Path.Combine(ReportDir, "validation_done.txt"), DateTime.Now.ToString("s"));
                Debug.Log("[FullOpt] validation + after metrics complete");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Fail("validation", e); }
        }

        private static void ValidateOptimizedMeshes()
        {
            var log = new List<string> { "asset,vertices,triangles,submeshes,has_normals,has_uv0,bounds_ok,nan_vertices,result" };
            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { MeshOutDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Mesh>(p);
                if (m == null) { log.Add(Csv(p, "0", "0", "0", "false", "false", "false", "-", "FAIL_load")); continue; }
                long tris = TriCount(m);
                int nan = 0;
                var verts = m.vertices;
                foreach (var v in verts)
                    if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)) nan++;
                bool boundsOk = m.bounds.size.magnitude > 1e-6f
                    && !float.IsNaN(m.bounds.size.x) && !float.IsNaN(m.bounds.size.y) && !float.IsNaN(m.bounds.size.z);
                bool hasNormals = m.normals != null && m.normals.Length == m.vertexCount;
                bool hasUv = m.uv != null && m.uv.Length == m.vertexCount;
                bool ok = m.vertexCount > 0 && tris > 0 && boundsOk && nan == 0;
                log.Add(Csv(p, m.vertexCount.ToString(), tris.ToString(), m.subMeshCount.ToString(),
                    hasNormals.ToString(), hasUv.ToString(), boundsOk.ToString(), nan.ToString(), ok ? "PASS" : "FAIL"));
            }
            File.WriteAllLines(Path.Combine(ReportDir, "Optimized_Mesh_Validation.csv"), log);
        }

        private static void ValidateScenes()
        {
            var sb = new StringBuilder();
            foreach (string scenePath in new[] { MainScenePath, BkScenePath, "Assets/BCaT/SceneTransitions/Scenes/LoadingScene.unity" })
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                int missingScripts = 0, nullMats = 0, missingMesh = 0, dupLodGroups = 0,
                    lodOrderErrors = 0, lodMatMismatch = 0, lod0NotSource = 0, badColliders = 0;
                var eventSystems = new List<string>();
                var audioListeners = new List<string>();

                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        var go = t.gameObject;
                        missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                        if (go.GetComponents<LODGroup>().Length > 1) dupLodGroups++;
                        foreach (var r in go.GetComponents<Renderer>())
                            foreach (var mat in r.sharedMaterials)
                                if (mat == null) nullMats++;
                        foreach (var mf in go.GetComponents<MeshFilter>())
                            if (mf.sharedMesh == null) missingMesh++;
                        foreach (var mc in go.GetComponents<MeshCollider>())
                            if (mc.sharedMesh != null && TriCount(mc.sharedMesh) == 0) badColliders++;
                        if (go.activeInHierarchy)
                        {
                            if (go.GetComponent<EventSystem>() != null && go.GetComponent<EventSystem>().enabled) eventSystems.Add(GetPath(t));
                            var al = go.GetComponent<AudioListener>();
                            if (al != null && al.enabled) audioListeners.Add(GetPath(t));
                        }
                    }

                    foreach (var lg in root.GetComponentsInChildren<LODGroup>(true))
                    {
                        var lods = lg.GetLODs();
                        long prevTris = long.MaxValue;
                        for (int i = 0; i < lods.Length; i++)
                        {
                            long lodTris = 0;
                            foreach (var r in lods[i].renderers)
                            {
                                if (r == null) continue;
                                var mf = r.GetComponent<MeshFilter>();
                                if (mf == null || mf.sharedMesh == null) continue;
                                lodTris += TriCount(mf.sharedMesh);
                                if (r.sharedMaterials.Length != mf.sharedMesh.subMeshCount) lodMatMismatch++;
                                string meshPath = AssetDatabase.GetAssetPath(mf.sharedMesh);
                                if (i == 0 && meshPath.StartsWith(MeshOutDir)) lod0NotSource++;
                            }
                            if (lodTris > prevTris) lodOrderErrors++;
                            if (lodTris > 0) prevTris = lodTris;
                        }
                    }
                }
                sb.AppendLine($"scene={scenePath}");
                sb.AppendLine($"  missingScripts={missingScripts} nullMaterialSlots={nullMats} missingMeshes={missingMesh}");
                sb.AppendLine($"  duplicateLODGroupComponents={dupLodGroups} lodTriangleOrderErrors={lodOrderErrors} lodMaterialSubmeshMismatches={lodMatMismatch} lod0UsingOptimizedMesh={lod0NotSource}");
                sb.AppendLine($"  emptyMeshColliders={badColliders}");
                sb.AppendLine($"  activeEventSystems={eventSystems.Count} [{string.Join("; ", eventSystems)}]");
                sb.AppendLine($"  activeAudioListeners={audioListeners.Count} [{string.Join("; ", audioListeners)}]");
            }

            sb.AppendLine("buildScenes:");
            foreach (var s in EditorBuildSettings.scenes)
                sb.AppendLine($"  enabled={s.enabled} {s.path} exists={File.Exists(s.path)}");
            try
            {
                var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
                sb.AppendLine($"addressablesSettings={(settings != null ? "OK groups=" + settings.groups.Count : "MISSING")}");
            }
            catch (Exception e) { sb.AppendLine($"addressablesSettings=ERROR {e.Message}"); }

            File.WriteAllText(Path.Combine(ReportDir, "scene_validation.txt"), sb.ToString());
        }

        // ================= helpers =================

        private static IEnumerable<T> AllComponents<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var c in root.GetComponentsInChildren<T>(true))
                    yield return c;
        }

        private static Dictionary<string, List<GameObject>> BuildPathMap(Scene scene)
        {
            var map = new Dictionary<string, List<GameObject>>();
            foreach (var root in scene.GetRootGameObjects())
            {
                var stack = new Stack<Transform>();
                stack.Push(root.transform);
                while (stack.Count > 0)
                {
                    var t = stack.Pop();
                    string p = GetPath(t);
                    if (!map.TryGetValue(p, out var list)) map[p] = list = new List<GameObject>();
                    list.Add(t.gameObject);
                    foreach (Transform c in t) stack.Push(c);
                }
            }
            return map;
        }

        private static string GetPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null) { parts.Add(t.name); t = t.parent; }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static long TriCount(Mesh m)
        {
            long t = 0;
            try { for (int s = 0; s < m.subMeshCount; s++) t += (long)m.GetIndexCount(s) / 3; }
            catch { }
            return t;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string Sanitize(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
                sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            return sb.ToString();
        }

        private static string Csv(params string[] fields)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(',');
                string f = fields[i] ?? "";
                if (f.Contains(',') || f.Contains('"') || f.Contains('\n'))
                    sb.Append('"').Append(f.Replace("\"", "\"\"")).Append('"');
                else sb.Append(f);
            }
            return sb.ToString();
        }

        // Minimal RFC-4180 CSV reader returning dictionaries keyed by header.
        private static List<Dictionary<string, string>> ReadCsv(string path)
        {
            var result = new List<Dictionary<string, string>>();
            string text = File.ReadAllText(path);
            var records = new List<List<string>>();
            var field = new StringBuilder();
            var record = new List<string>();
            bool inQuotes = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else field.Append(c);
                }
                else if (c == '"') inQuotes = true;
                else if (c == ',') { record.Add(field.ToString()); field.Clear(); }
                else if (c == '\r') { }
                else if (c == '\n')
                {
                    record.Add(field.ToString());
                    field.Clear();
                    if (record.Count > 1 || record[0].Length > 0) records.Add(record);
                    record = new List<string>();
                }
                else field.Append(c);
            }
            if (field.Length > 0 || record.Count > 0)
            {
                record.Add(field.ToString());
                if (record.Count > 1 || record[0].Length > 0) records.Add(record);
            }
            if (records.Count < 2) return result;
            var header = records[0];
            for (int r = 1; r < records.Count; r++)
            {
                var dict = new Dictionary<string, string>();
                for (int c = 0; c < header.Count && c < records[r].Count; c++)
                    dict[header[c]] = records[r][c];
                result.Add(dict);
            }
            return result;
        }
    }
}
