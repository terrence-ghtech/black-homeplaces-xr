using System.Collections.Generic;
using UnityEngine;

namespace BCaT.Production.Shell
{
    /// <summary>
    /// Reference-counted suspension of the desktop player controls and cursor
    /// lock, used by the menus, kiosk reset, and reset flows. Multiple owners
    /// may suspend simultaneously; controls resume only when the last owner
    /// releases. Exhibit-specific control disabling (e.g. Black Kitchen entry)
    /// is intentionally left untouched — this gate only adds the shell layer.
    /// </summary>
    public static class PlayerControlGate
    {
        static readonly HashSet<object> holds = new HashSet<object>();
        static readonly List<Behaviour> xrSuspendedLocomotion = new List<Behaviour>();

        public static bool IsSuspended => holds.Count > 0;

        public static void Suspend(object owner)
        {
            if (owner == null) return;
            bool wasSuspended = IsSuspended;
            holds.Add(owner);
            if (!wasSuspended)
                Apply(suspended: true);
        }

        public static void Resume(object owner)
        {
            if (owner == null) return;
            holds.Remove(owner);
            if (!IsSuspended)
                Apply(suspended: false);
        }

        public static void ForceResumeAll()
        {
            holds.Clear();
            Apply(suspended: false);
        }

        static void Apply(bool suspended)
        {
            if (PlatformCapabilities.IsQuestConfiguration || PlatformCapabilities.IsXRActive)
            {
                // Quest: suspend rig locomotion (move/turn/teleport providers)
                // while leaving head tracking and controller/UI interaction
                // untouched. Only behaviours this gate disabled are restored,
                // so exhibit-owned suspension (e.g. onboarding) stays in charge
                // of anything it disabled itself.
                ApplyXRLocomotion(suspended);
                return;
            }

            foreach (var inputs in Object.FindObjectsByType<StarterAssets.StarterAssetsInputs>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                inputs.enabled = !suspended;
                // Keep the component's own focus handler consistent with us.
                inputs.cursorLocked = !suspended;
                inputs.cursorInputForLook = !suspended;
                if (suspended)
                {
                    inputs.move = Vector2.zero;
                    inputs.look = Vector2.zero;
                    inputs.jump = false;
                    inputs.sprint = false;
                }
            }

            foreach (var fpc in Object.FindObjectsByType<StarterAssets.FirstPersonController>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                fpc.enabled = !suspended;

            Cursor.lockState = suspended ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = suspended;
        }

        static void ApplyXRLocomotion(bool suspended)
        {
            if (suspended)
            {
                foreach (var behaviour in Object.FindObjectsByType<Behaviour>(
                             FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (behaviour == null || !behaviour.enabled || !IsXRLocomotionBehaviour(behaviour))
                        continue;

                    behaviour.enabled = false;
                    xrSuspendedLocomotion.Add(behaviour);
                }
                return;
            }

            foreach (var behaviour in xrSuspendedLocomotion)
                if (behaviour != null)
                    behaviour.enabled = true;
            xrSuspendedLocomotion.Clear();
        }

        static bool IsXRLocomotionBehaviour(Behaviour behaviour)
        {
            // Same provider families the opening onboarding suspends; kept
            // name-based so XRI package types stay out of this assembly's
            // compile-time surface.
            //
            // GravityProvider is included because it also moves the rig ROOT:
            // suspending only the input-driven providers left gravity free to
            // settle or sink the XR Origin away from the authored spawn while
            // the player was held in place. Head tracking is unaffected either
            // way -- it moves the camera inside the rig, not the rig.
            string name = behaviour.GetType().Name;
            return name.Contains("MoveProvider") ||
                   name.Contains("TurnProvider") ||
                   name.Contains("TeleportationProvider") ||
                   name.Contains("ClimbProvider") ||
                   name.Contains("JumpProvider") ||
                   name.Contains("GravityProvider");
        }

        /// <summary>
        /// Re-assert the current state onto a freshly loaded scene's rig
        /// (called by the bootstrap on scene load).
        /// </summary>
        public static void Reapply() => Apply(IsSuspended);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            holds.Clear();
            xrSuspendedLocomotion.Clear();
        }
    }
}
