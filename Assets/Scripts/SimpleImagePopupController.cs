using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SimpleImagePopupController : MonoBehaviour
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

    private readonly List<Behaviour> disabledBehaviours = new List<Behaviour>();
    private Sprite currentSprite;
    private bool isOpen;
    private bool capturedInput;
    private bool previousCursorVisible;
    private bool closeKeyReleasedSinceOpen;
    private int openedFrame = -1;
    private CursorLockMode previousCursorLockState;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        RefreshContent();
        HidePopup();
    }

    private void OnDestroy()
    {
        ClearCurrentSprite();
    }

    private void Update()
    {
        if (!isOpen || Keyboard.current == null)
            return;

        if (Time.frameCount > openedFrame && !Keyboard.current.eKey.isPressed)
            closeKeyReleasedSinceOpen = true;

        if (Time.frameCount <= openedFrame)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame
            || (closeKeyReleasedSinceOpen && Keyboard.current.eKey.wasPressedThisFrame))
        {
            Close();
        }
    }

    public void Open()
    {
        if (isOpen)
            return;

        isOpen = true;
        openedFrame = Time.frameCount;
        closeKeyReleasedSinceOpen = Keyboard.current == null || !Keyboard.current.eKey.isPressed;

        RefreshContent();
        ShowPopup();
        PositionPopupInFrontOfCamera();
        CaptureInput();
    }

    public void Close()
    {
        if (!isOpen)
            return;

        isOpen = false;
        HidePopup();
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
        previousCursorLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        DisableWorldInput();
    }

    private void RestoreInput()
    {
        if (!capturedInput)
            return;

        RestoreWorldInput();
        Cursor.lockState = previousCursorLockState;
        Cursor.visible = previousCursorVisible;
        capturedInput = false;
    }

    private void DisableWorldInput()
    {
        disabledBehaviours.Clear();
        foreach (Behaviour behaviour in FindObjectsByType<Behaviour>(FindObjectsInactive.Exclude))
        {
            if (behaviour == null || !behaviour.enabled || behaviour == this || behaviour.transform.IsChildOf(transform))
                continue;

            if (!ShouldDisableWhileOpen(behaviour))
                continue;

            behaviour.enabled = false;
            disabledBehaviours.Add(behaviour);
        }
    }

    private bool ShouldDisableWhileOpen(Behaviour behaviour)
    {
        string typeName = behaviour.GetType().Name;
        string fullName = behaviour.GetType().FullName ?? typeName;

        return typeName == "FirstPersonController"
            || typeName == "StarterAssetsInputs"
            || typeName == "SimpleImagePopupInteractor"
            || typeName == "LindaLeaksPanelOpener"
            || typeName == "MediaVideoController"
            || typeName == "MeshellArticleNotebookInputRouter"
            || typeName == "MeshellArticleNotebookOpener"
            || typeName == "InteractableLinkLauncher"
            || typeName == "SpatialAudioToggle"
            || typeName == "QuiltVideoPopUp"
            || typeName == "LindaLeaksVideoPopUp"
            || fullName.Contains("ContinuousMoveProvider")
            || fullName.Contains("ContinuousTurnProvider")
            || fullName.Contains("SnapTurnProvider")
            || fullName.Contains("TeleportationProvider")
            || fullName.Contains("XRSimpleInteractable");
    }

    private void RestoreWorldInput()
    {
        foreach (Behaviour behaviour in disabledBehaviours)
        {
            if (behaviour != null)
                behaviour.enabled = true;
        }

        disabledBehaviours.Clear();
    }
}
