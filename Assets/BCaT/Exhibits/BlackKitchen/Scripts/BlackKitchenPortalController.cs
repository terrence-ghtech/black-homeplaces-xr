using System.Collections;
using System.Collections.Generic;
using BCaT.Production.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Black Kitchen entry portal in the main house. Interaction selection and
/// input are owned by the central InteractionRouter (no keyboard polling);
/// the portal keeps full ownership of its transition sequence: disabling
/// player controls, fading the overlay, requesting the shared transition, and
/// loading the LoadingScene.
/// </summary>
public class BlackKitchenPortalController : MonoBehaviour, IInteractionTarget
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
    [Tooltip("Behaviour type-name fragments disabled during transition (merged with the built-in defaults at runtime).")]
    [SerializeField] private string[] controlComponentNameFilters =
    {
        "LocomotionProvider", "ContinuousMoveProvider", "ContinuousTurnProvider", "SnapTurnProvider",
        "DynamicMoveProvider", "GrabMoveProvider", "GravityProvider", "JumpProvider",
        "TeleportationProvider", "ActionBasedController", "StarterAssetsInputs", "FirstPersonController", "PlayerInput"
    };

    // Scene instances may carry an older, shorter serialized filter list; the
    // runtime merges these defaults back in so newer XRI locomotion providers
    // are always covered.
    private static readonly string[] RequiredControlFilters =
    {
        "LocomotionProvider", "ContinuousMoveProvider", "ContinuousTurnProvider", "SnapTurnProvider",
        "DynamicMoveProvider", "GrabMoveProvider", "GravityProvider", "JumpProvider",
        "TeleportationProvider", "ActionBasedController", "StarterAssetsInputs", "FirstPersonController", "PlayerInput"
    };

    [Header("Desktop Prompts")]
    [SerializeField] private string desktopPrompt = "Press E to Enter Black Kitchen";
    [Header("Quest Prompts")]
    [SerializeField] private string xrPrompt = "Interact to Enter Black Kitchen";

    [Header("Debug")]
    [SerializeField] private float desktopInteractionDistance = 4f;
#pragma warning disable 0414 // retained for scene-data compatibility; router owns the interact key now
    [SerializeField] private Key interactionKey = Key.E;
#pragma warning restore 0414
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private Transform interactionRoot;

    private readonly List<Behaviour> disabledControls = new();
    private bool transitionActive;
    private Collider[] ownColliders;

    // ---- IInteractionTarget --------------------------------------------

    public Vector3 FocusPoint =>
        (interactionRoot != null ? interactionRoot : transform).position;

    public float MaxDistance => desktopInteractionDistance;
    public float MaxViewAngle => 18f;
    public bool RequireLineOfSight => true;
    public int Priority => 1; // the portal wins over decor targets around the doorway
    public bool IsAvailable => isActiveAndEnabled && !transitionActive &&
                               !SceneTransitionState.IsTransitionInProgress;
    public bool AllowDesktopClick => false;
    public bool Exists => this != null;

    public Collider[] OwnColliders
    {
        get
        {
            if (ownColliders == null)
            {
                var root = interactionRoot != null ? interactionRoot : transform;
                ownColliders = root.GetComponentsInChildren<Collider>(true);
            }
            return ownColliders;
        }
    }

    public string GetPrompt(bool xr) => xr ? xrPrompt : desktopPrompt;

    public void OnFocusChanged(bool focused) { }

    public void OnInteract(InteractionActivation activation) => EnterBlackKitchen();

    // ---------------------------------------------------------------------

    private void Awake()
    {
        if (transitionOverlay != null)
            transitionOverlay.alpha = 0f;
    }

    private void OnEnable() => InteractionRouter.Register(this);

    private void OnDisable() => InteractionRouter.Unregister(this);

    private void Update()
    {
        // Keep the world prompt's platform wording current (visibility is
        // managed by the scene as before; input is owned by the router).
        if (promptText != null)
            promptText.text = InteractionPromptText.IsXRActive() ? xrPrompt : desktopPrompt;
    }

    public void EnterBlackKitchen()
    {
        if (!transitionActive && !SceneTransitionState.IsTransitionInProgress)
            StartCoroutine(EnterRoutine());
    }

    public void OnXRSelect()
    {
        if (InteractionRouter.Instance != null)
            InteractionRouter.Instance.RequestXRSelect(this);
        else
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

        // Stop any exhibit media before leaving the house.
        BCaT.Production.Media.MediaPlaybackRegistry.StopAll();

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
            var filters = new HashSet<string>(RequiredControlFilters);
            if (controlComponentNameFilters != null)
                foreach (string filter in controlComponentNameFilters)
                    if (!string.IsNullOrEmpty(filter))
                        filters.Add(filter);

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
                    foreach (string filter in filters)
                    {
                        if (typeName.Contains(filter))
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
}
