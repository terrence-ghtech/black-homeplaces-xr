using BCaT.Production;
using BCaT.Production.Interaction;
using UnityEngine;

/// <summary>
/// Desktop-only router adapter for the House Grounds well. It owns no content
/// or XR callbacks; the existing controller remains the Quest target and popup owner.
/// </summary>
public sealed class HouseGroundsDesktopInteraction : MonoBehaviour, IInteractionTarget, IDesktopOnlyInteractionTarget
{
    [SerializeField] private HouseGroundsExhibitController houseGrounds;
    [SerializeField] private Collider interactionCollider;
    [SerializeField] private Transform wellColliderRoot;
    [SerializeField] private float interactionDistance = 4.5f;
    [SerializeField] private float maxViewAngle = 20f;

    Collider[] ownColliders;

    bool IsDesktopInteraction => PlatformCapabilities.IsDesktop && !PlatformCapabilities.IsXRActive;

    public Vector3 FocusPoint => interactionCollider != null
        ? interactionCollider.bounds.center
        : transform.position;
    public float MaxDistance => interactionDistance;
    public float MaxViewAngle => maxViewAngle;
    public bool RequireLineOfSight => true;
    // The frozen controller remains at priority 1; this Desktop-only adapter wins locally.
    public int Priority => 2;
    public bool IsAvailable => IsDesktopInteraction && isActiveAndEnabled &&
                               houseGrounds != null && !houseGrounds.IsOpen;
    public bool AllowDesktopClick => true;
    public bool Exists => this != null;
    public Collider[] OwnColliders
    {
        get
        {
            if (ownColliders != null)
                return ownColliders;

            // The configured well hierarchy is the physical exhibit this
            // Desktop target represents, so its mesh colliders are not LOS blockers.
            ownColliders = wellColliderRoot != null
                ? wellColliderRoot.GetComponentsInChildren<Collider>(true)
                : interactionCollider != null ? new[] { interactionCollider } : System.Array.Empty<Collider>();
            return ownColliders;
        }
    }

    public string GetPrompt(bool xr) =>
        SharedInteractionPrompt.Format(xr, SharedInteractionVerb.View, "House and Grounds");

    public void OnFocusChanged(bool focused) { }

    public void OnInteract(InteractionActivation activation)
    {
        if (!IsDesktopInteraction || houseGrounds == null)
            return;

        if (FocusedExhibitCoordinator.Instance != null)
            FocusedExhibitCoordinator.Instance.RequestOpen(houseGrounds);
        else
            houseGrounds.OpenExhibit();
    }

    void OnEnable()
    {
        if (IsDesktopInteraction)
            InteractionRouter.Register(this);
    }

    void OnDisable() => InteractionRouter.Unregister(this);

#if UNITY_EDITOR
    /// <summary>Used by the exhibit builder to wire the existing well references.</summary>
    public void Configure(HouseGroundsExhibitController controller, Collider collider, Transform well)
    {
        houseGrounds = controller;
        interactionCollider = collider;
        wellColliderRoot = well;
        ownColliders = null;
    }
#endif
}
