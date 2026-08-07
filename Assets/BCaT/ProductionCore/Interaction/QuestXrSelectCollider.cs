using UnityEngine;

namespace BCaT.Production.Interaction
{
    /// <summary>
    /// Marks a Quest-only XR select surface.
    ///
    /// Why this exists: both XRI casters on the Quest rig ignore trigger
    /// colliders (SphereInteractionCaster.physicsTriggerInteraction = Ignore for
    /// near casting, CurveInteractionCaster.raycastTriggerInteraction = Ignore
    /// for far casting). Most exhibits authored their interaction shell as a
    /// trigger volume for the desktop proximity/aim path, which made them
    /// completely invisible to the controller ray: no hover, no prompt, no
    /// select. The one video that always worked in headset (Sewing Room /
    /// "In My Sister's Room") worked because it has a dedicated child object
    /// with a NON-trigger collider — this component generalizes that fix.
    ///
    /// Two properties keep it safe:
    ///  * Quest-only. On desktop the whole object is deactivated in Awake before
    ///    any physics step, so desktop raycasts, line-of-sight tests, and
    ///    collision are bit-for-bit unchanged.
    ///  * Never blocks anything. The collider sets excludeLayers to every layer,
    ///    which suppresses contact generation while leaving scene queries
    ///    (Physics.Raycast / SphereCast, and therefore the XR casters) hitting
    ///    it normally — verified empirically on this Unity version. So it is a
    ///    pure aim target and cannot become an invisible wall.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuestXrSelectCollider : MonoBehaviour
    {
        [Tooltip("Informational: which exhibit interaction this surface forwards to.")]
        [SerializeField] private string forwardsTo;

        void Awake()
        {
            if (!PlatformCapabilities.UseXRPrompts)
            {
                // Desktop/editor play mode: remove this object from the scene
                // entirely so no desktop behavior can observe it.
                gameObject.SetActive(false);
                return;
            }

            foreach (var collider in GetComponents<Collider>())
            {
                collider.isTrigger = false;
                // Aim target only: never generate contacts with the player rig
                // or anything else.
                collider.excludeLayers = ~0;
                collider.includeLayers = 0;
            }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log($"[QuestXrSelectCollider] Active on '{name}' (forwards to {forwardsTo}).");
#endif
        }
    }
}
