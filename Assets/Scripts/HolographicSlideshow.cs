using System.Collections.Generic;
using BCaT.Production.Interaction;
using BCaT.Production.Shell;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Reusable holographic photo slideshow (floating gallery panel).
/// Field/method names intentionally match the retired
/// LindaLeaksPhotoAlbumController so serialized photo data and existing
/// Button onClick wiring survive the script swap. On Quest the glowing arrow /
/// close buttons are XR-ray clickable (canvas needs TrackedDeviceGraphicRaycaster).
/// Uses the PhotoEntry type (sprite/title/caption).
/// </summary>
public class HolographicSlideshow : MonoBehaviour, IFocusedExhibit
{
    private const float OpenDistanceFromCamera = 1.75f;

    [Header("Album")]
    [SerializeField] private GameObject albumRoot;
    [SerializeField] private Canvas albumCanvas;
    [SerializeField] private Image photoImage;

    [Header("Text")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text captionText;
    [SerializeField] private TMP_Text projectDescriptionText;
    [TextArea]
    [SerializeField] private string projectDescription;

    [Header("Photos")]
    [SerializeField] private List<PhotoEntry> photos = new List<PhotoEntry>();
    [SerializeField] private int startIndex;

    [Header("Keyboard Shortcuts")]
    [SerializeField] private bool enableKeyboardShortcuts = true;
    [SerializeField] private bool useArrowKeysForNavigation = true;
    [SerializeField] private bool useEscapeToClose = true;

    [Header("Desktop Positioning")]
    [SerializeField] private bool positionInFrontOfCameraOnOpen;

    [Header("External Link")]
    [SerializeField] private string externalWebsiteUrl;

    private int currentIndex;
    private bool isOpen;
    private bool capturedDesktopInput;
    private bool controlsSuspended;
    private bool closeKeyReleasedSinceOpen;
    private int openedFrame = -1;

    private string LogTag => $"[Slideshow:{gameObject.name}]";

    private void Start()
    {
        currentIndex = Mathf.Clamp(startIndex, 0, Mathf.Max(photos.Count - 1, 0));
        HideAlbum();
    }

    private void OnDestroy()
    {
        FocusedExhibitCoordinator.Instance?.NotifyClosed(this);
        InteractionState.Unblock(this);
        ReleasePlayerControls();
    }

    private void OnDisable()
    {
        if (isOpen)
            CloseAlbum();
        else
        {
            FocusedExhibitCoordinator.Instance?.NotifyClosed(this);
            ReleasePlayerControls();
        }
    }

    private void Update()
    {
        // Focused-modal input goes through the central FocusedUiInput helper;
        // world interaction (opening) is owned by the InteractionRouter via the
        // opener component.
        if (!isOpen)
            return;

        if (Time.frameCount > openedFrame && !BCaT.Production.Interaction.FocusedUiInput.InteractHeld)
            closeKeyReleasedSinceOpen = true;

        if (Time.frameCount > openedFrame && FocusedUiInput.CancelPressed)
        {
            CloseAlbum();
            return;
        }

        if (Time.frameCount > openedFrame && closeKeyReleasedSinceOpen && FocusedUiInput.InteractPressed)
        {
            IInteractionTarget target = InteractionRouter.Instance?.CurrentTarget;
            if (FocusedExhibitCoordinator.Instance?.IsReplacementTarget(target, this) == true)
                return;

            CloseAlbum();
            return;
        }

        if (!enableKeyboardShortcuts)
            return;

        if (useArrowKeysForNavigation && BCaT.Production.Interaction.FocusedUiInput.KeyPressed(Key.RightArrow))
            Next();
        else if (useArrowKeysForNavigation && BCaT.Production.Interaction.FocusedUiInput.KeyPressed(Key.LeftArrow))
            Previous();
        else if (useEscapeToClose && BCaT.Production.Interaction.FocusedUiInput.CancelPressed)
            CloseAlbum();
    }

    /// <summary>Wire XRSimpleInteractable.SelectEntered here.</summary>
    public void OnXRSelect()
    {
        Debug.Log($"{LogTag} XR SelectEntered received");
        LindaLeaksPanelOpener target = GetComponentInChildren<LindaLeaksPanelOpener>(true);
        if (target == null)
            target = GetComponentInParent<LindaLeaksPanelOpener>(true);
        if (target != null && InteractionRouter.Instance != null)
        {
            InteractionRouter.Instance.RequestXRSelect(target);
            return;
        }

        RequestFocusedOpen();
    }

    public void OpenAlbum()
    {
        RequestFocusedOpen();
    }

    void IFocusedExhibit.Open()
    {
        OpenInternal();
    }

    void IFocusedExhibit.Close()
    {
        CloseAlbum();
    }

    private void RequestFocusedOpen()
    {
        if (FocusedExhibitCoordinator.Instance != null)
        {
            FocusedExhibitCoordinator.Instance.RequestOpen(this);
            return;
        }

        Debug.LogWarning($"{LogTag} FocusedExhibitCoordinator is unavailable; opening without coordination.");
        OpenInternal();
    }

    private void OpenInternal()
    {
        if (isOpen)
            return;

        Debug.Log($"{LogTag} OpenAlbum ({photos.Count} photos)");
        ShowAlbum();
        if (positionInFrontOfCameraOnOpen)
            PositionAlbumInFrontOfCamera();
        Refresh();
        CaptureDesktopInput();

    }

    public void CloseAlbum()
    {
        if (!isOpen)
        {
            FocusedExhibitCoordinator.Instance?.NotifyClosed(this);
            return;
        }

        Debug.Log($"{LogTag} CloseAlbum");
        InteractionState.SuppressInputForCurrentFrame();
        HideAlbum();
        FocusedExhibitCoordinator.Instance?.NotifyClosed(this);
    }

    public void ToggleAlbum()
    {
        if (isOpen)
            CloseAlbum();
        else
            OpenAlbum();
    }

    public void AdvanceOrCloseAtEnd()
    {
        if (photos.Count == 0 || currentIndex >= photos.Count - 1)
            CloseAlbum();
        else
            Next();
    }

    public bool IsOpen => isOpen;

    public void Next()
    {
        if (photos.Count == 0)
            return;

        currentIndex = (currentIndex + 1) % photos.Count;
        Debug.Log($"{LogTag} Next -> {currentIndex + 1}/{photos.Count}");
        Refresh();
    }

    public void NextPhoto()
    {
        Next();
    }

    public void Previous()
    {
        if (photos.Count == 0)
            return;

        currentIndex = (currentIndex - 1 + photos.Count) % photos.Count;
        Debug.Log($"{LogTag} Previous -> {currentIndex + 1}/{photos.Count}");
        Refresh();
    }

    public void PreviousPhoto()
    {
        Previous();
    }

    public void OpenExternalWebsite()
    {
        if (string.IsNullOrWhiteSpace(externalWebsiteUrl))
            return;

        Debug.Log($"{LogTag} Opening external link: {externalWebsiteUrl}");
        BCaT.Production.QuestBrowserHeadTracking.OpenUrl(externalWebsiteUrl);
    }

    private void Refresh()
    {
        if (projectDescriptionText != null)
            projectDescriptionText.text = projectDescription;

        if (photos.Count == 0)
        {
            if (photoImage != null)
                photoImage.sprite = null;

            if (titleText != null)
                titleText.text = string.Empty;

            if (captionText != null)
                captionText.text = string.Empty;

            return;
        }

        currentIndex = Mathf.Clamp(currentIndex, 0, photos.Count - 1);
        PhotoEntry entry = photos[currentIndex];

        if (photoImage != null)
        {
            photoImage.sprite = entry.sprite;
            photoImage.enabled = entry.sprite != null;
        }

        if (titleText != null)
            titleText.text = entry.title;

        if (captionText != null)
            captionText.text = entry.caption;
    }

    private void ShowAlbum()
    {
        isOpen = true;
        openedFrame = Time.frameCount;
        closeKeyReleasedSinceOpen = !BCaT.Production.Interaction.FocusedUiInput.InteractHeld;

        if (albumRoot != null)
            albumRoot.SetActive(true);

        if (albumCanvas != null)
            albumCanvas.enabled = true;

    }

    private void PositionAlbumInFrontOfCamera()
    {
        Camera activeCamera = FindActiveCamera();
        if (activeCamera == null)
            return;

        Transform albumTransform = albumRoot != null ? albumRoot.transform : transform;
        Vector3 cameraForward = activeCamera.transform.forward;
        albumTransform.position = activeCamera.transform.position + cameraForward * OpenDistanceFromCamera;

        Vector3 directionAwayFromCamera =
            (albumTransform.position - activeCamera.transform.position).normalized;

        albumTransform.rotation =
            Quaternion.LookRotation(directionAwayFromCamera, Vector3.up);
    }

    private Camera FindActiveCamera()
    {
        if (Camera.main != null && Camera.main.isActiveAndEnabled)
            return Camera.main;

        foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (camera != null && camera.isActiveAndEnabled)
                return camera;
        }

        return null;
    }

    private void HideAlbum()
    {
        isOpen = false;
        BCaT.Production.Interaction.InteractionState.Unblock(this);

        if (albumRoot != null)
            albumRoot.SetActive(false);

        if (albumCanvas != null)
            albumCanvas.enabled = false;

        ClearDisplayedPhoto();
        RestoreDesktopInput();
    }

    private void ClearDisplayedPhoto()
    {
        if (photoImage != null)
        {
            photoImage.sprite = null;
            photoImage.enabled = false;
        }
    }

    private void CaptureDesktopInput()
    {
        if (capturedDesktopInput)
            return;

        capturedDesktopInput = true;
        SuspendPlayerControls();
    }

    private void RestoreDesktopInput()
    {
        if (!capturedDesktopInput)
            return;

        ReleasePlayerControls();
        capturedDesktopInput = false;
    }

    private void SuspendPlayerControls()
    {
        if (controlsSuspended)
            return;

        PlayerControlGate.Suspend(this);
        controlsSuspended = true;
    }

    private void ReleasePlayerControls()
    {
        if (!controlsSuspended)
            return;

        PlayerControlGate.Resume(this);
        controlsSuspended = false;
    }
}
