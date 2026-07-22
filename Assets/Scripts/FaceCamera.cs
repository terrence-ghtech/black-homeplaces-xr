using UnityEngine;

/// <summary>
/// Optional billboard behavior: rotates the object around Y to face the
/// active camera (desktop or XR head). Used by hologram exhibit canvases.
/// </summary>
public class FaceCamera : MonoBehaviour
{
    [SerializeField] private bool yAxisOnly = true;

    private Camera targetCamera;

    private void LateUpdate()
    {
        if (targetCamera == null || !targetCamera.gameObject.activeInHierarchy)
            targetCamera = Camera.main;

        if (targetCamera == null)
            return;

        Vector3 toCamera = targetCamera.transform.position - transform.position;
        if (yAxisOnly)
            toCamera.y = 0f;

        if (toCamera.sqrMagnitude < 0.0001f)
            return;

        // UI canvases face +Z away from the viewer, so look opposite the camera direction.
        transform.rotation = Quaternion.LookRotation(-toCamera);
    }
}
