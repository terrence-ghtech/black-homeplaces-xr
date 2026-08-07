using UnityEngine;
using UnityEngine.SceneManagement;

public static class BlackKitchenQuestTransitionDiagnostics
{
    public const string Prefix = "[BCAT_QUEST_KITCHEN_TRANSITION]";

    private static int nextTransitionId = 1;
    private static int currentTransitionId;
    private static float transitionStartRealtime;
    private static bool applicationPaused;

    public static bool Enabled
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    public static int BeginTransition(string stage, string message)
    {
        currentTransitionId = nextTransitionId++;
        transitionStartRealtime = Time.realtimeSinceStartup;
        Log(stage, message);
        return currentTransitionId;
    }

    public static void Log(string message)
    {
        if (Enabled)
            Debug.Log($"{Prefix} {Context()} {message}");
    }

    public static void Log(string stage, string message)
    {
        if (Enabled)
            Debug.Log($"{Prefix} {Context()} stage='{stage}' {message}");
    }

    public static void Warning(string message)
    {
        if (Enabled)
            Debug.LogWarning($"{Prefix} {Context()} {message}");
    }

    public static void Warning(string stage, string message)
    {
        if (Enabled)
            Debug.LogWarning($"{Prefix} {Context()} stage='{stage}' {message}");
    }

    public static void Error(string message)
    {
        if (Enabled)
            Debug.LogError($"{Prefix} {Context()} {message}");
    }

    public static void Error(string stage, string message)
    {
        if (Enabled)
            Debug.LogError($"{Prefix} {Context()} stage='{stage}' {message}");
    }

    public static string ActiveSceneName
    {
        get
        {
            Scene scene = SceneManager.GetActiveScene();
            return scene.IsValid() ? scene.name : "<invalid>";
        }
    }

    static string Context()
    {
        int id = currentTransitionId > 0 ? currentTransitionId : 0;
        float elapsed = transitionStartRealtime > 0f ? Time.realtimeSinceStartup - transitionStartRealtime : 0f;
        return $"id={id} t={elapsed:0.000}s scene='{ActiveSceneName}' platform='{Application.platform}' focus={Application.isFocused} paused={applicationPaused}";
    }

    static void SetPaused(bool paused) => applicationPaused = paused;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallPauseMonitor()
    {
        if (!Enabled || Object.FindAnyObjectByType<PauseMonitor>() != null)
            return;

        var monitor = new GameObject("BlackKitchenQuestTransitionDiagnostics");
        Object.DontDestroyOnLoad(monitor);
        monitor.hideFlags = HideFlags.HideAndDontSave;
        monitor.AddComponent<PauseMonitor>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        nextTransitionId = 1;
        currentTransitionId = 0;
        transitionStartRealtime = 0f;
        applicationPaused = false;
    }

    private sealed class PauseMonitor : MonoBehaviour
    {
        void OnApplicationPause(bool pauseStatus) => SetPaused(pauseStatus);
        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                SetPaused(false);
        }
    }
}
