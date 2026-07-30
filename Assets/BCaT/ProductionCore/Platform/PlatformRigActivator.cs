using UnityEngine;
using UnityEngine.SceneManagement;

namespace BCaT.Production
{
    /// <summary>
    /// Code-driven rig selection for every scene, complementing the existing
    /// ScenePlatformRigSelector (which is only authored into the Black Kitchen
    /// scene). On each scene load it finds all ScenePlayerRig markers —
    /// including inactive ones, because the main scene ships with its XR rig
    /// container authored inactive — and activates exactly the rig matching the
    /// runtime platform, deactivating the other. It also removes the XR Device
    /// Simulator from non-editor desktop/Quest sessions.
    ///
    /// Idempotent with ScenePlatformRigSelector: both derive the decision from
    /// ScenePlatformRigSelector.ShouldUseXR(), so running after it re-asserts
    /// the same result.
    /// </summary>
    public sealed class PlatformRigActivator : MonoBehaviour
    {
        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void Start() => ApplyToScene(SceneManager.GetActiveScene());

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyToScene(scene);

        public void ApplyToScene(Scene scene)
        {
            bool useXR = ScenePlatformRigSelector.ShouldUseXR();

            var rigs = FindObjectsByType<ScenePlayerRig>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            int activated = 0;
            foreach (var rig in rigs)
            {
                if (rig == null || rig.gameObject.scene != scene) continue;

                bool shouldBeActive = (rig.Kind == ScenePlayerRig.RigKind.XR) == useXR;
                if (shouldBeActive)
                {
                    ActivateWithAncestors(rig.gameObject);
                    activated++;
                }
                else
                {
                    // Deactivate the rig's whole platform branch when it lives
                    // under the authored 'BuildProfiles' organizer (main scene:
                    // Web / XR containers), so sibling platform-specific test
                    // objects are excluded too; otherwise just the rig itself.
                    var branch = FindBuildProfilesBranch(rig.transform);
                    var toDisable = branch != null ? branch.gameObject : rig.gameObject;
                    if (toDisable.activeSelf)
                        toDisable.SetActive(false);
                }
            }

            if (rigs.Length > 0)
                Debug.Log($"[PlatformRigActivator] Scene '{scene.name}': platform=" +
                          $"{(useXR ? "XR" : "Desktop")}, rigs={rigs.Length}, activated={activated}.");

            RemoveXRDeviceSimulatorIfPresent(scene, useXR);
        }

        /// <summary>
        /// A rig can sit under an inactive organizer container (main scene's
        /// 'BuildProfiles/XR'); activate the chain up to the scene root.
        /// </summary>
        static void ActivateWithAncestors(GameObject go)
        {
            var t = go.transform;
            while (t != null)
            {
                if (!t.gameObject.activeSelf)
                    t.gameObject.SetActive(true);
                t = t.parent;
            }
        }

        /// <summary>
        /// The XR Device Simulator is an editor development aid; it must not run
        /// in desktop players (it consumes input) nor on Quest (real devices).
        /// </summary>
        static void RemoveXRDeviceSimulatorIfPresent(Scene scene, bool useXR)
        {
#if !UNITY_EDITOR
            foreach (var root in scene.GetRootGameObjects())
            {
                var simulator = FindChildByName(root.transform, "XR Device Simulator");
                if (simulator != null)
                {
                    Debug.Log("[PlatformRigActivator] Disabling XR Device Simulator in player build.");
                    simulator.gameObject.SetActive(false);
                }
            }
#endif
        }

        /// <summary>The ancestor that is a direct child of a 'BuildProfiles' organizer, if any.</summary>
        static Transform FindBuildProfilesBranch(Transform t)
        {
            while (t != null && t.parent != null)
            {
                if (t.parent.name == "BuildProfiles")
                    return t;
                t = t.parent;
            }
            return null;
        }

        static Transform FindChildByName(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindChildByName(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
