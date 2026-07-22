using UnityEngine;

public class PrivacyLawProximityRelay : MonoBehaviour
{
    [SerializeField] private PrivacyLawExhibitController controller;

    private void Reset()
    {
        controller = GetComponentInParent<PrivacyLawExhibitController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (controller != null)
            controller.HandleProximityEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (controller != null)
            controller.HandleProximityExit(other);
    }
}
