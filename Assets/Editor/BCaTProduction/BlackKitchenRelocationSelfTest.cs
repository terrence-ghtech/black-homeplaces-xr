using System;
using System.Collections;
using System.Reflection;
using BCaT.Production;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BCaT.EditorTools
{
    /// <summary>Authored-target and platform-placement checks for Black Kitchen transitions.</summary>
    public static class BlackKitchenRelocationSelfTest
    {
        const string MainScene = "Assets/BH_XR_MainScene.unity";
        const string KitchenScene =
            "Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity";

        [MenuItem("BCaT/Diagnostics/Black Kitchen Relocation Self Test")]
        public static void Run()
        {
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                VerifyAuthoredSpawns();
                VerifyDesktopFeetPlacement();
                VerifyRepeatedXrHeadAlignment();
                Debug.Log("[BlackKitchenRelocationSelfTest] PASS");
            }
            finally
            {
                bool canRestore = previousSetup != null && previousSetup.Length > 0;
                if (canRestore)
                    foreach (SceneSetup setup in previousSetup)
                        canRestore &= !string.IsNullOrWhiteSpace(setup.path);

                if (canRestore)
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        static void VerifyAuthoredSpawns()
        {
            EditorSceneManager.OpenScene(KitchenScene, OpenSceneMode.Single);
            SceneSpawnPoint entry = FindSpawn(SceneTransitionState.BlackKitchenEntrySpawnId);
            Require(entry != null, "BlackKitchenEntry spawn is missing");
            Require(Vector3.Distance(entry.transform.position, new Vector3(0f, 0.02f, -4.43f)) < 0.001f,
                "BlackKitchenEntry position changed unexpectedly");
            Require(Vector3.Angle(entry.transform.forward, Vector3.forward) < 0.01f,
                "BlackKitchenEntry must face the authored +Z interior direction");

            EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
            SceneSpawnPoint[] points = UnityEngine.Object.FindObjectsByType<SceneSpawnPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            SceneSpawnPoint returnPoint = FindSpawn(SceneTransitionState.MainHouseKitchenReturnSpawnId);
            Require(returnPoint != null, "shared MainHouseKitchenReturn spawn is missing");
            int matches = 0;
            foreach (SceneSpawnPoint point in points)
                if (point != null && point.SpawnId == SceneTransitionState.MainHouseKitchenReturnSpawnId)
                    matches++;
            Require(matches == 1, "main house must contain exactly one shared kitchen return spawn");
            Require(returnPoint.transform.parent != null &&
                    returnPoint.transform.parent.name == "BlackKitchenPortal_ROOT",
                "kitchen return must remain authored relative to BlackKitchenPortal_ROOT");
            Require(Vector3.Distance(returnPoint.transform.localPosition,
                        new Vector3(0f, -0.55f, -4f)) < 0.001f,
                "kitchen return is not at the explicit safe first-floor marker");
            Require(Vector3.Angle(returnPoint.transform.forward,
                        returnPoint.transform.parent.forward) < 0.01f,
                "kitchen return yaw must follow the portal's authored heading");

            Physics.SyncTransforms();
            RaycastHit[] floorHits = Physics.RaycastAll(
                new Vector3(returnPoint.transform.position.x, 50f, returnPoint.transform.position.z),
                Vector3.down,
                100f,
                ~0,
                QueryTriggerInteraction.Ignore);
            float closestFloorDelta = float.PositiveInfinity;
            foreach (RaycastHit hit in floorHits)
                closestFloorDelta = Mathf.Min(closestFloorDelta,
                    Mathf.Abs(hit.point.y - returnPoint.transform.position.y));
            Require(closestFloorDelta <= 0.15f,
                "kitchen return must be authored on first-floor walkable geometry");

            Vector3 feet = returnPoint.transform.position + Vector3.up * 0.08f;
            Collider[] overlaps = Physics.OverlapCapsule(
                feet + Vector3.up * 0.35f,
                feet + Vector3.up * 1.45f,
                0.35f,
                ~0,
                QueryTriggerInteraction.Ignore);
            foreach (Collider overlap in overlaps)
                Require(overlap.bounds.max.y <= returnPoint.transform.position.y + 0.2f,
                    $"kitchen return capsule intersects '{overlap.name}' above the floor");

            Debug.Log($"[BlackKitchenRelocationSelfTest] Shared first-floor return world pose: " +
                      $"position={returnPoint.transform.position}, yaw={returnPoint.transform.eulerAngles.y:0.###}");
        }

        static void VerifyDesktopFeetPlacement()
        {
            GameObject root = new GameObject("BlackKitchenRelocationSelfTest_Desktop");
            GameObject targetObject = new GameObject("BlackKitchenRelocationSelfTest_Target");
            try
            {
                root.AddComponent<ScenePlayerRig>(); // default serialized kind is Desktop
                CharacterController controller = root.AddComponent<CharacterController>();
                controller.height = 1.8f;
                controller.center = new Vector3(0f, 0.9f, 0f);
                targetObject.transform.SetPositionAndRotation(
                    new Vector3(12f, 3f, -4f), Quaternion.Euler(0f, 73f, 0f));

                MethodInfo teleport = typeof(SceneArrivalController).GetMethod(
                    "TeleportPlayerRoot", BindingFlags.Static | BindingFlags.NonPublic);
                for (int i = 0; i < 3; i++)
                {
                    root.transform.SetPositionAndRotation(
                        new Vector3(i * 5f, 20f + i, -10f), Quaternion.Euler(0f, i * 91f, 0f));
                    teleport.Invoke(null, new object[] { root.transform, targetObject.transform, 0.08f });
                    Vector3 feet = controller.transform.TransformPoint(controller.center) -
                                   controller.transform.up * controller.height * 0.5f;
                    Require(Vector3.Distance(feet, targetObject.transform.position + Vector3.up * 0.08f) < 0.001f,
                        "desktop feet must repeatedly land on the shared destination with the safety lift");
                    Require(Vector3.Angle(root.transform.forward, targetObject.transform.forward) < 0.01f,
                        "desktop yaw must repeatedly match the authored destination");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(targetObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void VerifyRepeatedXrHeadAlignment()
        {
            GameObject root = new GameObject("BlackKitchenRelocationSelfTest_XR");
            GameObject headObject = new GameObject("BlackKitchenRelocationSelfTest_Head");
            GameObject spawnObject = new GameObject("BlackKitchenRelocationSelfTest_Spawn");
            try
            {
                Camera head = headObject.AddComponent<Camera>();
                headObject.transform.SetParent(root.transform, false);
                headObject.transform.localPosition = new Vector3(0.24f, 1.68f, -0.17f);
                XROrigin origin = root.AddComponent<XROrigin>();
                SerializedObject serializedOrigin = new SerializedObject(origin);
                serializedOrigin.FindProperty("m_OriginBaseGameObject").objectReferenceValue = root;
                serializedOrigin.FindProperty("m_CameraFloorOffsetObject").objectReferenceValue = root;
                serializedOrigin.FindProperty("m_Camera").objectReferenceValue = head;
                serializedOrigin.ApplyModifiedPropertiesWithoutUndo();

                spawnObject.transform.SetPositionAndRotation(
                    new Vector3(-7f, 0.02f, 14f), Quaternion.Euler(0f, 38f, 0f));
                SetTrackingOverride(() => true);
                foreach (float inheritedHeadYaw in new[] { -110f, 15f, 143f })
                {
                    root.transform.SetPositionAndRotation(spawnObject.transform.position, Quaternion.identity);
                    headObject.transform.localRotation = Quaternion.Euler(0f, inheritedHeadYaw, 0f);
                    Vector3 localPositionBefore = headObject.transform.localPosition;
                    Quaternion localRotationBefore = headObject.transform.localRotation;
                    Drain(XrArrivalAlignment.WaitForTrackingAndFaceSpawn(
                        root.transform, spawnObject.transform));

                    Vector3 flatHeadForward = Vector3.ProjectOnPlane(headObject.transform.forward, Vector3.up);
                    Require(Vector3.Angle(flatHeadForward, spawnObject.transform.forward) < 0.01f,
                        "XR world head yaw must repeatedly match the authored spawn forward");
                    Require(Mathf.Abs(headObject.transform.position.x - spawnObject.transform.position.x) < 0.001f &&
                            Mathf.Abs(headObject.transform.position.z - spawnObject.transform.position.z) < 0.001f,
                        "XR tracked head must repeatedly center over the authored destination");
                    Require(headObject.transform.localPosition == localPositionBefore &&
                            Quaternion.Angle(headObject.transform.localRotation, localRotationBefore) < 0.001f,
                        "XR alignment must not edit the tracked camera's local pose");
                }
            }
            finally
            {
                SetTrackingOverride(null);
                UnityEngine.Object.DestroyImmediate(spawnObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void Drain(IEnumerator routine)
        {
            while (routine.MoveNext())
                if (routine.Current is IEnumerator nested)
                    Drain(nested);
        }

        static void SetTrackingOverride(Func<bool> value)
        {
            typeof(XrArrivalAlignment).GetField(
                    "TrackingValidOverride", BindingFlags.Static | BindingFlags.NonPublic)
                ?.SetValue(null, value);
        }

        static SceneSpawnPoint FindSpawn(string id)
        {
            foreach (SceneSpawnPoint point in UnityEngine.Object.FindObjectsByType<SceneSpawnPoint>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (point != null && point.SpawnId == id)
                    return point;
            return null;
        }

        static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("[BlackKitchenRelocationSelfTest] FAIL: " + message);
        }
    }
}
