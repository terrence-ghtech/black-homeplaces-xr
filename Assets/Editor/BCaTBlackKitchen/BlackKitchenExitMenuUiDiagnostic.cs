using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BCaT.Production;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Temporary diagnostic: opens the Black Kitchen Quest exit-choice panel in
    /// forced-Quest Play Mode and reports the state of every link in the XRI UGUI
    /// interaction chain, so the reason the buttons receive no pointer events is
    /// measured rather than guessed.
    ///
    ///   Unity -executeMethod BCaT.EditorTools.BlackKitchenExitMenuUiDiagnostic.Run
    /// Result: Library/BlackKitchenExitMenuUi.log
    /// </summary>
    public static class BlackKitchenExitMenuUiDiagnostic
    {
        const string PendingKey = "BKExitMenuUi.Pending";
        const string ScenePath =
            "Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity";
        static readonly string ResultPath = Path.Combine("Library", "BlackKitchenExitMenuUi.log");

        static float startTime = -1f;
        static bool opened;
        static bool done;

        [MenuItem("BCaT/Black Kitchen/Diagnose Quest Exit Menu UI")]
        public static void Run()
        {
            Directory.CreateDirectory("Library");
            File.WriteAllText(ResultPath, "STARTED\n");
            SessionState.SetBool(PendingKey, true);
            SessionState.SetString(BCaTPlatform.EditorOverrideKey, BCaTPlatformTestMode.QuestSimulated);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        static void Resume()
        {
            if (!SessionState.GetBool(PendingKey, false))
                return;
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            startTime = -1f;
            opened = false;
            done = false;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (done || !EditorApplication.isPlaying)
                return;

            if (startTime < 0f)
                startTime = Time.realtimeSinceStartup;

            float elapsed = Time.realtimeSinceStartup - startTime;

            // Let the scene settle, then open the panel, then sample it.
            if (elapsed < 3f)
                return;

            var controller = Object.FindAnyObjectByType<BlackKitchenExperienceController>();
            if (!opened)
            {
                opened = true;
                if (controller != null)
                    controller.RequestExitChoice();
                return;
            }

            if (elapsed < 5f)
                return;

            done = true;
            EditorApplication.update -= Tick;

            var report = new StringBuilder();
            try
            {
                Sample(report, controller);
            }
            catch (System.Exception e)
            {
                report.AppendLine($"DIAGNOSTIC THREW: {e}");
            }

            File.WriteAllText(ResultPath, report.ToString());
            Debug.Log("[BKExitMenuUi]\n" + report);

            SessionState.SetBool(PendingKey, false);
            SessionState.SetString(BCaTPlatform.EditorOverrideKey, BCaTPlatformTestMode.Auto);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode)
                EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }

        static void Sample(StringBuilder r, BlackKitchenExperienceController controller)
        {
            r.AppendLine("=== Black Kitchen Quest exit-menu UI chain ===");
            r.AppendLine($"platform={BCaTPlatform.Current} isXRActive={PlatformCapabilities.IsXRActive}");
            r.AppendLine($"controller={(controller != null ? controller.name : "NULL")} " +
                         $"exitModalOpen={(controller != null && controller.IsExitModalOpen)}");
            r.AppendLine($"InteractionState.IsBlocked={BCaT.Production.Interaction.InteractionState.IsBlocked}");

            // --- EventSystem + input modules ---
            var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            r.AppendLine($"\n-- EventSystems: {eventSystems.Length}");
            foreach (EventSystem es in eventSystems)
            {
                r.AppendLine($"   '{es.name}' activeInHierarchy={es.gameObject.activeInHierarchy} " +
                             $"enabled={es.enabled} isCurrent={(EventSystem.current == es)}");
                foreach (BaseInputModule m in es.GetComponents<BaseInputModule>())
                    r.AppendLine($"      module {m.GetType().Name} enabled={m.enabled} " +
                                 $"isCurrentModule={(es.currentInputModule == m)}");
            }

            XRUIInputModule module = Object.FindAnyObjectByType<XRUIInputModule>();
            r.AppendLine($"\n-- XRUIInputModule: {(module != null ? "present" : "MISSING")}");
            if (module != null)
            {
                r.AppendLine($"   enabled={module.enabled} enableXRInput={module.enableXRInput} " +
                             $"activeInputMode={module.activeInputMode}");
                r.AppendLine($"   registered interactors: {DescribeRegisteredInteractors(module)}");
            }

            // --- Interaction manager ---
            var managers = Object.FindObjectsByType<XRInteractionManager>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            r.AppendLine($"\n-- XRInteractionManagers: {managers.Length} " +
                         $"({string.Join(", ", managers.Select(m => m.name + " enabled=" + m.enabled))})");

            // --- The panel canvas ---
            Canvas panel = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.name == "BlackKitchenExitChoice_Quest");
            r.AppendLine($"\n-- Panel canvas: {(panel != null ? "found" : "NOT FOUND")}");
            if (panel != null)
            {
                r.AppendLine($"   activeInHierarchy={panel.gameObject.activeInHierarchy} " +
                             $"enabled={panel.enabled} renderMode={panel.renderMode} " +
                             $"layer={LayerMask.LayerToName(panel.gameObject.layer)}({panel.gameObject.layer})");
                r.AppendLine($"   worldCamera={(panel.worldCamera != null ? panel.worldCamera.name : "null")} " +
                             $"scale={panel.transform.localScale} pos={panel.transform.position}");

                var group = panel.GetComponent<CanvasGroup>();
                if (group != null)
                    r.AppendLine($"   CanvasGroup alpha={group.alpha} interactable={group.interactable} " +
                                 $"blocksRaycasts={group.blocksRaycasts}");

                var tracked = panel.GetComponent<TrackedDeviceGraphicRaycaster>();
                r.AppendLine($"   TrackedDeviceGraphicRaycaster={(tracked != null ? "present enabled=" + tracked.enabled : "MISSING")}");
                var plainRaycaster = panel.GetComponent<GraphicRaycaster>();
                r.AppendLine($"   GraphicRaycaster(plain)={(plainRaycaster != null ? "present" : "absent")}");

                foreach (Button b in panel.GetComponentsInChildren<Button>(true))
                {
                    var image = b.GetComponent<Image>();
                    r.AppendLine($"   Button '{b.name}' active={b.gameObject.activeInHierarchy} " +
                                 $"interactable={b.interactable} listeners={b.onClick.GetPersistentEventCount()} " +
                                 $"image.raycastTarget={(image != null ? image.raycastTarget.ToString() : "no image")} " +
                                 $"size={((RectTransform)b.transform).rect.size}");
                }

                foreach (Graphic g in panel.GetComponentsInChildren<Graphic>(true))
                    if (g.raycastTarget)
                        r.AppendLine($"   raycastTarget graphic: '{g.name}' ({g.GetType().Name})");
            }

            // --- The runtime rays ---
            var rays = Object.FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            r.AppendLine($"\n-- XRRayInteractors in scene: {rays.Length}");
            foreach (XRRayInteractor ray in rays)
            {
                r.AppendLine($"   '{ray.name}' parent='{(ray.transform.parent != null ? ray.transform.parent.name : "none")}' " +
                             $"activeInHierarchy={ray.gameObject.activeInHierarchy} enabled={ray.enabled}");
                r.AppendLine($"      enableUIInteraction={ray.enableUIInteraction} " +
                             $"maxRaycastDistance={ray.maxRaycastDistance} lineType={ray.lineType} " +
                             $"raycastMask={ray.raycastMask.value}");
                r.AppendLine($"      interactionManager={(ray.interactionManager != null ? ray.interactionManager.name : "NULL")}");
                DescribeButtonReader(r, "uiPressInput", ray.uiPressInput);
                DescribeButtonReader(r, "selectInput", ray.selectInput);
            }

            // --- Controller parents the rays are supposed to hang from ---
            Camera cam = Camera.main;
            r.AppendLine($"\n-- Camera.main='{(cam != null ? cam.name : "NULL")}' " +
                         $"parent='{(cam != null && cam.transform.parent != null ? cam.transform.parent.name : "none")}'");
            if (cam != null && cam.transform.parent != null)
                foreach (Transform child in cam.transform.parent)
                    r.AppendLine($"   sibling '{child.name}' active={child.gameObject.activeInHierarchy}");
        }

        static void DescribeButtonReader(StringBuilder r, string label, object reader)
        {
            if (reader == null)
            {
                r.AppendLine($"      {label}: NULL");
                return;
            }

            System.Type t = reader.GetType();
            object mode = t.GetProperty("inputSourceMode")?.GetValue(reader);
            var action = t.GetProperty("inputActionPerformed")?.GetValue(reader) as UnityEngine.InputSystem.InputAction;
            string actionText = action == null
                ? "null"
                : $"'{action.name}' enabled={action.enabled} bindings=[{string.Join(" | ", action.bindings.Select(b => b.path))}] " +
                  $"controls={action.controls.Count}";
            r.AppendLine($"      {label}: mode={mode} action={actionText}");
        }

        static string DescribeRegisteredInteractors(XRUIInputModule module)
        {
            // The registered list is private; read it reflectively so the report
            // states a fact rather than an assumption.
            foreach (FieldInfo f in typeof(XRUIInputModule)
                         .GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (!typeof(System.Collections.ICollection).IsAssignableFrom(f.FieldType))
                    continue;

                if (f.GetValue(module) is System.Collections.ICollection collection &&
                    (f.Name.ToLower().Contains("interactor")))
                {
                    var names = new List<string>();
                    foreach (object item in collection)
                        names.Add(item is Object o ? o.name : item?.ToString() ?? "null");
                    return $"{f.Name} count={collection.Count} [{string.Join(", ", names)}]";
                }
            }

            return "could not locate registered-interactor collection by reflection";
        }
    }
}
