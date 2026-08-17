using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Attaches <see cref="BlackKitchenQuestOrientation"/> to the Black Kitchen
    /// experience root. One component, one wired reference; it creates no objects
    /// and touches nothing else. The card itself is built at runtime and only on
    /// Quest.
    ///
    ///   Unity -executeMethod BCaT.EditorTools.BlackKitchenQuestOrientationSetup.Apply
    /// </summary>
    public static class BlackKitchenQuestOrientationSetup
    {
        const string ScenePath =
            "Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity";

        const string HostName = "BlackKitchenExperience_ROOT";

        [MenuItem("BCaT/Black Kitchen/Set Up Quest Entry Orientation")]
        public static void Apply()
        {
            var log = new StringBuilder();
            log.AppendLine("[BlackKitchenQuestOrientationSetup] START");

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Fail(log, $"could not open '{ScenePath}'.");
                return;
            }

            GameObject host = scene.GetRootGameObjects().FirstOrDefault(r => r.name == HostName);
            if (host == null)
            {
                Fail(log, $"no root object named '{HostName}'.");
                return;
            }

            var manager = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<BlackKitchenInteractionManager>(true))
                .FirstOrDefault();
            if (manager == null)
            {
                Fail(log, "no BlackKitchenInteractionManager in the scene.");
                return;
            }

            var orientation = host.GetComponent<BlackKitchenQuestOrientation>();
            bool created = orientation == null;
            if (created)
                orientation = host.AddComponent<BlackKitchenQuestOrientation>();

            var so = new SerializedObject(orientation);
            so.FindProperty("interactionManager").objectReferenceValue = manager;
            so.ApplyModifiedPropertiesWithoutUndo();

            log.AppendLine($"  BlackKitchenQuestOrientation {(created ? "ADDED" : "updated")} on '{host.name}'");
            log.AppendLine($"    interactionManager = '{manager.name}'");

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Fail(log, "SaveScene returned false.");
                return;
            }

            Transform origin = scene.GetRootGameObjects()
                .FirstOrDefault(r => r.name == "Platform")?.transform.Find("Quest/XR Origin");
            if (origin != null)
            {
                var move = origin.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement
                    .ContinuousMoveProvider>();
                var turn = origin.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning
                    .ContinuousTurnProvider>();
                var xrOrigin = origin.GetComponent<Unity.XR.CoreUtils.XROrigin>();
                log.AppendLine("  FROZEN RIG (read-back): " +
                               $"localPos={origin.localPosition} scale={origin.localScale} " +
                               $"trackingOrigin={xrOrigin.RequestedTrackingOriginMode} " +
                               $"move={move.moveSpeed} turn={turn.turnSpeed}");
            }

            log.AppendLine("[BlackKitchenQuestOrientationSetup] DONE");
            Debug.Log(log.ToString());

            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        static void Fail(StringBuilder log, string message)
        {
            log.AppendLine($"  FAILED: {message}");
            Debug.LogError(log.ToString());
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }
}
