// TEMPORARY read-only validation. Created and removed by the stale-lightmap cleanup.
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TempLightingCheck
{
    public static void Run()
    {
        void W(string s) { Debug.Log("###LC### " + s); }

        // Guard: never trigger a bake.
        W("giWorkflowMode(before open) = " + Lightmapping.giWorkflowMode);

        var scene = EditorSceneManager.OpenScene("Assets/BH_XR_MainScene.unity", OpenSceneMode.Single);
        W("scene opened: " + scene.path + " isDirty=" + scene.isDirty);
        W("giWorkflowMode(after open)  = " + Lightmapping.giWorkflowMode);

        var lda = Lightmapping.lightingDataAsset;
        W("lightingDataAsset = " + (lda == null ? "<null>" : AssetDatabase.GetAssetPath(lda)));
        W("lightmaps.Length  = " + LightmapSettings.lightmaps.Length);
        W("lightmapsMode     = " + LightmapSettings.lightmapsMode);
        foreach (var lm in LightmapSettings.lightmaps)
            W("  lightmap entry: color=" + (lm.lightmapColor ? AssetDatabase.GetAssetPath(lm.lightmapColor) : "-"));

        var all = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int mapped = all.Count(r => r.lightmapIndex >= 0 && r.lightmapIndex < 65534);
        W("MeshRenderers total          = " + all.Length);
        W("MeshRenderers w/ lightmap    = " + mapped);

        W("ambientMode=" + RenderSettings.ambientMode + " ambientIntensity=" + RenderSettings.ambientIntensity
          + " reflectionIntensity=" + RenderSettings.reflectionIntensity
          + " skybox=" + (RenderSettings.skybox ? RenderSettings.skybox.name : "<null>"));

        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            W("light: " + l.name + " type=" + l.type + " bakeType=" + l.lightmapBakeType + " intensity=" + l.intensity + " enabled=" + l.enabled);

        W("scene.isDirty(end) = " + scene.isDirty + "   (NOT saving)");
        W("DONE");
        EditorApplication.Exit(0);
    }
}
