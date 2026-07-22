using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class MeshellArticleNotebookOpener : MonoBehaviour
{
    private const string LogTag = "[MeshellNotebookOpener]";

    [SerializeField] private MeshellArticleReaderController reader;

    public void Open()
    {
        Debug.Log($"{LogTag} Open invoked on '{name}'. Reader assigned={reader != null}.");
        if (reader != null)
            reader.OpenArticle(0);
    }

    public void Open(SelectEnterEventArgs args)
    {
        Debug.Log($"{LogTag} XR SelectEntered received on '{name}'. Interactable={args?.interactableObject?.transform?.name ?? "<null>"}.");
        Open();
    }
}
