using System.Linq;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Builds the Black Kitchen Quest XR embodiment baseline: the smallest
    /// standard-XRI rig that gives a correct spawn, a correct physical eye
    /// height, 1:1 head tracking and continuous stick walking. Nothing else.
    ///
    /// Everything here is derived from the XR Interaction Toolkit (3.4.1) and
    /// core-utils (2.6.0) packages plus Unity's own "XRI Default Input Actions"
    /// sample; no configuration is copied from another BCaT scene or rig.
    ///
    /// Three design decisions carry the correctness of the baseline:
    ///
    /// 1. Floor tracking with a zeroed offset. XROrigin is set to
    ///    TrackingOriginMode.Floor, which makes the runtime report head poses
    ///    that already include the wearer's height, and makes XROrigin force
    ///    Camera Offset's local Y to 0. The offset object is therefore authored
    ///    at 0 and CameraYOffset is authored at 0 as well, so no value in the
    ///    rig can add height on top of the tracked pose. Physical eye height
    ///    comes from the headset and only from the headset.
    ///
    /// 2. One placement, authored and idempotent. The rig is authored at the
    ///    BlackKitchenEntry spawn point's exact transform. The shared
    ///    SceneArrivalController re-applies that same transform when the player
    ///    arrives through a scene transition (its XR path is a single
    ///    SetPositionAndRotation), so the authored pose and the arrival pose are
    ///    identical and arriving cannot produce a jump. There is no second
    ///    corrective step and no Y fudge.
    ///
    /// 3. No CharacterController and no GravityProvider. ContinuousMoveProvider
    ///    projects its motion onto the origin's XZ plane, so with nothing
    ///    driving vertical motion the origin's Y never changes: the wearer
    ///    cannot fall, cannot be pushed up a step, and the view is perfectly
    ///    still the instant the stick is released. Collision and gravity are
    ///    deliberately out of this baseline's scope.
    ///
    /// The rig lives under Platform/Quest, authored INACTIVE, which is the
    /// contract ScenePlatformBinding and BCaTArchitectureValidator (BCAT-P002)
    /// already enforce: an inactive branch never runs Awake on Desktop.
    ///
    ///   Unity -executeMethod BCaT.EditorTools.BlackKitchenQuestBaselineRigBuilder.Build
    /// </summary>
    public static class BlackKitchenQuestBaselineRigBuilder
    {
        const string ScenePath =
            "Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity";

        const string SpawnId = "BlackKitchenEntry";

        const string PlatformGroupName = "Platform";
        const string QuestBranchName = "Quest";

        // Standard XRI names, so the hierarchy reads like Unity's own XR Origin.
        const string OriginName = "XR Origin";
        const string CameraOffsetName = "Camera Offset";
        const string CameraName = "Main Camera";
        const string LeftControllerName = "Left Controller";
        const string RightControllerName = "Right Controller";

        // The single tuned value in the baseline: a normal walking pace.
        const float MoveSpeed = 1.5f;

        // VR needs a nearer near-plane than Unity's 0.3 default, or geometry at
        // arm's length clips. 0.1 is the value Unity's own XR rig ships with.
        const float NearClip = 0.1f;
        const float FarClip = 1000f;

        [MenuItem("BCaT/Black Kitchen/Build Quest XR Baseline Rig")]
        public static void Build()
        {
            var log = new StringBuilder();
            log.AppendLine("[BlackKitchenQuestBaselineRigBuilder] START");

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Fail(log, $"could not open scene '{ScenePath}'.");
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();

            GameObject platformGroup = roots.FirstOrDefault(r => r.name == PlatformGroupName);
            if (platformGroup == null)
            {
                Fail(log, $"scene has no root '{PlatformGroupName}' group.");
                return;
            }

            // The spawn point is scene data: read it, never rewrite it.
            SceneSpawnPoint spawn = roots
                .SelectMany(r => r.GetComponentsInChildren<SceneSpawnPoint>(true))
                .FirstOrDefault(s => s.SpawnId == SpawnId);
            if (spawn == null)
            {
                Fail(log, $"scene has no SceneSpawnPoint with spawnId '{SpawnId}'.");
                return;
            }

            Vector3 spawnPosition = spawn.transform.position;
            Quaternion spawnRotation = spawn.transform.rotation;
            log.AppendLine($"  spawn '{SpawnId}' world position={Fmt(spawnPosition)} " +
                           $"rotation={spawnRotation.eulerAngles.y:F2}° yaw");

            // Rebuild from scratch every run so the result never depends on a
            // previous partial state.
            Transform existing = platformGroup.transform.Find(QuestBranchName);
            if (existing != null)
            {
                log.AppendLine($"  removing existing '{PlatformGroupName}/{QuestBranchName}' subtree");
                Object.DestroyImmediate(existing.gameObject);
            }

            // ---- Platform/Quest ------------------------------------------
            var questBranch = new GameObject(QuestBranchName);
            questBranch.transform.SetParent(platformGroup.transform, false);
            Identity(questBranch.transform);

            // ---- XR Origin ------------------------------------------------
            var originGo = new GameObject(OriginName);
            originGo.transform.SetParent(questBranch.transform, false);
            originGo.transform.localScale = Vector3.one;
            // The one deliberate placement: author the rig at the spawn.
            originGo.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

            var cameraOffsetGo = new GameObject(CameraOffsetName);
            cameraOffsetGo.transform.SetParent(originGo.transform, false);
            Identity(cameraOffsetGo.transform);

            var cameraGo = new GameObject(CameraName);
            cameraGo.transform.SetParent(cameraOffsetGo.transform, false);
            Identity(cameraGo.transform);
            cameraGo.tag = "MainCamera";

            Camera camera = cameraGo.AddComponent<Camera>();
            camera.nearClipPlane = NearClip;
            camera.farClipPlane = FarClip;
            // URP resolves per-camera data through this component; add it
            // explicitly rather than relying on lazy creation at runtime.
            cameraGo.AddComponent<UniversalAdditionalCameraData>();
            cameraGo.AddComponent<AudioListener>();

            // Head pose. This is the single tracked-pose owner for the view.
            TrackedPoseDriver headDriver = cameraGo.AddComponent<TrackedPoseDriver>();
            headDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            headDriver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            headDriver.ignoreTrackingState = false;
            headDriver.positionInput = Vector3Input("Head Position", "<XRHMD>/centerEyePosition");
            headDriver.rotationInput = QuaternionInput("Head Rotation", "<XRHMD>/centerEyeRotation");
            headDriver.trackingStateInput = IntegerInput("Head Tracking State", "<XRHMD>/trackingState");

            GameObject leftController = BuildController(cameraOffsetGo.transform, LeftControllerName, "LeftHand");
            GameObject rightController = BuildController(cameraOffsetGo.transform, RightControllerName, "RightHand");

            // ---- XROrigin -------------------------------------------------
            // Written through SerializedObject: XROrigin's property setters
            // also poke the XR input subsystem / offset height, which has no
            // meaning at author time. This writes the authored state only.
            XROrigin origin = originGo.AddComponent<XROrigin>();
            var originObject = new SerializedObject(origin);
            originObject.FindProperty("m_OriginBaseGameObject").objectReferenceValue = originGo;
            originObject.FindProperty("m_CameraFloorOffsetObject").objectReferenceValue = cameraOffsetGo;
            originObject.FindProperty("m_Camera").objectReferenceValue = camera;
            originObject.FindProperty("m_RequestedTrackingOriginMode").enumValueIndex =
                (int)XROrigin.TrackingOriginMode.Floor;
            // Floor mode ignores CameraYOffset; author 0 so no height is hidden.
            originObject.FindProperty("m_CameraYOffset").floatValue = 0f;
            originObject.ApplyModifiedPropertiesWithoutUndo();

            // ---- Locomotion: exactly one continuous move path -------------
            XRBodyTransformer bodyTransformer = originGo.AddComponent<XRBodyTransformer>();
            bodyTransformer.xrOrigin = origin;

            LocomotionMediator mediator = originGo.AddComponent<LocomotionMediator>();

            ContinuousMoveProvider move = originGo.AddComponent<ContinuousMoveProvider>();
            move.mediator = mediator;
            move.moveSpeed = MoveSpeed;
            move.enableStrafe = true;
            move.enableFly = false;
            // forwardSource stays null: ContinuousMoveProvider then uses the
            // camera transform, so "forward" is where the wearer is looking.
            // With no turn provider, physical head/body rotation is the only
            // way to steer, which is the intended baseline.
            move.forwardSource = null;

            // Left thumbstick only. The right stick is explicitly Unused so no
            // input path can produce artificial turning.
            BindMoveInput(move.leftHandMoveInput, "Left Hand Move",
                "<XRController>{LeftHand}/{Primary2DAxis}");
            move.rightHandMoveInput.inputSourceMode = XRInputValueReader.InputSourceMode.Unused;

            // ---- Rig identity for the shared services ---------------------
            var rig = originGo.AddComponent<ScenePlayerRig>();
            SetPrivateEnum(rig, "kind", (int)ScenePlayerRig.RigKind.XR);

            // ---- Wire the branch into the scene's platform binding --------
            var binding = roots
                .SelectMany(r => r.GetComponentsInChildren<BCaT.Production.ScenePlatformBinding>(true))
                .FirstOrDefault();
            if (binding == null)
            {
                Fail(log, "scene has no ScenePlatformBinding to wire the Quest branch into.");
                return;
            }

            var bindingObject = new SerializedObject(binding);
            SerializedProperty questProperty = bindingObject.FindProperty("questBranch");
            if (questProperty == null)
            {
                Fail(log, "ScenePlatformBinding has no 'questBranch' field.");
                return;
            }

            questProperty.objectReferenceValue = questBranch;
            bindingObject.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine($"  ScenePlatformBinding.questBranch → " +
                           $"{PlatformGroupName}/{QuestBranchName}");

            // Authored INACTIVE: BCAT-P002, and the reason no Quest component
            // ever runs Awake in a Desktop session.
            questBranch.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Fail(log, "SaveScene returned false.");
                return;
            }

            // ---- Evidence -------------------------------------------------
            log.AppendLine("  built hierarchy:");
            Describe(questBranch.transform, log, 2);
            log.AppendLine($"  XROrigin: mode={origin.RequestedTrackingOriginMode} " +
                           $"cameraYOffset={origin.CameraYOffset} " +
                           $"origin='{origin.Origin.name}' " +
                           $"floorOffset='{origin.CameraFloorOffsetObject.name}' " +
                           $"camera='{origin.Camera.name}'");
            log.AppendLine($"  ContinuousMoveProvider: speed={move.moveSpeed} strafe={move.enableStrafe} " +
                           $"fly={move.enableFly} forwardSource=" +
                           $"{(move.forwardSource == null ? "null (camera)" : move.forwardSource.name)} " +
                           $"left={move.leftHandMoveInput.inputSourceMode} " +
                           $"right={move.rightHandMoveInput.inputSourceMode}");
            log.AppendLine($"  controllers: '{leftController.name}', '{rightController.name}'");
            log.AppendLine($"  branch activeSelf={questBranch.activeSelf} (must be False)");
            log.AppendLine("[BlackKitchenQuestBaselineRigBuilder] DONE");
            Debug.Log(log.ToString());

            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        static GameObject BuildController(Transform parent, string name, string handUsage)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Identity(go.transform);

            TrackedPoseDriver driver = go.AddComponent<TrackedPoseDriver>();
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            driver.ignoreTrackingState = false;
            driver.positionInput = Vector3Input($"{name} Position",
                $"<XRController>{{{handUsage}}}/devicePosition");
            driver.rotationInput = QuaternionInput($"{name} Rotation",
                $"<XRController>{{{handUsage}}}/deviceRotation");
            driver.trackingStateInput = IntegerInput($"{name} Tracking State",
                $"<XRController>{{{handUsage}}}/trackingState");
            return go;
        }

        // Directly-serialized actions, not references into a project asset:
        // TrackedPoseDriver and XRInputValueReader both enable an embedded
        // action themselves, so the rig needs no InputActionManager and
        // inherits no project input configuration.
        static InputActionProperty Vector3Input(string name, string path) =>
            new InputActionProperty(Action(name, "Vector3", path));

        static InputActionProperty QuaternionInput(string name, string path) =>
            new InputActionProperty(Action(name, "Quaternion", path));

        static InputActionProperty IntegerInput(string name, string path) =>
            new InputActionProperty(Action(name, "Integer", path));

        static InputAction Action(string name, string expectedControlType, string path)
        {
            var action = new InputAction(name, InputActionType.Value,
                expectedControlType: expectedControlType);
            action.AddBinding(path);
            return action;
        }

        static void BindMoveInput(XRInputValueReader<Vector2> reader, string name, string path)
        {
            reader.inputSourceMode = XRInputValueReader.InputSourceMode.InputAction;
            reader.inputAction = Action(name, "Vector2", path);
        }

        static void Identity(Transform t)
        {
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        static void SetPrivateEnum(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[BlackKitchenQuestBaselineRigBuilder] " +
                               $"'{target.GetType().Name}' has no field '{field}'.");
                return;
            }

            property.enumValueIndex = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Describe(Transform t, StringBuilder log, int depth)
        {
            string indent = new string(' ', depth * 2);
            string components = string.Join(", ", t.GetComponents<Component>()
                .Where(c => c != null && !(c is Transform))
                .Select(c => c.GetType().Name));
            log.AppendLine($"{indent}{t.name}  localPos={Fmt(t.localPosition)} " +
                           $"localScale={Fmt(t.localScale)} lossyScale={Fmt(t.lossyScale)} " +
                           $"tag={t.tag} active={t.gameObject.activeSelf}" +
                           (components.Length > 0 ? $"  [{components}]" : string.Empty));
            foreach (Transform child in t)
                Describe(child, log, depth + 1);
        }

        static string Fmt(Vector3 v) => $"({v.x:F4}, {v.y:F4}, {v.z:F4})";

        static void Fail(StringBuilder log, string message)
        {
            log.AppendLine($"  FAILED: {message}");
            Debug.LogError(log.ToString());
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }
}
