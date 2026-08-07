using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Read-only audit: for every exhibit image, compare the SOURCE file aspect
    /// against the IMPORTED texture aspect. Any mismatch means the importer
    /// reshaped the picture (npotScale) and it is displayed distorted no matter
    /// what the UI RectTransform says. Changes nothing.
    ///
    /// -executeMethod BCaT.EditorTools.TextureAspectAudit.Run
    /// </summary>
    public static class TextureAspectAudit
    {
        public static void Run()
        {
            var distorted = new List<string>();
            var ok = new List<string>();
            var r = new StringBuilder();
            r.AppendLine("=== Texture aspect audit (source vs imported) ===");

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/BCaT_assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (importer == null || texture == null)
                    continue;

                importer.GetSourceTextureWidthAndHeight(out int srcW, out int srcH);
                if (srcW <= 0 || srcH <= 0 || texture.height == 0)
                    continue;

                float srcAspect = (float)srcW / srcH;
                float impAspect = (float)texture.width / texture.height;
                float driftPercent = Mathf.Abs(impAspect - srcAspect) / srcAspect * 100f;

                string line = $"{path}\n      source {srcW}x{srcH} ({srcAspect:F3})  ->  " +
                              $"imported {texture.width}x{texture.height} ({impAspect:F3})  " +
                              $"drift {driftPercent:F1}%  npot={importer.npotScale}";

                if (driftPercent > 1f)
                    distorted.Add(line);
                else
                    ok.Add(path);
            }

            r.AppendLine($"scanned: {guids.Length}");
            r.AppendLine($"aspect-correct: {ok.Count}");
            r.AppendLine($"RESHAPED BY IMPORT: {distorted.Count}");
            r.AppendLine();
            distorted.Sort();
            foreach (string line in distorted)
                r.AppendLine("  * " + line);

            string text = r.ToString();
            Debug.Log(text);
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(Application.dataPath, "..", "Builds", "TextureAspectAudit.txt"), text);
            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }
    }
}
