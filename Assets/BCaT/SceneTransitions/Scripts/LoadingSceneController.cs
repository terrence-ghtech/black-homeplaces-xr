using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class LoadingSceneController : MonoBehaviour
{
    [SerializeField] private Text progressText;

    private bool loadStarted;

    private IEnumerator Start()
    {
        if (loadStarted)
            yield break;

        loadStarted = true;

        string destinationScene = SceneTransitionState.DestinationSceneName;
        if (string.IsNullOrWhiteSpace(destinationScene))
        {
            string message = "[LoadingSceneController] No destination scene was requested.";
            SceneTransitionState.CancelTransition(message);
            Debug.LogError(message);
            yield break;
        }

        yield return null;

        AsyncOperation unloadUnusedAssets = Resources.UnloadUnusedAssets();
        while (unloadUnusedAssets != null && !unloadUnusedAssets.isDone)
        {
            UpdateProgress(0.05f);
            yield return null;
        }

        System.GC.Collect();
        yield return null;

        // Scenes that ship inside the player load through SceneManager exactly
        // as before. Scenes moved to remote Addressables (currently the Black
        // Kitchen on WebGL) download on demand with visible progress.
        if (Application.CanStreamedLevelBeLoaded(destinationScene))
            yield return LoadBuiltInScene(destinationScene);
        else
            yield return LoadAddressableScene(destinationScene);
    }

    private IEnumerator LoadBuiltInScene(string destinationScene)
    {
        AsyncOperation loadOperation = null;
        try
        {
            string loadingScene = gameObject.scene.name;
            Debug.Log($"[LoadingSceneController] Scene '{loadingScene}' destination scene load requested: '{destinationScene}'.");
            loadOperation = SceneManager.LoadSceneAsync(destinationScene, LoadSceneMode.Single);
            if (loadOperation != null)
                loadOperation.completed += _ =>
                {
                    Debug.Log($"[LoadingSceneController] Scene '{loadingScene}' destination scene load completed: '{destinationScene}'.");
                    // Leaving a remote scene: free its downloaded bundle.
                    AddressableSceneHandleStore.ReleaseIfHeld(exceptScene: destinationScene);
                };
        }
        catch (System.Exception exception)
        {
            string message = $"[LoadingSceneController] Failed to start loading destination scene '{destinationScene}': {exception.Message}";
            SceneTransitionState.CancelTransition(message);
            Debug.LogError(message);
            yield break;
        }

        if (loadOperation == null)
        {
            string message = $"[LoadingSceneController] Failed to start loading destination scene '{destinationScene}'.";
            SceneTransitionState.CancelTransition(message);
            Debug.LogError(message);
            yield break;
        }

        while (!loadOperation.isDone)
        {
            UpdateProgress(Mathf.Clamp01(loadOperation.progress / 0.9f));
            yield return null;
        }
    }

    private IEnumerator LoadAddressableScene(string destinationScene)
    {
        AsyncOperationHandle<SceneInstance> handle = default;
        string startFailure = null;
        try
        {
            Debug.Log($"[LoadingSceneController] Remote (Addressables) scene load requested: '{destinationScene}'.");
            handle = Addressables.LoadSceneAsync(destinationScene, LoadSceneMode.Single);
        }
        catch (System.Exception exception)
        {
            startFailure = $"[LoadingSceneController] Failed to start remote scene load '{destinationScene}': {exception.Message}";
        }

        if (startFailure != null)
        {
            yield return FailAndReturnToMainHouse(destinationScene, startFailure);
            yield break;
        }

        while (!handle.IsDone)
        {
            UpdateProgress(Mathf.Clamp01(handle.PercentComplete));
            yield return null;
        }

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            yield return FailAndReturnToMainHouse(destinationScene,
                $"[LoadingSceneController] Remote scene download/load failed for '{destinationScene}': {handle.OperationException?.Message}");
            yield break;
        }

        Debug.Log($"[LoadingSceneController] Remote scene load completed: '{destinationScene}'.");
        AddressableSceneHandleStore.Store(destinationScene, handle);
    }

    private IEnumerator FailAndReturnToMainHouse(string destinationScene, string message)
    {
        Debug.LogError(message);
        SceneTransitionState.CancelTransition(message);

        if (progressText != null)
            progressText.text = "Couldn't load this exhibit.\nCheck your connection — returning to the house…";

        yield return new WaitForSeconds(4f);

        // Walking back into the portal retries the download.
        SceneTransitionState.RequestTransition(
            SceneTransitionState.MainHouseSceneName,
            SceneTransitionState.MainHouseKitchenReturnSpawnId,
            gameObject.scene.name);
        yield return LoadBuiltInScene(SceneTransitionState.MainHouseSceneName);
    }

    private void UpdateProgress(float progress)
    {
        if (progressText != null)
            progressText.text = $"{Mathf.RoundToInt(progress * 100f)}%";
    }
}
