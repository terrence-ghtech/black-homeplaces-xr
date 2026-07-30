using UnityEngine;

namespace BCaT.Production.Interaction
{
    /// <summary>
    /// Convenience base class for router-managed interactables. Handles
    /// registration, focus-driven world prompt visibility, and prompt wording;
    /// subclasses implement only their exhibit-specific outcome.
    ///
    /// Existing exhibit MonoBehaviours migrated to the router either extend this
    /// class or implement IInteractionTarget directly when they already have a
    /// base class.
    /// </summary>
    public abstract class InteractionTargetBase : MonoBehaviour, IInteractionTarget
    {
        [Header("Router target")]
        [Tooltip("Point used for distance/angle tests. Defaults to this transform.")]
        [SerializeField] protected Transform focusPoint;

        [Tooltip("Maximum interaction distance from the player camera.")]
        [SerializeField] protected float maxDistance = 4f;

        [Tooltip("Maximum angle from the view center in degrees; <= 0 means proximity-only.")]
        [SerializeField] protected float maxViewAngle = 30f;

        [SerializeField] protected bool requireLineOfSight = true;

        [SerializeField] protected int priority = 0;

        [Tooltip("Whether a desktop left-click can also activate this target.")]
        [SerializeField] protected bool allowDesktopClick = false;

        [Header("Prompt")]
        [Tooltip("Desktop wording after the verb, e.g. 'to listen'. Full prompt becomes 'Press E to listen'.")]
        [SerializeField] protected string desktopPromptSuffix = "to interact";

        [Tooltip("Quest wording, e.g. 'Interact to listen'. Leave blank to derive from the desktop suffix.")]
        [SerializeField] protected string xrPromptOverride = "";

        [Tooltip("Optional world-space prompt object shown while this target is focused.")]
        [SerializeField] protected GameObject worldPromptRoot;

        Collider[] ownColliders;

        public virtual Vector3 FocusPoint =>
            (focusPoint != null ? focusPoint : transform).position;

        public virtual float MaxDistance => maxDistance;
        public virtual float MaxViewAngle => maxViewAngle;
        public virtual bool RequireLineOfSight => requireLineOfSight;
        public virtual int Priority => priority;
        public virtual bool AllowDesktopClick => allowDesktopClick;
        public virtual bool IsAvailable => isActiveAndEnabled;
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

        public virtual string GetPrompt(bool xr)
        {
            if (xr)
                return string.IsNullOrEmpty(xrPromptOverride)
                    ? $"Interact {desktopPromptSuffix}".Trim()
                    : xrPromptOverride;
            return $"Press E {desktopPromptSuffix}".Trim();
        }

        public virtual void OnFocusChanged(bool focused)
        {
            if (worldPromptRoot != null)
                worldPromptRoot.SetActive(focused);
        }

        public abstract void OnInteract(InteractionActivation activation);

        /// <summary>Quest relay entry point (wire XRSimpleInteractable.selectEntered here).</summary>
        public void OnXRSelect()
        {
            if (InteractionRouter.Instance != null)
                InteractionRouter.Instance.RequestXRSelect(this);
            else
                OnInteract(InteractionActivation.XRSelect);
        }

        protected virtual void OnEnable() => InteractionRouter.Register(this);

        protected virtual void OnDisable()
        {
            InteractionRouter.Unregister(this);
            if (worldPromptRoot != null)
                worldPromptRoot.SetActive(false);
        }
    }
}
