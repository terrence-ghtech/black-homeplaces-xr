using UnityEngine;

namespace BCaT.Production.Interaction
{
    /// <summary>How an interaction was activated.</summary>
    public enum InteractionActivation
    {
        DesktopInteractKey, // E
        DesktopClick,       // left mouse button on focused target
        XRSelect,           // Quest controller select through XRI
        Programmatic,       // directory jump, smoke test, kiosk scripting
    }

    /// <summary>
    /// Contract between the central InteractionRouter and anything interactable.
    /// The router owns candidate evaluation, focus, prompts, and input dispatch;
    /// implementations own only their exhibit-specific outcome in OnInteract.
    /// </summary>
    public interface IInteractionTarget
    {
        /// <summary>World point used for distance/view-angle tests.</summary>
        Vector3 FocusPoint { get; }

        /// <summary>Maximum interaction distance from the player camera.</summary>
        float MaxDistance { get; }

        /// <summary>
        /// Maximum angle (degrees) between the camera forward vector and the
        /// direction to the focus point. Values &lt;= 0 mean the target is
        /// proximity-based and does not require camera focus.
        /// </summary>
        float MaxViewAngle { get; }

        /// <summary>When true the router requires an unobstructed camera ray.</summary>
        bool RequireLineOfSight { get; }

        /// <summary>Higher priority wins when several targets are valid.</summary>
        int Priority { get; }

        /// <summary>Exhibit-specific availability gate (e.g. closed while its own UI is open).</summary>
        bool IsAvailable { get; }

        /// <summary>Whether a desktop left-click may also activate this target.</summary>
        bool AllowDesktopClick { get; }

        /// <summary>Full prompt text for the platform (desktop or Quest wording).</summary>
        string GetPrompt(bool xr);

        /// <summary>
        /// Router notification when this target gains or loses focus. Targets
        /// with their own world-space prompt canvas show/hide it here.
        /// </summary>
        void OnFocusChanged(bool focused);

        /// <summary>Perform the exhibit-specific interaction.</summary>
        void OnInteract(InteractionActivation activation);

        /// <summary>The colliders that belong to this target (for line-of-sight self-hits). May be null.</summary>
        Collider[] OwnColliders { get; }

        /// <summary>Unity object validity (implemented for free by MonoBehaviour).</summary>
        bool Exists { get; }
    }

    /// <summary>
    /// An exhibit subsystem that manages its own interaction selection for a
    /// bounded area (the Black Kitchen station manager). While an exclusive zone
    /// is registered and active, the router suppresses its own selection and
    /// forwards the shared per-frame input to the zone instead, so there is
    /// still exactly one interaction input owner.
    /// </summary>
    public interface IExclusiveInteractionZone
    {
        bool ZoneActive { get; }

        /// <summary>Called once per frame with the shared input state while the zone owns interaction.</summary>
        void ZoneTick(bool interactPressed);

        /// <summary>Hide the zone's prompt (called when blockers activate).</summary>
        void ZoneSuppressPrompts();
    }
}
