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
    [Tooltip("Quest wording for the floating entrance prompt. No keyboard wording.")]
    [SerializeField] private string xrPrompt = "Enter — Black Kitchen";
    [SerializeField] private SharedInteractionPromptConfig sharedPrompt =
        new SharedInteractionPromptConfig { verb = SharedInteractionVerb.Enter };

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
    private Collider focusCollider;

    // ---- IInteractionTarget --------------------------------------------

    public Vector3 FocusPoint
    {
        get
        {
            Collider collider = FocusCollider;
            if (collider != null)
                return collider.bounds.center;

            return (interactionRoot != null ? interactionRoot : transform).position;
        }
    }

    public float MaxDistance => desktopInteractionDistance;
    public float MaxViewAngle => 24f;
    public bool RequireLineOfSight => true;
    public int Priority => 1; // the portal wins over decor targets around the doorway
    public bool IsAvailable => isActiveAndEnabled && !transitionActive &&
                               !SceneTransitionState.IsTransitionInProgress;
    public bool AllowDesktopClick => true;
    public bool Exists => this != null;

    public Collider[] OwnColliders
    {
        get
        {
            if (ownColliders == null)
            {
                Transform root = ResolveInteractionRoot();
                ownColliders = root != null
                    ? root.GetComponentsInChildren<Collider>(true)
                    : GetComponentsInChildren<Collider>(true);
            }
            return ownColliders;
        }
    }

    /// <summary>
    /// Shared bottom-HUD prompt. On Quest the entrance is one of the two
    /// sanctioned world-space prompt systems, so this returns nothing there:
    /// the floating entrance prompt is the only affordance and an empty string
    /// keeps the bottom HUD hidden (no duplicate). Desktop is unchanged.
    /// </summary>
    public string GetPrompt(bool xr) =>
        xr ? string.Empty : WorldPromptText(false);

    /// <summary>Wording for the floating entrance prompt itself.</summary>
    public string WorldPromptText(bool xr)
    {
        if (sharedPrompt == null)
            sharedPrompt = new SharedInteractionPromptConfig { verb = SharedInteractionVerb.Enter };

        sharedPrompt.verb = SharedInteractionVerb.Enter;
        if (string.IsNullOrWhiteSpace(sharedPrompt.desktopPrompt))
            sharedPrompt.desktopPrompt = desktopPrompt;
        if (string.IsNullOrWhiteSpace(sharedPrompt.xrPrompt))
            sharedPrompt.xrPrompt = xrPrompt;
        return SharedInteractionPrompt.Format(xr, sharedPrompt);
    }

    public void OnFocusChanged(bool focused) { }

    public void OnInteract(InteractionActivation activation) => EnterBlackKitchen();

    // ---------------------------------------------------------------------

    private void Awake()
    {
        if (transitionOverlay != null)
            transitionOverlay.alpha = 0f;

        ResolveInteractionRoot();
    }

    private void OnEnable() => InteractionRouter.Register(this);

    private void OnDisable() => InteractionRouter.Unregister(this);

    private void Update()
    {
        // The Black Kitchen entrance is one of the two sanctioned world-space
        // prompt systems. On Quest its original floating prompt is restored;
        // it hides while a transition is running and comes back automatically
        // if that transition fails (transitionActive is cleared on every
        // failure path in EnterRoutine).
        if (promptText == null)
            return;

        bool xr = InteractionPromptText.IsXRActive();
        bool visible = !transitionActive && !SceneTransitionState.IsTransitionInProgress;
        WorldInteractionPromptVisual.SetSanctionedText(promptText, WorldPromptText(xr), visible);
    }

    public void EnterBlackKitchen()
    {
        BlackKitchenQuestTransitionDiagnostics.BeginTransition("Entrance callback received",
            $"Interaction callback entered. currentScene='{gameObject.scene.name}', activeScene='{BlackKitchenQuestTransitionDiagnostics.ActiveSceneName}', destination='{memorySceneName}', loadingScene='{loadingSceneName}'.");

        if (transitionActive || SceneTransitionState.IsTransitionInProgress || InteractionState.IsBlocked)
        {
            BlackKitchenQuestTransitionDiagnostics.Warning("Transition failed",
                $"Transition request rejected. transitionActive={transitionActive}, transitionInProgress={SceneTransitionState.IsTransitionInProgress}, interactionBlocked={InteractionState.IsBlocked}, reasons='{InteractionState.ActiveReasons}', lastTransitionError='{SceneTransitionState.LastError}'.");
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.LogWarning($"[BlackKitchenPortalController] Enter blocked: transitionActive={transitionActive}, transitionInProgress={SceneTransitionState.IsTransitionInProgress}, interactionBlocked={InteractionState.IsBlocked}, reasons={InteractionState.ActiveReasons}, lastTransitionError='{SceneTransitionState.LastError}'.");
#endif
            return;
        }

        transitionActive = true;
        InteractionState.SuppressInputForCurrentFrame();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.Log($"[BlackKitchenPortalController] EnterBlackKitchen accepted. destination='{memorySceneName}', spawn='{SceneTransitionState.BlackKitchenEntrySpawnId}', loadingScene='{loadingSceneName}'.");
#endif
        BlackKitchenQuestTransitionDiagnostics.Log("Transition request accepted",
            $"Transition request accepted. requestedScene='{memorySceneName}', spawn='{SceneTransitionState.BlackKitchenEntrySpawnId}', sourceScene='{gameObject.scene.name}'.");
        StartCoroutine(EnterRoutine());
    }

    public void OnXRSelect()
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.Log($"[BlackKitchenPortalController] OnXRSelect received on '{gameObject.name}'. Router present={InteractionRouter.Instance != null}.");
#endif
        if (InteractionRouter.Instance != null)
            InteractionRouter.Instance.RequestXRSelect(this);
        else
            EnterBlackKitchen();
    }

    private IEnumerator EnterRoutine()
    {
        ResolvePlayerReferences();
        BlackKitchenQuestTransitionDiagnostics.Log("Input/control lock",
            $"Player control lock requested. playerRoot='{(playerRoot != null ? playerRoot.name : "null")}', camera='{(playerCamera != null ? playerCamera.name : "null")}'.");
        SetPlayerControls(false);

        if (!SceneTransitionState.RequestTransition(memorySceneName, SceneTransitionState.BlackKitchenEntrySpawnId, gameObject.scene.name))
        {
            BlackKitchenQuestTransitionDiagnostics.Warning("Transition failed", $"Transition state request rejected: {SceneTransitionState.LastError}");
            Debug.LogWarning($"[BlackKitchenPortalController] Transition request blocked: {SceneTransitionState.LastError}");
            SetPlayerControls(true);
            transitionActive = false;
            yield break;
        }

        // Stop any exhibit media before leaving the house.
        BCaT.Production.Media.MediaPlaybackRegistry.StopAll();

        BlackKitchenQuestTransitionDiagnostics.Log("Fade-to-black start", $"Fade-to-black started. overlay='{(transitionOverlay != null ? transitionOverlay.name : "null")}', duration={fadeOutDuration:0.00}.");
        yield return FadeOverlay(1f, fadeOutDuration);
        BlackKitchenQuestTransitionDiagnostics.Log("Fade-to-black complete", $"Fade-to-black completed. overlayAlpha={(transitionOverlay != null ? transitionOverlay.alpha.ToString("0.00") : "null")}.");

        AsyncOperation load = null;
        string loadFailure = null;
        try
        {
            BlackKitchenQuestTransitionDiagnostics.Log("Scene load start",
                $"LoadingScene load start. currentScene='{gameObject.scene.name}', loadingScene='{loadingSceneName}', requestedScene='{memorySceneName}'.");
            Debug.Log($"[BlackKitchenPortalController] Scene '{gameObject.scene.name}' loading scene load requested: '{loadingSceneName}' via rig '{(playerRoot != null ? playerRoot.name : "unresolved")}'.");
            load = SceneManager.LoadSceneAsync(loadingSceneName, LoadSceneMode.Single);
        }
        catch (System.Exception exception)
        {
            loadFailure = $"[BlackKitchenPortalController] Failed to load '{loadingSceneName}' for Black Kitchen transition: {exception}";
        }

        if (loadFailure != null)
        {
            BlackKitchenQuestTransitionDiagnostics.Error("Transition failed", $"LoadingScene load exception. {loadFailure}");
            Debug.LogError(loadFailure);
            SceneTransitionState.CancelTransition(loadFailure);
            InteractionState.ForceCloseAll();
            SetPlayerControls(true);
            yield return FadeOverlay(0f, fadeOutDuration);
            transitionActive = false;
            yield break;
        }

        if (load == null)
        {
            string message = $"[BlackKitchenPortalController] Failed to load '{loadingSceneName}' for Black Kitchen transition.";
            BlackKitchenQuestTransitionDiagnostics.Error("Transition failed", message);
            Debug.LogError(message);
            SceneTransitionState.CancelTransition(message);
            InteractionState.ForceCloseAll();
            SetPlayerControls(true);
            yield return FadeOverlay(0f, fadeOutDuration);
            transitionActive = false;
            yield break;
        }

        while (!load.isDone)
        {
            BlackKitchenQuestTransitionDiagnostics.Log("Periodic load progress", $"LoadingScene load progress={load.progress:0.00}, allowSceneActivation={load.allowSceneActivation}.");
            yield return null;
        }

        BlackKitchenQuestTransitionDiagnostics.Log("Scene activation", $"LoadingScene activation completed. activeScene='{BlackKitchenQuestTransitionDiagnostics.ActiveSceneName}'.");
    }

    private Collider FocusCollider
    {
        get
        {
            if (focusCollider == null)
            {
                Collider[] colliders = OwnColliders;
                if (colliders != null)
                {
                    foreach (Collider candidate in colliders)
                    {
                        if (candidate != null && candidate.enabled)
                        {
                            focusCollider = candidate;
                            break;
                        }
                    }
                }
            }
            return focusCollider;
        }
    }

    private Transform ResolveInteractionRoot()
    {
        Transform root = interactionRoot != null ? interactionRoot : transform;
        if (root.GetComponentInChildren<Collider>(true) != null)
            return root;

        Transform parent = root.parent != null ? root.parent : transform.parent;
        if (parent != null)
        {
            Transform siblingTrigger = parent.Find("KitchenIslandTrigger");
            if (siblingTrigger != null && siblingTrigger.GetComponentInChildren<Collider>(true) != null)
                return siblingTrigger;
        }

        if (parent != null && parent.GetComponentInChildren<Collider>(true) != null)
            return parent;

        return root;
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
