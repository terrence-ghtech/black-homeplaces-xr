using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class InteractableLinkLauncher : MonoBehaviour
{
    [Header("Link Settings")]
    [SerializeField] private string targetUrl;

    [Header("Interaction")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactDistance = 4f;

    [Header("Prompt Text Only")]
    [SerializeField] private TMP_Text promptText;

    void Start()
    {
        if (promptText == null) return;

        // Centralized platform-aware verb: "Press E" on WebGL/desktop, "Interact" in XR.
        promptText.text = InteractionPromptText.Verb + " to Open";
    }

    void Update()
    {
        if (playerCamera == null || !playerCamera.gameObject.activeInHierarchy)
            playerCamera = Camera.main;

        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            bool hitThisObject =
                hit.collider.transform == transform ||
                hit.collider.transform.IsChildOf(transform);

            if (hitThisObject && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                OpenLink();
        }
    }

    public void OpenLink()
    {
        if (!string.IsNullOrWhiteSpace(targetUrl))
            Application.OpenURL(targetUrl);
    }
}
