using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using BCaT.Production.Interaction;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Quest-only interaction repair.
    ///
    /// For every exhibit that is unreachable by the Quest controller ray (its
    /// interaction shell is a trigger collider, which both XRI casters ignore),
    /// this creates a sibling "&lt;name&gt;_QuestXRSelect" child carrying:
    ///   * a NON-trigger BoxCollider sized to the artifact,
    ///   * an XRSimpleInteractable whose colliders list is exactly that box,
    ///   * a persistent selectEntered listener to the exhibit's existing handler,
    ///   * a QuestXrSelectCollider marker (Quest-only, contact-free).
    ///
    /// It also writes the curatorial Quest prompt strings into the exhibits'
    /// existing serialized prompt configs, so headset prompts read
    /// "Play — &lt;Title&gt;" instead of desktop "Press E" wording.
    ///
    /// Idempotent: re-running updates the existing objects in place.
    /// Run with -executeMethod BCaT.EditorTools.QuestInteractionRepair.Run
    /// </summary>
    public static class QuestInteractionRepair
    {
        const string SceneAssetPath = "Assets/BH_XR_MainScene.unity";
        const string VintageCameraPrefab =
            "Assets/BCaT_assets/LindaLeaks/Prefabs/LindaLeaks_Exhibit_VintageCamera.prefab";
        const string PrivacyLawPrefab =
            "Assets/BCaT/Exhibits/PrivacyLawExhibit/Prefabs/PrivacyLawExhibit.prefab";
        const string ChildSuffix = "_QuestXRSelect";

        static readonly StringBuilder Report = new StringBuilder();

        sealed class Target
        {
            public string HostPath;          // GO that owns the trigger interaction shell
            public string HandlerPath;       // GO holding the handler component (null = HostPath)
            public string HandlerType;       // component type name
            public string Method;            // method wired to selectEntered
            public string BoundsFromPath;    // GO whose renderers define the box (null = HostPath)
            public float MaxExtent = 2.5f;   // clamp per axis, metres
            public string CuratorialName;    // Quest prompt object name
            public string Note;
        }

        const string MeshellRoot = "_SceneContent/ImplementedContributorInstallations/Meshell_Sturgis";
        const string RiRoot = "_SceneContent/ImplementedContributorInstallations/RI";
        const string LindaLeaksRoot = "_SceneContent/ImplementedContributorInstallations/LindaLeaks_Exhibit";

        static readonly Target[] SceneTargets =
        {
            new Target
            {
                HostPath = MeshellRoot + "/SecurityCamera/Camera_VideoInteraction",
                HandlerType = "MediaVideoController",
                Method = "OnXRSelect",
                BoundsFromPath = MeshellRoot + "/SecurityCamera",
                MaxExtent = 1.2f,
                CuratorialName = "Front Home Security Camera",
                Note = "Meshell Sturgis — front-home security camera",
            },
            new Target
            {
                HostPath = MeshellRoot + "/SecurityMonitor/Camera_VideoInteraction",
                HandlerType = "MediaVideoController",
                Method = "OnXRSelect",
                BoundsFromPath = MeshellRoot + "/SecurityMonitor",
                MaxExtent = 1.2f,
                CuratorialName = "Security Monitor",
                Note = "Meshell Sturgis — security monitor",
            },
            new Target
            {
                HostPath = MeshellRoot + "/NotePads",
                HandlerType = "LindaLeaksPanelOpener",
                Method = "OnXRSelect",
                MaxExtent = 1.1f,
                CuratorialName = "Meshell Sturgis Research Papers",
                Note = "Meshell Sturgis — research papers",
            },
            new Target
            {
                HostPath = RiRoot + "/Vanity_asset/Vanity_VideoInteraction",
                HandlerType = "MediaVideoController",
                Method = "OnXRSelect",
                BoundsFromPath = RiRoot + "/Vanity_asset",
                MaxExtent = 2.0f,
                CuratorialName = "You Don't Know About Style, My Darling",
                Note = "Vanity video",
            },
            new Target
            {
                HostPath = RiRoot + "/Kitchen_asset/Kitchen_VideoInteraction",
                HandlerType = "MediaVideoController",
                Method = "OnXRSelect",
                BoundsFromPath = RiRoot + "/Kitchen_asset",
                MaxExtent = 2.0f,
                CuratorialName = "Such Lovely Gravy",
                Note = "RI kitchen video (same defect class)",
            },
            new Target
            {
                // Sized at instance level, not in the prefab: the prefab authors
                // the camera mesh ~20x oversized and the instance scales it down,
                // so a box sized in prefab space came out 7 cm wide in world space.
                HostPath = LindaLeaksRoot + "/VintageCamera_Preview/Artifact_VintageCamera",
                HandlerType = "MediaVideoController",
                Method = "OnXRSelect",
                MaxExtent = 1.0f,
                CuratorialName = "Linda Leaks Video",
                Note = "Linda Leaks video (vintage camera artifact)",
            },
        };

        public static void Run()
        {
            int failures = 0;
            Report.Clear();
            Report.AppendLine("=== Quest interaction repair ===");
            Report.AppendLine($"time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Report.AppendLine();

            try
            {
                failures += RepairScene();
                failures += RepairPrivacyLawPrefab();
            }
            catch (Exception e)
            {
                failures++;
                Report.AppendLine($"EXCEPTION: {e}");
            }

            Report.AppendLine();
            Report.AppendLine(failures == 0 ? "RESULT: OK" : $"RESULT: {failures} FAILURE(S)");
            string text = Report.ToString();
            Debug.Log(text);
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(Application.dataPath, "..", "Builds", "QuestInteractionRepair.txt"), text);

            if (Application.isBatchMode)
                EditorApplication.Exit(failures == 0 ? 0 : 2);
        }

        // ---- scene ---------------------------------------------------------

        static int RepairScene()
        {
            int failures = 0;
            Scene scene = EditorSceneManager.OpenScene(SceneAssetPath, OpenSceneMode.Single);
            Report.AppendLine($"--- scene {SceneAssetPath}");

            foreach (Target target in SceneTargets)
            {
                GameObject host = FindByPath(scene, target.HostPath);
                if (host == null)
                {
                    Report.AppendLine($"  FAIL host not found: {target.HostPath}");
                    failures++;
                    continue;
                }

                GameObject handlerGo = target.HandlerPath == null
                    ? host
                    : FindByPath(scene, target.HandlerPath);
                if (handlerGo == null)
                {
                    Report.AppendLine($"  FAIL handler host not found: {target.HandlerPath}");
                    failures++;
                    continue;
                }

                GameObject boundsGo = target.BoundsFromPath == null
                    ? host
                    : FindByPath(scene, target.BoundsFromPath);

                if (!BuildSelectSurface(host, handlerGo, boundsGo, target))
                    failures++;

                if (!string.IsNullOrEmpty(target.CuratorialName))
                    ApplyCuratorialName(handlerGo, target);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Report.AppendLine("  scene saved.");
            return failures;
        }

        static int RepairPrivacyLawPrefab()
        {
            Report.AppendLine($"--- prefab {PrivacyLawPrefab}");
            GameObject root = PrefabUtility.LoadPrefabContents(PrivacyLawPrefab);
            if (root == null)
            {
                Report.AppendLine("  FAIL could not load prefab.");
                return 1;
            }

            int failures = 0;
            try
            {
                Transform proximity = root.transform.Find("ProximityTrigger");
                Transform controller = root.transform.Find("PrivacyLawExhibitController");
                if (proximity == null || controller == null)
                {
                    Report.AppendLine("  FAIL ProximityTrigger or PrivacyLawExhibitController not found.");
                    failures++;
                }
                else
                {
                    var target = new Target
                    {
                        HostPath = "ProximityTrigger",
                        HandlerType = "PrivacyLawExhibitController",
                        Method = "OpenFromXR",
                        MaxExtent = 2.2f,
                        // Floating hologram prompt owns the wording; no bottom prompt.
                        CuratorialName = null,
                        Note = "Front Home Privacy Zones hologram",
                    };
                    if (!BuildSelectSurface(proximity.gameObject, controller.gameObject,
                            proximity.gameObject, target))
                        failures++;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrivacyLawPrefab);
                Report.AppendLine("  prefab saved.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return failures;
        }

        // ---- surface construction -----------------------------------------

        static bool BuildSelectSurface(GameObject host, GameObject handlerGo, GameObject boundsGo,
            Target target)
        {
            Component handler = handlerGo.GetComponents<Component>()
                .FirstOrDefault(c => c != null && c.GetType().Name == target.HandlerType);
            if (handler == null)
            {
                Report.AppendLine($"  FAIL {target.HandlerType} not found on '{handlerGo.name}'.");
                return false;
            }

            if (handler.GetType().GetMethod(target.Method,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) == null)
            {
                Report.AppendLine($"  FAIL {target.HandlerType}.{target.Method}() not found.");
                return false;
            }

            string childName = host.name + ChildSuffix;
            Transform existing = host.transform.Find(childName);
            GameObject child;
            if (existing != null)
            {
                child = existing.gameObject;
            }
            else
            {
                child = new GameObject(childName);
                child.transform.SetParent(host.transform, false);
            }

            child.layer = 0; // Default: the only layer both XRI casters include
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;

            if (!TryResolveBox(host, boundsGo, target, out Vector3 worldCenter, out Vector3 worldSize))
            {
                Report.AppendLine($"  FAIL could not resolve bounds for '{host.name}'.");
                return false;
            }

            child.transform.position = worldCenter;

            // NOTE: do not use '??' with GetComponent — Unity returns a
            // fake-null wrapper whose reference is non-null, so '??' would skip
            // AddComponent and then throw MissingComponentException.
            var box = child.GetComponent<BoxCollider>();
            if (box == null)
                box = child.AddComponent<BoxCollider>();
            box.isTrigger = false;
            // Local size must undo the inherited world scale.
            Vector3 lossy = child.transform.lossyScale;
            box.center = Vector3.zero;
            box.size = new Vector3(
                Mathf.Approximately(lossy.x, 0f) ? worldSize.x : worldSize.x / Mathf.Abs(lossy.x),
                Mathf.Approximately(lossy.y, 0f) ? worldSize.y : worldSize.y / Mathf.Abs(lossy.y),
                Mathf.Approximately(lossy.z, 0f) ? worldSize.z : worldSize.z / Mathf.Abs(lossy.z));
            box.excludeLayers = ~0;
            box.includeLayers = 0;

            var interactable = child.GetComponent<XRSimpleInteractable>();
            if (interactable == null)
                interactable = child.AddComponent<XRSimpleInteractable>();
            interactable.colliders.Clear();
            interactable.colliders.Add(box);

            // Rebuild the persistent listener so re-runs never duplicate it.
            int count = interactable.selectEntered.GetPersistentEventCount();
            for (int i = count - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(interactable.selectEntered, i);

            var action = Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), handler,
                target.Method) as UnityEngine.Events.UnityAction;
            UnityEventTools.AddVoidPersistentListener(interactable.selectEntered, action);

            var marker = child.GetComponent<QuestXrSelectCollider>();
            if (marker == null)
                marker = child.AddComponent<QuestXrSelectCollider>();
            SetPrivateString(marker, "forwardsTo", $"{target.HandlerType}.{target.Method}");

            Report.AppendLine($"  OK {target.Note}");
            Report.AppendLine($"     host   : {Path(host.transform)}");
            Report.AppendLine($"     surface: {childName} worldCenter={Fmt(worldCenter)} worldSize={Fmt(worldSize)}");
            Report.AppendLine($"     select : {target.HandlerType}.{target.Method}() on '{handlerGo.name}'");
            return true;
        }

        /// <summary>
        /// Box = renderer bounds of the artifact when available (that volume is
        /// already occupied by its own geometry), else the existing trigger
        /// collider's bounds. Clamped per axis so one exhibit cannot dominate
        /// the ray, and floored so tiny artifacts stay comfortably aimable.
        /// </summary>
        static bool TryResolveBox(GameObject host, GameObject boundsGo, Target target,
            out Vector3 center, out Vector3 size)
        {
            center = default;
            size = default;

            Bounds bounds = default;
            bool has = false;
            string source = "none";

            if (boundsGo != null)
            {
                foreach (Renderer renderer in boundsGo.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null || renderer is ParticleSystemRenderer)
                        continue;
                    if (!has) { bounds = renderer.bounds; has = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                if (has) source = "renderers";
            }

            if (!has)
            {
                foreach (Collider collider in host.GetComponentsInChildren<Collider>(true))
                {
                    if (collider == null || collider.GetComponent<QuestXrSelectCollider>() != null)
                        continue;
                    if (!has) { bounds = collider.bounds; has = true; }
                    else bounds.Encapsulate(collider.bounds);
                }
                if (has) source = "colliders";
            }

            if (!has)
                return false;

            center = bounds.center;
            float max = target.MaxExtent;
            size = new Vector3(
                Mathf.Clamp(bounds.size.x, 0.35f, max),
                Mathf.Clamp(bounds.size.y, 0.35f, max),
                Mathf.Clamp(bounds.size.z, 0.35f, max));

            Report.AppendLine($"     bounds : source={source} raw={Fmt(bounds.size)} clamp={max}m");
            return true;
        }

        // ---- curatorial prompt text ---------------------------------------

        /// <summary>
        /// Writes the Quest prompt string into the exhibit's existing
        /// SharedInteractionPromptConfig (field "prompt"). Only the xrPrompt and
        /// objectName are set; desktopPrompt is left untouched so desktop
        /// wording is unchanged.
        /// </summary>
        static void ApplyCuratorialName(GameObject handlerGo, Target target)
        {
            Component handler = handlerGo.GetComponents<Component>()
                .FirstOrDefault(c => c != null && c.GetType().Name == target.HandlerType);
            if (handler == null)
                return;

            var so = new SerializedObject(handler);
            SerializedProperty prompt = so.FindProperty("prompt");
            if (prompt == null)
            {
                Report.AppendLine($"     prompt : no 'prompt' field on {target.HandlerType}; skipped.");
                return;
            }

            string verb = target.HandlerType == "LindaLeaksPanelOpener" ? "View" : "Play";
            string xrText = $"{verb} — {target.CuratorialName}";

            SerializedProperty xr = prompt.FindPropertyRelative("xrPrompt");
            SerializedProperty objectName = prompt.FindPropertyRelative("objectName");
            if (xr != null) xr.stringValue = xrText;
            if (objectName != null) objectName.stringValue = target.CuratorialName;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(handler);

            Report.AppendLine($"     prompt : xrPrompt=\"{xrText}\"");
        }

        // ---- helpers -------------------------------------------------------

        static void SetPrivateString(Component component, string field, string value)
        {
            var so = new SerializedObject(component);
            SerializedProperty property = so.FindProperty(field);
            if (property != null)
            {
                property.stringValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(component);
            }
        }

        static GameObject FindByPath(Scene scene, string path)
        {
            string[] parts = path.Split('/');
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != parts[0])
                    continue;

                Transform current = root.transform;
                for (int i = 1; i < parts.Length && current != null; i++)
                    current = current.Find(parts[i]);

                if (current != null)
                    return current.gameObject;
            }
            return null;
        }

        static string Path(Transform transform)
        {
            string path = transform.name;
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            return path;
        }

        static string Fmt(Vector3 v) => $"({v.x:F2}, {v.y:F2}, {v.z:F2})";
    }
}
