using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class SceneArrivalController : MonoBehaviour
{
    [SerializeField] private float fadeInDuration = 0.7f;
    [SerializeField] private float desktopSpawnSafetyLift = 0.08f;

    private CanvasGroup fadeGroup;

    private IEnumerator Start()
    {
        CreateBlackOverlay();
        yield return null;

        Exception arrivalException = null;
        IEnumerator arrival = RunArrival();
        while (true)
        {
            object current = null;
            bool keepGoing = false;
            try
            {
                keepGoing = arrival.MoveNext();
                if (keepGoing)
                    current = arrival.Current;
            }
            catch (Exception exception)
            {
                arrivalException = exception;
            }

            if (arrivalException != null || !keepGoing)
                break;

            yield return current;
        }

        if (arrivalException != null)
            Debug.LogError($"[SceneArrivalController] Exception during arrival in scene '{gameObject.scene.name}': {arrivalException}");

        if (SceneTransitionState.IsTransitionInProgress)
            SceneTransitionState.ClearRequest();

        yield return FadeFromBlack();
        DestroyOverlay();
    }

    private IEnumerator RunArrival()
    {
        string spawnId = SceneTransitionState.DestinationSpawnId;
        if (!string.IsNullOrWhiteSpace(spawnId))
        {
            SceneSpawnPoint spawnPoint = FindSpawnPoint(spawnId);
            if (spawnPoint != null)
                yield return PlaceActivePlayerAtRoutine(spawnPoint.transform, desktopSpawnSafetyLift);
            else
                Debug.LogWarning($"[SceneArrivalController] Spawn point '{spawnId}' was not found in '{gameObject.scene.name}'. Continuing at the scene default player position.");
        }
    }

    public static void PlaceActivePlayerAt(Transform target)
    {
        Transform playerRoot = ResolvePlayerRoot();
        if (playerRoot == null || target == null)
        {
            Debug.LogWarning("[SceneArrivalController] Could not resolve an active player root for teleport.");
            return;
        }

        Debug.Log($"[SceneArrivalController] Scene '{playerRoot.gameObject.scene.name}' teleport started for rig '{playerRoot.name}' to '{target.name}'.");
        PlayerPhysicsState physicsState = new(playerRoot);
        physicsState.DisableForTeleport();
        ResetPlayerVerticalMotion(playerRoot);
        TeleportPlayerRoot(playerRoot, target, 0.08f);
        Physics.SyncTransforms();
        ResetPlayerVerticalMotion(playerRoot);
        physicsState.EnablePhysicsAfterTeleport();
        physicsState.EnableMovementAfterTeleport();
        Debug.Log($"[SceneArrivalController] Scene '{playerRoot.gameObject.scene.name}' teleport completed for rig '{playerRoot.name}'.");
    }

    private static IEnumerator PlaceActivePlayerAtRoutine(Transform target, float desktopSafetyLift)
    {
        Transform playerRoot = ResolvePlayerRoot();
        if (playerRoot == null || target == null)
        {
            Debug.LogWarning("[SceneArrivalController] Could not resolve an active player root for arrival spawn.");
            yield break;
        }

        Debug.Log($"[SceneArrivalController] Scene '{playerRoot.gameObject.scene.name}' teleport started for rig '{playerRoot.name}' to '{target.name}'.");
        PlayerPhysicsState physicsState = new(playerRoot);
        physicsState.DisableForTeleport();
        ResetPlayerVerticalMotion(playerRoot);
        TeleportPlayerRoot(playerRoot, target, desktopSafetyLift);
        Physics.SyncTransforms();
        yield return null;
        ResetPlayerVerticalMotion(playerRoot);
        physicsState.EnablePhysicsAfterTeleport();
        yield return null;
        physicsState.EnableMovementAfterTeleport();
        Debug.Log($"[SceneArrivalController] Scene '{playerRoot.gameObject.scene.name}' teleport completed for rig '{playerRoot.name}'.");
    }

    private static void TeleportPlayerRoot(Transform playerRoot, Transform target, float desktopSafetyLift)
    {
        CharacterController controller = ResolveDesktopCharacterController(playerRoot);
        if (controller == null)
        {
            Debug.Log($"[SceneArrivalController] Scene '{playerRoot.gameObject.scene.name}' spawn world position '{target.position}'. Rig root before '{playerRoot.position}'. No desktop CharacterController feet alignment used for rig '{playerRoot.name}'.");
            playerRoot.SetPositionAndRotation(target.position, target.rotation);
            LogCameraPositionAfterTeleport(playerRoot);
            return;
        }

        Vector3 rootBefore = playerRoot.position;
        Vector3 feetBefore = GetControllerFeetPosition(controller);
        Debug.Log($"[SceneArrivalController] Scene '{playerRoot.gameObject.scene.name}' spawn world position '{target.position}'. Rig root before '{rootBefore}'. CharacterController '{controller.name}' center '{controller.center}', height {controller.height:F3}, radius {controller.radius:F3}, feet before '{feetBefore}'.");

        playerRoot.rotation = target.rotation;
        Physics.SyncTransforms();

        Vector3 feetAfterRotation = GetControllerFeetPosition(controller);
        Vector3 desiredFeet = target.position + Vector3.up * Mathf.Max(0f, desktopSafetyLift);
        Vector3 rootDelta = desiredFeet - feetAfterRotation;
        playerRoot.position += rootDelta;
        Physics.SyncTransforms();

        Vector3 feetAfter = GetControllerFeetPosition(controller);
        LogBlockingColliders(playerRoot, controller);
        LogCameraPositionAfterTeleport(playerRoot);
        Debug.Log($"[SceneArrivalController] Scene '{playerRoot.gameObject.scene.name}' rig root after '{playerRoot.position}'. CharacterController feet after '{feetAfter}'. Desktop feet target '{desiredFeet}' using safety lift {Mathf.Max(0f, desktopSafetyLift):F3}.");
    }

    private static CharacterController ResolveDesktopCharacterController(Transform playerRoot)
    {
        ScenePlayerRig rig = playerRoot.GetComponentInParent<ScenePlayerRig>();
        if (rig == null || rig.Kind != ScenePlayerRig.RigKind.Desktop)
            return null;

        return playerRoot.GetComponentInChildren<CharacterController>(true);
    }

    private static Vector3 GetControllerFeetPosition(CharacterController controller)
    {
        Vector3 up = controller.transform.up;
        float halfHeight = GetWorldControllerHeight(controller) * 0.5f;
        return controller.transform.TransformPoint(controller.center) - up * halfHeight;
    }

    private static float GetWorldControllerHeight(CharacterController controller)
    {
        return controller.height * Mathf.Abs(controller.transform.lossyScale.y);
    }

    private static float GetWorldControllerRadius(CharacterController controller)
    {
        Vector3 scale = controller.transform.lossyScale;
        return controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
    }

    private static void LogBlockingColliders(Transform playerRoot, CharacterController controller)
    {
        Vector3 center = controller.transform.TransformPoint(controller.center);
        Vector3 up = controller.transform.up;
        float radius = GetWorldControllerRadius(controller);
        float halfSegment = Mathf.Max(0f, GetWorldControllerHeight(controller) * 0.5f - radius);
        Vector3 bottom = center - up * halfSegment;
        Vector3 top = center + up * halfSegment;
        Collider[] overlaps = Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider overlap in overlaps)
        {
            if (overlap == null || overlap.transform.IsChildOf(playerRoot))
                continue;

            Debug.LogWarning($"[SceneArrivalController] Scene '{playerRoot.gameObject.scene.name}' destination capsule overlaps collider '{overlap.name}' on '{overlap.gameObject.name}' while placing rig '{playerRoot.name}'.");
        }
    }

    private static void LogCameraPositionAfterTeleport(Transform playerRoot)
    {
        foreach (Camera camera in playerRoot.GetComponentsInChildren<Camera>(true))
        {
            if (camera != null && camera.CompareTag("MainCamera"))
            {
                Debug.Log($"[SceneArrivalController] Scene '{playerRoot.gameObject.scene.name}' camera '{camera.name}' world position after teleport '{camera.transform.position}'.");
                return;
            }
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
            Debug.Log($"[SceneArrivalController] Scene '{playerRoot.gameObject.scene.name}' active main camera '{mainCamera.name}' world position after teleport '{mainCamera.transform.position}'.");
    }

    private static Transform ResolvePlayerRoot()
    {
        bool useXR = BCaT.Production.BCaTPlatform.IsQuest;

        // The scene's ScenePlatformBinding published the rig it activated, so
        // ask it before falling back to scene-wide searches.
        ScenePlayerRig registered = BCaT.Production.ScenePlayerRigRegistry.Active;
        if (registered != null && registered.Kind == (useXR ? ScenePlayerRig.RigKind.XR : ScenePlayerRig.RigKind.Desktop))
        {
            Debug.Log($"[SceneArrivalController] Scene '{registered.gameObject.scene.name}' resolved player transform '{registered.name}' from the ScenePlayerRigRegistry for platform '{(useXR ? "XR" : "Desktop")}'.");
            return registered.transform;
        }

        Transform markedRig = ResolveMarkedPlayerRig(useXR);
        if (markedRig != null)
        {
            Debug.Log($"[SceneArrivalController] Scene '{markedRig.gameObject.scene.name}' resolved player transform '{markedRig.name}' using ScenePlayerRig for platform '{(useXR ? "XR" : "Desktop")}'.");
            return markedRig;
        }

        foreach (CharacterController controller in FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (controller != null && controller.gameObject.activeInHierarchy && IsRigAppropriateForPlatform(controller.transform, useXR))
            {
                Debug.Log($"[SceneArrivalController] Scene '{controller.gameObject.scene.name}' resolved player transform '{controller.transform.name}' using active CharacterController for platform '{(useXR ? "XR" : "Desktop")}'.");
                return controller.transform;
            }
        }

        foreach (Camera camera in Camera.allCameras)
        {
            if (camera == null || !camera.isActiveAndEnabled || !camera.CompareTag("MainCamera"))
                continue;

            CharacterController cameraController = camera.GetComponentInParent<CharacterController>();
            if (cameraController != null && cameraController.gameObject.activeInHierarchy && IsRigAppropriateForPlatform(cameraController.transform, useXR))
            {
                Debug.Log($"[SceneArrivalController] Scene '{camera.gameObject.scene.name}' resolved player transform '{cameraController.transform.name}' using active MainCamera '{camera.name}' for platform '{(useXR ? "XR" : "Desktop")}'.");
                return cameraController.transform;
            }
        }

        GameObject[] taggedPlayers = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject taggedPlayer in taggedPlayers)
        {
            if (taggedPlayer == null || !taggedPlayer.activeInHierarchy)
                continue;

            CharacterController taggedController = taggedPlayer.GetComponentInChildren<CharacterController>();
            Transform root = taggedController != null ? taggedController.transform : taggedPlayer.transform;
            if (IsRigAppropriateForPlatform(root, useXR))
            {
                Debug.Log($"[SceneArrivalController] Scene '{taggedPlayer.scene.name}' resolved player transform '{root.name}' using active Player tag for platform '{(useXR ? "XR" : "Desktop")}'.");
                return root;
            }
        }

        return null;
    }

    private static Transform ResolveMarkedPlayerRig(bool useXR)
    {
        ScenePlayerRig.RigKind expectedKind = useXR ? ScenePlayerRig.RigKind.XR : ScenePlayerRig.RigKind.Desktop;
        foreach (ScenePlayerRig rig in FindObjectsByType<ScenePlayerRig>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (rig != null && rig.gameObject.activeInHierarchy && rig.Kind == expectedKind)
                return rig.transform;
        }

        return null;
    }

    private static bool IsRigAppropriateForPlatform(Transform root, bool useXR)
    {
        ScenePlayerRig rig = root.GetComponentInParent<ScenePlayerRig>();
        if (rig != null)
            return rig.Kind == (useXR ? ScenePlayerRig.RigKind.XR : ScenePlayerRig.RigKind.Desktop);

        return !useXR;
    }

    private static SceneSpawnPoint FindSpawnPoint(string spawnId)
    {
        SceneSpawnPoint[] spawnPoints = FindObjectsByType<SceneSpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (SceneSpawnPoint spawnPoint in spawnPoints)
        {
            if (spawnPoint != null && spawnPoint.SpawnId == spawnId)
                return spawnPoint;
        }

        return null;
    }

    private void CreateBlackOverlay()
    {
        fadeGroup = BCaT.Production.Shell.FadeOverlayBuilder.Create("SceneArrivalFade", 32760);
        fadeGroup.alpha = 1f;
        fadeGroup.blocksRaycasts = true;
    }

    private IEnumerator FadeFromBlack()
    {
        if (fadeGroup == null)
            yield break;

        float start = fadeGroup.alpha;
        for (float elapsed = 0f; elapsed < fadeInDuration; elapsed += Time.deltaTime)
        {
            float t = fadeInDuration <= 0f ? 1f : elapsed / fadeInDuration;
            fadeGroup.alpha = Mathf.Lerp(start, 0f, t);
            yield return null;
        }

        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
    }

    private void DestroyOverlay()
    {
        if (fadeGroup != null)
            Destroy(fadeGroup.gameObject);
    }

    private static void ResetPlayerVerticalMotion(Transform playerRoot)
    {
        foreach (Rigidbody body in playerRoot.GetComponentsInChildren<Rigidbody>(true))
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        foreach (Behaviour behaviour in playerRoot.GetComponentsInChildren<Behaviour>(true))
        {
            if (behaviour == null)
                continue;

            MethodInfo resetFallForce = behaviour.GetType().GetMethod("ResetFallForce", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (resetFallForce != null && resetFallForce.GetParameters().Length == 0)
                resetFallForce.Invoke(behaviour, null);

            ResetVectorField(behaviour, "m_CurrentFallVelocity");
            ResetVectorField(behaviour, "m_GravityDrivenVelocity");
            ResetVectorField(behaviour, "m_VerticalVelocity");
            ResetVectorField(behaviour, "m_InAirVelocity");
            ResetFloatField(behaviour, "_verticalVelocity");
        }
    }

    private static void ResetVectorField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null && field.FieldType == typeof(Vector3))
            field.SetValue(target, Vector3.zero);
    }

    private static void ResetFloatField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null && field.FieldType == typeof(float))
            field.SetValue(target, 0f);
    }

    private sealed class PlayerPhysicsState
    {
        private readonly List<Behaviour> movementBehaviours = new();
        private readonly List<CharacterController> characterControllers = new();
        private readonly List<RigidbodyState> rigidbodies = new();

        public PlayerPhysicsState(Transform playerRoot)
        {
            foreach (Behaviour behaviour in playerRoot.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour == null || !behaviour.enabled)
                    continue;

                string typeName = behaviour.GetType().Name;
                if (typeName == "FirstPersonController" || typeName == "StarterAssetsInputs" || typeName == "PlayerInput")
                    movementBehaviours.Add(behaviour);
            }

            foreach (CharacterController controller in playerRoot.GetComponentsInChildren<CharacterController>(true))
            {
                if (controller.enabled)
                    characterControllers.Add(controller);
            }

            foreach (Rigidbody body in playerRoot.GetComponentsInChildren<Rigidbody>(true))
                rigidbodies.Add(new RigidbodyState(body));
        }

        public void DisableForTeleport()
        {
            foreach (Behaviour behaviour in movementBehaviours)
            {
                if (behaviour != null)
                    behaviour.enabled = false;
            }

            foreach (CharacterController controller in characterControllers)
            {
                if (controller != null)
                    controller.enabled = false;
            }

            foreach (RigidbodyState state in rigidbodies)
            {
                if (state.Body == null)
                    continue;

                state.Body.linearVelocity = Vector3.zero;
                state.Body.angularVelocity = Vector3.zero;
                state.Body.useGravity = false;
                state.Body.detectCollisions = false;
            }
        }

        public void EnablePhysicsAfterTeleport()
        {
            foreach (RigidbodyState state in rigidbodies)
            {
                if (state.Body == null)
                    continue;

                state.Body.linearVelocity = Vector3.zero;
                state.Body.angularVelocity = Vector3.zero;
                state.Body.useGravity = state.UseGravity;
                state.Body.detectCollisions = state.DetectCollisions;
            }

            foreach (CharacterController controller in characterControllers)
            {
                if (controller != null)
                    controller.enabled = true;
            }
        }

        public void EnableMovementAfterTeleport()
        {
            foreach (Behaviour behaviour in movementBehaviours)
            {
                if (behaviour != null)
                    behaviour.enabled = true;
            }
        }
    }

    private readonly struct RigidbodyState
    {
        public readonly Rigidbody Body;
        public readonly bool DetectCollisions;
        public readonly bool UseGravity;

        public RigidbodyState(Rigidbody body)
        {
            Body = body;
            DetectCollisions = body.detectCollisions;
            UseGravity = body.useGravity;
        }
    }
}
