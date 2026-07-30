using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BCaT.EditorTools
{
    /// <summary>
    /// One-shot batch setup for the production architecture:
    ///  1. creates the lightweight MainMenuScene and registers the build scene
    ///     list (menu → main house → loading),
    ///  2. corrects player metadata (application identifiers, windowing),
    ///  3. copies the six production videos into Assets/StreamingAssets for
    ///     packaged local playback and records them in RemoteMediaConfig.
    /// Idempotent: safe to re-run.
    /// </summary>
    public static class ProductionProjectSetup
    {
        const string MenuScenePath = "Assets/BCaT/ProductionCore/Scenes/MainMenuScene.unity";
        const string MainScenePath = "Assets/BH_XR_MainScene.unity";
        const string LoadingScenePath = "Assets/BCaT/SceneTransitions/Scenes/LoadingScene.unity";
        const string BlackKitchenScenePath =
            "Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity";
        const string ApplicationIdentifier = "org.bcatlab.blackhomeplaces";
        const string MediaArchive = "webgl-public-optimized/StreamingAssets";

        [MenuItem("BCaT/Production Setup/Run All")]
        public static void RunAll()
        {
            CreateMainMenuScene();
            ConfigureBuildScenes();
            ConfigurePlayerSettings();
            SyncStreamingMedia();
            AssetDatabase.SaveAssets();
            Debug.Log("[ProductionSetup] Completed.");
            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        public static void CreateMainMenuScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MenuScenePath));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Menu Camera", typeof(Camera), typeof(AudioListener));
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.045f, 0.04f, 1f);
            cam.tag = "MainCamera";

            new GameObject("MainMenu", typeof(BCaT.Production.Shell.MainMenuController));

            EditorSceneManager.SaveScene(scene, MenuScenePath);
            Debug.Log($"[ProductionSetup] Menu scene saved: {MenuScenePath}");
        }

        public static void ConfigureBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(MainScenePath, true),
                new EditorBuildSettingsScene(LoadingScenePath, true),
                // Black Kitchen stays Addressables-loaded (disabled entry preserved).
                new EditorBuildSettingsScene(BlackKitchenScenePath, false),
            };
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[ProductionSetup] Build scenes: " +
                      string.Join(" | ", scenes.Select(s => $"{s.path}({(s.enabled ? "on" : "off")})")));
        }

        public static void ConfigurePlayerSettings()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, ApplicationIdentifier);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationIdentifier);
            PlayerSettings.resizableWindow = true;

            // Quest devices run Android 10+ (API 29); the previous value (25)
            // predates the Quest configuration.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;

            Debug.Log("[ProductionSetup] Player settings updated " +
                      $"(identifier={ApplicationIdentifier}, resizableWindow=on, Android minSdk=29).");
        }

        public static void SyncStreamingMedia()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string source = Path.Combine(projectRoot, MediaArchive);
            if (!Directory.Exists(source))
            {
                Debug.LogWarning($"[ProductionSetup] Media archive not found: {source}");
                return;
            }

            string target = Path.Combine(Application.dataPath, "StreamingAssets");
            Directory.CreateDirectory(target);

            var copied = new List<string>();
            foreach (string file in Directory.GetFiles(source, "*.mp4"))
            {
                string name = Path.GetFileName(file);
                string dest = Path.Combine(target, name);
                if (!File.Exists(dest) || new FileInfo(dest).Length != new FileInfo(file).Length)
                    File.Copy(file, dest, overwrite: true);
                copied.Add(name);
            }
            AssetDatabase.Refresh();

            // Record the packaged names in RemoteMediaConfig so runtime path
            // resolution can trust local files on platforms where File.Exists
            // cannot see into the package (Android/Quest).
            var config = AssetDatabase.LoadAssetAtPath<RemoteMediaConfig>(
                "Assets/Resources/RemoteMediaConfig.asset");
            if (config != null)
            {
                var so = new SerializedObject(config);
                var prop = so.FindProperty("packagedFileNames");
                if (prop != null)
                {
                    prop.arraySize = copied.Count;
                    for (int i = 0; i < copied.Count; i++)
                        prop.GetArrayElementAtIndex(i).stringValue = copied[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(config);
                    Debug.Log($"[ProductionSetup] RemoteMediaConfig.packagedFileNames = {copied.Count} entries.");
                }
                else
                {
                    Debug.LogWarning("[ProductionSetup] RemoteMediaConfig has no packagedFileNames field yet.");
                }
            }

            Debug.Log($"[ProductionSetup] StreamingAssets media synced ({copied.Count} files).");
        }
    }
}
