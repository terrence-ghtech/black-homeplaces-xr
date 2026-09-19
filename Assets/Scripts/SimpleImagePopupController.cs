using BCaT.Production.Interaction;
using BCaT.Production.Shell;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SimpleImagePopupController : MonoBehaviour, IFocusedExhibit
{
    private const float DefaultOpenDistanceFromCamera = 1.65f;

    [Header("Content")]
    [SerializeField] private Texture2D imageTexture;
    [SerializeField] private string title = "My Grandma's Garden";

    [Header("Popup")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Canvas popupCanvas;
    [SerializeField] private Image image;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button closeButton;
    [SerializeField] private float openDistanceFromCamera = DefaultOpenDistanceFromCamera;

    private Sprite currentSprite;
    private bool isOpen;
    private bool capturedInput;
    private bool controlsSuspended;
    private bool closeKeyReleasedSinceOpen;
    private int openedFrame = -1;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (titleText != null)
            titleText.text = title;
        ClearCurrentSprite();
        HidePopup();
    }

    private void OnDestroy()
    {
        FocusedExhibitCoordinator.Instance?.NotifyClosed(this);
        InteractionState.Unblock(this);
        ReleasePlayerControls();
        ClearCurrentSprite();
    }

    private void OnDisable()
    {
        if (isOpen)
            Close();
        else
        {
            FocusedExhibitCoordinator.Instance?.NotifyClosed(this);
            ReleasePlayerControls();
        }
    }

    private void Update()
    {
        // Focused-modal input reads the central FocusedUiInput helper; world
        // interaction (opening) is owned by the InteractionRouter via the
        // SimpleImagePopupInteractor target.
        if (!isOpen)
            return;

        if (Time.frameCount > openedFrame && !BCaT.Production.Interaction.FocusedUiInput.InteractHeld)
            closeKeyReleasedSinceOpen = true;

        if (Time.frameCount <= openedFrame)
            return;

        if (FocusedUiInput.CancelPressed)
        {
            Close();
            return;
        }

        if (closeKeyReleasedSinceOpen && FocusedUiInput.InteractPressed)
        {
            IInteractionTarget target = InteractionRouter.Instance?.CurrentTarget;
            if (FocusedExhibitCoordinator.Instance?.IsReplacementTarget(target, this) == true)
                return;

            Close();
        }
    }

    public void Open()
    {
        RequestFocusedOpen();
    }

    void IFocusedExhibit.Open()
    {
        OpenInternal();
    }

    private void RequestFocusedOpen()
    {
        if (FocusedExhibitCoordinator.Instance != null)
        {
            FocusedExhibitCoordinator.Instance.RequestOpen(this);
            return;
        }

        Debug.LogWarning($"[SimpleImagePopup:{gameObject.name}] FocusedExhibitCoordinator is unavailable; opening without coordination.");
        OpenInternal();
    }

    private void OpenInternal()
    {
        if (isOpen)
            return;

        isOpen = true;
        openedFrame = Time.frameCount;
        closeKeyReleasedSinceOpen = !BCaT.Production.Interaction.FocusedUiInput.InteractHeld;

        RefreshContent();
        ShowPopup();
        PositionPopupInFrontOfCamera();
        CaptureInput();

    }

    public void Close()
    {
        if (!isOpen)
        {
            FocusedExhibitCoordinator.Instance?.NotifyClosed(this);
            return;
        }

        InteractionState.SuppressInputForCurrentFrame();
        isOpen = false;
        HidePopup();
        FocusedExhibitCoordinator.Instance?.NotifyClosed(this);
        InteractionState.Unblock(this);
        ClearCurrentSprite();
        RestoreInput();
    }

    public void Toggle()
    {
        if (isOpen)
            Close();
        else
            Open();
    }

    private void RefreshContent()
    {
        if (titleText != null)
            titleText.text = title;

        if (image == null)
            return;

        ClearCurrentSprite();

        if (imageTexture == null)
        {
            image.sprite = null;
            image.enabled = false;
            return;
        }

        currentSprite = Sprite.Create(
            imageTexture,
            new Rect(0f, 0f, imageTexture.width, imageTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        image.sprite = currentSprite;
        image.preserveAspect = true;
        image.enabled = true;
    }

    private void ClearCurrentSprite()
    {
        if (image != null)
            image.sprite = null;

        if (currentSprite != null)
        {
            Destroy(currentSprite);
            currentSprite = null;
        }
    }

    private void ShowPopup()
    {
        if (popupRoot != null)
            popupRoot.SetActive(true);

        if (popupCanvas != null)
        {
            popupCanvas.enabled = true;
            popupCanvas.overrideSorting = true;
            popupCanvas.sortingOrder = 100;
        }
    }

    private void HidePopup()
    {
        if (popupRoot != null)
            popupRoot.SetActive(false);

        if (popupCanvas != null)
            popupCanvas.enabled = false;
    }

    private void PositionPopupInFrontOfCamera()
    {
        Camera activeCamera = FindActiveCamera();
        if (activeCamera == null)
            return;

        Transform popupTransform = popupRoot != null ? popupRoot.transform : transform;
        Vector3 cameraForward = activeCamera.transform.forward;
        popupTransform.position = activeCamera.transform.position + cameraForward * openDistanceFromCamera;

        Vector3 directionAwayFromCamera =
            (popupTransform.position - activeCamera.transform.position).normalized;

        popupTransform.rotation =
            Quaternion.LookRotation(directionAwayFromCamera, Vector3.up);

        EnsureCameraRendersUiLayer(activeCamera);
    }

    private Camera FindActiveCamera()
    {
        if (Camera.main != null && Camera.main.isActiveAndEnabled)
            return Camera.main;

        foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsInactive.Exclude))
        {
            if (camera != null && camera.isActiveAndEnabled)
                return camera;
        }

        return null;
    }

    private void EnsureCameraRendersUiLayer(Camera activeCamera)
    {
        int uiLayer = LayerMask.NameToLayer("UI");
        if (activeCamera == null || uiLayer < 0)
            return;

        int uiMask = 1 << uiLayer;
        if ((activeCamera.cullingMask & uiMask) == 0)
            activeCamera.cullingMask |= uiMask;
    }

    private void CaptureInput()
    {
        if (capturedInput)
            return;

        capturedInput = true;
        SuspendPlayerControls();
    }

    private void RestoreInput()
    {
        if (!capturedInput)
            return;

        ReleasePlayerControls();
        capturedInput = false;
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
