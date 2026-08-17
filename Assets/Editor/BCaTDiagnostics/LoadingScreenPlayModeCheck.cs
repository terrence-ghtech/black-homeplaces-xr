using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BCaT.Diagnostics.Editor
{
    /// <summary>
    /// Headless Play Mode check for the loading-screen presentation. Run with:
    ///   Unity -batchmode -nographics -executeMethod
    ///     BCaT.Diagnostics.Editor.LoadingScreenPlayModeCheck.Run
    /// (no -quit; the check exits the editor itself).
    ///
    /// It enters Play Mode in the main menu scene, requests a transition
    /// through the LoadingScene back to the menu (a light destination that is
    /// safe to load headless), and passes only if the LoadingScreenUi was
    /// observed alive during the load and the destination scene activated.
    /// The transition request is made from the editor domain, which is valid
    /// because editor and runtime share statics while in Play Mode.
    /// </summary>
    public static class LoadingScreenPlayModeCheck
    {
        const string ActiveFlag = "BCaT.LoadingScreenPlayModeCheck.Active";
        const string MenuScene = "MainMenuScene";
        const float TimeoutSeconds = 180f;

        static bool requested;
        static bool sawLoadingScene;
        static bool sawLoadingUi;
        static double startTime;
        static int errorCount;

        public static void Run()
        {
            SessionState.SetBool(ActiveFlag, true);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                "Assets/BCaT/ProductionCore/Scenes/MainMenuScene.unity");
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        static void HookAfterDomainReload()
        {
            if (!SessionState.GetBool(ActiveFlag, false))
                return;

            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying)
                return;

            string scene = SceneManager.GetActiveScene().name;
            if (!requested)
            {
                if (scene != MenuScene)
                    return;

                requested = true;
                startTime = EditorApplication.timeSinceStartup;
                bool accepted = SceneTransitionState.RequestTransition(MenuScene, string.Empty, scene);
                Debug.Log($"[LoadingScreenPlayModeCheck] Transition request accepted={accepted}; loading '{SceneTransitionState.LoadingSceneName}'.");
                if (!accepted)
                {
                    Finish(3, "transition request was rejected");
                    return;
                }
                SceneManager.LoadScene(SceneTransitionState.LoadingSceneName, LoadSceneMode.Single);
                return;
            }

            if (scene == SceneTransitionState.LoadingSceneName)
            {
                sawLoadingScene = true;
                if (Object.FindFirstObjectByType<LoadingScreenUi>() != null)
                    sawLoadingUi = true;
            }
            else if (scene == MenuScene && sawLoadingScene)
            {
                if (!sawLoadingUi)
                {
                    Finish(2, "LoadingScene ran but no LoadingScreenUi was observed");
                    return;
                }
                Finish(errorCount == 0 ? 0 : 1,
                    $"destination activated after loading screen; runtimeErrors={errorCount}");
                return;
            }

            if (EditorApplication.timeSinceStartup - startTime > TimeoutSeconds)
                Finish(4, $"timed out; lastScene='{scene}', sawLoadingScene={sawLoadingScene}, sawLoadingUi={sawLoadingUi}");
        }

        static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception)
                return;
            if (condition.StartsWith("[LoadingScreenPlayModeCheck]"))
                return;
            errorCount++;
            Debug.Log($"[LoadingScreenPlayModeCheck] Captured runtime {type}: {condition}");
        }

        static void Finish(int exitCode, string reason)
        {
            SessionState.SetBool(ActiveFlag, false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            Debug.Log($"[LoadingScreenPlayModeCheck] RESULT exitCode={exitCode} sawLoadingUi={sawLoadingUi} reason: {reason}");
            EditorApplication.Exit(exitCode);
        }
    }
}
