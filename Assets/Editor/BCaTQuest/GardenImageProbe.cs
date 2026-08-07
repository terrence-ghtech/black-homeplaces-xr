using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Diagnostic for the "My Grandma's Garden" popup image: reports the source
    /// PNG dimensions, the IMPORTED texture dimensions (after maxTextureSize and
    /// any non-power-of-two rescale), the importer's npotScale setting, the
    /// GardenImage RectTransform, and the size the sprite will actually render
    /// at given preserveAspect. This separates "the rect aspect is wrong" from
    /// "the texture itself got reshaped at import".
    ///
    /// -executeMethod BCaT.EditorTools.GardenImageProbe.Run
    /// </summary>
    public static class GardenImageProbe
    {
        const string TexturePath = "Assets/BCaT_assets/Meshell_Sturgis/My Grandma's Garden.png";
        const string ScenePath = "Assets/BH_XR_MainScene.unity";

        public static void Run()
        {
            var r = new StringBuilder();
            r.AppendLine("=== Garden image probe ===");

            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);

            if (importer == null || texture == null)
            {
                r.AppendLine("FAIL: texture or importer not found at " + TexturePath);
                Finish(r);
                return;
            }

            importer.GetSourceTextureWidthAndHeight(out int srcW, out int srcH);
            r.AppendLine($"source PNG          : {srcW} x {srcH}   aspect={(float)srcW / srcH:F4}");
            r.AppendLine($"IMPORTED texture    : {texture.width} x {texture.height}   aspect={(float)texture.width / texture.height:F4}");
            r.AppendLine($"npotScale           : {importer.npotScale}");
            r.AppendLine($"textureType         : {importer.textureType}");
            r.AppendLine($"maxTextureSize      : {importer.maxTextureSize}");
            r.AppendLine($"isReadable          : {importer.isReadable}");

            var androidSettings = importer.GetPlatformTextureSettings("Android");
            r.AppendLine($"Android override    : overridden={androidSettings.overridden} " +
                         $"maxTextureSize={androidSettings.maxTextureSize} " +
                         $"format={androidSettings.format} compression={androidSettings.textureCompression}");

            bool aspectPreservedByImport =
                Mathf.Abs((float)texture.width / texture.height - (float)srcW / srcH) < 0.01f;
            r.AppendLine(aspectPreservedByImport
                ? "=> import PRESERVES source aspect (no texture reshaping)"
                : "=> import RESHAPES the texture! This alone distorts the photo.");

            // Scene side
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            RectTransform imageRect = null;
            Image image = null;
            foreach (var candidate in Resources.FindObjectsOfTypeAll<Image>())
            {
                if (candidate != null && candidate.gameObject.name == "GardenImage" &&
                    candidate.gameObject.scene.IsValid())
                {
                    image = candidate;
                    imageRect = candidate.GetComponent<RectTransform>();
                    break;
                }
            }

            if (imageRect == null)
            {
                r.AppendLine("FAIL: GardenImage not found in scene");
                Finish(r);
                return;
            }

            Vector2 rect = imageRect.rect.size;
            r.AppendLine();
            r.AppendLine($"GardenImage rect    : {rect.x} x {rect.y}   aspect={rect.x / rect.y:F4}");
            r.AppendLine($"preserveAspect      : {image.preserveAspect}");
            r.AppendLine($"AspectRatioFitter   : {(imageRect.GetComponent<AspectRatioFitter>() != null ? "present" : "none")}");
            r.AppendLine($"anchorMin/Max       : {imageRect.anchorMin} / {imageRect.anchorMax}");
            r.AppendLine($"localScale          : {imageRect.localScale}");

            // Layout controllers anywhere in the parent chain would fight manual sizes.
            r.AppendLine();
            r.AppendLine("parent chain layout controllers:");
            for (Transform t = imageRect.parent; t != null; t = t.parent)
            {
                var group = t.GetComponent<LayoutGroup>();
                var fitter = t.GetComponent<ContentSizeFitter>();
                r.AppendLine($"  {t.name}: layoutGroup={(group != null ? group.GetType().Name : "none")}, " +
                             $"contentSizeFitter={(fitter != null ? "PRESENT" : "none")}, " +
                             $"scale={t.localScale}");
            }
            r.AppendLine($"  GardenImage itself: layoutElement=" +
                         $"{(imageRect.GetComponent<LayoutElement>() != null ? "PRESENT" : "none")}");

            // What preserveAspect actually renders, using the imported texture aspect.
            float texAspect = (float)texture.width / texture.height;
            float rectAspect = rect.x / rect.y;
            Vector2 drawn = rectAspect > texAspect
                ? new Vector2(rect.y * texAspect, rect.y)   // rect wider than image: fit height
                : new Vector2(rect.x, rect.x / texAspect);  // rect taller than image: fit width
            r.AppendLine();
            r.AppendLine($"With preserveAspect ON, the photo renders at {drawn.x:F1} x {drawn.y:F1} " +
                         $"inside the {rect.x} x {rect.y} rect");
            r.AppendLine($"  => empty space: {rect.x - drawn.x:F1} px horizontal, {rect.y - drawn.y:F1} px vertical");
            r.AppendLine($"Rect width needed for a full 4:3 photo at height {rect.y}: {rect.y * 4f / 3f:F2}");

            Finish(r);
        }

        static void Finish(StringBuilder r)
        {
            string text = r.ToString();
            Debug.Log(text);
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(Application.dataPath, "..", "Builds", "GardenImageProbe.txt"), text);
            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }
    }
}
