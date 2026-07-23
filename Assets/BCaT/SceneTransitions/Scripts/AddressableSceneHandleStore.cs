using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

/// <summary>
/// Holds the handle of the currently loaded remote (Addressables) scene so its
/// AssetBundle memory can be released after the visitor moves to another scene.
/// Single-mode scene loads destroy the scene objects automatically; releasing
/// the stored handle afterwards frees the downloaded bundle itself.
/// </summary>
public static class AddressableSceneHandleStore
{
    private static AsyncOperationHandle<SceneInstance> heldHandle;
    private static string heldSceneName;
    private static bool hasHandle;

    public static void Store(string sceneName, AsyncOperationHandle<SceneInstance> handle)
    {
        ReleaseIfHeld(exceptScene: sceneName);
        heldHandle = handle;
        heldSceneName = sceneName;
        hasHandle = true;
    }

    /// <summary>Releases the held remote-scene handle unless it belongs to exceptScene.</summary>
    public static void ReleaseIfHeld(string exceptScene = null)
    {
        if (!hasHandle || heldSceneName == exceptScene)
            return;

        try
        {
            if (heldHandle.IsValid())
                Addressables.Release(heldHandle);
            Debug.Log($"[AddressableSceneHandleStore] Released remote scene bundle '{heldSceneName}'.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[AddressableSceneHandleStore] Release of '{heldSceneName}' failed: {e.Message}");
        }
        finally
        {
            heldHandle = default;
            heldSceneName = null;
            hasHandle = false;
        }
    }
}
