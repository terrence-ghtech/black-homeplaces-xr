using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class BlackKitchenOvenInteraction : MonoBehaviour
{
    public enum ContinuationMode
    {
        SubsequentInteraction,
        DwellAfterFirstStory,
        AutomaticAfterCompletion
    }

    [Header("Interaction")]
    [SerializeField] private float interactionDistance = 3.5f;
    [SerializeField] private Key interactionKey = Key.E;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private string firstDesktopPrompt = "Press E to Listen to Oven Story";
    [SerializeField] private string nextDesktopPrompt = "Press E to Continue Oven Story";
    [SerializeField] private string xrPrompt = "Interact to Listen";
    [Tooltip("Seconds after entering the existing interaction collider where E is accepted even if trigger callbacks and input arrive nearly together.")]
    [SerializeField] private float interactionGracePeriod = 0.35f;
    [Tooltip("Minimum seconds between accepted desktop/XR activations.")]
    [SerializeField] private float inputCooldown = 0.6f;

    [Header("Oven Stories")]
    [SerializeField] private AudioClip birthdayCakeClip;
    [SerializeField] private AudioClip nieceCakeClip;
    [SerializeField] private AudioSource source;
    [SerializeField] private BlackKitchenAudioCoordinator coordinator;
    [SerializeField] private ContinuationMode continuationMode = ContinuationMode.SubsequentInteraction;
    [SerializeField] private bool replayable = true;
    [SerializeField] private bool restartIfAlreadyPlaying = false;
    [SerializeField] private bool automaticAfterCompletion = false;
    [Range(0f, 1f)]
    [SerializeField] private float birthdayVolume = 0.85f;
    [Range(0f, 1f)]
    [SerializeField] private float nieceVolume = 0.65f;
    [SerializeField] private float minimumDistance = 0.75f;
    [SerializeField] private float maximumDistance = 4.5f;
    [SerializeField] private float fadeIn = 0.65f;
    [SerializeField] private float fadeOut = 1f;

    private bool birthdayEncountered;
    private bool nieceEncountered;
    private bool playerInRange;
    private float lastRangeTime = -999f;
    private float lastAcceptedTime = -999f;
    private Collider[] interactionColliders;

    private void Start()
    {
        ConfigureSource();
        CacheInteractionColliders();
        RefreshPrompt();
    }

    private void Update()
    {
        RefreshPrompt();

        if (Keyboard.current == null || !Keyboard.current[interactionKey].wasPressedThisFrame)
            return;

        UpdateRangeFromColliders();

        if (CanAcceptDesktopInteraction(out float interactionDistanceToPlayer))
        {
            BlackKitchenInteractionGate.RegisterCandidate(this, interactionDistanceToPlayer);
            StartCoroutine(ActivateIfSelectedForInput());
        }
    }

    public void OnXRSelect()
    {
        Debug.Log($"[BlackKitchenOvenInteraction] XR interaction accepted for {name}.");
        Activate();
    }

    public void Activate()
    {
        if (Time.time - lastAcceptedTime < inputCooldown)
        {
            Debug.Log($"[BlackKitchenOvenInteraction] Interaction rejected for {name}: cooldown.");
            return;
        }

        AudioClip clip = ChooseClip(out float selectedVolume);
        if (clip == null)
        {
            Debug.Log($"[BlackKitchenOvenInteraction] Interaction rejected for {name}: no clip selected.");
            return;
        }

        if (coordinator != null && coordinator.TryPlayStory(source, clip, selectedVolume, restartIfAlreadyPlaying, fadeIn, fadeOut))
        {
            lastAcceptedTime = Time.time;
            if (clip == birthdayCakeClip)
                birthdayEncountered = true;
            if (clip == nieceCakeClip)
                nieceEncountered = true;

            Debug.Log($"[BlackKitchenOvenInteraction] Interaction accepted for {name}: {clip.name}.");
            BlackKitchenExperienceController.NotifyMeaningfulInteraction();
        }
        else
        {
            Debug.Log($"[BlackKitchenOvenInteraction] Interaction rejected for {name}: audio coordinator did not start clip.");
        }
    }

    public void Configure(AudioClip birthdayClip, AudioClip nieceClip, AudioSource audioSource, BlackKitchenAudioCoordinator audioCoordinator, TMP_Text prompt)
    {
        birthdayCakeClip = birthdayClip;
        nieceCakeClip = nieceClip;
        source = audioSource;
        coordinator = audioCoordinator;
        promptText = prompt;
        ConfigureSource();
        RefreshPrompt();
    }

    private AudioClip ChooseClip(out float selectedVolume)
    {
        selectedVolume = birthdayVolume;

        if (!birthdayEncountered || (replayable && birthdayEncountered && nieceEncountered))
            return birthdayCakeClip;

        selectedVolume = nieceVolume;
        return nieceCakeClip;
    }

    private void ConfigureSource()
    {
        if (source == null)
            return;

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0.85f;
        source.minDistance = minimumDistance;
        source.maxDistance = maximumDistance;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
    }

    private void CacheInteractionColliders()
    {
        interactionColliders = GetComponentsInChildren<Collider>(true);
    }

    private void RefreshPrompt()
    {
        if (promptText == null)
            return;

        if (InteractionPromptText.IsXRActive())
            promptText.text = xrPrompt;
        else
            promptText.text = birthdayEncountered ? nextDesktopPrompt : firstDesktopPrompt;
    }

    private IEnumerator ActivateIfSelectedForInput()
    {
        yield return new WaitForEndOfFrame();

        if (BlackKitchenInteractionGate.IsSelected(this))
            Activate();
        else
            Debug.Log($"[BlackKitchenOvenInteraction] Interaction rejected for {name}: another Black Kitchen interactable was nearer.");
    }

    private bool CanAcceptDesktopInteraction(out float distanceToPlayer)
    {
        distanceToPlayer = float.PositiveInfinity;
        if (Time.time - lastAcceptedTime < inputCooldown)
            return false;

        if (playerInRange || Time.time - lastRangeTime <= interactionGracePeriod)
        {
            distanceToPlayer = GetDistanceToPlayer();
            return true;
        }

        return IsPlayerLookingAtThisObject(out distanceToPlayer);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerInRange = true;
        lastRangeTime = Time.time;
        Debug.Log($"[BlackKitchenOvenInteraction] Entered range for {name}: {other.name}.");
    }

    private void OnTriggerStay(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerInRange = true;
        lastRangeTime = Time.time;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerInRange = false;
        lastRangeTime = Time.time;
        Debug.Log($"[BlackKitchenOvenInteraction] Exited range for {name}: {other.name}.");
    }

    private void UpdateRangeFromColliders()
    {
        if (interactionColliders == null || interactionColliders.Length == 0)
            CacheInteractionColliders();

        bool inExistingCollider = IsPlayerInsideExistingCollider();
        if (inExistingCollider)
        {
            if (!playerInRange)
                Debug.Log($"[BlackKitchenOvenInteraction] Entered range for {name}: overlap check.");
            playerInRange = true;
            lastRangeTime = Time.time;
        }
        else if (playerInRange && Time.time - lastRangeTime > interactionGracePeriod)
        {
            playerInRange = false;
            Debug.Log($"[BlackKitchenOvenInteraction] Exited range for {name}: overlap check.");
        }
    }

    private bool IsPlayerInsideExistingCollider()
    {
        Camera cam = Camera.main;
        if (cam == null || interactionColliders == null)
            return false;

        Vector3 cameraPosition = cam.transform.position;
        CharacterController controller = cam.GetComponentInParent<CharacterController>();
        Vector3 rootPosition = controller != null ? controller.transform.position : cam.transform.root.position;

        foreach (Collider interactionCollider in interactionColliders)
        {
            if (interactionCollider == null || !interactionCollider.enabled)
                continue;

            if (IsPointInsideCollider(interactionCollider, cameraPosition) || IsPointInsideCollider(interactionCollider, rootPosition))
                return true;
        }

        return false;
    }

    private static bool IsPointInsideCollider(Collider collider, Vector3 point)
    {
        Vector3 closest = collider.ClosestPoint(point);
        return (closest - point).sqrMagnitude <= 0.0001f;
    }

    private float GetDistanceToPlayer()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return float.PositiveInfinity;

        CharacterController controller = cam.GetComponentInParent<CharacterController>();
        Vector3 playerPosition = controller != null ? controller.transform.position : cam.transform.position;
        return Vector3.Distance(playerPosition, transform.position);
    }

    private bool IsPlayerLookingAtThisObject(out float hitDistance)
    {
        hitDistance = float.PositiveInfinity;
        Camera cam = Camera.main;
        if (cam == null)
            return false;

        RaycastHit[] hits = Physics.RaycastAll(new Ray(cam.transform.position, cam.transform.forward), interactionDistance, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (IsColliderPartOfInteraction(hit.collider))
            {
                hitDistance = hit.distance;
                return true;
            }
            if (!hit.collider.isTrigger)
                return false;
        }

        return false;
    }

    private bool IsColliderPartOfInteraction(Collider hitCollider)
    {
        if (hitCollider == null)
            return false;

        Transform hitTransform = hitCollider.transform;
        return hitTransform == transform || hitTransform.IsChildOf(transform) || transform.IsChildOf(hitTransform);
    }

    private static bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (other.GetComponentInParent<CharacterController>() != null)
            return true;

        Transform root = other.transform.root;
        return root.CompareTag("Player") || other.CompareTag("Player");
    }
}
