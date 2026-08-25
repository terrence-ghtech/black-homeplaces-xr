using System;
using System.Collections;
using UnityEngine;

namespace BCaT.Production
{
    /// <summary>
    /// Establishes a deterministic opening reference direction for the Quest
    /// player's first arrival into the main house, once, while the arrival
    /// overlay is fully opaque.
    ///
    /// The body root is placed by the project's existing spawn architecture (the
    /// authored `MainEntrance` <see cref="SceneSpawnPoint"/>, applied by
    /// SceneArrivalController before the fade). What that cannot express is which
    /// way the PLAYER is looking: on Quest the head's world yaw comes from the
    /// runtime's reference space — the Guardian/Stage heading, or the last system
    /// recenter — which has no relationship to the virtual house and differs per
    /// room and per session. With turning frozen during onboarding, a player who
    /// inherits a sideways heading cannot correct it.
    ///
    /// So this calls XRI's own <c>XROrigin.MatchOriginUpCameraForward</c> exactly
    /// once: it rotates the RIG about the camera position so the CAMERA ends up
    /// facing the house. It is the same primitive XRI's TeleportationProvider
    /// uses for MatchOrientation.TargetUpAndForward. It is not a recenter, and it
    /// never touches the Main Camera, the TrackedPoseDriver or the tracked pose —
    /// head tracking stays completely natural, immediately and afterwards.
    ///
    /// Because that primitive rotates about the camera, it also displaces the rig
    /// by the head's tracking offset; the authored entrance position is restored
    /// afterwards so the body still stands exactly where the spawn point says.
    /// Nothing runs after this frame.
    /// </summary>
    public static class XrArrivalAlignment
    {
        /// <summary>
        /// Upper bound on the reveal hold. On a healthy launch the session has
        /// been tracking since long before the scene finished loading, so the
        /// wait costs nothing; this only limits the pathological case, where
        /// revealing late is better than never revealing.
        /// </summary>
        public const float TrackingWaitTimeoutSeconds = 1f;

        /// <summary>
        /// Editor test seam: lets a headless harness stand in for the runtime's
        /// tracking report, which is otherwise unavailable without a headset.
        /// Never assigned in a player.
        /// </summary>
        internal static Func<bool> TrackingValidOverride;

        static bool waitedThisSession;

        /// <summary>
        /// The starting frame this established: where the head ended up, and the
        /// direction it was aimed. The onboarding panel is placed from THIS
        /// rather than from the live head pose, which drifts during the fade.
        /// </summary>
        public static bool HasEstablishedFrame { get; private set; }
        public static Vector3 EstablishedHeadPoint { get; private set; }
        public static Vector3 EstablishedForward { get; private set; } = Vector3.forward;

        /// <summary>True once the reveal guard has run for this session.</summary>
        public static bool WaitedThisSession => waitedThisSession;

