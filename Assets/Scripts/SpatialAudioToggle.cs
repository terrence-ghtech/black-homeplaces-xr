using UnityEngine;
using UnityEngine.InputSystem;

public class SpatialAudioToggle : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Key interactKey = Key.E;
    [SerializeField] private float interactionDistance = 5f;
    [SerializeField] private Camera playerCamera;

    [Header("Spatial Defaults")]
    [SerializeField] private bool configureSpatialAudio = true;
    [SerializeField] private float spatialBlend = 1f;
    [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Custom;
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 4f;
    [SerializeField] private float dopplerLevel;

    private void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            return;

        if (configureSpatialAudio)
        {
            audioSource.spatialBlend = spatialBlend;
            audioSource.rolloffMode = rolloffMode;
            audioSource.minDistance = minDistance;
            audioSource.maxDistance = maxDistance;
            audioSource.dopplerLevel = dopplerLevel;
        }

        audioSource.playOnAwake = false;
        audioSource.Stop();
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current[interactKey].wasPressedThisFrame)
            return;

        if (IsPlayerLookingAtThisObject())
            ToggleAudio();
    }

    public void OnXRSelect()
    {
        ToggleAudio();
    }

    public void ToggleAudio()
    {
        if (audioSource == null)
            return;

        if (audioSource.isPlaying)
            audioSource.Pause();
        else
            audioSource.Play();
    }

    private bool IsPlayerLookingAtThisObject()
    {
        if (playerCamera == null || !playerCamera.gameObject.activeInHierarchy)
            playerCamera = Camera.main;

        if (playerCamera == null)
            return false;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
            return false;

        return hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform);
    }
}
