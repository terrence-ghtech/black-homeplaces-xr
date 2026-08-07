using BCaT.Production.Interaction;
using UnityEngine;
using TMPro;

/// <summary>
/// Exhibit that opens an external web resource. Desktop selection and input
/// are owned by the central InteractionRouter; the XR select wiring continues
/// to call OpenLink, which validates against the shared blocking rules so a
/// menu or modal can never leak a browser launch.
/// </summary>
public class InteractableLinkLauncher : MonoBehaviour, IInteractionTarget
{
    public enum OpenBehavior
    {
        ExternalUrl,
    }

    [Header("Exhibit Content")]
    [SerializeField] private string displayName;
    [SerializeField] private string projectName;

    [Header("Link Settings")]
    [SerializeField] private string targetUrl;
    [SerializeField] private OpenBehavior openBehavior = OpenBehavior.ExternalUrl;

    [Header("Platform Restrictions")]
    [SerializeField] private bool allowDesktop = true;
    [SerializeField] private bool allowQuest = true;

    [Header("Prompt")]
    [SerializeField] private SharedInteractionPromptConfig prompt =
        new SharedInteractionPromptConfig { verb = SharedInteractionVerb.Open };

    [Header("Interaction")]
#pragma warning disable 0414 // retained for scene-data compatibility; router owns the camera now
    [SerializeField] private Camera playerCamera;
#pragma warning restore 0414
    [SerializeField] private float interactDistance = 4f;

    [Header("Prompt Text Only")]
    [SerializeField] private TMP_Text promptText;

    private Collider[] ownColliders;

    // ---- IInteractionTarget --------------------------------------------

    public Vector3 FocusPoint => ColliderFocusPoint();
    public float MaxDistance => interactDistance;
    public float MaxViewAngle => 16f;
    public bool RequireLineOfSight => true;
    public int Priority => 0;
    public bool IsAvailable => isActiveAndEnabled && PlatformAllowed &&
                               openBehavior == OpenBehavior.ExternalUrl &&
                               !string.IsNullOrWhiteSpace(targetUrl);
    public bool AllowDesktopClick => true;
    public bool Exists => this != null;

    bool PlatformAllowed =>
        (BCaT.Production.PlatformCapabilities.IsQuestConfiguration ||
         BCaT.Production.PlatformCapabilities.IsXRActive)
            ? allowQuest
            : allowDesktop;

    public Collider[] OwnColliders
    {
        get
        {
            if (ownColliders == null)
                ownColliders = GetComponentsInChildren<Collider>(true);
            return ownColliders;
        }
    }

    Vector3 ColliderFocusPoint()
    {
        Collider[] colliders = OwnColliders;
        if (colliders == null || colliders.Length == 0)
            return transform.position;

        bool hasBounds = false;
        Bounds bounds = default;
        foreach (Collider collider in colliders)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                continue;

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds ? bounds.center : transform.position;
    }

    public string GetPrompt(bool xr)
    {
        if (prompt == null)
            prompt = new SharedInteractionPromptConfig { verb = SharedInteractionVerb.Open };

        prompt.verb = SharedInteractionVerb.Open;
        if (string.IsNullOrWhiteSpace(prompt.objectName))
            prompt.objectName = !string.IsNullOrWhiteSpace(displayName)
                ? displayName
                : projectName;
        return SharedInteractionPrompt.Format(xr, prompt);
    }

    public void OnFocusChanged(bool focused) { }

    public void OnInteract(InteractionActivation activation) => LaunchUrl();

    // ---------------------------------------------------------------------

    void OnEnable() => InteractionRouter.Register(this);

    void OnDisable() => InteractionRouter.Unregister(this);

    void Start()
    {
        if (promptText == null) return;

        WorldInteractionPromptVisual.SetText(promptText, GetPrompt(InteractionPromptText.IsXRActive()));
    }

    /// <summary>
    /// Public entry point kept for the existing XR select wiring. Applies the
    /// shared blocking rules so menus/modals suppress link launches too.
    /// </summary>
    public void OpenLink()
    {
        if (InteractionRouter.Instance != null)
        {
            InteractionRouter.Instance.RequestXRSelect(this);
            return;
        }

        if (InteractionState.IsBlocked)
        {
            Debug.Log($"[LinkLauncher:{gameObject.name}] OpenLink suppressed (interaction blocked).");
            return;
        }
        LaunchUrl();
    }

    void LaunchUrl()
    {
        if (string.IsNullOrWhiteSpace(targetUrl))
            return;

        Debug.Log($"[LinkLauncher:{gameObject.name}] Opening external link: {targetUrl}");
        Application.OpenURL(targetUrl);
    }
}
