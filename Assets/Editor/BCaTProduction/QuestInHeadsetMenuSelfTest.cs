using System;
using System.Collections.Generic;
using System.Reflection;
using BCaT.Production;
using BCaT.Production.Interaction;
using BCaT.Production.Shell;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace BCaT.EditorTools
{
    /// <summary>Edit-time structure/state checks for the minimal Quest menu.</summary>
    public static class QuestInHeadsetMenuSelfTest
    {
        [MenuItem("BCaT/Diagnostics/Quest In-Headset Menu Self Test")]
        public static void Run()
        {
            string previousMode = SessionState.GetString(BCaTPlatform.EditorOverrideKey, "Auto");
            var existingEventSystems = new HashSet<int>();
            foreach (EventSystem system in UnityEngine.Object.FindObjectsByType<EventSystem>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                existingEventSystems.Add(system.GetInstanceID());

            GameObject cameraObject = null;
            GameObject controllerObject = null;
            GameObject moveObject = null;
            GameObject disabledMoveObject = null;
            GameObject rayObject = null;
            GameObject confirmObject = null;
            try
            {
                SessionState.SetString(BCaTPlatform.EditorOverrideKey, "QuestSimulated");
                ResetPlatform();
                Require(BCaTPlatform.IsQuest, "test must resolve the Quest profile");
                Require(Mathf.Approximately(UiFactory.PanelColor.a, 1f),
                    "the shared shell panel color must be opaque for every popup family");
                Require(!CanOpenInScene(SceneTransitionState.BlackKitchenSceneName),
                    "the global Quest menu must remain disabled in Black Kitchen's gaze-only XR scene");
                Require(CanOpenInScene("BH_XR_MainScene"),
                    "the global Quest menu must remain available in the main house");

                cameraObject = new GameObject("QuestMenuSelfTest_Head", typeof(Camera));
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetPositionAndRotation(
                    new Vector3(2f, 1.7f, -3f), Quaternion.Euler(0f, 27f, 0f));
                Camera head = cameraObject.GetComponent<Camera>();

                moveObject = new GameObject("QuestMenuSelfTest_MoveProvider");
                QuestMenuSelfTestMoveProvider move = moveObject.AddComponent<QuestMenuSelfTestMoveProvider>();
                disabledMoveObject = new GameObject("QuestMenuSelfTest_DisabledMoveProvider");
                QuestMenuSelfTestMoveProvider disabledMove =
                    disabledMoveObject.AddComponent<QuestMenuSelfTestMoveProvider>();
                disabledMove.enabled = false;
                rayObject = new GameObject("QuestMenuSelfTest_ControllerRay");
                QuestMenuSelfTestControllerRay ray =
                    rayObject.AddComponent<QuestMenuSelfTestControllerRay>();

                controllerObject = new GameObject("QuestMenuSelfTest_Controller");
                QuestInHeadsetMenuController menu =
                    controllerObject.AddComponent<QuestInHeadsetMenuController>();
                Invoke(menu, "Awake");
                Invoke(menu, "OnEnable");

                InputAction action = GetField(menu, "menuAction") as InputAction;
                Require(action != null && action.enabled && action.bindings.Count == 1 &&
                        action.bindings[0].effectivePath == "<XRController>{LeftHand}/menuButton",
                    "menu input must be one enabled standard left-controller menuButton binding");

                int eventSystemsBeforeOpen = ActiveEventSystemCount();
                menu.Open();
                Require(menu.IsOpen, "first toggle/open must open the menu");
                Require(PlayerControlGate.IsSuspended &&
                        InteractionState.HasReason(InteractionBlockReason.Menu),
                    "open menu must own the existing locomotion and interaction gates");
                Require(!move.enabled && !disabledMove.enabled,
                    "open menu must disable enabled locomotion and preserve pre-disabled locomotion");
                Require(head.enabled && ray.enabled,
                    "open menu must preserve head and controller-ray components");

                GameObject root = GameObject.Find("BCaT_QuestInHeadsetMenu");
                Require(root != null &&
                        root.GetComponent<Canvas>()?.renderMode == RenderMode.WorldSpace &&
                        root.GetComponent<TrackedDeviceGraphicRaycaster>() != null,
                    "menu must be one world-space canvas with the existing XRI UI raycaster");
                Require(ActiveEventSystemCount() == Mathf.Max(1, eventSystemsBeforeOpen),
                    "opening must reuse the active EventSystem instead of duplicating it");

                Vector3 flatForward = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up).normalized;
                Require(Vector3.Angle(root.transform.forward, flatForward) < 0.01f &&
                        Mathf.Abs(Vector3.Distance(root.transform.position, head.transform.position) - 1.55f) < 0.12f,
                    "menu must open at reading distance with world-up, head-relative facing");
                Vector3 fixedPosition = root.transform.position;
                Quaternion fixedRotation = root.transform.rotation;
                head.transform.position += new Vector3(3f, 1f, -2f);
                head.transform.rotation = Quaternion.Euler(0f, 140f, 0f);
                Require(root.transform.position == fixedPosition && root.transform.rotation == fixedRotation,
                    "menu must remain stationary after opening");

                Transform navigation = root.transform.Find("Backdrop/Body/Navigation");
                Require(navigation != null && navigation.gameObject.activeInHierarchy,
                    "left navigation must remain present");
                Button resumeButton = navigation.Find("Button_Resume")?.GetComponent<Button>();
                Outline resumeOutline = resumeButton?.GetComponent<Outline>();
                Require(resumeOutline != null && !resumeOutline.enabled,
                    "Quest navigation buttons must carry an initially hidden focus border");
                var pointerData = new PointerEventData(EventSystem.current);
                ExecuteEvents.Execute<IPointerEnterHandler>(
                    resumeButton.gameObject, pointerData, ExecuteEvents.pointerEnterHandler);
                Require(resumeOutline.enabled,
                    "pointing at a Quest navigation button must show its focus border");
                ExecuteEvents.Execute<IPointerExitHandler>(
                    resumeButton.gameObject, pointerData, ExecuteEvents.pointerExitHandler);
                Require(!resumeOutline.enabled,
                    "moving off a Quest navigation button must hide its focus border");
                menu.Open();
                Require(UnityEngine.Object.FindObjectsByType<QuestInHeadsetMenuController>(
                            FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1 &&
                        CountNamedRoots("BCaT_QuestInHeadsetMenu") == 1,
                    "a repeated open request must not duplicate controller or view");

                VerifyDirectory(menu, root);
                Invoke(menu, "ShowAbout");
                Require(ContainsActiveText(root, AboutBlackHomeplacesUi.BodyCopy) &&
                        navigation.gameObject.activeInHierarchy && ActiveScrollCount(root) == 1,
                    "About must reuse exact paragraph copy in the shared scrollable right panel");
                Invoke(menu, "ShowCredits");
                Require(ContainsActiveTextFragment(root, MainMenuController.CreditsAttributionText) &&
                        navigation.gameObject.activeInHierarchy && ActiveScrollCount(root) == 1,
                    "Credits must reuse the existing attribution source in the shared right panel");

                menu.Toggle();
                Require(!menu.IsOpen && !PlayerControlGate.IsSuspended &&
                        !InteractionState.HasReason(InteractionBlockReason.Menu),
                    "second toggle/Resume path must close and release both gates");
                Require(move.enabled && !disabledMove.enabled && head.enabled && ray.enabled,
                    "close must restore exactly the prior locomotion state and preserve tracking/rays");

                confirmObject = UiFactory.CreateConfirmDialog(
                    "Quit the application?", "Quit", null);
                Image confirmPanel = confirmObject.transform.Find("Panel")?.GetComponent<Image>();
                Require(confirmPanel != null && Mathf.Approximately(confirmPanel.color.a, 1f),
                    "shared shell panels must be opaque so parent menus cannot bleed through popups");
                DestroyImmediateIfPresent(confirmObject);
                confirmObject = null;

                Debug.Log("[QuestInHeadsetMenuSelfTest] PASS");
            }
            finally
            {
                InteractionState.ForceCloseAll();
                PlayerControlGate.ForceResumeAll();
                DestroyImmediateIfPresent(GameObject.Find("BCaT_QuestInHeadsetMenu"));
                DestroyImmediateIfPresent(confirmObject);
                DestroyImmediateIfPresent(controllerObject);
                DestroyImmediateIfPresent(rayObject);
                DestroyImmediateIfPresent(disabledMoveObject);
                DestroyImmediateIfPresent(moveObject);
                DestroyImmediateIfPresent(cameraObject);

                foreach (EventSystem system in UnityEngine.Object.FindObjectsByType<EventSystem>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (!existingEventSystems.Contains(system.GetInstanceID()))
                        DestroyImmediateIfPresent(system.gameObject);

                SessionState.SetString(BCaTPlatform.EditorOverrideKey, previousMode);
                ResetPlatform();
            }
        }

        static void VerifyDirectory(QuestInHeadsetMenuController menu, GameObject root)
        {
            Invoke(menu, "ShowDirectory");
            Require(ContainsActiveText(root, "Front Yard") &&
                    ContainsActiveText(root, "Such Lovely Gravy") &&
                    ActiveScrollCount(root) == 1,
                "Directory must show curated room headings/project titles in one scroll view");
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(false))
            {
                string text = label.text ?? string.Empty;
                Require(text.IndexOf("available", StringComparison.OrdinalIgnoreCase) < 0 &&
                        text.IndexOf("GameObject", StringComparison.OrdinalIgnoreCase) < 0 &&
                        text.IndexOf("Controller", StringComparison.OrdinalIgnoreCase) < 0 &&
                        text.IndexOf("prefab", StringComparison.OrdinalIgnoreCase) < 0 &&
                        text.IndexOf("(Audio exhibit)", StringComparison.OrdinalIgnoreCase) < 0 &&
                        text.IndexOf("(Video exhibit)", StringComparison.OrdinalIgnoreCase) < 0,
                    "Directory must not expose status or Unity/internal labels");
            }
        }

        static int ActiveEventSystemCount() =>
            UnityEngine.Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;

        static int ActiveScrollCount(GameObject root) =>
            root.GetComponentsInChildren<ScrollRect>(false).Length;

        static int CountNamedRoots(string name)
        {
            int count = 0;
            foreach (Transform transform in UnityEngine.Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (transform != null && transform.name == name)
                    count++;
            return count;
        }

        static bool ContainsActiveText(GameObject root, string expected)
        {
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(false))
                if (label.text == expected)
                    return true;
            return false;
        }

        static bool ContainsActiveTextFragment(GameObject root, string expected)
        {
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(false))
                if ((label.text ?? string.Empty).Contains(expected))
                    return true;
            return false;
        }

        static object Invoke(object target, string method)
        {
            return target.GetType().GetMethod(method,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.Invoke(target, null);
        }

        static object GetField(object target, string field)
        {
            return target.GetType().GetField(field,
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(target);
        }

        static bool CanOpenInScene(string scene)
        {
            MethodInfo method = typeof(QuestInHeadsetMenuController).GetMethod(
                "CanOpenInScene", BindingFlags.Static | BindingFlags.NonPublic);
            return method != null && (bool)method.Invoke(null, new object[] { scene });
        }

        static void ResetPlatform()
        {
            typeof(BCaTPlatform).GetMethod("ResetStatics",
                    BindingFlags.Static | BindingFlags.NonPublic)
                ?.Invoke(null, null);
        }

        static void DestroyImmediateIfPresent(UnityEngine.Object value)
        {
            if (value != null)
                UnityEngine.Object.DestroyImmediate(value);
        }

        static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("[QuestInHeadsetMenuSelfTest] FAIL: " + message);
        }
    }

    internal sealed class QuestMenuSelfTestMoveProvider : MonoBehaviour { }
    internal sealed class QuestMenuSelfTestControllerRay : MonoBehaviour { }
}
