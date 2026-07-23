using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BlackKitchenPortalController : MonoBehaviour
{
    [Header("Scene Transition")]
    [SerializeField] private string memorySceneName = "BlackKitchen_MemoryScene";
    [SerializeField] private string loadingSceneName = SceneTransitionState.LoadingSceneName;
    [SerializeField] private float fadeOutDuration = 0.7f;
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
    private bool transitionActive;

    private void Awake()
    {
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
        if (!transitionActive && !SceneTransitionState.IsTransitionInProgress)
            StartCoroutine(EnterRoutine());
    }

    public void OnXRSelect()
    {
        EnterBlackKitchen();
    }

    private IEnumerator EnterRoutine()
    {
        transitionActive = true;
        ResolvePlayerReferences();
        SetPlayerControls(false);

        if (!SceneTransitionState.RequestTransition(memorySceneName, SceneTransitionState.BlackKitchenEntrySpawnId, gameObject.scene.name))
        {
            Debug.LogWarning($"[BlackKitchenPortalController] Transition request blocked: {SceneTransitionState.LastError}");
            SetPlayerControls(true);
            transitionActive = false;
            yield break;
        }

        yield return FadeOverlay(1f, fadeOutDuration);

        AsyncOperation load = null;
        try
        {
            Debug.Log($"[BlackKitchenPortalController] Scene '{gameObject.scene.name}' loading scene load requested: '{loadingSceneName}' via rig '{(playerRoot != null ? playerRoot.name : "unresolved")}'.");
            load = SceneManager.LoadSceneAsync(loadingSceneName, LoadSceneMode.Single);
        }
        catch (System.Exception exception)
        {
            string message = $"[BlackKitchenPortalController] Failed to load '{loadingSceneName}' for Black Kitchen transition: {exception.Message}";
            Debug.LogError(message);
            SceneTransitionState.CancelTransition(message);
            SetPlayerControls(true);
            transitionActive = false;
            yield break;
        }

        if (load == null)
        {
            string message = $"[BlackKitchenPortalController] Failed to load '{loadingSceneName}' for Black Kitchen transition.";
            Debug.LogError(message);
            SceneTransitionState.CancelTransition(message);
            SetPlayerControls(true);
            transitionActive = false;
            yield break;
        }

        while (!load.isDone)
            yield return null;
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
