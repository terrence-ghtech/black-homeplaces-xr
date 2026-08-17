using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Attaches <see cref="BlackKitchenExitSign"/> to the existing exit plaque so
    /// the exit is discoverable. Adds one component and wires two references; it
    /// creates no objects, moves nothing, and touches no other system.
    ///
    /// The plaque itself (ExitInterface/ExitPrompt: a world-space canvas with a
    /// dark background Image and the policy-hidden activation TMP_Text) is left
    /// structurally as authored — the sign component adds its signage label as a
    /// runtime child.
    ///
    ///   Unity -executeMethod BCaT.EditorTools.BlackKitchenExitSignatureSetup.Apply
    /// </summary>
    public static class BlackKitchenExitSignatureSetup
    {
        const string ScenePath =
            "Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity";

        const string PlaqueName = "ExitPrompt";
        const string BackgroundName = "PromptBackground";

        [MenuItem("BCaT/Black Kitchen/Set Up Exit Signage")]
        public static void Apply()
        {
            var log = new StringBuilder();
            log.AppendLine("[BlackKitchenExitSignatureSetup] START");

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Fail(log, $"could not open '{ScenePath}'.");
                return;
            }

            Transform plaque = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == PlaqueName);
            if (plaque == null)
            {
                Fail(log, $"no '{PlaqueName}' object in the scene.");
                return;
            }

            var controller = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<BlackKitchenExperienceController>(true))
                .FirstOrDefault();
            if (controller == null)
            {
                Fail(log, "no BlackKitchenExperienceController in the scene.");
                return;
            }

            Image background = plaque.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(i => i.name == BackgroundName)
                ?? plaque.GetComponentInChildren<Image>(true);
            if (background == null)
            {
                Fail(log, $"'{PlaqueName}' has no Image to use as the sign background.");
                return;
            }

            var sign = plaque.GetComponent<BlackKitchenExitSign>();
            bool created = sign == null;
            if (created)
                sign = plaque.gameObject.AddComponent<BlackKitchenExitSign>();

            var signObject = new SerializedObject(sign);
            signObject.FindProperty("controller").objectReferenceValue = controller;
            signObject.FindProperty("background").objectReferenceValue = background;
            signObject.ApplyModifiedPropertiesWithoutUndo();

            log.AppendLine($"  BlackKitchenExitSign {(created ? "ADDED" : "updated")} on '{Path(plaque)}'");
            log.AppendLine($"    controller = '{controller.name}'");
            log.AppendLine($"    background = '{background.name}' (colour {background.color})");
            log.AppendLine($"    plaque world position = {plaque.position}, canvas scale = {plaque.localScale}");

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Fail(log, "SaveScene returned false.");
                return;
            }

            // Prove the frozen systems were not disturbed by this pass.
            Transform origin = scene.GetRootGameObjects()
                .FirstOrDefault(r => r.name == "Platform")?.transform.Find("Quest/XR Origin");
            if (origin != null)
            {
                var xrOrigin = origin.GetComponent<Unity.XR.CoreUtils.XROrigin>();
                var move = origin.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement
                    .ContinuousMoveProvider>();
                var turn = origin.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning
                    .ContinuousTurnProvider>();
                log.AppendLine("  FROZEN RIG (read-back):");
                log.AppendLine($"    XR Origin localPos={origin.localPosition} scale={origin.localScale}");
                log.AppendLine($"    trackingOrigin={xrOrigin.RequestedTrackingOriginMode} " +
                               $"cameraYOffset={xrOrigin.CameraYOffset}");
                log.AppendLine($"    move={move.moveSpeed} turn={turn.turnSpeed} " +
                               $"turnAround={turn.enableTurnAround}");
            }

            int stations = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<BlackKitchenAudioInteractable>(true)).Count();
            log.AppendLine($"  audio stations still present: {stations}");

            log.AppendLine("[BlackKitchenExitSignatureSetup] DONE");
            Debug.Log(log.ToString());

            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        static string Path(Transform t)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (Transform cursor = t; cursor != null; cursor = cursor.parent)
                parts.Add(cursor.name);
            parts.Reverse();
            return string.Join("/", parts);
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
