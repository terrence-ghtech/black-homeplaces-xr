using BCaT.Production.Interaction;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class BlackKitchenXrSelectRelay : MonoBehaviour
{
    [SerializeField] private MonoBehaviour receiver;
    [SerializeField] private string methodName;
    [SerializeField] private XRSimpleInteractable interactable;

    private bool listenerRegistered;

    private void Awake()
    {
        if (interactable == null)
            interactable = GetComponent<XRSimpleInteractable>();

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.Log($"[BlackKitchenXrSelectRelay] Awake '{gameObject.name}' interactable='{(interactable != null ? interactable.name : "null")}' receiver='{(receiver != null ? receiver.name : "null")}' method='{methodName}'.");
#endif
    }

    private void OnEnable()
    {
        RegisterListener();
    }

    private void OnDisable()
    {
        UnregisterListener();
    }

    public void Configure(MonoBehaviour targetReceiver, string targetMethodName)
    {
        UnregisterListener();
        receiver = targetReceiver;
        methodName = targetMethodName;
        RegisterListener();
    }

    public bool TryResolveRouterTarget(out IInteractionTarget target)
    {
        target = receiver as IInteractionTarget;
        return target != null;
    }

    public bool TryResolveBlackKitchenAudio(out BlackKitchenAudioInteractable station)
    {
        station = receiver as BlackKitchenAudioInteractable;
        return station != null;
    }

    public bool TryResolveBlackKitchenExit(out BlackKitchenExperienceController controller)
    {
        controller = receiver as BlackKitchenExperienceController;
        return controller != null &&
               methodName == nameof(BlackKitchenExperienceController.OnXRExitSelect);
    }

    private void RegisterListener()
    {
        if (listenerRegistered || interactable == null)
            return;

        interactable.selectEntered.AddListener(OnSelectEntered);
        listenerRegistered = true;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.Log($"[BlackKitchenXrSelectRelay] Registered select listener on '{gameObject.name}'.");
#endif
    }

    private void UnregisterListener()
    {
        if (!listenerRegistered || interactable == null)
            return;

        interactable.selectEntered.RemoveListener(OnSelectEntered);
        listenerRegistered = false;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.Log($"[BlackKitchenXrSelectRelay] Unregistered select listener on '{gameObject.name}'.");
#endif
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.Log($"[BlackKitchenXrSelectRelay] SelectEntered '{gameObject.name}' receiver='{(receiver != null ? receiver.name : "null")}' method='{methodName}' interactor='{args?.interactorObject?.transform?.name ?? "unknown"}'.");
#endif
        if (receiver == null || string.IsNullOrEmpty(methodName))
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.LogWarning($"[BlackKitchenXrSelectRelay] Select ignored on '{gameObject.name}': missing receiver or method.");
#endif
            return;
        }

        if (receiver is BlackKitchenAudioInteractable station)
        {
            BlackKitchenInteractionManager manager = FindAnyObjectByType<BlackKitchenInteractionManager>();
            if (manager != null && manager.RequestXRSelect(station))
                return;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.LogWarning($"[BlackKitchenXrSelectRelay] Audio select was not accepted for '{station.name}'.");
#endif
        }

        if (receiver is BlackKitchenExperienceController && methodName == nameof(BlackKitchenExperienceController.OnXRExitSelect))
        {
            BlackKitchenInteractionManager manager = FindAnyObjectByType<BlackKitchenInteractionManager>();
            if (manager != null && manager.RequestXRExit())
                return;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.LogWarning("[BlackKitchenXrSelectRelay] Exit select was not accepted by BlackKitchenInteractionManager.");
#endif
        }

        if (receiver is IInteractionTarget target && InteractionRouter.Instance != null)
        {
            bool accepted = InteractionRouter.Instance.RequestXRSelect(target);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log($"[BlackKitchenXrSelectRelay] Router request for '{receiver.name}' accepted={accepted}.");
#endif
            return;
        }

        if (BCaT.Production.Interaction.InteractionState.IsBlocked)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.LogWarning($"[BlackKitchenXrSelectRelay] SendMessage suppressed on '{gameObject.name}': interaction blocked ({BCaT.Production.Interaction.InteractionState.ActiveReasons}).");
#endif
            return;
        }

        receiver.SendMessage(methodName, SendMessageOptions.DontRequireReceiver);
    }
}
