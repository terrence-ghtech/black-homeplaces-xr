using System.Collections;
using UnityEngine;
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

        AsyncOperation loadOperation = null;
        try
        {
            string loadingScene = gameObject.scene.name;
            Debug.Log($"[LoadingSceneController] Scene '{loadingScene}' destination scene load requested: '{destinationScene}'.");
            loadOperation = SceneManager.LoadSceneAsync(destinationScene, LoadSceneMode.Single);
            if (loadOperation != null)
                loadOperation.completed += _ => Debug.Log($"[LoadingSceneController] Scene '{loadingScene}' destination scene load completed: '{destinationScene}'.");
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

    private void UpdateProgress(float progress)
    {
        if (progressText != null)
            progressText.text = $"{Mathf.RoundToInt(progress * 100f)}%";
    }
}
