using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using BCaT.Production.Interaction;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Static verification of the Quest interaction repair, run against the
    /// saved scene so results reflect shipped data rather than in-memory state.
    ///
    /// Checks, per Quest XR select surface: real WORLD size of the collider
    /// (catches prefab-scale mistakes), non-trigger, layer 0, contact-free
    /// exclusion mask, interactable collider assignment, and the persistent
    /// selectEntered target/method.
    ///
    /// Then evaluates every IInteractionTarget's prompt for xr=true and xr=false
    /// to prove Quest wording contains no keyboard instructions while desktop
    /// wording is unchanged.
    ///
    /// -executeMethod BCaT.EditorTools.QuestInteractionVerify.Run
    /// </summary>
    public static class QuestInteractionVerify
    {
        const string SceneAssetPath = "Assets/BH_XR_MainScene.unity";
        static readonly StringBuilder R = new StringBuilder();
        static int problems;

        public static void Run()
        {
            R.Clear();
            problems = 0;
            R.AppendLine("=== Quest interaction verification ===");
            R.AppendLine($"time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            Scene scene = EditorSceneManager.OpenScene(SceneAssetPath, OpenSceneMode.Single);

            VerifySelectSurfaces();
            VerifyPrompts();
            VerifySewingRoomReference();
            VerifyContentTextNotSuppressed();

            R.AppendLine();
            R.AppendLine(problems == 0 ? "RESULT: OK" : $"RESULT: {problems} PROBLEM(S)");
            string text = R.ToString();
            Debug.Log(text);
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(Application.dataPath, "..", "Builds", "QuestInteractionVerify.txt"), text);

            if (Application.isBatchMode)
                EditorApplication.Exit(problems == 0 ? 0 : 2);
        }

        static void Fail(string message)
        {
            problems++;
            R.AppendLine("  PROBLEM: " + message);
        }

        // ---- XR select surfaces -------------------------------------------

        static void VerifySelectSurfaces()
        {
            R.AppendLine();
            R.AppendLine("--- Quest XR select surfaces (world-space, as instantiated) ---");

            var markers = UnityEngine.Object.FindObjectsByType<QuestXrSelectCollider>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            R.AppendLine($"count: {markers.Length}");

            foreach (var marker in markers.OrderBy(m => Path(m.transform)))
            {
                R.AppendLine();
                R.AppendLine("  " + Path(marker.transform));

                var box = marker.GetComponent<BoxCollider>();
                if (box == null)
                {
                    Fail($"{marker.name}: no BoxCollider");
                    continue;
                }

                Vector3 worldSize = box.bounds.size;
                R.AppendLine($"    world bounds size : {Fmt(worldSize)}  centre {Fmt(box.bounds.center)}");
                R.AppendLine($"    isTrigger={box.isTrigger} layer={marker.gameObject.layer} " +
                             $"activeSelf={marker.gameObject.activeSelf}");

                // Aimability: too small is unusable in headset, too large steals the ray.
                float min = Mathf.Min(worldSize.x, worldSize.y, worldSize.z);
                float max = Mathf.Max(worldSize.x, worldSize.y, worldSize.z);
                if (min < 0.12f)
                    Fail($"{marker.name}: world size {Fmt(worldSize)} is too small to aim at " +
                         "(likely a prefab-scale error)");
                if (max > 5f)
                    Fail($"{marker.name}: world size {Fmt(worldSize)} is too large; it would steal the ray");

                if (box.isTrigger)
                    Fail($"{marker.name}: collider is a trigger; both XRI casters ignore triggers");
                if (marker.gameObject.layer != 0)
                    Fail($"{marker.name}: layer {marker.gameObject.layer} is outside the XRI cast masks (need 0)");

                var interactable = marker.GetComponent<XRSimpleInteractable>();
                if (interactable == null)
                {
                    Fail($"{marker.name}: no XRSimpleInteractable");
                    continue;
                }

                bool assigned = interactable.colliders != null &&
                                interactable.colliders.Count == 1 &&
                                interactable.colliders[0] == box;
                R.AppendLine($"    interactable colliders: {(interactable.colliders?.Count ?? 0)} " +
                             $"explicitlyAssigned={assigned}");
                if (!assigned)
                    Fail($"{marker.name}: XRSimpleInteractable colliders not exactly [own box]");

                int count = interactable.selectEntered.GetPersistentEventCount();
                if (count != 1)
                    Fail($"{marker.name}: expected exactly 1 persistent selectEntered listener, found {count}");
                for (int i = 0; i < count; i++)
                {
                    UnityEngine.Object t = interactable.selectEntered.GetPersistentTarget(i);
                    string method = interactable.selectEntered.GetPersistentMethodName(i);
                    R.AppendLine($"    selectEntered[{i}] -> {(t == null ? "NULL" : t.GetType().Name)}.{method}()");
                    if (t == null)
                        Fail($"{marker.name}: selectEntered target is null");
                }
            }
        }

        // ---- prompts -------------------------------------------------------

        static void VerifyPrompts()
        {
            R.AppendLine();
            R.AppendLine("--- prompt wording for every router target ---");
            R.AppendLine("(xr=true is what Quest shows; xr=false is desktop and must be unchanged)");

            string[] banned = { "Press E", "press E", "keyboard", "Keyboard", "mouse", "Mouse", "click", "Click" };

            foreach (MonoBehaviour behaviour in UnityEngine.Object
                         .FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                         .OrderBy(b => b == null ? "" : Path(b.transform)))
            {
                if (behaviour is not IInteractionTarget target)
                    continue;

                string xrPrompt, desktopPrompt;
                try
                {
                    xrPrompt = target.GetPrompt(true) ?? string.Empty;
                    desktopPrompt = target.GetPrompt(false) ?? string.Empty;
                }
                catch (Exception e)
                {
                    Fail($"{Path(behaviour.transform)}: GetPrompt threw {e.GetType().Name}");
                    continue;
                }

                R.AppendLine();
                R.AppendLine($"  {Path(behaviour.transform)} [{behaviour.GetType().Name}]");
                R.AppendLine($"    quest  : \"{xrPrompt}\"");
                R.AppendLine($"    desktop: \"{desktopPrompt}\"");

                foreach (string bad in banned)
                {
                    if (xrPrompt.Contains(bad))
                        Fail($"{Path(behaviour.transform)}: Quest prompt contains \"{bad}\" -> \"{xrPrompt}\"");
                }

                // Empty Quest prompt is only legitimate for the two sanctioned
                // floating-prompt exhibits, which deliberately suppress the HUD.
                bool sanctioned = behaviour.GetType().Name is "BlackKitchenPortalController"
                                                            or "PrivacyLawExhibitController";
                if (string.IsNullOrWhiteSpace(xrPrompt) && !sanctioned)
                    Fail($"{Path(behaviour.transform)}: empty Quest prompt on a bottom-HUD interactable");

                if (!string.IsNullOrWhiteSpace(xrPrompt) && xrPrompt.Trim() == "Interact")
                    Fail($"{Path(behaviour.transform)}: Quest prompt is bare \"Interact\" with no object name");
            }
        }

        // ---- Sewing Room reference ----------------------------------------

        static void VerifySewingRoomReference()
        {
            R.AppendLine();
            R.AppendLine("--- Sewing Room video (working reference) must be untouched ---");

            GameObject quiltSelect = FindDeep("Quilt_XRSelect");
            if (quiltSelect == null)
            {
                Fail("Quilt_XRSelect not found — the working Quest video reference is missing");
                return;
            }

            var box = quiltSelect.GetComponent<BoxCollider>();
            var interactable = quiltSelect.GetComponent<XRSimpleInteractable>();
            R.AppendLine($"  {Path(quiltSelect.transform)}");
            R.AppendLine($"    isTrigger={box?.isTrigger} worldSize={(box != null ? Fmt(box.bounds.size) : "n/a")} " +
                         $"layer={quiltSelect.layer}");
            R.AppendLine($"    selectEntered listeners={interactable?.selectEntered.GetPersistentEventCount()}");
            if (quiltSelect.GetComponent<QuestXrSelectCollider>() != null)
                Fail("Quilt_XRSelect was modified by the repair (it must stay exactly as it shipped)");
            if (box == null || box.isTrigger)
                Fail("Quilt_XRSelect collider changed");
        }

        // ---- curatorial content must not be suppressed ---------------------

        static void VerifyContentTextNotSuppressed()
        {
            R.AppendLine();
            R.AppendLine("--- exhibit content text that the old suppressor hid by name-guessing ---");

            string[] contentObjects = { "SewingPrompt", "9NightPrompt" };
            foreach (string name in contentObjects)
            {
                GameObject go = FindDeep(name);
                if (go == null)
                {
                    Fail($"content object '{name}' not found");
                    continue;
                }

                var text = go.GetComponent<TMP_Text>();
                if (text == null)
                {
                    Fail($"'{name}' has no TMP_Text");
                    continue;
                }

                string preview = (text.text ?? string.Empty).Replace("\n", " ");
                if (preview.Length > 80) preview = preview.Substring(0, 80) + "…";
                R.AppendLine($"  {name}: componentEnabled={text.enabled} gameObjectActive={go.activeSelf}");
                R.AppendLine($"    text=\"{preview}\"");

                if (string.IsNullOrWhiteSpace(text.text))
                    Fail($"'{name}' text content is empty");
                if (!text.enabled)
                    Fail($"'{name}' TMP component is disabled in scene data");
                if (go.GetComponent<PlatformInteractionPrompt>() != null)
                    Fail($"'{name}' carries PlatformInteractionPrompt, so the suppressor would hide it");
            }
        }

        // ---- helpers -------------------------------------------------------

        static GameObject FindDeep(string name)
        {
            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t != null && t.name == name && t.gameObject.scene.IsValid())
                    return t.gameObject;
            }
            return null;
        }

        static string Path(Transform transform)
        {
            if (transform == null) return "(null)";
            string path = transform.name;
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            return path;
        }

        static string Fmt(Vector3 v) => $"({v.x:F2}, {v.y:F2}, {v.z:F2})";
    }
}
