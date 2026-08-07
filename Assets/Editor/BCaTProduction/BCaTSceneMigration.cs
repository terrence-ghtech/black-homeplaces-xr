using System.Collections.Generic;
using System.Linq;
using BCaT.Production;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Migrates the production scenes to the platform architecture: a root
    /// Platform group with authored-inactive Desktop/Quest branches, one
    /// EventSystem under SceneServices/UI, and one ScenePlatformBinding.
    ///
    /// Everything goes through the Unity object model — GameObject creation,
    /// Transform.SetParent(worldPositionStays: true), SerializedObject wiring,
    /// EditorSceneManager.SaveScene — so prefab instances, terrain, lighting,
    /// transforms, Addressables references, spawn points and exhibit content are
    /// preserved exactly. No scene YAML is edited by hand.
    ///
    /// Idempotent: re-running finds the structures it already created and only
    /// fills in what is missing.
    ///
    /// Deliberately NOT done here: regrouping exhibit content into
    /// Environment/Navigation/Interactables/Media. That is presentational
    /// grouping with real risk (large prefab instances, collision proxies) and
    /// no platform benefit, so it is left for a separate, separately-validated
    /// change.
    ///
    ///   Unity -executeMethod BCaT.EditorTools.BCaTSceneMigration.MigrateBlackKitchen
    ///   Unity -executeMethod BCaT.EditorTools.BCaTSceneMigration.MigrateMainScene
    ///   Unity -executeMethod BCaT.EditorTools.BCaTSceneMigration.MigratePresentationScenes
    /// </summary>
    public static class BCaTSceneMigration
    {
        const string BlackKitchenScenePath =
            "Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity";
        const string MainScenePath = "Assets/BH_XR_MainScene.unity";

        const string SimulatorPrefabPath =
            "Assets/Samples/XR Interaction Toolkit/3.3.1/XR Device Simulator/XR Device Simulator.prefab";

        const string PlatformGroup = "Platform";
        const string DesktopBranch = "Desktop";
        const string QuestBranch = "Quest";
        const string DevOnlyGroup = "DevOnly";
        const string SceneServices = "SceneServices";
        const string UiGroup = "UI";
        const string SpawnPointsGroup = "SceneSpawnPoints";

        static readonly List<string> Log = new List<string>();

        // ---- Black Kitchen -------------------------------------------------

        [MenuItem("BCaT/Architecture/Migrate Black Kitchen Scene")]
        public static void MigrateBlackKitchen()
        {
            Log.Clear();
            Scene scene = EditorSceneManager.OpenScene(BlackKitchenScenePath, OpenSceneMode.Single);

            GameObject platform = EnsureRoot(scene, PlatformGroup);
            GameObject desktop = EnsureChild(platform, DesktopBranch);
            GameObject quest = EnsureChild(platform, QuestBranch);

            MoveIntoBranch(scene, "DesktopRigRoot", desktop);
            MoveIntoBranch(scene, "XR Origin (XR Rig)", quest);
            MoveIntoBranch(scene, "XR Interaction Manager", quest);

            EnsureDeviceSimulator(quest);

            // Both branches authored inactive: ScenePlatformBinding activates
            // exactly one in Awake, so no wrong-platform component ever wakes up.
            SetAuthoredActive(desktop, false);
            SetAuthoredActive(quest, false);

            GameObject services = EnsureRoot(scene, SceneServices);
            MoveIntoBranch(scene, "SceneArrivalController", services);

            EventSystem eventSystem = ConsolidateEventSystems(scene, services);
            MoveSpawnPoints(scene, services);
            RemoveLegacySelector(scene);
            WireBinding(services, desktop, quest, eventSystem);

            Save(scene);
            Report("Black Kitchen");
        }

        // ---- Main scene ----------------------------------------------------

        [MenuItem("BCaT/Architecture/Migrate Main Scene")]
        public static void MigrateMainScene()
        {
            Log.Clear();
            Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

            // The main scene already groups its rigs, under legacy names:
            // BuildProfiles/{XR,Web}. Rename in place so prefab instances,
            // transforms and sibling references are untouched.
            GameObject platform = FindRoot(scene, PlatformGroup) ?? FindRoot(scene, "BuildProfiles");
            if (platform == null)
            {
                platform = EnsureRoot(scene, PlatformGroup);
            }
            else if (platform.name != PlatformGroup)
            {
                Note($"renamed root '{platform.name}' → '{PlatformGroup}'");
                platform.name = PlatformGroup;
            }

            GameObject quest = FindChild(platform, QuestBranch) ?? FindChild(platform, "XR");
            if (quest == null) quest = EnsureChild(platform, QuestBranch);
            else if (quest.name != QuestBranch)
            {
                Note($"renamed branch '{platform.name}/{quest.name}' → '{QuestBranch}'");
                quest.name = QuestBranch;
            }

            GameObject desktop = FindChild(platform, DesktopBranch) ?? FindChild(platform, "Web");
            if (desktop == null) desktop = EnsureChild(platform, DesktopBranch);
            else if (desktop.name != DesktopBranch)
            {
                Note($"renamed branch '{platform.name}/{desktop.name}' → '{DesktopBranch}'");
                desktop.name = DesktopBranch;
            }

            // The XR Device Simulator was parented under the DESKTOP branch (via
            // 'Test_Headset_W_Keyboard'), so it was disabled with that branch
            // whenever XR was active — it could never drive the rig it exists
            // for. Move it into the Quest branch's DevOnly group.
            RelocateExistingSimulator(scene, quest);
            EnsureDeviceSimulator(quest);

            // The rigs must be active inside their (inactive) branch so
            // activating the branch activates the rig.
            EnsureRigActiveInsideBranch(desktop);
            EnsureRigActiveInsideBranch(quest);

            SetAuthoredActive(desktop, false);
            SetAuthoredActive(quest, false);

            GameObject services = EnsureRoot(scene, SceneServices);
            MoveIntoBranch(scene, "SceneArrivalController", services);

            EventSystem eventSystem = ConsolidateEventSystems(scene, services);
            MoveSpawnPoints(scene, services);
            RemoveLegacySelector(scene);
            WireBinding(services, desktop, quest, eventSystem);

            Save(scene);
            Report("Main scene");
        }

        // ---- Presentation scenes -------------------------------------------

        const string LoadingScenePath = "Assets/BCaT/SceneTransitions/Scenes/LoadingScene.unity";
        const string MainMenuScenePath = "Assets/BCaT/ProductionCore/Scenes/MainMenuScene.unity";

        const string XrRigPrefabPath =
            "Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

        [MenuItem("BCaT/Architecture/Migrate Presentation Scenes")]
        public static void MigratePresentationScenes()
        {
            Log.Clear();
            MigratePresentationScene(LoadingScenePath, "Loading Camera");
            MigratePresentationScene(MainMenuScenePath, "Menu Camera");
            Report("Presentation scenes");
        }

        /// <summary>
        /// Menu and loading scenes are presentational: no locomotion, no
        /// interaction. But a plain camera in a headset is head-locked — it does
        /// not respond to head rotation at all — and the Black Kitchen bundle
        /// load can hold that view for a long time. A head-locked view is a
        /// recognized discomfort trigger, so these scenes get a Quest branch
        /// whose camera is head-tracked.
        /// </summary>
        static void MigratePresentationScene(string scenePath, string desktopCameraName)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Note($"--- {scene.name}");

            GameObject platform = EnsureRoot(scene, PlatformGroup);
            GameObject desktop = EnsureChild(platform, DesktopBranch);
            GameObject quest = EnsureChild(platform, QuestBranch);

            MoveIntoBranch(scene, desktopCameraName, desktop);
            EnsurePresentationXrCamera(quest);

            SetAuthoredActive(desktop, false);
            SetAuthoredActive(quest, false);

            GameObject services = EnsureRoot(scene, SceneServices);
            MoveIntoBranch(scene, "LoadingSceneController", services);
            MoveIntoBranch(scene, "MainMenu", services);

            EventSystem eventSystem = ConsolidateEventSystems(scene, services);
            WireBinding(services, desktop, quest, eventSystem, expectsPlayerRig: false);

            Save(scene);
        }

        /// <summary>
        /// Build the head-tracked presentation camera by reusing the project's
        /// XR rig prefab and switching off locomotion and the interactors. That
        /// reuses a known-good TrackedPoseDriver configuration (with its input
        /// action bindings) instead of hand-wiring one, and keeps a loading
        /// screen from carrying interactors that would auto-provision an
        /// XRInteractionManager.
        /// </summary>
        static void EnsurePresentationXrCamera(GameObject questBranch)
        {
            bool hasCamera = questBranch.GetComponentsInChildren<Camera>(true).Length > 0;
            if (hasCamera)
                return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrRigPrefabPath);
            if (prefab == null)
            {
                Note($"WARNING XR rig prefab not found at {XrRigPrefabPath}; presentation scene will " +
                     "stay head-locked on Quest.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, questBranch.scene);
            instance.name = "XR Presentation";
            instance.transform.SetParent(questBranch.transform, worldPositionStays: false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            // Unpack so the unwanted subtrees can be removed outright rather
            // than merely deactivated. What remains is a small, self-contained
            // head-tracked camera that cannot drift when the XRI sample is
            // reimported — and a loading screen carries no controller meshes,
            // interactors or locomotion it will never use.
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);

            foreach (string childName in new[]
                     {
                         "Locomotion",
                         "Camera Offset/Left Controller",
                         "Camera Offset/Right Controller",
                         "Camera Offset/Gaze Interactor",
                         "Camera Offset/Gaze Stabilized",
                         "Camera Offset/Left Controller Teleport Stabilized Origin",
                         "Camera Offset/Right Controller Teleport Stabilized Origin",
                     })
            {
                Transform child = instance.transform.Find(childName);
                if (child != null)
                {
                    Object.DestroyImmediate(child.gameObject);
                    Note($"removed '{childName}' from the presentation rig");
                }
            }

            // Interaction and locomotion behaviours on the origin itself have
            // nothing left to drive in a presentation scene.
            foreach (Component component in instance.GetComponents<Component>())
            {
                if (component == null || component is Transform)
                    continue;

                string typeName = component.GetType().Name;
                if (typeName == "XROrigin" || typeName == "CharacterController")
                    continue;

                Object.DestroyImmediate(component);
                Note($"removed '{typeName}' from the presentation rig root");
            }

            // A ScenePlayerRig marker would make this look like an inhabited
            // scene; it is deliberately absent.
            foreach (ScenePlayerRig marker in instance.GetComponentsInChildren<ScenePlayerRig>(true))
                Object.DestroyImmediate(marker);

            bool tracked = instance.GetComponentsInChildren<Component>(true)
                .Any(c => c != null && c.GetType().Name == "TrackedPoseDriver");
            Note($"added head-tracked presentation camera '{Path(instance.transform)}' " +
                 $"(trackedPoseDriver={tracked})");
        }

        // ---- Shared steps --------------------------------------------------

        static GameObject FindRoot(Scene scene, string name) =>
            scene.GetRootGameObjects().FirstOrDefault(go => go.name == name);

        static GameObject FindChild(GameObject parent, string name)
        {
            foreach (Transform child in parent.transform)
                if (child.name == name)
                    return child.gameObject;
            return null;
        }

        static GameObject EnsureRoot(Scene scene, string name)
        {
            GameObject existing = FindRoot(scene, name);
            if (existing != null)
                return existing;

            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            Note($"created root '{name}'");
            return go;
        }

        static GameObject EnsureChild(GameObject parent, string name)
        {
            GameObject existing = FindChild(parent, name);
            if (existing != null)
                return existing;

            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            Note($"created '{Path(parent.transform)}/{name}'");
            return go;
        }

        /// <summary>
        /// Move a root object into a group, preserving its world transform. Also
        /// makes sure the object itself is active so activating the group
        /// activates it.
        /// </summary>
        static void MoveIntoBranch(Scene scene, string rootName, GameObject target)
        {
            GameObject go = FindRoot(scene, rootName);
            if (go == null)
            {
                // Already migrated, or not present in this scene.
                return;
            }

            go.transform.SetParent(target.transform, worldPositionStays: true);
            if (!go.activeSelf)
            {
                go.SetActive(true);
                Note($"activated '{go.name}' inside its (inactive) branch");
            }
            Note($"moved '{rootName}' → '{Path(target.transform)}'");
        }

        static void EnsureRigActiveInsideBranch(GameObject branch)
        {
            foreach (ScenePlayerRig rig in branch.GetComponentsInChildren<ScenePlayerRig>(true))
            {
                Transform cursor = rig.transform;
                while (cursor != null && cursor != branch.transform)
                {
                    if (!cursor.gameObject.activeSelf)
                    {
                        cursor.gameObject.SetActive(true);
                        Note($"activated '{Path(cursor)}' inside its (inactive) branch");
                    }
                    cursor = cursor.parent;
                }
            }
        }

        static void SetAuthoredActive(GameObject go, bool active)
        {
            if (go.activeSelf == active)
                return;
            go.SetActive(active);
            Note($"authored '{Path(go.transform)}' {(active ? "active" : "INACTIVE")}");
        }

        static void EnsureDeviceSimulator(GameObject questBranch)
        {
            GameObject devOnly = EnsureChild(questBranch, DevOnlyGroup);

            if (devOnly.GetComponent<EditorOnlyObject>() == null)
            {
                devOnly.AddComponent<EditorOnlyObject>();
                Note($"added EditorOnlyObject to '{Path(devOnly.transform)}'");
            }

            bool hasSimulator = devOnly.GetComponentsInChildren<Component>(true)
                .Any(c => c != null && c.GetType().Name == "XRDeviceSimulator");
            if (hasSimulator)
                return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPrefabPath);
            if (prefab == null)
            {
                Note($"WARNING simulator prefab not found at {SimulatorPrefabPath}");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, devOnly.scene);
            instance.transform.SetParent(devOnly.transform, worldPositionStays: false);
            instance.transform.localPosition = Vector3.zero;
            Note($"instantiated XR Device Simulator into '{Path(devOnly.transform)}'");
        }

        /// <summary>
        /// The main scene already contains a simulator instance, in the wrong
        /// branch. Move that instance rather than adding a second one.
        /// </summary>
        static void RelocateExistingSimulator(Scene scene, GameObject questBranch)
        {
            GameObject simulator = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component != null && component.GetType().Name == "XRDeviceSimulator")
                    {
                        simulator = component.gameObject;
                        break;
                    }
                }
                if (simulator != null) break;
            }

            if (simulator == null)
                return;

            if (simulator.transform.IsChildOf(questBranch.transform))
                return;

            GameObject devOnly = EnsureChild(questBranch, DevOnlyGroup);
            string from = Path(simulator.transform);
            simulator.transform.SetParent(devOnly.transform, worldPositionStays: false);
            if (!simulator.activeSelf)
                simulator.SetActive(true);
            Note($"relocated simulator '{from}' → '{Path(devOnly.transform)}' " +
                 "(it was inside the desktop branch and was disabled whenever XR was active)");
        }

        /// <summary>
        /// Collapse every EventSystem to one, under SceneServices/UI, authored
        /// with NO input module — ScenePlatformBinding assigns the active
        /// profile's module in Awake, before it activates the rig branch, so
        /// XRI's auto-provisioning finds it instead of creating a second one.
        /// </summary>
        static EventSystem ConsolidateEventSystems(Scene scene, GameObject services)
        {
            GameObject ui = EnsureChild(services, UiGroup);
            GameObject host = EnsureChild(ui, "EventSystem");

            EventSystem kept = host.GetComponent<EventSystem>();
            if (kept == null)
            {
                kept = host.AddComponent<EventSystem>();
                Note($"added EventSystem to '{Path(host.transform)}'");
            }

            foreach (BaseInputModule module in host.GetComponents<BaseInputModule>())
            {
                Note($"removed authored '{module.GetType().Name}' from the shared EventSystem " +
                     "(the platform binding assigns the module at runtime)");
                Object.DestroyImmediate(module);
            }

            var doomed = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (EventSystem other in root.GetComponentsInChildren<EventSystem>(true))
                {
                    if (other == null || other == kept)
                        continue;
                    if (!doomed.Contains(other.gameObject))
                        doomed.Add(other.gameObject);
                }
            }

            foreach (GameObject go in doomed)
            {
                Note($"removed duplicate EventSystem '{Path(go.transform)}'");
                Object.DestroyImmediate(go);
            }

            return kept;
        }

        static void MoveSpawnPoints(Scene scene, GameObject services)
        {
            var spawnPoints = new List<SceneSpawnPoint>();
            foreach (GameObject root in scene.GetRootGameObjects())
                spawnPoints.AddRange(root.GetComponentsInChildren<SceneSpawnPoint>(true));

            if (spawnPoints.Count == 0)
                return;

            GameObject group = EnsureChild(services, SpawnPointsGroup);

            foreach (SceneSpawnPoint spawn in spawnPoints)
            {
                if (spawn == null || spawn.transform.parent == group.transform)
                    continue;

                string from = Path(spawn.transform);
                Transform previousParent = spawn.transform.parent;
                Vector3 worldPosition = spawn.transform.position;
                Quaternion worldRotation = spawn.transform.rotation;

                // worldPositionStays preserves the pose, but only when the new
                // and old parent chains are representable — non-uniform ancestor
                // scale can introduce skew Unity cannot express in a local
                // transform. Verify and revert rather than silently moving a
                // spawn point the player lands on.
                spawn.transform.SetParent(group.transform, worldPositionStays: true);

                float positionDrift = Vector3.Distance(spawn.transform.position, worldPosition);
                float rotationDrift = Quaternion.Angle(spawn.transform.rotation, worldRotation);
                if (positionDrift > 1e-4f || rotationDrift > 1e-3f)
                {
                    spawn.transform.SetParent(previousParent, worldPositionStays: true);
                    spawn.transform.SetPositionAndRotation(worldPosition, worldRotation);
                    Note($"SKIPPED moving spawn point '{from}': re-parenting drifted the world pose " +
                         $"by {positionDrift:F5} m / {rotationDrift:F4}° (non-uniform ancestor scale). " +
                         "Left in place; grouping is cosmetic, spawn accuracy is not.");
                    continue;
                }

                Note($"moved spawn point '{from}' → '{Path(group.transform)}' (world pose preserved)");
            }
        }

        static void RemoveLegacySelector(Scene scene)
        {
            var doomed = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour != null && behaviour.GetType().Name == "ScenePlatformRigSelector")
                        doomed.Add(behaviour.gameObject);
                }
            }

            foreach (GameObject go in doomed)
            {
                // The selector object exists only to host the selector.
                if (go.GetComponents<Component>().Length <= 2 && go.transform.childCount == 0)
                {
                    Note($"removed legacy selector object '{Path(go.transform)}'");
                    Object.DestroyImmediate(go);
                }
                else
                {
                    Note($"removed legacy ScenePlatformRigSelector component from '{Path(go.transform)}'");
                    foreach (MonoBehaviour behaviour in go.GetComponents<MonoBehaviour>())
                        if (behaviour != null && behaviour.GetType().Name == "ScenePlatformRigSelector")
                            Object.DestroyImmediate(behaviour);
                }
            }
        }

        static void WireBinding(GameObject services, GameObject desktop, GameObject quest,
            EventSystem eventSystem, bool expectsPlayerRig = true)
        {
            var binding = services.GetComponent<ScenePlatformBinding>();
            if (binding == null)
            {
                binding = services.AddComponent<ScenePlatformBinding>();
                Note($"added ScenePlatformBinding to '{Path(services.transform)}'");
            }

            var so = new SerializedObject(binding);
            so.FindProperty("desktopBranch").objectReferenceValue = desktop;
            so.FindProperty("questBranch").objectReferenceValue = quest;
            so.FindProperty("sceneEventSystem").objectReferenceValue = eventSystem;
            so.FindProperty("expectsPlayerRig").boolValue = expectsPlayerRig;
            so.ApplyModifiedPropertiesWithoutUndo();
            Note($"wired ScenePlatformBinding (desktopBranch, questBranch, sceneEventSystem, " +
                 $"expectsPlayerRig={expectsPlayerRig})");
        }

        // ---- Plumbing ------------------------------------------------------

        static void Save(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            bool ok = EditorSceneManager.SaveScene(scene);
            Note(ok ? $"saved '{scene.path}'" : $"FAILED to save '{scene.path}'");
        }

        static void Note(string message)
        {
            Log.Add(message);
            Debug.Log("[BCaTSceneMigration] " + message);
        }

        static void Report(string label)
        {
            Debug.Log($"[BCaTSceneMigration] {label}: {Log.Count} change(s).");
            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        static string Path(Transform t)
        {
            string path = t.name;
            Transform parent = t.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
