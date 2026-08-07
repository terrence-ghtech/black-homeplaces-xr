using System.Collections.Generic;
using System.Linq;
using BCaT.Production.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Adds XrSelectSurface to interactables that the XRI casters cannot reach
    /// on Quest — the objects rules BCAT-Q001 and BCAT-D004 report.
    ///
    /// Driven by the same condition the validator checks rather than a hardcoded
    /// list, so it stays correct as content changes: an interaction target whose
    /// every collider is a trigger, with no existing XR select surface, is
    /// invisible to the controller ray and gets one.
    ///
    /// Deliberately does NOT touch the existing hand-authored '*_QuestXRSelect'
    /// twins. Those work on Quest today, and replacing a working headset
    /// interaction cannot be verified without a headset; XrSelectSurface exists
    /// so each can be converted as a separate, device-validated change.
    ///
    ///   Unity -executeMethod BCaT.EditorTools.BCaTXrSelectSurfaceRepair.Repair
    /// </summary>
    public static class BCaTXrSelectSurfaceRepair
    {
        static readonly string[] Scenes =
        {
            "Assets/BH_XR_MainScene.unity",
            "Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity",
        };

        [MenuItem("BCaT/Architecture/Add Missing XR Select Surfaces")]
        public static void Repair() => Execute(apply: true);

        [MenuItem("BCaT/Architecture/Report Missing XR Select Surfaces")]
        public static void Report() => Execute(apply: false);

        static void Execute(bool apply)
        {
            int total = 0;

            foreach (string scenePath in Scenes)
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var targets = new List<GameObject>();

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    {
                        GameObject go = t.gameObject;
                        if (!NeedsSurface(go))
                            continue;

                        GameObject host = ResolveHost(go);
                        if (host != null)
                        {
                            if (!targets.Contains(host))
                                targets.Add(host);
                        }
                        else
                        {
                            // Unreachable on Quest but XrSelectSurface cannot own
                            // it: dispatch does not go through an
                            // IInteractionTarget, so there is nothing to forward
                            // to. Report it rather than guessing.
                            Debug.LogWarning($"[XrSelectSurfaceRepair] '{scene.name}' → " +
                                $"'{Path(t)}' is unreachable by the XRI casters but has no " +
                                "IInteractionTarget to forward to; components: " +
                                string.Join(", ", go.GetComponents<Component>()
                                    .Where(c => c != null).Select(c => c.GetType().Name)) +
                                ". Needs an IInteractionTarget (or a legacy authored twin).");
                        }
                    }
                }

                foreach (GameObject host in targets)
                {
                    total++;
                    if (!apply)
                    {
                        Debug.Log($"[XrSelectSurfaceRepair] WOULD add XrSelectSurface to " +
                                  $"'{scene.name}' → '{Path(host.transform)}'.");
                        continue;
                    }

                    var surface = host.AddComponent<XrSelectSurface>();
                    var so = new SerializedObject(surface);
                    so.FindProperty("forwardsTo").stringValue =
                        DescribeTarget(host) ?? host.name;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log($"[XrSelectSurfaceRepair] added XrSelectSurface to " +
                              $"'{scene.name}' → '{Path(host.transform)}'.");
                }

                if (apply && targets.Count > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log($"[XrSelectSurfaceRepair] saved '{scenePath}'.");
                }
            }

            Debug.Log($"[XrSelectSurfaceRepair] {(apply ? "added" : "would add")} {total} surface(s).");

            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        /// <summary>
        /// The BCAT-Q001 / BCAT-D004 condition: something interactive here, with
        /// colliders, none of which a caster can hit, and no surface already.
        /// </summary>
        static bool NeedsSurface(GameObject go)
        {
            bool interactive = go.GetComponents<MonoBehaviour>()
                                   .Any(c => c is IInteractionTarget) ||
                               go.GetComponent<XRSimpleInteractable>() != null;
            if (!interactive)
                return false;

            // Mirror BCAT-D004 exactly. When an XRSimpleInteractable declares a
            // collider list, that list is what gets registered with the casters —
            // a non-trigger collider elsewhere on the object is NOT reachable
            // through this interactable, so it does not count.
            var interactable = go.GetComponent<XRSimpleInteractable>();
            IReadOnlyList<Collider> colliders =
                interactable != null && interactable.colliders != null && interactable.colliders.Count > 0
                    ? interactable.colliders
                    : go.GetComponentsInChildren<Collider>(true);

            if (colliders.Count == 0)
                return false;
            if (colliders.Any(c => c != null && !c.isTrigger))
                return false;

            bool hasSurface = go.GetComponentsInChildren<MonoBehaviour>(true)
                .Any(c => c != null &&
                          (c.GetType().Name == "XrSelectSurface" ||
                           c.GetType().Name == "QuestXrSelectCollider"));
            return !hasSurface;
        }

        /// <summary>
        /// Put the surface on the object that owns the interaction target, so
        /// XrSelectSurface.ResolveOwner finds it without walking past siblings.
        /// </summary>
        static GameObject ResolveHost(GameObject go)
        {
            if (go.GetComponents<MonoBehaviour>().Any(c => c is IInteractionTarget))
                return go;

            foreach (MonoBehaviour behaviour in go.GetComponentsInParent<MonoBehaviour>(true))
                if (behaviour is IInteractionTarget)
                    return go; // surface stays here; ResolveOwner walks up to the target

            return null;
        }

        static string DescribeTarget(GameObject go)
        {
            foreach (MonoBehaviour behaviour in go.GetComponentsInParent<MonoBehaviour>(true))
                if (behaviour is IInteractionTarget)
                    return behaviour.GetType().Name;
            return null;
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
