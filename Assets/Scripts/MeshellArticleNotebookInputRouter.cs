using BCaT.Production.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Interaction target for the Meshell article notebook. Previously polled the
/// keyboard and raycast for the notebook's parent BoxCollider each press; the
/// central InteractionRouter now owns selection and input, preserving the
/// parent-collider pattern (all child colliders belong to this target, and the
/// router's line-of-sight test skips foreign trigger volumes the same way the
/// original raycast walk did).
/// </summary>
public class MeshellArticleNotebookInputRouter : MonoBehaviour, IInteractionTarget
{
    private const string LogTag = "[MeshellNotebookInput]";

#pragma warning disable 0414 // retained for scene-data compatibility; router owns input/camera now
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Key interactionKey = Key.E;
#pragma warning restore 0414
    [SerializeField] private float interactionDistance = 4f;

    private Collider[] ownColliders;

    // ---- IInteractionTarget --------------------------------------------

    public Vector3 FocusPoint => transform.position;
    public float MaxDistance => interactionDistance;
    public float MaxViewAngle => 16f;
    public bool RequireLineOfSight => true;
    public int Priority => 0;
    public bool IsAvailable => isActiveAndEnabled;
    public bool AllowDesktopClick => true;
    public bool Exists => this != null;

    public Collider[] OwnColliders
    {
        get
        {
            if (ownColliders == null)
                ownColliders = GetComponentsInChildren<Collider>(true);
            return ownColliders;
        }
    }

    public string GetPrompt(bool xr) => SharedInteractionPrompt.Format(xr, SharedInteractionVerb.Read, gameObject.name);

    public void OnFocusChanged(bool focused) { }

    public void OnInteract(InteractionActivation activation)
    {
        MeshellArticleNotebookOpener opener = GetComponent<MeshellArticleNotebookOpener>();
        Debug.Log($"{LogTag} Interaction dispatched to '{gameObject.name}'. Opener present={opener != null}.");
        if (opener != null)
            opener.Open();
    }

    // ---------------------------------------------------------------------

    private void OnEnable() => InteractionRouter.Register(this);

    private void OnDisable() => InteractionRouter.Unregister(this);
}