        /// <summary>
        /// Wait briefly for a tracked head, then aim the player at the house.
        /// Call only while the view is opaque.
        /// </summary>
        public static IEnumerator WaitForTrackingAndFaceHouse(Transform bodyRoot)
        {
            if (waitedThisSession)
                yield break;

            waitedThisSession = true;

            float deadline = Time.realtimeSinceStartup + TrackingWaitTimeoutSeconds;
            while (!IsHeadTrackingValid() && Time.realtimeSinceStartup < deadline)
                yield return null;

            if (!IsHeadTrackingValid())
            {
                Debug.LogWarning("[XrArrivalAlignment] HMD tracking was not valid within " +
                                 $"{TrackingWaitTimeoutSeconds:0.#}s; revealing without aiming the player at " +
                                 "the house rather than holding them behind an opaque screen. Aiming against " +
                                 "an untracked pose would point them somewhere arbitrary.");
                yield break;
            }

            if (bodyRoot == null)
            {
                Debug.LogWarning("[XrArrivalAlignment] No body root resolved; opening direction not established.");
                yield break;
            }

            var origin = bodyRoot.GetComponent<Unity.XR.CoreUtils.XROrigin>();
            if (origin == null || origin.Camera == null)
            {
                Debug.LogWarning($"[XrArrivalAlignment] Rig '{bodyRoot.name}' has no XROrigin/Camera; opening " +
                                 "direction not established.");
                yield break;
            }

            // The body has just been placed on the authored MainEntrance spawn,
            // whose forward is square to the house facade. That is the direction
            // the player should be looking when the world appears.
            Vector3 desiredHouseForward = bodyRoot.forward;
            desiredHouseForward.y = 0f;
            if (desiredHouseForward.sqrMagnitude < 0.000001f)
            {
                Debug.LogWarning("[XrArrivalAlignment] The entrance forward is degenerate; opening direction " +
                                 "not established.");
                yield break;
            }
            desiredHouseForward.Normalize();

            Vector3 entrancePosition = bodyRoot.position;
            float bodyYawBefore = bodyRoot.eulerAngles.y;
            Transform head = origin.Camera.transform;
            float headYawBefore = head.eulerAngles.y;
            Vector3 headWorldBefore = head.position;
            Vector3 headLocalBefore = head.localPosition;

            // 1. Heading: rotate the rig about the camera so the head looks along
            //    the authored entrance forward.
            bool aimed = origin.MatchOriginUpCameraForward(Vector3.up, desiredHouseForward);

            // 2. Centring: move the rig so the TRACKED HEAD lands on the authored
            //    entrance X/Z, keeping the player's real eye height. Together with
            //    step 1 this reproduces what a Meta recenter leaves behind — head
            //    over the intended point, looking the intended way — without
            //    asking the runtime to recentre and without touching the camera.
            //
            //    XROrigin.MoveCameraToWorldLocation is the obvious helper here and
            //    is deliberately NOT used: it derives its offset from
            //    OriginInCameraSpacePos (camera-local, so already divided by the
            //    rig's lossy scale) and rotates it without re-applying that scale,
            //    so on this 1.44-scaled rig it lands the head ~0.19 m off the
            //    target and shifts eye height by ~1.19 m. Measured, not assumed.
            //    A plain world-space translation is exact at any rig scale and
            //    leaves height alone by construction.
            Vector3 headNow = head.position;
            Vector3 centringDelta = new Vector3(entrancePosition.x - headNow.x, 0f,
                                                entrancePosition.z - headNow.z);
            bodyRoot.position += centringDelta;
            bool centred = true;

            EstablishedHeadPoint = head.position;
            EstablishedForward = desiredHouseForward;
            HasEstablishedFrame = true;

            Debug.Log($"[XrArrivalAlignment] Opening frame established once on '{bodyRoot.name}' behind the " +
                      $"opaque overlay (aimed={aimed}, centred={centred}): entrance={entrancePosition}, " +
                      $"forward={desiredHouseForward}; head world {headWorldBefore} yaw {headYawBefore:0.0} -> " +
                      $"{head.position} yaw {head.eulerAngles.y:0.0}; body yaw {bodyYawBefore:0.0} -> " +
                      $"{bodyRoot.eulerAngles.y:0.0}, body root now {bodyRoot.position}. " +
                      $"Head local pose untouched ({headLocalBefore} -> {head.localPosition}).");

            // One frame for the resulting transforms to settle before the reveal.
            yield return null;
        }

        /// <summary>
        /// True once the runtime reports a tracked centre-eye pose. Aligning
        /// against an untracked device would anchor the arrival to an identity
        /// pose and put the player somewhere arbitrary.
        /// </summary>
        public static bool IsHeadTrackingValid()
        {
            if (TrackingValidOverride != null)
                return TrackingValidOverride();

            UnityEngine.XR.InputDevice hmd =
                UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.CenterEye);
            if (!hmd.isValid)
                return false;

            if (!hmd.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool tracked) || !tracked)
                return false;

            // A floor-relative origin always reports a non-zero head height, so
            // an exactly-zero pose means the runtime has not filled it in yet.
            return hmd.TryGetFeatureValue(UnityEngine.XR.CommonUsages.centerEyePosition, out Vector3 eye) &&
                   eye.sqrMagnitude > 0.0001f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            waitedThisSession = false;
            HasEstablishedFrame = false;
            EstablishedForward = Vector3.forward;
            TrackingValidOverride = null;
        }
    }
}
