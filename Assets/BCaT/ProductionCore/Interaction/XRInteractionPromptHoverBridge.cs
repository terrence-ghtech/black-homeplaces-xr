using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace BCaT.Production.Interaction
{
    /// <summary>
    /// Visual-only bridge from XRI hover events to the shared InteractionRouter
    /// prompt. Selection remains owned by existing XRSimpleInteractable listeners.
    /// </summary>
    public sealed class XRInteractionPromptHoverBridge : MonoBehaviour
    {
        readonly HashSet<XRSimpleInteractable> subscribed = new HashSet<XRSimpleInteractable>();

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            RefreshSubscriptions();
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            foreach (var interactable in subscribed)
            {
                if (interactable == null)
                    continue;
                interactable.hoverEntered.RemoveListener(OnHoverEntered);
                interactable.hoverExited.RemoveListener(OnHoverExited);
            }
            subscribed.Clear();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => StartCoroutine(RefreshAfterSceneLoad());

        IEnumerator RefreshAfterSceneLoad()
        {
            yield return null;
            RefreshSubscriptions();
        }

        void RefreshSubscriptions()
        {
            foreach (var interactable in FindObjectsByType<XRSimpleInteractable>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (interactable == null || subscribed.Contains(interactable))
                    continue;

                interactable.hoverEntered.AddListener(OnHoverEntered);
                interactable.hoverExited.AddListener(OnHoverExited);
                subscribed.Add(interactable);
            }
        }

        void OnHoverEntered(HoverEnterEventArgs args)
        {
            var interactable = args?.interactableObject as XRSimpleInteractable;
            if (interactable == null)
                return;

            if (TryForwardBlackKitchenHover(interactable))
                return;

            IInteractionTarget target = ResolveRouterTarget(interactable);
            if (target != null && InteractionRouter.Instance != null)
                InteractionRouter.Instance.RequestXRHover(interactable, target);
        }

        void OnHoverExited(HoverExitEventArgs args)
        {
            var interactable = args?.interactableObject as XRSimpleInteractable;
            if (interactable == null)
                return;

            ClearBlackKitchenHover(interactable);
            if (InteractionRouter.Instance != null)
                InteractionRouter.Instance.ClearXRHover(interactable);
        }

        static bool TryForwardBlackKitchenHover(XRSimpleInteractable interactable)
        {
            var manager = FindAnyObjectByType<BlackKitchenInteractionManager>();
            if (manager == null)
                return false;

            var relay = interactable.GetComponent<BlackKitchenXrSelectRelay>();
            if (relay != null)
            {
                if (relay.TryResolveBlackKitchenAudio(out var station))
                {
                    manager.RequestXRHover(station);
                    return true;
                }

                if (relay.TryResolveBlackKitchenExit(out var experienceController))
                {
                    manager.RequestXRExitHover(experienceController);
                    return true;
                }
            }

            var directStation = interactable.GetComponent<BlackKitchenAudioInteractable>();
            if (directStation != null)
            {
                manager.RequestXRHover(directStation);
                return true;
            }

            return false;
        }

        static void ClearBlackKitchenHover(XRSimpleInteractable interactable)
        {
            var manager = FindAnyObjectByType<BlackKitchenInteractionManager>();
            if (manager == null)
                return;

            var relay = interactable.GetComponent<BlackKitchenXrSelectRelay>();
            if (relay != null)
            {
                if (relay.TryResolveBlackKitchenAudio(out var station))
                {
                    manager.ClearXRHover(station);
                    return;
                }

                if (relay.TryResolveBlackKitchenExit(out _))
                {
                    manager.ClearXRExitHover();
                    return;
                }
            }

            var directStation = interactable.GetComponent<BlackKitchenAudioInteractable>();
            if (directStation != null)
                manager.ClearXRHover(directStation);
        }

        static IInteractionTarget ResolveRouterTarget(XRSimpleInteractable interactable)
        {
            if (interactable.TryGetComponent<IInteractionTarget>(out var directTarget))
                return directTarget;

            foreach (var behaviour in interactable.GetComponents<MonoBehaviour>())
            {
                if (behaviour is IInteractionTarget target)
                    return target;
            }

            var relay = interactable.GetComponent<BlackKitchenXrSelectRelay>();
            if (relay != null && relay.TryResolveRouterTarget(out var relayTarget))
                return relayTarget;

            UnityEventBase selectEvent = interactable.selectEntered;
            int count = selectEvent.GetPersistentEventCount();
            for (int i = 0; i < count; i++)
            {
                Object targetObject = selectEvent.GetPersistentTarget(i);
                if (targetObject is IInteractionTarget eventTarget)
                    return eventTarget;
            }

            foreach (var behaviour in interactable.GetComponentsInParent<MonoBehaviour>(true))
            {
                if (behaviour is IInteractionTarget parentTarget)
                    return parentTarget;
            }

            return null;
        }
    }
}
