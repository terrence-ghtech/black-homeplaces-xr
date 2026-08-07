using UnityEngine;

public sealed class ScenePlatformRigSelector : MonoBehaviour
{
    [SerializeField] private GameObject desktopRigRoot;
    [SerializeField] private GameObject desktopEventSystem;
    [SerializeField] private GameObject xrRigRoot;
    [SerializeField] private GameObject xrInteractionManager;
    [SerializeField] private GameObject xrEventSystem;

    private void Awake()
    {
        bool useXR = ShouldUseXR();
        string sceneName = gameObject.scene.name;

        Debug.Log($"[ScenePlatformRigSelector] Scene '{sceneName}' selected platform '{(useXR ? "XR" : "Desktop")}'.");

        SetRigActive(desktopRigRoot, !useXR, sceneName);
        SetActive(desktopEventSystem, !useXR, sceneName);
        SetRigActive(xrRigRoot, useXR, sceneName);
        SetActive(xrInteractionManager, useXR, sceneName);
        SetActive(xrEventSystem, useXR, sceneName);
    }

    /// <summary>
    /// Legacy entry point retained while the scenes migrate to
    /// ScenePlatformBinding. Forwards to the single platform authority so this
    /// class can no longer disagree with it.
    /// </summary>
    public static bool ShouldUseXR() => BCaT.Production.BCaTPlatform.IsQuest;

    private static void SetRigActive(GameObject target, bool active, string sceneName)
    {
        if (target == null)
            return;

        if (!active)
            DisableDesktopMovement(target);

        SetActive(target, active, sceneName);
    }

    private static void SetActive(GameObject target, bool active, string sceneName)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
            Debug.Log($"[ScenePlatformRigSelector] Scene '{sceneName}' rig/object '{target.name}' {(active ? "activated" : "deactivated")}.");
        }
    }

    private static void DisableDesktopMovement(GameObject target)
    {
        foreach (Behaviour behaviour in target.GetComponentsInChildren<Behaviour>(true))
        {
            if (behaviour == null || !behaviour.enabled)
                continue;

            string typeName = behaviour.GetType().Name;
            if (typeName == "FirstPersonController" || typeName == "StarterAssetsInputs" || typeName == "PlayerInput")
                behaviour.enabled = false;
        }
    }
}
