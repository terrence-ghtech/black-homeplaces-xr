using BCaT.Production.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Linda Leaks artifact opener (video popup / photo album / article reader).
/// World interaction is owned by the central InteractionRouter (no keyboard
/// polling); while the photo album is open, its navigation shortcuts read the
/// central FocusedUiInput helper, preserving the authored Q/R/E behavior.
/// The artifact itself is the interaction target and the interaction hint
/// lives on the accompanying plaque (no floating prompt).
/// </summary>
public class LindaLeaksPanelOpener : MonoBehaviour, IInteractionTarget
{
    private enum PanelTarget
    {
        VideoPopup,
        PhotoAlbum,
        MeshellArticleReader
    }

    [Header("Target")]
    [SerializeField] private PanelTarget target = PanelTarget.VideoPopup;
    [SerializeField] private MediaVideoController videoPopUp;
    [SerializeField] private HolographicSlideshow photoAlbum;
    [SerializeField] private MeshellArticleNotebookOpener meshellArticleReader;

    [Header("Desktop Interaction")]
#pragma warning disable 0414 // retained for scene-data compatibility; router owns input/camera now
    [SerializeField] private Camera playerCamera;
#pragma warning restore 0414
    [SerializeField] private float interactionDistance = 4f;
    [SerializeField] private Key interactionKey = Key.E;
    [SerializeField] private bool advanceAlbumWithInteractionKey;
    [SerializeField] private bool enableAlbumKeyboardNavigation;
    [SerializeField] private Key previousPhotoKey = Key.Q;
    [SerializeField] private Key nextPhotoKey = Key.R;
    [SerializeField] private SharedInteractionPromptConfig prompt =
        new SharedInteractionPromptConfig { verb = SharedInteractionVerb.Open };

    private Collider[] ownColliders;

    // ---- IInteractionTarget --------------------------------------------

    public Vector3 FocusPoint => ColliderFocusPoint();
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

    private Vector3 ColliderFocusPoint()
    {
        Collider[] colliders = OwnColliders;
        if (colliders == null || colliders.Length == 0)
            return transform.position;

        bool hasBounds = false;
        Bounds bounds = default;
        foreach (Collider collider in colliders)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                continue;

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds ? bounds.center : transform.position;
    }

    public string GetPrompt(bool xr)
    {
        SharedInteractionVerb verb = target switch
        {
            PanelTarget.VideoPopup => SharedInteractionVerb.Watch,
            PanelTarget.PhotoAlbum => SharedInteractionVerb.View,
            _ => SharedInteractionVerb.Read,
        };
        if (prompt == null)
            prompt = new SharedInteractionPromptConfig();
        prompt.verb = verb;
        return SharedInteractionPrompt.Format(xr, prompt);
    }

    public void OnFocusChanged(bool focused) { }

    public void OnInteract(InteractionActivation activation) => HandleKeyboardInteraction();

    // ---------------------------------------------------------------------

    private void OnEnable() => InteractionRouter.Register(this);

    private void OnDisable() => InteractionRouter.Unregister(this);

    private void Update()
    {
        // Focused-album shortcuts only; opening is dispatched by the router.
        if (target != PanelTarget.PhotoAlbum || photoAlbum == null || !photoAlbum.IsOpen)
            return;

        if (FocusedUiInput.KeyPressed(interactionKey))
        {
            if (advanceAlbumWithInteractionKey)
                photoAlbum.AdvanceOrCloseAtEnd();
            else
                photoAlbum.ToggleAlbum();

            return;
        }

        if (enableAlbumKeyboardNavigation && FocusedUiInput.KeyPressed(previousPhotoKey))
        {
            photoAlbum.Previous();
            return;
        }

        if (enableAlbumKeyboardNavigation && FocusedUiInput.KeyPressed(nextPhotoKey))
            photoAlbum.Next();
    }

    public void Open()
    {
        if (BCaT.Production.Interaction.InteractionState.IsBlocked)
        {
            Debug.Log($"[PanelOpener:{gameObject.name}] Open suppressed (interaction blocked).");
            return;
        }

        Debug.Log($"[PanelOpener:{gameObject.name}] Open ({target})");

        if (target == PanelTarget.VideoPopup)
        {
            if (videoPopUp != null)
                videoPopUp.OpenPopUp();

            return;
        }

        if (target == PanelTarget.PhotoAlbum)
        {
            if (photoAlbum != null)
                photoAlbum.OpenAlbum();

            return;
        }

        if (meshellArticleReader != null)
            meshellArticleReader.Open();
    }

    public void Open(SelectEnterEventArgs args)
    {
        OnXRSelect();
    }

    public void OnXRSelect()
    {
        if (InteractionRouter.Instance != null)
        {
            InteractionRouter.Instance.RequestXRSelect(this);
            return;
        }

        Open();
    }

    private void HandleKeyboardInteraction()
    {
        if (target == PanelTarget.VideoPopup)
        {
            Open();
            return;
        }

        if (target == PanelTarget.PhotoAlbum && photoAlbum != null)
        {
            photoAlbum.ToggleAlbum();
            return;
        }

        if (meshellArticleReader != null)
            meshellArticleReader.Open();
    }
}
