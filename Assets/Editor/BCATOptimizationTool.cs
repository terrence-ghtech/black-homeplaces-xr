// BCAT public-WebGL optimization tooling — added 2026-07-23.
// Batch-mode entry points for asset inventory and post-optimization validation.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BCATOptimizationTool
{
    private static string GetArg(string name, string fallback)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return fallback;
    }

    private static string ReportDir()
    {
        string dir = _reportDirOverride ?? GetArg("-bcatReportDir", "webgl-public-optimization-reports");
        Directory.CreateDirectory(dir);
        return dir;
    }

    // Detailed inventory of heavy assets: GLB/model sub-meshes, sub-textures,
    // materials, plus standalone texture importer settings.
    public static void InventoryBigAssets()
    {
        try
        {
            InventoryBigAssetsCore();
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(ReportDir(), "inventory_exception.txt"), e.ToString());
            EditorApplication.Exit(1);
        }
    }

    private static void InventoryBigAssetsCore()
    {
        string reportDir = ReportDir();
        {
            var targets = File.ReadAllLines(Path.Combine(reportDir, "inventory_targets.txt"))
                .Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).ToList();

            var glbOut = new StringBuilder("asset\tsubAsset\tkind\tdetail\n");
            var texOut = new StringBuilder("asset\twidth\theight\tformat\tmips\treadable\tmaxSize\tcompression\tcrunch\tsRGB\ttype\tsourceSizeBytes\n");
            var sumOut = new StringBuilder("asset\ttriangles\tvertices\tmeshes\tmaterials\ttextures\tmaxTexDim\ttexMemEstMB\n");

            foreach (string path in targets)
            {
                var main = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (main == null) { glbOut.Append(path).Append("\t-\tMISSING\t-\n"); continue; }

                if (path.EndsWith(".glb") || path.EndsWith(".gltf") || path.EndsWith(".fbx") || path.EndsWith(".obj"))
                {
                    long tris = 0, verts = 0; int meshes = 0, mats = 0, texs = 0, maxDim = 0;
                    double texMem = 0;
                    foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        if (obj is Mesh m)
                        {
                            meshes++;
                            verts += m.vertexCount;
                            long t = 0;
                            try { for (int s = 0; s < m.subMeshCount; s++) t += (long)m.GetIndexCount(s) / 3; }
                            catch { }
                            tris += t;
                            glbOut.Append(path).Append('\t').Append(obj.name).Append("\tMesh\t")
                                  .Append($"verts={m.vertexCount} tris={t} submeshes={m.subMeshCount} readable={m.isReadable} bounds={m.bounds.size}\n");
                        }
                        else if (obj is Material mat)
                        {
                            mats++;
                            glbOut.Append(path).Append('\t').Append(obj.name).Append("\tMaterial\t")
                                  .Append($"shader={(mat.shader ? mat.shader.name : "null")}\n");
                        }
                        else if (obj is Texture2D tex)
                        {
                            texs++;
                            maxDim = Mathf.Max(maxDim, Mathf.Max(tex.width, tex.height));
                            double bpp = GraphicsFormatBpp(tex.format);
                            double mem = tex.width * (double)tex.height * bpp * (tex.mipmapCount > 1 ? 1.333 : 1.0) / 8.0;
                            texMem += mem;
                            glbOut.Append(path).Append('\t').Append(obj.name).Append("\tTexture2D\t")
                                  .Append($"{tex.width}x{tex.height} fmt={tex.format} mips={tex.mipmapCount} estMB={(mem/1e6):F1}\n");
                        }
                    }
                    sumOut.Append(path).Append('\t').Append(tris).Append('\t').Append(verts).Append('\t')
                          .Append(meshes).Append('\t').Append(mats).Append('\t').Append(texs).Append('\t')
                          .Append(maxDim).Append('\t').Append((texMem / 1e6).ToString("F1")).Append('\n');
                }
                else
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (tex != null && imp != null)
                    {
                        var plat = imp.GetPlatformTextureSettings("WebGL");
                        int maxSize = plat.overridden ? plat.maxTextureSize : imp.maxTextureSize;
                        long src = 0; try { src = new FileInfo(path).Length; } catch { }
                        texOut.Append(path).Append('\t').Append(tex.width).Append('\t').Append(tex.height).Append('\t')
                              .Append(tex.format).Append('\t').Append(tex.mipmapCount).Append('\t').Append(imp.isReadable)
                              .Append('\t').Append(maxSize).Append('\t').Append(imp.textureCompression)
                              .Append('\t').Append(imp.crunchedCompression).Append('\t').Append(imp.sRGBTexture)
                              .Append('\t').Append(imp.textureType).Append('\t').Append(src).Append('\n');
                    }
                }
            }
            File.WriteAllText(Path.Combine(reportDir, "inventory_glb_details.tsv"), glbOut.ToString());
            File.WriteAllText(Path.Combine(reportDir, "inventory_textures.tsv"), texOut.ToString());
            File.WriteAllText(Path.Combine(reportDir, "inventory_summary.tsv"), sumOut.ToString());

            // Sweep: all packed standalone textures with post-import dims > 2048
            var big = new StringBuilder("asset\twidth\theight\tmaxSizeSetting\ttype\n");
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(p) as TextureImporter;
                if (imp == null) continue;
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                if (t != null && Mathf.Max(t.width, t.height) > 2048)
                    big.Append(p).Append('\t').Append(t.width).Append('\t').Append(t.height)
                       .Append('\t').Append(imp.maxTextureSize).Append('\t').Append(imp.textureType).Append('\n');
            }
            File.WriteAllText(Path.Combine(reportDir, "textures_over_2048.tsv"), big.ToString());

            Debug.Log("[BCATOpt] Inventory complete");
        }
    }

    private static double GraphicsFormatBpp(TextureFormat f)
    {
        switch (f)
        {
            case TextureFormat.DXT1: case TextureFormat.BC4: return 4;
            case TextureFormat.DXT5: case TextureFormat.BC5: case TextureFormat.BC7: return 8;
            case TextureFormat.RGBA32: case TextureFormat.BGRA32: return 32;
            case TextureFormat.RGB24: return 24;
            case TextureFormat.ASTC_4x4: return 8;
            case TextureFormat.ASTC_6x6: return 3.56;
            case TextureFormat.ASTC_8x8: return 2;
            case TextureFormat.ETC2_RGBA8: return 8;
            case TextureFormat.ETC_RGB4: case TextureFormat.ETC2_RGB: return 4;
            case TextureFormat.R8: case TextureFormat.Alpha8: return 8;
            case TextureFormat.RGBA4444: case TextureFormat.RGB565: case TextureFormat.R16: return 16;
            default: return 8;
        }
    }

    // WebGL-only platform overrides for third-party environment pack textures.
    // Caps textures above 1024 at 1024 on WebGL only (Quest/desktop unaffected).
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
    };

    public static void SetWebGLTextureOverrides()
    {
        string reportDir = ReportDir();
        var log = new StringBuilder("asset\twidth\theight\tpreviousMax\tnewWebGLMax\n");
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
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                if (tex == null || Mathf.Max(tex.width, tex.height) <= 1024) continue;

                var plat = imp.GetPlatformTextureSettings("WebGL");
                plat.overridden = true;
                plat.maxTextureSize = 1024;
                plat.format = TextureImporterFormat.Automatic;
                imp.SetPlatformTextureSettings(plat);
                imp.SaveAndReimport();
                log.Append(p).Append('\t').Append(tex.width).Append('\t').Append(tex.height)
                   .Append('\t').Append(imp.maxTextureSize).Append("\t1024\n");
                changed++;
            }
            AssetDatabase.StopAssetEditing();
            File.WriteAllText(Path.Combine(reportDir, "webgl_texture_overrides.tsv"), log.ToString());
            Debug.Log($"[BCATOpt] WebGL overrides applied to {changed} textures");
        }
        catch (Exception e)
        {
            AssetDatabase.StopAssetEditing();
            File.WriteAllText(Path.Combine(reportDir, "overrides_exception.txt"), e.ToString());
            EditorApplication.Exit(1);
            return;
        }
    }

    // One batch entry point: apply overrides, then re-inventory, then deep-validate.
    public static void TextureOptimizationPass()
    {
        SetWebGLTextureOverrides();
        InventoryAfterwards();
        ValidateScenesDeep(); // exits the editor
    }

    private static void InventoryAfterwards()
    {
        // Re-run the big-asset inventory into an "after" subdirectory.
        string reportDir = ReportDir();
        string after = Path.Combine(reportDir, "after_textures");
        Directory.CreateDirectory(after);
        File.Copy(Path.Combine(reportDir, "inventory_targets.txt"),
                  Path.Combine(after, "inventory_targets.txt"), true);
        // Temporarily re-point the report dir for the inventory pass.
        _reportDirOverride = after;
        try { InventoryBigAssetsCore(); }
        finally { _reportDirOverride = null; }
    }

    private static string _reportDirOverride;

    public static void ListNullMaterialSlots()
    {
        string reportDir = ReportDir();
        var sb = new StringBuilder();
        try
        {
            foreach (var sceneEntry in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager
                    .OpenScene(sceneEntry.path, UnityEditor.SceneManagement.OpenSceneMode.Single);
                var stack = new Stack<GameObject>(scene.GetRootGameObjects());
                while (stack.Count > 0)
                {
                    var go = stack.Pop();
                    foreach (var r in go.GetComponents<Renderer>())
                    {
                        var mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++)
                            if (mats[i] == null)
                                sb.Append(sceneEntry.path).Append('\t')
                                  .Append(GetPath(go.transform)).Append('\t')
                                  .Append(r.GetType().Name).Append(" slot ").Append(i).Append('\n');
                    }
                    foreach (Transform c in go.transform) stack.Push(c.gameObject);
                }
            }
            File.WriteAllText(Path.Combine(reportDir, "null_material_slots.txt"), sb.ToString());
            Debug.Log("[BCATOpt] Null slot listing complete");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            sb.Append("EXCEPTION: ").Append(e);
            File.WriteAllText(Path.Combine(reportDir, "null_material_slots.txt"), sb.ToString());
            EditorApplication.Exit(1);
        }
    }

    private static string GetPath(Transform t)
    {
        var parts = new List<string>();
        while (t != null) { parts.Add(t.name); t = t.parent; }
        parts.Reverse();
        return string.Join("/", parts);
    }

    // Post-optimization scene validation: open scenes, look for null materials,
    // missing meshes, and missing scripts.
    public static void ValidateScenesDeep()
    {
        string reportDir = ReportDir();
        var sb = new StringBuilder();
        try
        {
            foreach (var sceneEntry in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager
                    .OpenScene(sceneEntry.path, UnityEditor.SceneManagement.OpenSceneMode.Single);
                int missingScripts = 0, nullMats = 0, missingMesh = 0, renderers = 0;
                var stack = new Stack<GameObject>(scene.GetRootGameObjects());
                while (stack.Count > 0)
                {
                    var go = stack.Pop();
                    missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                    foreach (var r in go.GetComponents<Renderer>())
                    {
                        renderers++;
                        foreach (var m in r.sharedMaterials) if (m == null) nullMats++;
                    }
                    foreach (var mf in go.GetComponents<MeshFilter>())
                        if (mf.sharedMesh == null) missingMesh++;
                    foreach (Transform c in go.transform) stack.Push(c.gameObject);
                }
                sb.Append(sceneEntry.path)
                  .Append($" — renderers={renderers} nullMaterialSlots={nullMats} missingMeshes={missingMesh} missingScripts={missingScripts}\n");
            }
            File.WriteAllText(Path.Combine(reportDir, "scene_deep_validation.txt"), sb.ToString());
            Debug.Log("[BCATOpt] Deep validation complete");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            sb.Append("EXCEPTION: ").Append(e);
            File.WriteAllText(Path.Combine(reportDir, "scene_deep_validation.txt"), sb.ToString());
            EditorApplication.Exit(1);
        }
    }
}
