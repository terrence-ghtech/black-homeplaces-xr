using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using BCaT.Production.Interaction;

public class MeshellArticleNotebookOpener : MonoBehaviour
{
    private const string LogTag = "[MeshellNotebookOpener]";

    [SerializeField] private MeshellArticleReaderController reader;

    public bool IsOpen => reader != null && reader.IsOpen;
    public IFocusedExhibit FocusedExhibit => reader;

    public void Open()
    {
        Debug.Log($"{LogTag} Open invoked on '{name}'. Reader assigned={reader != null}.");
        if (reader != null)
            reader.OpenArticle(0);
    }

    public void Open(SelectEnterEventArgs args)
    {
        Debug.Log($"{LogTag} XR SelectEntered received on '{name}'. Interactable={args?.interactableObject?.transform?.name ?? "<null>"}.");
        IInteractionTarget target = GetComponent<MeshellArticleNotebookInputRouter>();
        if (target == null)
            target = GetComponentInParent<MeshellArticleNotebookInputRouter>();
        if (target == null)
            target = GetComponent<LindaLeaksPanelOpener>();
        if (target != null && InteractionRouter.Instance != null)
        {
            InteractionRouter.Instance.RequestXRSelect(target);
            return;
        }

        Open();
    }
}
