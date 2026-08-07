using System.Collections.Generic;
using BCaT.Production;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Removes development-only objects from scenes as they are built into the
    /// player. Runs on the build's in-memory copy of the scene, so the authored
    /// scene asset is never modified — the simulator stays available in the
    /// Editor while being absent from every shipped build.
    ///
    /// Replaces PlatformRigActivator.RemoveXRDeviceSimulatorIfPresent, which
    /// searched for a literal GameObject name at runtime and merely deactivated
    /// what it found.
    /// </summary>
    public sealed class BCaTEditorOnlyStripper : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            // report is null for Play Mode scene processing; never strip there.
            if (report == null)
                return;

            var doomed = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (EditorOnlyObject marker in root.GetComponentsInChildren<EditorOnlyObject>(true))
                {
                    if (marker != null)
                        doomed.Add(marker.gameObject);
                }
            }

            if (doomed.Count == 0)
                return;

            foreach (GameObject go in doomed)
            {
                if (go == null)
                    continue;
                Debug.Log($"[BCaTEditorOnlyStripper] Stripping development-only object " +
                          $"'{HierarchyPath(go.transform)}' from scene '{scene.name}'.");
                Object.DestroyImmediate(go);
            }

            Debug.Log($"[BCaTEditorOnlyStripper] Scene '{scene.name}': stripped {doomed.Count} " +
                      "development-only object(s).");
        }

        static string HierarchyPath(Transform t)
        {
            string path = t.name;
            Transform parent = t.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
