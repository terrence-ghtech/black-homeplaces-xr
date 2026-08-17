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
    [SerializeField] private float questAddressablesInitializationTimeout = 30f;
    [SerializeField] private float questAddressablesSceneLoadTimeout = 180f;
    [Tooltip("Quest-only: seconds without any load progress before the transition watchdog recovers.")]
    [SerializeField] private float questLoadStallTimeout = 20f;

    private bool loadStarted;
    private bool failureRecoveryStarted;
    private float lastLoadHeartbeat;

    private IEnumerator Start()
    {
        if (loadStarted)
            yield break;

        loadStarted = true;
        Heartbeat();

        // Quest-only safety net. An unhandled exception inside a load coroutine
        // terminates it silently, leaving this loading scene active behind an
        // opaque fade overlay with no failure path ever running. The watchdog
        // notices that no load loop is ticking any more and recovers.
        if (BCaT.Production.BCaTPlatform.IsQuest)
            StartCoroutine(LoadStallWatchdog());

        // Shared lifecycle: the previous scene is already unloaded (single-mode
        // load), but any registered media stop-actions and stale blockers from
        // it must not leak into the destination scene.
        BCaT.Production.Media.MediaPlaybackRegistry.StopAll();

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
            Heartbeat();
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
            Heartbeat();
            yield return null;
        }
    }

    private void Heartbeat() => lastLoadHeartbeat = Time.realtimeSinceStartup;

    /// <summary>
    /// Quest-only last line of defence: every load path ticks a heartbeat each
    /// frame, so a heartbeat that stops advancing means the coroutine driving
    /// it died without reaching any failure path. Recover to the main house
    /// rather than leaving the player behind an opaque overlay forever.
    /// </summary>
    private IEnumerator LoadStallWatchdog()
    {
        var poll = new WaitForSecondsRealtime(1f);
        int consecutiveStalls = 0;
        while (!failureRecoveryStarted)
        {
            yield return poll;

            if (failureRecoveryStarted)
                yield break;

            float stalled = Time.realtimeSinceStartup - lastLoadHeartbeat;
            if (stalled <= questLoadStallTimeout)
            {
                consecutiveStalls = 0;
                continue;
            }

            // A single long main-thread hitch (large bundle decompression)
            // stalls the heartbeat and this watchdog alike, so one over-budget
            // reading is not proof the load died. Two in a row is: a live load
            // loop beats the heartbeat every frame it runs.
            if (++consecutiveStalls < 2)
                continue;

            yield return FailAndReturnToMainHouse(SceneTransitionState.DestinationSceneName,
                $"[LoadingSceneController] Transition stalled with no load progress for {stalled:0}s; recovering to the main house.");
            yield break;
        }
    }

    private IEnumerator LoadAddressableScene(string destinationScene)
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.Log($"[LoadingSceneController] Addressables initialization requested before loading '{destinationScene}'.");
#endif
        // Addressables 2.x: the parameterless InitializeAsync() overload passes
        // autoReleaseHandle:true and frees the handle the moment it completes.
        // Reading Status/PercentComplete on the next frame then throws
        // "Attempting to use an invalid operation handle", which kills this
        // coroutine outright and strands the player behind the fade overlay.
        // Own the handle instead and release it explicitly below.
        var initHandle = Addressables.InitializeAsync(false);
        float initStartTime = Time.realtimeSinceStartup;
        while (initHandle.IsValid() && !initHandle.IsDone)
        {
            UpdateProgress(Mathf.Clamp01(initHandle.PercentComplete * 0.1f));
            Heartbeat();
            if (BCaT.Production.BCaTPlatform.IsQuest &&
                Time.realtimeSinceStartup - initStartTime > questAddressablesInitializationTimeout)
            {
                ReleaseWhenSafe(initHandle, $"Addressables initialization timeout for '{destinationScene}'");
                yield return FailAndReturnToMainHouse(destinationScene,
                    $"[LoadingSceneController] Addressables initialization timed out after {questAddressablesInitializationTimeout:0}s before loading '{destinationScene}'.");
                yield break;
            }
            yield return null;
        }

        // An invalidated handle was auto-released on completion, which only
        // happens after a successful init; treat that as initialized rather
        // than touching Status and throwing.
        if (initHandle.IsValid() && initHandle.Status != AsyncOperationStatus.Succeeded)
        {
            string initError = initHandle.OperationException != null
                ? initHandle.OperationException.ToString()
                : "unknown Addressables initialization error";
            Addressables.Release(initHandle);
            yield return FailAndReturnToMainHouse(destinationScene,
                $"[LoadingSceneController] Addressables initialization failed before loading '{destinationScene}': {initError}");
            yield break;
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.Log($"[LoadingSceneController] Addressables initialization completed before loading '{destinationScene}'.");
#endif
        if (initHandle.IsValid())
            Addressables.Release(initHandle);

        AsyncOperationHandle<SceneInstance> handle = default;
        string startFailure = null;
        try
        {
            Debug.Log($"[LoadingSceneController] Remote (Addressables) scene load requested: '{destinationScene}'.");
            handle = Addressables.LoadSceneAsync(destinationScene, LoadSceneMode.Single);

            // The single-mode activation unloads THIS loading scene, destroying
            // this controller and stopping this coroutine — code after the
            // progress loop is not guaranteed to run on success. Store the
            // handle from the operation's own callback, which survives the
            // controller's destruction, so the bundle can actually be released
            // on the way back (pre-existing latent bug: the handle was never
            // stored and the bundle stayed resident for the app's lifetime).
            handle.Completed += completedHandle =>
            {
                BCaT.Production.Addressing.AddressablesHandleRegistry.NotifyCompleted(
                    "LoadingSceneController", destinationScene, completedHandle.Status);
                if (completedHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    Debug.Log($"[LoadingSceneController] Remote scene load completed: '{destinationScene}'.");
                    AddressableSceneHandleStore.Store(destinationScene, completedHandle);
                }
                else if (!failureRecoveryStarted)
                {
                    Debug.LogError($"[LoadingSceneController] Addressable scene load failed for '{destinationScene}': {completedHandle.OperationException}");
                }
            };
        }
        catch (System.Exception exception)
        {
            startFailure = $"[LoadingSceneController] Failed to start remote scene load '{destinationScene}': {exception}";
        }

        if (startFailure != null)
        {
            yield return FailAndReturnToMainHouse(destinationScene, startFailure);
            yield break;
        }

        float loadStartTime = Time.realtimeSinceStartup;
        while (handle.IsValid() && !handle.IsDone)
        {
            UpdateProgress(Mathf.Clamp01(handle.PercentComplete));
            Heartbeat();
            if (BCaT.Production.BCaTPlatform.IsQuest &&
                Time.realtimeSinceStartup - loadStartTime > questAddressablesSceneLoadTimeout)
            {
                ReleaseWhenSafe(handle, $"Addressable scene load timeout for '{destinationScene}'");
                yield return FailAndReturnToMainHouse(destinationScene,
                    $"[LoadingSceneController] Remote scene download/load timed out after {questAddressablesSceneLoadTimeout:0}s for '{destinationScene}'.");
                yield break;
            }
            yield return null;
        }

        if (handle.IsValid() && handle.Status != AsyncOperationStatus.Succeeded)
        {
            string loadError = handle.OperationException != null
                ? handle.OperationException.ToString()
                : "unknown Addressables scene load error";
            Addressables.Release(handle);
            yield return FailAndReturnToMainHouse(destinationScene,
                $"[LoadingSceneController] Remote scene download/load failed for '{destinationScene}': {loadError}");
            yield break;
        }

        // Success handling (handle storage + registry notification) runs in the
        // Completed callback above — this coroutine dies with the loading scene
        // when the destination activates, so nothing more can be done here.
    }

    private IEnumerator FailAndReturnToMainHouse(string destinationScene, string message)
    {
        if (failureRecoveryStarted)
            yield break;

        failureRecoveryStarted = true;
        Debug.LogError(message);
        SceneTransitionState.CancelTransition(message);
        BCaT.Production.Interaction.InteractionState.ForceCloseAll();

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

    private static void ReleaseWhenSafe<T>(AsyncOperationHandle<T> handle, string reason)
    {
        if (!handle.IsValid())
            return;

        if (handle.IsDone)
        {
            Addressables.Release(handle);
            return;
        }

        Debug.LogWarning($"[LoadingSceneController] Addressables handle still running during failure cleanup. reason='{reason}'. It will be released when it completes.");
        handle.Completed += completedHandle =>
        {
            if (completedHandle.IsValid())
                Addressables.Release(completedHandle);
        };
    }

    private void UpdateProgress(float progress)
    {
        if (progressText != null)
            progressText.text = $"{Mathf.RoundToInt(progress * 100f)}%";
    }
}
