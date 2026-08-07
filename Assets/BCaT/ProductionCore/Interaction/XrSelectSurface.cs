using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace BCaT.Production.Interaction
{
    /// <summary>
    /// Makes an interactable reachable by the Quest controller rays, by building
    /// its XR aim surface at runtime instead of requiring a hand-authored twin
    /// object.
    ///
    /// Why any of this is needed: both XRI casters ignore trigger colliders
    /// (SphereInteractionCaster.physicsTriggerInteraction = Ignore for near
    /// casting, CurveInteractionCaster.raycastTriggerInteraction = Ignore for
    /// far casting). Most exhibits author their interaction shell as a trigger
    /// volume for the desktop proximity/aim path, which makes them completely
    /// invisible to the controller ray: no hover, no prompt, no select. The
    /// established fix was a sibling '*_QuestXRSelect' object carrying a
    /// non-trigger collider, an XRSimpleInteractable and a relay — hand-authored
    /// per interactable, with bounds to keep in sync and a silent Quest-only
    /// failure whenever it was forgotten.
    ///
    /// This component collapses that to one component on the interactable
    /// itself:
    ///  * Desktop: disables itself in Awake and creates nothing, so desktop
    ///    raycasts, line-of-sight tests and collision are bit-for-bit unchanged.
    ///  * Quest: mirrors each source collider with a NON-trigger collider that
    ///    sets excludeLayers to every layer and includeLayers to none. That
    ///    suppresses contact generation while leaving scene queries (and
    ///    therefore the XRI casters) hitting it normally — the configuration
    ///    verified empirically for QuestXrSelectCollider — so it is a pure aim
    ///    target and can never become an invisible wall.
    ///
    /// Dispatch goes through the router (or the active exclusive zone), so XR
    /// selection obeys the same availability, blocking and cooldown rules as
    /// desktop.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class XrSelectSurface : MonoBehaviour
    {
        [Tooltip("Colliders to mirror. Empty means every collider on this object and its children.")]
        [SerializeField] private Collider[] sourceColliders;

        [Tooltip("Uniform expansion of the mirrored collider, in metres. Small positive values make " +
                 "a thin exhibit easier to hit with a controller ray.")]
        [SerializeField] private float padding = 0f;

        [Tooltip("Informational: which interaction this surface forwards to.")]
        [SerializeField] private string forwardsTo;

        const string SurfaceName = "XrSelectSurface (runtime)";

        readonly List<GameObject> created = new List<GameObject>();
        IInteractionTarget owner;

        public IInteractionTarget Owner => owner;

        void Awake()
        {
            if (!BCaTPlatform.IsQuest)
            {
                // Desktop: contribute nothing at all.
                enabled = false;
                return;
            }

            owner = ResolveOwner();
            if (owner == null)
            {
                Debug.LogWarning($"[XrSelectSurface] '{name}' found no IInteractionTarget on itself or " +
                                 "an ancestor; nothing to forward to. Remove the component or move it " +
                                 "onto the interactable.");
                enabled = false;
                return;
            }

            foreach (Collider source in ResolveSourceColliders())
                Build(source);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log($"[XrSelectSurface] '{name}' built {created.Count} XR aim surface(s) for " +
                      $"'{owner.GetType().Name}'{(string.IsNullOrEmpty(forwardsTo) ? "" : " (" + forwardsTo + ")")}.");
#endif
        }

        void OnDestroy()
        {
            foreach (GameObject go in created)
                if (go != null)
                    Destroy(go);
            created.Clear();
        }

        IInteractionTarget ResolveOwner()
        {
            foreach (MonoBehaviour behaviour in GetComponents<MonoBehaviour>())
                if (behaviour is IInteractionTarget own)
                    return own;

            foreach (MonoBehaviour behaviour in GetComponentsInParent<MonoBehaviour>(true))
                if (behaviour is IInteractionTarget parent)
                    return parent;

            return null;
        }

        IEnumerable<Collider> ResolveSourceColliders()
        {
            if (sourceColliders != null && sourceColliders.Length > 0)
            {
                foreach (Collider explicitCollider in sourceColliders)
                    if (explicitCollider != null)
                        yield return explicitCollider;
                yield break;
            }

            foreach (Collider found in GetComponentsInChildren<Collider>(true))
            {
                if (found == null)
                    continue;

                // Never mirror another surface's own collider.
                if (found.transform.name == SurfaceName)
                    continue;

                yield return found;
            }
        }

        /// <summary>
        /// Mirror one source collider with an aim-only twin. The twin is
        /// parented to the source's own transform at identity, so box/sphere
        /// dimensions transfer exactly with no scale maths.
        /// </summary>
        void Build(Collider source)
        {
            var host = new GameObject(SurfaceName);
            host.transform.SetParent(source.transform, worldPositionStays: false);
            host.transform.localPosition = Vector3.zero;
            host.transform.localRotation = Quaternion.identity;
            host.transform.localScale = Vector3.one;
            host.layer = source.gameObject.layer;

            Collider mirrored = MirrorShape(source, host);
            if (mirrored == null)
            {
                Destroy(host);
                return;
            }

            mirrored.isTrigger = false;
            // Aim target only: never generate contacts with the player rig or
            // anything else, while remaining visible to scene queries.
            mirrored.excludeLayers = ~0;
            mirrored.includeLayers = 0;

            var interactable = host.AddComponent<XRSimpleInteractable>();
            interactable.colliders.Clear();
            interactable.colliders.Add(mirrored);
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
            interactable.selectEntered.AddListener(OnSelectEntered);

            created.Add(host);
        }

        Collider MirrorShape(Collider source, GameObject host)
        {
            switch (source)
            {
                case BoxCollider box:
                {
                    var mirror = host.AddComponent<BoxCollider>();
                    mirror.center = box.center;
                    mirror.size = box.size + Vector3.one * (padding * 2f);
                    return mirror;
                }
                case SphereCollider sphere:
                {
                    var mirror = host.AddComponent<SphereCollider>();
                    mirror.center = sphere.center;
                    mirror.radius = sphere.radius + padding;
                    return mirror;
                }
                case CapsuleCollider capsule:
                {
                    var mirror = host.AddComponent<CapsuleCollider>();
                    mirror.center = capsule.center;
                    mirror.radius = capsule.radius + padding;
                    mirror.height = capsule.height + padding * 2f;
                    mirror.direction = capsule.direction;
                    return mirror;
                }
                default:
                {
                    // Mesh and terrain colliders: approximate with a box around
                    // the source bounds, expressed in the source's local space.
                    Bounds worldBounds = source.bounds;
                    var mirror = host.AddComponent<BoxCollider>();
                    mirror.center = host.transform.InverseTransformPoint(worldBounds.center);
                    Vector3 lossyScale = host.transform.lossyScale;
                    mirror.size = new Vector3(
                        SafeDivide(worldBounds.size.x, lossyScale.x),
                        SafeDivide(worldBounds.size.y, lossyScale.y),
                        SafeDivide(worldBounds.size.z, lossyScale.z)) + Vector3.one * (padding * 2f);
                    return mirror;
                }
            }
        }

        static float SafeDivide(float value, float divisor) =>
            Mathf.Approximately(divisor, 0f) ? value : value / Mathf.Abs(divisor);

        // ---- Dispatch --------------------------------------------------------

        void OnHoverEntered(HoverEnterEventArgs args)
        {
            if (owner == null || InteractionRouter.Instance == null)
                return;
            InteractionRouter.Instance.RequestXRHover(args?.interactableObject as Object, owner);
        }

        void OnHoverExited(HoverExitEventArgs args)
        {
            if (InteractionRouter.Instance == null)
                return;
            InteractionRouter.Instance.ClearXRHover(args?.interactableObject as Object);
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (owner == null)
                return;

            if (InteractionRouter.Instance != null)
            {
                InteractionRouter.Instance.RequestXRSelect(owner);
                return;
            }

            // No router (should not happen in a booted app): still honour the
            // interaction rather than silently dropping it.
            owner.OnInteract(InteractionActivation.XRSelect);
        }
    }
}
