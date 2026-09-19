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
        static readonly List<DesktopInputState> desktopSuspendedInputs = new List<DesktopInputState>();
        static readonly List<StarterAssets.FirstPersonController> desktopSuspendedControllers =
            new List<StarterAssets.FirstPersonController>();
        static readonly List<Behaviour> xrSuspendedLocomotion = new List<Behaviour>();
        static bool desktopCursorCaptured;
        static CursorLockMode desktopPreviousCursorLock;
        static bool desktopPreviousCursorVisible;

        struct DesktopInputState
        {
            public StarterAssets.StarterAssetsInputs Inputs;
            public bool CursorLocked;
            public bool CursorInputForLook;
        }

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

            if (suspended)
            {
                if (!desktopCursorCaptured)
                {
                    desktopPreviousCursorLock = Cursor.lockState;
                    desktopPreviousCursorVisible = Cursor.visible;
                    desktopCursorCaptured = true;
                }

                foreach (var inputs in Object.FindObjectsByType<StarterAssets.StarterAssetsInputs>(
                             FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (inputs == null || !inputs.enabled)
                        continue;

                    desktopSuspendedInputs.Add(new DesktopInputState
                    {
                        Inputs = inputs,
                        CursorLocked = inputs.cursorLocked,
                        CursorInputForLook = inputs.cursorInputForLook,
                    });
                    inputs.enabled = false;
                    inputs.cursorLocked = false;
                    inputs.cursorInputForLook = false;
                    inputs.move = Vector2.zero;
                    inputs.look = Vector2.zero;
                    inputs.jump = false;
                    inputs.sprint = false;
                }

                foreach (var controller in Object.FindObjectsByType<StarterAssets.FirstPersonController>(
                             FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (controller == null || !controller.enabled)
                        continue;
                    controller.enabled = false;
                    desktopSuspendedControllers.Add(controller);
                }

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            foreach (DesktopInputState state in desktopSuspendedInputs)
            {
                if (state.Inputs == null)
                    continue;
                state.Inputs.cursorLocked = state.CursorLocked;
                state.Inputs.cursorInputForLook = state.CursorInputForLook;
                state.Inputs.enabled = true;
            }
            desktopSuspendedInputs.Clear();

            foreach (StarterAssets.FirstPersonController controller in desktopSuspendedControllers)
                if (controller != null)
                    controller.enabled = true;
            desktopSuspendedControllers.Clear();

            if (desktopCursorCaptured)
            {
                Cursor.lockState = desktopPreviousCursorLock;
                Cursor.visible = desktopPreviousCursorVisible;
                desktopCursorCaptured = false;
            }
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
            desktopSuspendedInputs.Clear();
            desktopSuspendedControllers.Clear();
            xrSuspendedLocomotion.Clear();
            desktopCursorCaptured = false;
        }
    }
}
