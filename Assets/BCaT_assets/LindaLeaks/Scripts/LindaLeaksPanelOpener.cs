using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

public class LindaLeaksPanelOpener : MonoBehaviour
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

    // Desktop fallback only. No floating prompt is shown; the artifact itself is the
    // interaction target and the interaction hint lives on the accompanying plaque.
    [Header("Desktop Interaction")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactionDistance = 4f;
    [SerializeField] private Key interactionKey = Key.E;
    [SerializeField] private bool advanceAlbumWithInteractionKey;
    [SerializeField] private bool enableAlbumKeyboardNavigation;
    [SerializeField] private Key previousPhotoKey = Key.Q;
    [SerializeField] private Key nextPhotoKey = Key.R;

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (target == PanelTarget.PhotoAlbum && photoAlbum != null && photoAlbum.IsOpen)
        {
            if (Keyboard.current[interactionKey].wasPressedThisFrame)
            {
                if (advanceAlbumWithInteractionKey)
                    photoAlbum.AdvanceOrCloseAtEnd();
                else
                    photoAlbum.ToggleAlbum();

                return;
            }

            if (enableAlbumKeyboardNavigation && Keyboard.current[previousPhotoKey].wasPressedThisFrame)
            {
                photoAlbum.Previous();
                return;
            }

            if (enableAlbumKeyboardNavigation && Keyboard.current[nextPhotoKey].wasPressedThisFrame)
            {
                photoAlbum.Next();
                return;
            }
        }

        if (!Keyboard.current[interactionKey].wasPressedThisFrame)
            return;

        if (IsPlayerLookingAtThisObject())
            HandleKeyboardInteraction();
    }

    public void Open()
    {
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

    private bool IsPlayerLookingAtThisObject()
    {
        if (playerCamera == null || !playerCamera.gameObject.activeInHierarchy)
            playerCamera = Camera.main;

        if (playerCamera == null)
            return false;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, interactionDistance);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                return true;

            // Foreign trigger volumes (other exhibits' interaction zones) are not
            // visual obstructions; only solid geometry blocks line of sight.
            if (!hit.collider.isTrigger)
                return false;
        }

        return false;
    }
}
