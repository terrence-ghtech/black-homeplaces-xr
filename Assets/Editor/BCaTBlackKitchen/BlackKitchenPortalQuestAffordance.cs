using System.Linq;
using System.Text;
using BCaT.Production.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Gives the existing Black Kitchen entrance portal in the main house a Quest
    /// interaction affordance, using only the shared BCaT Quest interaction
    /// system that the working main-house exhibits already use.
    ///
    /// Why the portal is currently dead in headset: both of its colliders
    /// (KitchenIslandTrigger and KitchenIslandInteractable) are TRIGGERS, and
    /// both XRI casters ignore trigger colliders. The controller ray therefore
    /// never hits the portal, so there is no hover, no prompt, no haptic pulse
    /// and no select — while desktop, which uses camera raycasts, works fine.
    ///
    /// The shared fix is one <see cref="XrSelectSurface"/> component. On Quest it
    /// mirrors a source collider with a non-trigger, contact-free twin carrying
    /// an XRSimpleInteractable, and forwards hover/select through the
    /// InteractionRouter. On desktop it disables itself in Awake and creates
    /// nothing, so desktop behaviour is bit-for-bit unchanged.
    ///
    /// Everything else the entrance needs already exists and is inherited rather
    /// than re-implemented:
    ///   * hover announcement + prompt — the router's XR hover path drives the
    ///     shared InteractionPromptUi, which asks the portal for GetPrompt(xr)
    ///   * haptics — the main-house rig's interactors carry SimpleHapticFeedback
    ///     (hover 0.25/0.1s, select 0.5/0.1s), so any XRSimpleInteractable gets
    ///     the same pulses as every other interactable
    ///   * transition — select dispatches through the router to the portal's
    ///     existing EnterBlackKitchen()/BlackKitchenEntry sequence
    ///
    /// This adds no new runtime type, no Black Kitchen-specific XR prompt logic,
    /// and no legacy relay or *_QuestXRSelect twin object.
    ///
    ///   Unity -executeMethod BCaT.EditorTools.BlackKitchenPortalQuestAffordance.Apply
    /// </summary>
    public static class BlackKitchenPortalQuestAffordance
    {
        const string ScenePath = "Assets/BH_XR_MainScene.unity";

        const string PortalRootName = "BlackKitchenPortal_ROOT";
        const string ControllerName = "BlackKitchenPortalController";

        // The portal's authored interaction volume — the same object the
        // controller already serializes as its interactionRoot.
        const string AimVolumeName = "KitchenIslandInteractable";

        [MenuItem("BCaT/Black Kitchen/Add Quest Affordance To Entrance Portal")]
        public static void Apply()
        {
            var log = new StringBuilder();
            log.AppendLine("[BlackKitchenPortalQuestAffordance] START");

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Fail(log, $"could not open '{ScenePath}'.");
                return;
            }

            Transform portalRoot = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == PortalRootName);
            if (portalRoot == null)
            {
                Fail(log, $"no '{PortalRootName}' in the scene.");
                return;
            }

            Transform controller = portalRoot.Find(ControllerName);
            if (controller == null)
            {
                Fail(log, $"no '{PortalRootName}/{ControllerName}'.");
                return;
            }

            var target = controller.GetComponent<BlackKitchenPortalController>();
            if (target == null)
            {
                Fail(log, $"'{ControllerName}' carries no BlackKitchenPortalController.");
                return;
            }

            Transform aimVolume = portalRoot.Find(AimVolumeName);
            if (aimVolume == null)
            {
                Fail(log, $"no '{PortalRootName}/{AimVolumeName}' to mirror.");
                return;
            }

            var sourceCollider = aimVolume.GetComponent<Collider>();
            if (sourceCollider == null)
            {
                Fail(log, $"'{AimVolumeName}' has no Collider to mirror.");
                return;
            }

            log.AppendLine($"  target      : {Path(controller)} ({target.GetType().Name})");
            log.AppendLine($"  aim volume  : {Path(aimVolume)} " +
                           $"({sourceCollider.GetType().Name}, isTrigger={sourceCollider.isTrigger})");
            log.AppendLine($"  desktop prompt: '{target.GetPrompt(false)}'");
            log.AppendLine($"  quest prompt  : '{target.GetPrompt(true)}'");

            // The surface must live on the object that carries the
            // IInteractionTarget: XrSelectSurface resolves its owner on itself or
            // an ancestor, and the colliders here are SIBLINGS of the controller,
            // so the source collider is named explicitly.
            XrSelectSurface surface = controller.GetComponent<XrSelectSurface>();
            bool created = surface == null;
            if (created)
                surface = controller.gameObject.AddComponent<XrSelectSurface>();

            var surfaceObject = new SerializedObject(surface);
            SerializedProperty sources = surfaceObject.FindProperty("sourceColliders");
            sources.arraySize = 1;
            sources.GetArrayElementAtIndex(0).objectReferenceValue = sourceCollider;
            surfaceObject.FindProperty("padding").floatValue = 0f;
            surfaceObject.FindProperty("forwardsTo").stringValue =
                "BlackKitchenPortalController (Enter Black Kitchen)";
            surfaceObject.ApplyModifiedPropertiesWithoutUndo();

            log.AppendLine($"  XrSelectSurface {(created ? "added" : "updated")} on " +
                           $"'{controller.name}' → mirrors 1 collider ('{aimVolume.name}'), padding 0");

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Fail(log, "SaveScene returned false.");
                return;
            }

            log.AppendLine("[BlackKitchenPortalQuestAffordance] DONE");
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
