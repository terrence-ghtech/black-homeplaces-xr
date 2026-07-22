using UnityEngine;
using UnityEngine.InputSystem;

public class MeshellArticleNotebookInputRouter : MonoBehaviour
{
    private const string LogTag = "[MeshellNotebookInput]";

    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactionDistance = 4f;
    [SerializeField] private Key interactionKey = Key.E;

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current[interactionKey].wasPressedThisFrame)
            return;

        if (playerCamera == null || !playerCamera.gameObject.activeInHierarchy)
            playerCamera = Camera.main;

        if (playerCamera == null)
        {
            Debug.Log($"{LogTag} E pressed, but no active player camera was found.");
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, interactionDistance, ~0, QueryTriggerInteraction.Collide);
        if (hits.Length == 0)
        {
            Debug.Log($"{LogTag} E pressed. Raycast hit nothing within {interactionDistance:0.##}m.");
            return;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        RaycastHit firstHit = hits[0];
        Debug.Log($"{LogTag} First collider hit: {GetPath(firstHit.collider.transform)} on GameObject '{firstHit.collider.gameObject.name}' at {firstHit.distance:0.###}m.");

        RaycastHit notebookHit = default;
        bool foundNotebookHit = false;
        foreach (RaycastHit hit in hits)
        {
            bool isNotebookChild = hit.collider != null && hit.collider.transform.IsChildOf(transform);
            Debug.Log($"{LogTag} Raycast candidate: {GetPath(hit.collider.transform)} distance={hit.distance:0.###}m childOfNotePads={isNotebookChild}.");

            if (!isNotebookChild)
                continue;

            notebookHit = hit;
            foundNotebookHit = true;
            break;
        }

        if (!foundNotebookHit)
        {
            Debug.Log($"{LogTag} No NotePads child collider was hit. Desktop interaction target remains blocked by '{firstHit.collider.gameObject.name}'.");
            return;
        }

        MeshellArticleNotebookOpener opener = GetComponent<MeshellArticleNotebookOpener>();
        Debug.Log($"{LogTag} Resolved notebook collider '{notebookHit.collider.gameObject.name}' upward to parent interaction object '{gameObject.name}'. Opener present={opener != null}.");

        if (opener == null)
            return;

        Debug.Log($"{LogTag} Invoking MeshellArticleNotebookOpener.Open from desktop E.");
        opener.Open();
    }

    private static string GetPath(Transform current)
    {
        if (current == null)
            return "<null>";

        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = $"{current.name}/{path}";
        }

        return path;
    }
}
