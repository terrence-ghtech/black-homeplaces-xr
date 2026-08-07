using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace BCaT.Production
{
    /// <summary>
    /// The per-scene half of the platform architecture: applies the platform
    /// that <see cref="BCaTPlatform"/> already decided.
    ///
    /// Why a scene component rather than a global manager doing everything:
    /// Unity runs every active object's Awake before any
    /// RuntimeInitializeOnLoadMethod(AfterSceneLoad), so a global sweep is
    /// always too late to stop wrong-platform code from waking up. This
    /// component runs in Awake, and correctness comes not from execution order
    /// but from both platform branches being authored INACTIVE — an inactive
    /// object never runs Awake at all.
    ///
    /// Step order inside Awake is deliberate and load-bearing:
    ///   1. Configure the scene's single EventSystem (activate it and give it
    ///      the profile's input module) BEFORE anything else. XRI's
    ///      RegisteredUIInteractorCache auto-creates its own EventSystem when it
    ///      cannot find an ACTIVE one, so activating the rig first leaves the
    ///      scene with two EventSystems and two XRUIInputModules.
    ///   2. Activate the matching platform branch; leave the other inactive.
    ///   3. Resolve development-only subtrees inside the activated branch.
    ///   4. Register the activated rig so services can find it without
    ///      scene-wide searches.
    ///   5. Log one structured line and self-verify.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScenePlatformBinding : MonoBehaviour
    {
        [Header("Platform branches (both authored INACTIVE)")]
        [Tooltip("Platform/Desktop — holds the desktop rig.")]
        [SerializeField] private GameObject desktopBranch;

        [Tooltip("Platform/Quest — holds the XR rig, XR Interaction Manager and DevOnly aids.")]
        [SerializeField] private GameObject questBranch;

        [Header("Shared UI")]
        [Tooltip("The scene's single EventSystem. Its input module is assigned here at runtime; " +
                 "author it with no module so there is exactly one owner.")]
        [SerializeField] private EventSystem sceneEventSystem;

        [Header("Behavior")]
        [Tooltip("Name of the development-only container inside a platform branch.")]
        [SerializeField] private string devOnlyGroupName = "DevOnly";

        [Tooltip("Inhabited scenes carry a player rig. Presentation scenes (menu, loading) carry a " +
                 "head-tracked camera only, so a missing rig is expected rather than an error.")]
        [SerializeField] private bool expectsPlayerRig = true;

        public GameObject ActiveBranch { get; private set; }
        public ScenePlayerRig ActiveRig { get; private set; }

        void Awake()
        {
            BCaTPlatformId platform = BCaTPlatform.Current;
            BCaTPlatformProfile profile = BCaTPlatform.Profile;

            // 1. EventSystem first — see the class comment.
            ConfigureEventSystem(profile);

            // 2. Exactly one branch.
            GameObject wanted = platform == BCaTPlatformId.Quest ? questBranch : desktopBranch;
            GameObject other = platform == BCaTPlatformId.Quest ? desktopBranch : questBranch;

            if (other != null && other.activeSelf)
            {
                Debug.LogWarning($"[ScenePlatformBinding] Scene '{gameObject.scene.name}': branch " +
                                 $"'{other.name}' was authored ACTIVE and its components have already " +
                                 "run Awake on the wrong platform. Author both branches inactive.");
                other.SetActive(false);
            }

            if (wanted == null)
            {
                Debug.LogError($"[ScenePlatformBinding] Scene '{gameObject.scene.name}': no branch is " +
                               $"wired for platform {platform}. The scene has no player rig and is " +
                               "unplayable on this platform.");
                return;
            }

            ActiveBranch = wanted;
            wanted.SetActive(true);

            // 3. Development aids ride along with the Quest branch; keep them
            //    out of any session that did not ask for them.
            ResolveDevOnly(wanted);

            // 4. Publish the rig.
            ActiveRig = FindRig(wanted, profile.rigKind);
            ScenePlayerRigRegistry.Register(gameObject.scene, ActiveRig);

            // 5. One structured line, and the checks that catch a mis-authored scene.
            Camera rigCamera = ActiveRig != null ? FindMainCamera(ActiveRig.transform) : null;
            Debug.Log($"[ScenePlatformBinding] Scene '{gameObject.scene.name}': platform={platform} " +
                      $"profile='{profile.displayName}' branch='{wanted.name}' " +
                      $"rig='{(ActiveRig != null ? ActiveRig.name : "none")}' " +
                      $"camera='{(rigCamera != null ? rigCamera.name : "none")}' " +
                      $"eventSystem='{(sceneEventSystem != null ? sceneEventSystem.name : "none")}' " +
                      $"module={profile.uiInputModule} " +
                      $"devSimulator={BCaTPlatform.WantsEditorDeviceSimulator}.");

            if (ActiveRig == null)
            {
                if (expectsPlayerRig)
                    Debug.LogError($"[ScenePlatformBinding] Scene '{gameObject.scene.name}': branch " +
                                   $"'{wanted.name}' contains no ScenePlayerRig of kind {profile.rigKind}.");
                else if (FindMainCamera(wanted.transform) == null)
                    Debug.LogError($"[ScenePlatformBinding] Scene '{gameObject.scene.name}': " +
                                   $"presentation branch '{wanted.name}' has no camera tagged " +
                                   "MainCamera; the scene would render nothing.");
            }
            else if (rigCamera == null)
            {
                Debug.LogError($"[ScenePlatformBinding] Scene '{gameObject.scene.name}': rig " +
                               $"'{ActiveRig.name}' has no camera tagged MainCamera; Camera.main will " +
                               "not resolve to the player.");
            }
        }

        void OnDestroy() => ScenePlayerRigRegistry.Unregister(gameObject.scene);

        /// <summary>
        /// Activate the scene's EventSystem and give it exactly one input
        /// module, the one the active profile asks for. Any other module is
        /// removed, so a desktop module can never survive into a Quest session
        /// or vice versa.
        /// </summary>
        void ConfigureEventSystem(BCaTPlatformProfile profile)
        {
            if (sceneEventSystem == null)
                return;

            if (!sceneEventSystem.gameObject.activeSelf)
                sceneEventSystem.gameObject.SetActive(true);

            bool wantsXr = profile.uiInputModule == BCaTUiInputModuleKind.XRUI;

            foreach (BaseInputModule module in sceneEventSystem.GetComponents<BaseInputModule>())
            {
                if (module == null)
                    continue;

                bool keep = wantsXr ? module is XRUIInputModule : module is InputSystemUIInputModule;
                if (!keep)
                {
                    Debug.Log($"[ScenePlatformBinding] Removing '{module.GetType().Name}' from " +
                              $"'{sceneEventSystem.name}': the {profile.displayName} profile uses " +
                              $"{profile.uiInputModule}.");
                    Destroy(module);
                }
            }

            bool hasWanted = wantsXr
                ? sceneEventSystem.GetComponent<XRUIInputModule>() != null
                : sceneEventSystem.GetComponent<InputSystemUIInputModule>() != null;

            if (!hasWanted)
            {
                if (wantsXr)
                    sceneEventSystem.gameObject.AddComponent<XRUIInputModule>();
                else
                    sceneEventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        /// <summary>
        /// Development-only subtrees (the XR Device Simulator) are stripped from
        /// player builds by BCaTEditorOnlyStripper. In the Editor they exist, so
        /// they must be switched off unless the current test mode wants them:
        /// a simulator running against a real headset fights it for input.
        /// </summary>
        void ResolveDevOnly(GameObject branch)
        {
            if (string.IsNullOrEmpty(devOnlyGroupName))
                return;

            bool wanted = BCaTPlatform.WantsEditorDeviceSimulator;

            foreach (Transform child in branch.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != devOnlyGroupName)
                    continue;

                if (child.gameObject.activeSelf != wanted)
                    child.gameObject.SetActive(wanted);
            }
        }

        static ScenePlayerRig FindRig(GameObject branch, ScenePlayerRig.RigKind kind)
        {
            foreach (ScenePlayerRig rig in branch.GetComponentsInChildren<ScenePlayerRig>(true))
                if (rig != null && rig.Kind == kind)
                    return rig;
            return null;
        }

        static Camera FindMainCamera(Transform root)
        {
            Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
            foreach (Camera camera in cameras)
                if (camera != null && camera.CompareTag("MainCamera"))
                    return camera;
            return cameras.Length > 0 ? cameras[0] : null;
        }
    }

    /// <summary>
    /// The rig each loaded scene activated. Lets the arrival controller, control
    /// gate and reset service ask one question instead of running the four-tier
    /// FindObjectsByType fallback that previously reconstructed the answer.
    /// </summary>
    public static class ScenePlayerRigRegistry
    {
        static readonly Dictionary<int, ScenePlayerRig> byScene = new Dictionary<int, ScenePlayerRig>();

        public static void Register(UnityEngine.SceneManagement.Scene scene, ScenePlayerRig rig)
        {
            if (rig == null)
                return;
            byScene[scene.handle] = rig;
        }

        public static void Unregister(UnityEngine.SceneManagement.Scene scene) =>
            byScene.Remove(scene.handle);

        /// <summary>The active rig, preferring the active scene's registration.</summary>
        public static ScenePlayerRig Active
        {
            get
            {
                var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (byScene.TryGetValue(activeScene.handle, out ScenePlayerRig rig) &&
                    rig != null && rig.gameObject.activeInHierarchy)
                    return rig;

                foreach (var pair in byScene)
                    if (pair.Value != null && pair.Value.gameObject.activeInHierarchy)
                        return pair.Value;

                return null;
            }
        }

        public static int Count => byScene.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => byScene.Clear();
    }
}
