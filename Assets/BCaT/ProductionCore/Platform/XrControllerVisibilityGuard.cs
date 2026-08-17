using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace BCaT.Production
{
    /// <summary>
    /// Quest-only safety net for the "controllers don't appear" failure class.
    ///
    /// The rig's XRInputModalityManager deactivates a controller GameObject the
    /// moment its device stops being tracked, and — because the rig wires no
    /// tracked-hand fallback — nothing is shown in that hand until the manager's
    /// tracking-acquired callback re-activates it. That callback rides on device
    /// events which are dropped when they land while the app is paused (headset
    /// dozing between sessions, system menu, sleep/resume), leaving a tracked,
    /// working controller permanently invisible.
    ///
    /// This guard polls once per second: if a hand's controller device reports
    /// isTracked but the manager's controller GameObject is inactive, it re-runs
    /// the manager's own mode resolution (so internal state stays consistent)
    /// and falls back to activating the GameObject directly if that was not
    /// enough. Every intervention is logged so on-device sessions show exactly
    /// when and why a controller had to be recovered.
    /// </summary>
    public sealed class XrControllerVisibilityGuard : MonoBehaviour
    {
        const float PollIntervalSeconds = 1f;

        static readonly MethodInfo UpdateLeftModeMethod = typeof(XRInputModalityManager).GetMethod(
            "UpdateLeftMode", BindingFlags.Instance | BindingFlags.NonPublic, null, System.Type.EmptyTypes, null);
        static readonly MethodInfo UpdateRightModeMethod = typeof(XRInputModalityManager).GetMethod(
            "UpdateRightMode", BindingFlags.Instance | BindingFlags.NonPublic, null, System.Type.EmptyTypes, null);

        static readonly FieldInfo LeftControllerField = typeof(XRInputModalityManager).GetField(
            "m_LeftController", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo RightControllerField = typeof(XRInputModalityManager).GetField(
            "m_RightController", BindingFlags.Instance | BindingFlags.NonPublic);

        readonly List<XRInputModalityManager> managers = new List<XRInputModalityManager>();
        float nextPollTime;
        bool managersDirty = true;

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            managersDirty = true;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => managersDirty = true;

        void Update()
        {
            if (Time.unscaledTime < nextPollTime)
                return;
            nextPollTime = Time.unscaledTime + PollIntervalSeconds;

            if (managersDirty)
            {
                managers.Clear();
                managers.AddRange(FindObjectsByType<XRInputModalityManager>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None));
                managersDirty = false;
            }

            for (int i = managers.Count - 1; i >= 0; i--)
            {
                XRInputModalityManager manager = managers[i];
                if (manager == null)
                {
                    managers.RemoveAt(i);
                    managersDirty = true;
                    continue;
                }

                if (!manager.isActiveAndEnabled)
                    continue;

                GuardHand(manager, XRNode.LeftHand, LeftControllerField, UpdateLeftModeMethod);
                GuardHand(manager, XRNode.RightHand, RightControllerField, UpdateRightModeMethod);
            }
        }

        static void GuardHand(XRInputModalityManager manager, XRNode node,
            FieldInfo controllerField, MethodInfo updateModeMethod)
        {
            if (controllerField == null)
                return;

            var controllerObject = controllerField.GetValue(manager) as GameObject;
            if (controllerObject == null || controllerObject.activeSelf)
                return;

            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid ||
                !device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || !tracked)
                return;

            // The device is tracked but the manager left its GameObject off:
            // the tracking-acquired event was missed. Re-run the manager's own
            // resolution first so its internal mode matches what we restore.
            updateModeMethod?.Invoke(manager, null);

            if (!controllerObject.activeSelf)
                controllerObject.SetActive(true);

            Debug.Log($"[XrControllerVisibilityGuard] Restored {node} controller " +
                      $"'{controllerObject.name}' on '{manager.gameObject.scene.name}': device was " +
                      "tracked while its GameObject was inactive (missed tracking-acquired event).");
        }
    }
}
