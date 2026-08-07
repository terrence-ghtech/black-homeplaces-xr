using System.Text;
using UnityEditor;
using UnityEngine;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Verifies what the texture importer actually produces for the
    /// "My Grandma's Garden" PNG under different Non-Power-of-2 settings, then
    /// RESTORES the original importer state so the project is left untouched.
    ///
    /// This exists because the popup image is distorted at the TEXTURE level:
    /// npotScale=ToNearest reshapes the 4032x3024 (4:3) source into 4096x2048
    /// (2:1), which no RectTransform value can undo.
    ///
    /// -executeMethod BCaT.EditorTools.GardenNpotVerify.Run
    /// </summary>
    public static class GardenNpotVerify
    {
        const string TexturePath = "Assets/BCaT_assets/Meshell_Sturgis/My Grandma's Garden.png";

        public static void Run()
        {
            var r = new StringBuilder();
            r.AppendLine("=== Garden npotScale verification (non-destructive) ===");

            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            if (importer == null)
            {
                r.AppendLine("FAIL: importer not found");
                Finish(r);
                return;
            }

            // Remember original state so we can put it back exactly.
            TextureImporterNPOTScale originalNpot = importer.npotScale;
            TextureImporterPlatformSettings originalAndroid =
                importer.GetPlatformTextureSettings("Android");
            r.AppendLine($"ORIGINAL: npotScale={originalNpot}, defaultMax={importer.maxTextureSize}, " +
                         $"androidOverridden={originalAndroid.overridden}, androidMax={originalAndroid.maxTextureSize}");
            r.AppendLine($"ORIGINAL imported size: {Measure()}");
            r.AppendLine();

            try
            {
                // Candidate A: npotScale = None, default max (4096) unchanged.
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
                r.AppendLine($"A) npotScale=None, max {importer.maxTextureSize}: {Measure()}");

                // Candidate B: npotScale = None + Android override to 2048
                // (keeps headset texture memory sane for a full-view photo).
                var android = importer.GetPlatformTextureSettings("Android");
                android.overridden = true;
                android.maxTextureSize = 2048;
                importer.SetPlatformTextureSettings(android);
                importer.SaveAndReimport();
                r.AppendLine($"B) npotScale=None + Android max 2048: {Measure()}");
            }
            finally
            {
                // Restore exactly.
                importer.npotScale = originalNpot;
                importer.SetPlatformTextureSettings(originalAndroid);
                importer.SaveAndReimport();
                r.AppendLine();
                r.AppendLine($"RESTORED: npotScale={importer.npotScale}, imported size: {Measure()}");
            }

            r.AppendLine();
            r.AppendLine("Rect width for a full 4:3 photo at height 1111 = 1481.33");
            Finish(r);
        }

        static string Measure()
        {
            AssetDatabase.Refresh();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture == null)
                return "(texture not loadable)";
            float aspect = (float)texture.width / texture.height;
            string verdict = Mathf.Abs(aspect - 4f / 3f) < 0.01f ? "4:3 CORRECT" : "WRONG ASPECT";
            return $"{texture.width} x {texture.height}  aspect={aspect:F4}  [{verdict}]";
        }

        static void Finish(StringBuilder r)
        {
            string text = r.ToString();
            Debug.Log(text);
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(Application.dataPath, "..", "Builds", "GardenNpotVerify.txt"), text);
            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }
    }
}
