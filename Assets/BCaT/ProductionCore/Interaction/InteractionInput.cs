using UnityEngine;
using UnityEngine.InputSystem;

namespace BCaT.Production.Interaction
{
    /// <summary>Platform input provider abstraction for the interaction router.</summary>
    public interface IInteractionInputProvider
    {
        /// <summary>True on the frame the primary interact action was pressed.</summary>
        bool InteractPressedThisFrame { get; }

        /// <summary>True on the frame the pointer-select action was pressed (desktop left click).</summary>
        bool ClickPressedThisFrame { get; }
    }

    /// <summary>
    /// Desktop keyboard/mouse provider. This is the single sanctioned place in
    /// production code where the world-interaction key is read; exhibit scripts
    /// must not poll Keyboard.current for interaction themselves.
    /// </summary>
    public sealed class DesktopInteractionInputProvider : IInteractionInputProvider
    {
        public Key InteractKey = Key.E;

        public bool InteractPressedThisFrame
        {
            get
            {
                var keyboard = Keyboard.current;
                return keyboard != null && keyboard[InteractKey].wasPressedThisFrame;
            }
        }

        public bool ClickPressedThisFrame
        {
            get
            {
                var mouse = Mouse.current;
                return mouse != null && mouse.leftButton.wasPressedThisFrame;
            }
        }
    }

    /// <summary>
    /// Quest provider. Quest interaction is event-driven through the XR
    /// Interaction Toolkit (XRSimpleInteractable select events call
    /// InteractionRouter.RequestXRSelect), so per-frame polling reports nothing.
    /// </summary>
    public sealed class QuestInteractionInputProvider : IInteractionInputProvider
    {
        public bool InteractPressedThisFrame => false;
        public bool ClickPressedThisFrame => false;
    }

    /// <summary>
    /// Central keyboard access for focused/modal interfaces (article readers,
    /// slideshows, exit modals). Modal interfaces read navigation keys through
    /// this class instead of Keyboard.current so modal input remains auditable
    /// and consistent. World interaction must go through the router, never here.
    /// </summary>
    public static class FocusedUiInput
    {
        static Keyboard K => Keyboard.current;

        public static bool CancelPressed =>
            K != null && K.escapeKey.wasPressedThisFrame;

        public static bool SubmitPressed =>
            K != null && (K.enterKey.wasPressedThisFrame || K.numpadEnterKey.wasPressedThisFrame);

        public static bool InteractPressed =>
            K != null && K.eKey.wasPressedThisFrame;

        public static bool InteractHeld =>
            K != null && K.eKey.isPressed;

        public static bool NextPressed =>
            K != null && (K.rightArrowKey.wasPressedThisFrame || K.dKey.wasPressedThisFrame);

        public static bool PreviousPressed =>
            K != null && (K.leftArrowKey.wasPressedThisFrame || K.aKey.wasPressedThisFrame);

        /// <summary>Arbitrary extra key for exhibit-specific modal shortcuts (documented per exhibit).</summary>
        public static bool KeyPressed(Key key) =>
            K != null && K[key].wasPressedThisFrame;

        public static bool KeyHeld(Key key) =>
            K != null && K[key].isPressed;

        /// <summary>Continuous scroll input for modal text viewers (+1 up, -1 down, ±8 page).</summary>
        public static float ScrollStep()
        {
            if (K == null) return 0f;
            float step = 0f;
            if (K.upArrowKey.isPressed) step = 1f;
            if (K.downArrowKey.isPressed) step = -1f;
            if (K.pageUpKey.wasPressedThisFrame) step = 8f;
            if (K.pageDownKey.wasPressedThisFrame) step = -8f;
            return step;
        }
    }
}
