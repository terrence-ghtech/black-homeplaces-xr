using System;
using System.Collections.Generic;
using UnityEngine;

namespace BCaT.Production.Interaction
{
    /// <summary>Why world interaction is currently suppressed.</summary>
    [Flags]
    public enum InteractionBlockReason
    {
        None = 0,
        Menu = 1 << 0,          // main/pause/settings menu open
        Modal = 1 << 1,         // focused exhibit interface (reader, slideshow, exit modal)
        Loading = 1 << 2,       // loading screen active
        Transition = 1 << 3,    // scene transition in progress
        Media = 1 << 4,         // focused media interface that must own input
        PlayerControl = 1 << 5, // player controls intentionally suspended (reset, kiosk reset)
    }

    /// <summary>
    /// Central interaction blocking state (the InteractionStateController /
    /// InteractionBlocker of the production architecture). Anything that opens a
    /// menu, modal, or focused interface registers itself here; the
    /// InteractionRouter refuses to select targets or dispatch interaction input
    /// while any blocker is active. Blockers may register a close action so the
    /// kiosk reset can force-close every open interface through one call.
    /// </summary>
    public static class InteractionState
    {
        sealed class Blocker
        {
            public InteractionBlockReason Reason;
            public Action ForceClose;
        }

        static readonly Dictionary<object, Blocker> blockers = new Dictionary<object, Blocker>();

        public static event Action Changed;

        /// <summary>Register (or update) a blocker owned by <paramref name="owner"/>.</summary>
        public static void Block(object owner, InteractionBlockReason reason, Action forceClose = null)
        {
            if (owner == null) return;
            blockers[owner] = new Blocker { Reason = reason, ForceClose = forceClose };
            Changed?.Invoke();
        }

        public static void Unblock(object owner)
        {
            if (owner == null) return;
            if (blockers.Remove(owner))
                Changed?.Invoke();
        }

        /// <summary>
        /// True when interaction should be suppressed. Includes the scene
        /// transition flag from the existing transition system so callers do not
        /// need to consult both.
        /// </summary>
        public static bool IsBlocked =>
            blockers.Count > 0 || SceneTransitionState.IsTransitionInProgress;

        public static InteractionBlockReason ActiveReasons
        {
            get
            {
                var reasons = InteractionBlockReason.None;
                foreach (var b in blockers.Values)
                    reasons |= b.Reason;
                if (SceneTransitionState.IsTransitionInProgress)
                    reasons |= InteractionBlockReason.Transition;
                return reasons;
            }
        }

        public static bool HasReason(InteractionBlockReason reason) =>
            (ActiveReasons & reason) != 0;

        /// <summary>
        /// Force-close every blocking interface that registered a close action
        /// (used by the kiosk inactivity reset), then clear all blockers.
        /// Close actions are invoked defensively; one failing interface must not
        /// prevent the others from closing.
        /// </summary>
        public static void ForceCloseAll()
        {
            var snapshot = new List<KeyValuePair<object, Blocker>>(blockers);
            foreach (var pair in snapshot)
            {
                try { pair.Value.ForceClose?.Invoke(); }
                catch (Exception e)
                {
                    Debug.LogError($"[InteractionState] Force-close of blocker '{pair.Key}' failed: {e}");
                }
                blockers.Remove(pair.Key);
            }
            Changed?.Invoke();
        }

        /// <summary>Editor/domain-reload hygiene: statics survive disabled domain reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            blockers.Clear();
            Changed = null;
        }
    }
}
