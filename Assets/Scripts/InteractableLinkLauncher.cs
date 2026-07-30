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
    [Header("Link Settings")]
    [SerializeField] private string targetUrl;

    [Header("Interaction")]
#pragma warning disable 0414 // retained for scene-data compatibility; router owns the camera now
    [SerializeField] private Camera playerCamera;
#pragma warning restore 0414
    [SerializeField] private float interactDistance = 4f;

    [Header("Prompt Text Only")]
    [SerializeField] private TMP_Text promptText;

    private Collider[] ownColliders;

    // ---- IInteractionTarget --------------------------------------------

    public Vector3 FocusPoint => transform.position;
    public float MaxDistance => interactDistance;
    public float MaxViewAngle => 16f;
    public bool RequireLineOfSight => true;
    public int Priority => 0;
    public bool IsAvailable => isActiveAndEnabled && !string.IsNullOrWhiteSpace(targetUrl);
    public bool AllowDesktopClick => false;
    public bool Exists => this != null;

    public Collider[] OwnColliders
    {
        get
        {
            if (ownColliders == null)
                ownColliders = GetComponentsInChildren<Collider>(true);
            return ownColliders;
        }
    }

    public string GetPrompt(bool xr) => xr ? "Interact to open" : "Press E to open";

    public void OnFocusChanged(bool focused) { }

    public void OnInteract(InteractionActivation activation) => LaunchUrl();

    // ---------------------------------------------------------------------

    void OnEnable() => InteractionRouter.Register(this);

    void OnDisable() => InteractionRouter.Unregister(this);

    void Start()
    {
        if (promptText == null) return;

        // Centralized platform-aware verb: "Press E" on desktop, "Interact" in XR.
        promptText.text = InteractionPromptText.Verb + " to Open";
    }

    /// <summary>
    /// Public entry point kept for the existing XR select wiring. Applies the
    /// shared blocking rules so menus/modals suppress link launches too.
    /// </summary>
    public void OpenLink()
    {
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
