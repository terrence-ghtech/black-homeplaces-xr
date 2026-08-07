using System.IO;
using System.Linq;
using BCaT.Production;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Captures the two production rigs as project-owned prefab assets.
    ///
    /// Why: both scenes currently instance rigs that live under
    /// Assets/Samples/XR Interaction Toolkit/3.3.1/... and
    /// Assets/StarterAssets/..., which are regenerated when those samples are
    /// reimported. A project-owned prefab gives one place to change rig
    /// configuration for all scenes and makes the ScenePlayerRig marker part of
    /// the asset instead of a per-instance added component.
    ///
    /// Deliberately does NOT re-point the existing scene instances. Those rigs
    /// are the most behavior-critical objects in the project and are validated
    /// working on both platforms; swapping a working, validated rig instance for
    /// a freshly minted one is a change whose blast radius is much larger than
    /// its benefit. The assets are created so new scenes (and the scene
    /// template) instantiate project-owned rigs from the start, and re-pointing
    /// the two existing scenes is a separate, separately-validated step.
    ///
    ///   Unity -executeMethod BCaT.EditorTools.BCaTRigPrefabs.CreateProjectOwnedRigs
    /// </summary>
    public static class BCaTRigPrefabs
    {
        const string MainScenePath = "Assets/BH_XR_MainScene.unity";
        const string PrefabFolder = "Assets/BCaT/ProductionCore/Platform/Prefabs";

        const string DesktopPrefabPath = PrefabFolder + "/BCaT_DesktopRig.prefab";
        const string QuestPrefabPath = PrefabFolder + "/BCaT_QuestRig.prefab";

        [MenuItem("BCaT/Architecture/Create Project-Owned Rig Prefabs")]
        public static void CreateProjectOwnedRigs()
        {
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh();

            Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

            int created = 0;
            created += Capture(scene, ScenePlayerRig.RigKind.Desktop, DesktopPrefabPath) ? 1 : 0;
            created += Capture(scene, ScenePlayerRig.RigKind.XR, QuestPrefabPath) ? 1 : 0;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // The scene must be left exactly as it was: this tool captures, it
            // does not migrate.
            if (EditorSceneManager.GetSceneManagerSetup().Any(s => s.isSubScene == false))
                Debug.Log($"[BCaTRigPrefabs] {created} prefab(s) created. Scene left unmodified " +
                          "(existing instances are intentionally not re-pointed).");

            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        static bool Capture(Scene scene, ScenePlayerRig.RigKind kind, string path)
        {
            if (File.Exists(path))
            {
                Debug.Log($"[BCaTRigPrefabs] {path} already exists; left unchanged.");
                return false;
            }

            ScenePlayerRig rig = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                rig = root.GetComponentsInChildren<ScenePlayerRig>(true)
                    .FirstOrDefault(r => r != null && r.Kind == kind);
                if (rig != null)
                    break;
            }

            if (rig == null)
            {
                Debug.LogWarning($"[BCaTRigPrefabs] No ScenePlayerRig of kind {kind} in " +
                                 $"'{scene.name}'; cannot capture {path}.");
                return false;
            }

            // SaveAsPrefabAsset (not ...AndConnect): the scene instance keeps its
            // current identity, so nothing in the validated scenes changes.
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(rig.gameObject, path, out bool success);
            if (!success || saved == null)
            {
                Debug.LogError($"[BCaTRigPrefabs] Failed to save {path} from '{rig.name}'.");
                return false;
            }

            Debug.Log($"[BCaTRigPrefabs] captured {kind} rig '{rig.name}' → {path}.");
            return true;
        }
    }
}
