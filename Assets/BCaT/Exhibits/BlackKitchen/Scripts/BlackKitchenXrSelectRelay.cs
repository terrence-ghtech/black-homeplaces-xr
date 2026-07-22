using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class BlackKitchenXrSelectRelay : MonoBehaviour
{
    [SerializeField] private MonoBehaviour receiver;
    [SerializeField] private string methodName;
    [SerializeField] private XRSimpleInteractable interactable;

    private void Awake()
    {
        if (interactable == null)
            interactable = GetComponent<XRSimpleInteractable>();
    }

    private void OnEnable()
    {
        if (interactable != null)
            interactable.selectEntered.AddListener(OnSelectEntered);
    }

    private void OnDisable()
    {
        if (interactable != null)
            interactable.selectEntered.RemoveListener(OnSelectEntered);
    }

    public void Configure(MonoBehaviour targetReceiver, string targetMethodName)
    {
        receiver = targetReceiver;
        methodName = targetMethodName;
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (receiver != null && !string.IsNullOrEmpty(methodName))
            receiver.SendMessage(methodName, SendMessageOptions.DontRequireReceiver);
    }
}
