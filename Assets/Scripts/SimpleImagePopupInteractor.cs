using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class SimpleImagePopupInteractor : MonoBehaviour
{
    [SerializeField] private SimpleImagePopupController popup;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactionDistance = 4f;
    [SerializeField] private Key interactionKey = Key.E;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private string desktopPrompt = "Press E to view My Grandma's Garden.";
    [SerializeField] private string xrPrompt = "Interact to view My Grandma's Garden.";

    private void Start()
    {
        RefreshPrompt();
    }

    private void Update()
    {
        RefreshPrompt();

        if (Keyboard.current == null || popup == null || popup.IsOpen)
            return;

        if (!Keyboard.current[interactionKey].wasPressedThisFrame)
            return;

        if (IsPlayerLookingAtThisObject())
            popup.Open();
    }

    public void OpenFromXR(SelectEnterEventArgs args)
    {
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

        promptText.text = XRSettings.isDeviceActive ? xrPrompt : desktopPrompt;
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

            if (!hit.collider.isTrigger)
                return false;
        }

        return false;
    }
}
