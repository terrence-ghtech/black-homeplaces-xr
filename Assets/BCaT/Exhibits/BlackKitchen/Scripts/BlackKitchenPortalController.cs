using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BlackKitchenPortalController : MonoBehaviour
{
    private static BlackKitchenPortalController active;

    [Header("Scene Transition")]
    [SerializeField] private string memorySceneName = "BlackKitchen_MemoryScene";
    [SerializeField] private float fadeOutDuration = 0.7f;
    [SerializeField] private float fadeInDuration = 0.7f;
    [SerializeField] private CanvasGroup transitionOverlay;

    [Header("Player Control")]
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Camera playerCamera;
    [Tooltip("Optional roots whose movement/look/input behaviours are disabled during transitions.")]
    [SerializeField] private Transform[] playerControlRoots;
    [Tooltip("Behaviour type-name fragments disabled during transition.")]
    [SerializeField] private string[] controlComponentNameFilters =
    {
        "LocomotionProvider", "ContinuousMoveProvider", "ContinuousTurnProvider", "SnapTurnProvider",
        "DynamicMoveProvider", "GrabMoveProvider", "GravityProvider", "JumpProvider",
        "TeleportationProvider", "ActionBasedController", "StarterAssetsInputs", "FirstPersonController", "PlayerInput"
    };

    [Header("Spawn and Return")]
    [SerializeField] private Transform returnPoint;

    [Header("WebGL Prompts")]
    [SerializeField] private string desktopPrompt = "Press E to Enter Black Kitchen";
    [Header("Quest Prompts")]
    [SerializeField] private string xrPrompt = "Interact to Enter Black Kitchen";

    [Header("Debug")]
    [SerializeField] private float desktopInteractionDistance = 4f;
    [SerializeField] private Key interactionKey = Key.E;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private Transform interactionRoot;

    private readonly List<Behaviour> disabledControls = new();
    private readonly List<CharacterController> disabledCharacterControllers = new();
    private readonly List<RigidbodyState> disabledRigidbodies = new();
    private bool transitionActive;
    private Scene loadedMemoryScene;

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

    private void Awake()
    {
        active = this;
        if (transitionOverlay != null)
            transitionOverlay.alpha = 0f;
    }

    private void Update()
    {
        if (promptText != null)
            promptText.text = InteractionPromptText.IsXRActive() ? xrPrompt : desktopPrompt;

        if (transitionActive || Keyboard.current == null || !Keyboard.current[interactionKey].wasPressedThisFrame)
            return;

        if (IsLookingAtInteraction())
            EnterBlackKitchen();
    }

    public void EnterBlackKitchen()
    {
        if (!transitionActive)
            StartCoroutine(EnterRoutine());
    }

    public void OnXRSelect()
    {
        EnterBlackKitchen();
    }

    public static void ReturnFromMemory(AudioSource exitReflectionSource, float exitReflectionFadeDuration)
    {
        if (active != null)
            active.StartCoroutine(active.ExitRoutine(exitReflectionSource, exitReflectionFadeDuration));
    }

    private IEnumerator EnterRoutine()
    {
        transitionActive = true;
        ResolvePlayerReferences();
        SetPlayerControls(false);
        yield return FadeOverlay(1f, fadeOutDuration);

        AsyncOperation load = SceneManager.LoadSceneAsync(memorySceneName, LoadSceneMode.Additive);
        while (load != null && !load.isDone)
            yield return null;

        loadedMemoryScene = SceneManager.GetSceneByName(memorySceneName);
        if (loadedMemoryScene.IsValid())
            SceneManager.SetActiveScene(loadedMemoryScene);

        BlackKitchenExperienceController experience = FindFirstObjectByType<BlackKitchenExperienceController>();
        if (experience != null && experience.SpawnPoint != null)
            yield return TeleportPlayerSafely(experience.SpawnPoint);

        yield return FadeOverlay(0f, fadeInDuration);
        SetPlayerControls(true);
        transitionActive = false;
    }

    private IEnumerator ExitRoutine(AudioSource exitReflectionSource, float exitReflectionFadeDuration)
    {
        if (transitionActive)
            yield break;

        transitionActive = true;
        ResolvePlayerReferences();
        SetPlayerControls(false);
        yield return FadeOverlay(1f, fadeOutDuration);

        if (returnPoint != null)
            yield return TeleportPlayerSafely(returnPoint);

        if (loadedMemoryScene.IsValid() && loadedMemoryScene.isLoaded)
        {
            AsyncOperation unload = SceneManager.UnloadSceneAsync(loadedMemoryScene);
            while (unload != null && !unload.isDone)
                yield return null;
        }

        if (exitReflectionSource != null && exitReflectionSource.isPlaying && exitReflectionFadeDuration > 0f)
            StartCoroutine(FadeExitReflection(exitReflectionSource, exitReflectionFadeDuration));

        yield return FadeOverlay(0f, fadeInDuration);
        SetPlayerControls(true);
        transitionActive = false;
    }

    private IEnumerator FadeOverlay(float target, float duration)
    {
        if (transitionOverlay == null)
            yield break;

        transitionOverlay.blocksRaycasts = target > 0.5f;
        float start = transitionOverlay.alpha;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = duration <= 0f ? 1f : elapsed / duration;
            transitionOverlay.alpha = Mathf.Lerp(start, target, t);
            yield return null;
        }

        transitionOverlay.alpha = target;
        transitionOverlay.blocksRaycasts = target > 0.5f;
    }

    private static IEnumerator FadeExitReflection(AudioSource source, float duration)
    {
        float start = source.volume;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            if (source == null)
                yield break;

            source.volume = Mathf.Lerp(start, 0f, elapsed / duration);
            yield return null;
        }

        if (source != null)
            source.Stop();
    }

    public static void RecoverActivePlayerTo(Transform target)
    {
        if (active == null || target == null)
            return;

        active.ResolvePlayerReferences();
        active.SetPlayerPhysicsEnabled(false);
        active.ResetPlayerVerticalMotion();
        active.TeleportPlayerImmediate(target);
        active.SetPlayerPhysicsEnabled(true);
    }

    private void ResolvePlayerReferences()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (playerCamera != null)
        {
            CharacterController cameraController = playerCamera.GetComponentInParent<CharacterController>();
            if (cameraController != null)
            {
                playerRoot = cameraController.transform;
                return;
            }
        }

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            CharacterController taggedController = taggedPlayer.GetComponentInChildren<CharacterController>();
            playerRoot = taggedController != null ? taggedController.transform : taggedPlayer.transform;
            return;
        }

        if (playerRoot == null && playerCamera != null)
            playerRoot = playerCamera.transform.root;
    }

    private IEnumerator TeleportPlayerSafely(Transform target)
    {
        ResolvePlayerReferences();
        SetPlayerPhysicsEnabled(false);
        ResetPlayerVerticalMotion();
        TeleportPlayerImmediate(target);
        yield return null;
        Physics.SyncTransforms();
        ResetPlayerVerticalMotion();
        SetPlayerPhysicsEnabled(true);
    }

    private void TeleportPlayerImmediate(Transform target)
    {
        if (playerRoot == null || target == null)
            return;

        playerRoot.SetPositionAndRotation(target.position, target.rotation);
        Physics.SyncTransforms();
        ResetPlayerVerticalMotion();
    }

    private void SetPlayerPhysicsEnabled(bool enabled)
    {
        ResolvePlayerReferences();
        if (playerRoot == null)
            return;

        if (!enabled)
        {
            disabledCharacterControllers.Clear();
            foreach (CharacterController characterController in playerRoot.GetComponentsInChildren<CharacterController>(true))
            {
                if (!characterController.enabled)
                    continue;

                characterController.enabled = false;
                disabledCharacterControllers.Add(characterController);
            }

            disabledRigidbodies.Clear();
            foreach (Rigidbody body in playerRoot.GetComponentsInChildren<Rigidbody>(true))
            {
                disabledRigidbodies.Add(new RigidbodyState(body));
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.useGravity = false;
                body.detectCollisions = false;
            }
        }
        else
        {
            foreach (RigidbodyState state in disabledRigidbodies)
            {
                if (state.Body == null)
                    continue;

                state.Body.linearVelocity = Vector3.zero;
                state.Body.angularVelocity = Vector3.zero;
                state.Body.useGravity = state.UseGravity;
                state.Body.detectCollisions = state.DetectCollisions;
            }
            disabledRigidbodies.Clear();

            foreach (CharacterController characterController in disabledCharacterControllers)
            {
                if (characterController != null)
                    characterController.enabled = true;
            }
            disabledCharacterControllers.Clear();
        }
    }

    private void ResetPlayerVerticalMotion()
    {
        if (playerRoot == null)
            return;

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

    private void SetPlayerControls(bool enabled)
    {
        if (!enabled)
        {
            disabledControls.Clear();
            foreach (Transform root in ResolveControlRoots())
            {
                if (root == null)
                    continue;

                foreach (Behaviour behaviour in root.GetComponentsInChildren<Behaviour>(true))
                {
                    if (behaviour == null || !behaviour.enabled || behaviour == this)
                        continue;

                    string typeName = behaviour.GetType().Name;
                    foreach (string filter in controlComponentNameFilters)
                    {
                        if (!string.IsNullOrEmpty(filter) && typeName.Contains(filter))
                        {
                            behaviour.enabled = false;
                            disabledControls.Add(behaviour);
                            break;
                        }
                    }
                }
            }
        }
        else
        {
            foreach (Behaviour behaviour in disabledControls)
            {
                if (behaviour != null)
                    behaviour.enabled = true;
            }
            disabledControls.Clear();
        }
    }

    private IEnumerable<Transform> ResolveControlRoots()
    {
        if (playerControlRoots != null && playerControlRoots.Length > 0)
        {
            foreach (Transform root in playerControlRoots)
                yield return root;
        }
        else
        {
            ResolvePlayerReferences();
            yield return playerRoot;
        }
    }

    private bool IsLookingAtInteraction()
    {
        if (interactionRoot == null)
            interactionRoot = transform;
        if (playerCamera == null)
            playerCamera = Camera.main;
        if (playerCamera == null)
            return false;

        RaycastHit[] hits = Physics.RaycastAll(new Ray(playerCamera.transform.position, playerCamera.transform.forward), desktopInteractionDistance, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform == interactionRoot || hit.collider.transform.IsChildOf(interactionRoot))
                return true;
            if (!hit.collider.isTrigger)
                return false;
        }

        return false;
    }
}
