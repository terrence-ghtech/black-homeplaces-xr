using System;
using UnityEngine;

namespace BCaT.Production.Interaction
{
    /// <summary>
    /// Owns the one focused exhibit UI that may be open at a time. Input and
    /// target selection remain the responsibility of InteractionRouter.
    /// </summary>
    public sealed class FocusedExhibitCoordinator : MonoBehaviour
    {
        public static FocusedExhibitCoordinator Instance { get; private set; }

        IFocusedExhibit currentExhibit;
        bool transitionInProgress;

        public IFocusedExhibit CurrentExhibit
        {
            get
            {
                ClearDestroyedCurrent();
                return currentExhibit;
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        void Update() => ClearDestroyedCurrent();

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void RequestOpen(IFocusedExhibit requestedExhibit)
        {
            ClearDestroyedCurrent();

            if (!IsAlive(requestedExhibit))
                return;

            if (ReferenceEquals(currentExhibit, requestedExhibit))
                return;

            if (transitionInProgress)
            {
                Debug.LogWarning(
                    $"[FocusedExhibitCoordinator] Open request for '{Describe(requestedExhibit)}' ignored while another focused-exhibit transition is running.");
                return;
            }

            transitionInProgress = true;
            try
            {
                IFocusedExhibit previousExhibit = currentExhibit;
                if (IsAlive(previousExhibit))
                {
                    try
                    {
                        previousExhibit.Close();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError(
                            $"[FocusedExhibitCoordinator] Failed to close current exhibit '{Describe(previousExhibit)}' before opening '{Describe(requestedExhibit)}': {exception}");
                    }

                    if (IsAlive(previousExhibit) && previousExhibit.IsOpen)
                    {
                        currentExhibit = previousExhibit;
                        Debug.LogError(
                            $"[FocusedExhibitCoordinator] Current exhibit '{Describe(previousExhibit)}' remained open; requested exhibit '{Describe(requestedExhibit)}' was not opened.");
                        return;
                    }
                }

                currentExhibit = null;

                if (!IsAlive(requestedExhibit))
                    return;

                try
                {
                    requestedExhibit.Open();
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"[FocusedExhibitCoordinator] Requested exhibit '{Describe(requestedExhibit)}' threw while opening: {exception}");
                    return;
                }

                if (IsAlive(requestedExhibit) && requestedExhibit.IsOpen)
                {
                    currentExhibit = requestedExhibit;
                    return;
                }

                Debug.LogWarning(
                    $"[FocusedExhibitCoordinator] Requested exhibit '{Describe(requestedExhibit)}' did not report open; no focused exhibit was retained.");
            }
            finally
            {
                transitionInProgress = false;
            }
        }

        public void RequestClose(IFocusedExhibit exhibit)
        {
            ClearDestroyedCurrent();
            if (!IsAlive(exhibit))
                return;

            try
            {
                exhibit.Close();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[FocusedExhibitCoordinator] Failed to close exhibit '{Describe(exhibit)}': {exception}");
            }

            if (!exhibit.IsOpen)
            {
                NotifyClosed(exhibit);
                return;
            }

            Debug.LogError(
                $"[FocusedExhibitCoordinator] Exhibit '{Describe(exhibit)}' remained open after a close request.");
        }

        public void CloseCurrent()
        {
            ClearDestroyedCurrent();
            if (IsAlive(currentExhibit))
                RequestClose(currentExhibit);
        }

        public bool IsReplacementTarget(IInteractionTarget target, IFocusedExhibit current)
        {
            if (target == null || !IsAlive(current))
                return false;

            IFocusedExhibit targetExhibit = target as IFocusedExhibit;
            if (targetExhibit == null && target is IFocusedExhibitTarget adapter)
                targetExhibit = adapter.FocusedExhibit;

            return IsAlive(targetExhibit) && !ReferenceEquals(targetExhibit, current);
        }

        public void NotifyClosed(IFocusedExhibit exhibit)
        {
            if (!ReferenceEquals(currentExhibit, exhibit))
                return;

            if (IsAlive(exhibit) && exhibit.IsOpen)
            {
                Debug.LogWarning(
                    $"[FocusedExhibitCoordinator] Ignored close notification from '{Describe(exhibit)}' because it still reports open.");
                return;
            }

            currentExhibit = null;
        }

        void ClearDestroyedCurrent()
        {
            if (currentExhibit == null || IsAlive(currentExhibit))
                return;

            string staleName = Describe(currentExhibit);
            currentExhibit = null;
            Debug.LogWarning(
                $"[FocusedExhibitCoordinator] Cleared stale current exhibit '{staleName}' after its Unity object was destroyed.");
        }

        static bool IsAlive(IFocusedExhibit exhibit)
        {
            if (exhibit == null)
                return false;

            return !(exhibit is UnityEngine.Object unityObject) || unityObject != null;
        }

        static string Describe(IFocusedExhibit exhibit)
        {
            if (exhibit == null)
                return "<null>";
            if (exhibit is Component component)
                return component != null ? component.gameObject.name : "<destroyed Unity object>";
            return exhibit.ToString();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;
    }
}
