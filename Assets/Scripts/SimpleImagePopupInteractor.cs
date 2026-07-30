using BCaT.Production.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Interaction entry point for the simple image popup exhibit. Selection and
/// input are owned by the central InteractionRouter; the world prompt text now
/// uses the shared InteractionPromptText helper (previously this script read
/// XRSettings directly and missed the XR-initialization fallback).
/// </summary>
public class SimpleImagePopupInteractor : MonoBehaviour, IInteractionTarget
{
    [SerializeField] private SimpleImagePopupController popup;
#pragma warning disable 0414 // retained for scene-data compatibility; router owns input/camera now
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Key interactionKey = Key.E;
#pragma warning restore 0414
    [SerializeField] private float interactionDistance = 4f;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private string desktopPrompt = "Press E to view My Grandma's Garden.";
    [SerializeField] private string xrPrompt = "Interact to view My Grandma's Garden.";

    private Collider[] ownColliders;

    // ---- IInteractionTarget --------------------------------------------

    public Vector3 FocusPoint => transform.position;
    public float MaxDistance => interactionDistance;
    public float MaxViewAngle => 16f;
    public bool RequireLineOfSight => true;
    public int Priority => 0;
    public bool IsAvailable => isActiveAndEnabled && popup != null && !popup.IsOpen;
    public bool AllowDesktopClick => false;
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

    public string GetPrompt(bool xr) => xr ? xrPrompt : desktopPrompt;

    public void OnFocusChanged(bool focused) { }

    public void OnInteract(InteractionActivation activation) => Open();

    // ---------------------------------------------------------------------

    private void OnEnable() => InteractionRouter.Register(this);

    private void OnDisable() => InteractionRouter.Unregister(this);

    private void Start() => RefreshPrompt();

    private void Update() => RefreshPrompt();

    public void OpenFromXR(SelectEnterEventArgs args)
    {
        if (BCaT.Production.Interaction.InteractionState.IsBlocked)
        {
            Debug.Log($"[SimpleImagePopupInteractor:{gameObject.name}] XR open suppressed (interaction blocked).");
            return;
        }
        Open();
    }

    public void Open()
    {
        if (popup != null)
            popup.Open();
    }

    private void RefreshPrompt()
    {
        if (promptText == null)
            return;

        promptText.text = InteractionPromptText.IsXRActive() ? xrPrompt : desktopPrompt;
    }
}
