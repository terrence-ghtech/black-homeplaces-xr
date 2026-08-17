using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Adds right-stick turning to the Black Kitchen Quest baseline rig, and
    /// nothing else.
    ///
    /// Which provider: the main house rig's effective right-stick behaviour is
    /// SMOOTH turning, not snap. That was read off the scene rather than assumed:
    /// the main house instance of Unity's Starter Assets XR Rig overrides
    /// SnapTurnProvider.m_Enabled to 0, keeps ContinuousTurnProvider enabled with
    /// m_EnableTurnAround = 0, and sets the Right Controller's
    /// ControllerInputActionManager to m_SmoothTurnEnabled = 1 /
    /// m_SmoothMotionEnabled = 0 — which is exactly the combination that enables
    /// the continuous "Turn" action and disables the "Snap Turn" action.
    /// So this adds one <see cref="ContinuousTurnProvider"/> and no
    /// SnapTurnProvider: there is only ever one artificial rotation source.
    ///
    /// Values match the main house rig (turn speed 60 deg/s, turn-left/right on,
    /// turn-around off). The input is a directly serialized action bound to the
    /// right thumbstick — the same binding path the main house "Turn" action uses
    /// (&lt;XRController&gt;{RightHand}/{Primary2DAxis}) — rather than a reference
    /// into the XRI sample asset. That keeps the baseline self-contained and
    /// avoids dragging in ControllerInputActionManager and the rest of the
    /// Starter Assets input machinery, which is not what was asked for.
    ///
    /// The left-hand turn input is explicitly Unused, so the left stick stays
    /// movement-only. Nothing about the existing walking, tracking origin, camera
    /// offset, camera, TrackedPoseDrivers or controller tracking is touched.
    ///
    ///   Unity -executeMethod BCaT.EditorTools.BlackKitchenQuestRightStickTurn.Apply
    /// </summary>
    public static class BlackKitchenQuestRightStickTurn
    {
        const string ScenePath =
            "Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity";

        const string OriginPath = "Platform/Quest/XR Origin";

        // Main house values.
        const float TurnSpeed = 60f;
        const bool EnableTurnLeftRight = true;
        const bool EnableTurnAround = false;

        const string RightStickBinding = "<XRController>{RightHand}/{Primary2DAxis}";

        [MenuItem("BCaT/Black Kitchen/Add Quest Right-Stick Turning")]
        public static void Apply()
        {
            var log = new StringBuilder();
            log.AppendLine("[BlackKitchenQuestRightStickTurn] START");

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Fail(log, $"could not open '{ScenePath}'.");
                return;
            }

            Transform platform = scene.GetRootGameObjects()
                .FirstOrDefault(r => r.name == "Platform")?.transform;
            Transform origin = platform != null ? platform.Find("Quest/XR Origin") : null;
            if (origin == null)
            {
                Fail(log, $"no '{OriginPath}' in the scene.");
                return;
            }

            var mediator = origin.GetComponent<LocomotionMediator>();
            if (mediator == null)
            {
                Fail(log, $"'{OriginPath}' has no LocomotionMediator to attach a turn provider to.");
                return;
            }

            // Guard the "one artificial rotation source" rule.
            SnapTurnProvider[] snapProviders = origin.root.GetComponentsInChildren<SnapTurnProvider>(true);
            if (snapProviders.Length > 0)
            {
                Fail(log, $"{snapProviders.Length} SnapTurnProvider(s) already exist in this rig; " +
                          "a second turning mode would compete with continuous turning. Nothing changed.");
                return;
            }

            ContinuousTurnProvider turn = origin.GetComponent<ContinuousTurnProvider>();
            bool created = turn == null;
            if (created)
                turn = origin.gameObject.AddComponent<ContinuousTurnProvider>();

            turn.mediator = mediator;
            turn.transformationPriority = 0;
            turn.turnSpeed = TurnSpeed;
            turn.enableTurnLeftRight = EnableTurnLeftRight;
            turn.enableTurnAround = EnableTurnAround;

            // Right stick only.
            var rightAction = new InputAction("Right Hand Turn", InputActionType.Value,
                expectedControlType: "Vector2");
            rightAction.AddBinding(RightStickBinding);
            turn.rightHandTurnInput.inputSourceMode = XRInputValueReader.InputSourceMode.InputAction;
            turn.rightHandTurnInput.inputAction = rightAction;

            // Left stick stays movement-only: no turn contribution at all.
            turn.leftHandTurnInput.inputSourceMode = XRInputValueReader.InputSourceMode.Unused;

            EditorUtility.SetDirty(turn);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Fail(log, "SaveScene returned false.");
                return;
            }

            // ---- Evidence, including that nothing else moved ----------------
            var move = origin.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement
                .ContinuousMoveProvider>();
            log.AppendLine($"  ContinuousTurnProvider {(created ? "ADDED" : "updated")} on '{origin.name}'");
            log.AppendLine($"    turnSpeed={turn.turnSpeed} enableTurnLeftRight={turn.enableTurnLeftRight} " +
                           $"enableTurnAround={turn.enableTurnAround} " +
                           $"transformationPriority={turn.transformationPriority} " +
                           $"mediator='{(turn.mediator != null ? turn.mediator.name : "none")}'");
            log.AppendLine($"    rightHandTurnInput mode={turn.rightHandTurnInput.inputSourceMode} " +
                           $"binding='{RightStickBinding}'");
            log.AppendLine($"    leftHandTurnInput  mode={turn.leftHandTurnInput.inputSourceMode}");
            log.AppendLine($"  SnapTurnProvider count in rig: " +
                           $"{origin.root.GetComponentsInChildren<SnapTurnProvider>(true).Length} (must be 0)");
            log.AppendLine($"  turn providers on rig: " +
                           $"{origin.root.GetComponentsInChildren<LocomotionProvider>(true).Count(p => p is ContinuousTurnProvider || p is SnapTurnProvider)} (must be 1)");

            if (move != null)
            {
                log.AppendLine("  UNCHANGED ContinuousMoveProvider (left-stick walking):");
                log.AppendLine($"    moveSpeed={move.moveSpeed} strafe={move.enableStrafe} fly={move.enableFly} " +
                               $"left={move.leftHandMoveInput.inputSourceMode} " +
                               $"right={move.rightHandMoveInput.inputSourceMode} " +
                               $"forwardSource={(move.forwardSource == null ? "null (camera)" : move.forwardSource.name)}");
            }
            else
            {
                log.AppendLine("  WARNING: no ContinuousMoveProvider found — expected the walking baseline.");
            }

            var xrOrigin = origin.GetComponent<Unity.XR.CoreUtils.XROrigin>();
            if (xrOrigin != null)
            {
                log.AppendLine("  UNCHANGED XROrigin (tracking origin / offsets):");
                log.AppendLine($"    mode={xrOrigin.RequestedTrackingOriginMode} " +
                               $"cameraYOffset={xrOrigin.CameraYOffset} " +
                               $"originScale={origin.localScale} " +
                               $"offsetLocal={(xrOrigin.CameraFloorOffsetObject != null ? xrOrigin.CameraFloorOffsetObject.transform.localPosition.ToString() : "n/a")}");
            }

            log.AppendLine("[BlackKitchenQuestRightStickTurn] DONE");
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
