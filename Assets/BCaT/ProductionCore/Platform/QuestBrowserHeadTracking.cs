using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace BCaT.Production
{
    /// <summary>
    /// Keeps the world behind an external browser tracked when Quest's system UI
    /// owns input focus. Input actions can stop supplying camera poses in that
    /// state even though the compositor still displays the immersive projection.
    /// Normal gameplay continues to use the rig's existing TrackedPoseDriver.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class QuestBrowserHeadTracking : MonoBehaviour
    {
        static QuestBrowserHeadTracking instance;
        readonly FocusWindow window = new FocusWindow();
        readonly List<XRNodeState> nodes = new List<XRNodeState>();
        TrackedPoseDriver headDriver;
        bool paused;
        bool reportedTracking;

        public static void OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            if (BCaTPlatform.IsQuestPlayerBinary && instance != null)
                instance.Arm();

            Application.OpenURL(url);
        }

        void OnEnable()
        {
            instance = this;
            Application.onBeforeRender += BeforeRender;
        }

        void OnDisable()
        {
            Application.onBeforeRender -= BeforeRender;
            Clear();
            if (instance == this)
                instance = null;
        }

        void Arm()
        {
            Clear();
            Camera camera = Camera.main;
            if (camera == null)
                return;

            var origin = camera.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
            if (origin == null || origin.Camera != camera)
                return;

            headDriver = camera.GetComponent<TrackedPoseDriver>();
            if (headDriver == null || !headDriver.isActiveAndEnabled)
                return;

            window.Arm(Time.realtimeSinceStartupAsDouble);
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused)
                window.NoteFocusLoss();
        }

        void OnApplicationPause(bool isPaused)
        {
            paused = isPaused;
            if (isPaused)
                window.NoteFocusLoss();
        }

        void LateUpdate() => UpdateTracking();

        // Run after input's BeforeRender pose update; never invert or compose a
        // second head rotation. This replaces only the camera's local pose.
        [BeforeRenderOrder(10000)]
        void BeforeRender() => UpdateTracking();

        void UpdateTracking()
        {
            if (!window.Armed || !BCaTPlatform.IsQuestPlayerBinary)
                return;

            if (headDriver == null || !headDriver.isActiveAndEnabled ||
                !BCaTPlatform.TryGetOpenXRSessionState(out bool sessionFocused,
                    out bool displayRunning, out bool userPresent))
            {
                Clear();
                return;
            }

            // Android window focus and OpenXR input focus can change on different
            // frames. Do not end the handoff while either still belongs to UI.
            bool focused = sessionFocused && Application.isFocused;
            bool shouldTrack = window.Tick(Time.realtimeSinceStartupAsDouble, focused, paused);
            if (!window.Armed)
            {
                Clear();
                return;
            }

            if (!shouldTrack || !displayRunning || !userPresent)
                return;

            // XR node poses come from the XR subsystem, independently of the
            // Input System action enable/reset policy for unfocused applications.
            InputTracking.GetNodeStates(nodes);
            foreach (XRNodeState node in nodes)
            {
                if (node.nodeType != XRNode.CenterEye || !ApplyPose(headDriver.transform, node))
                    continue;

                if (!reportedTracking)
                {
                    reportedTracking = true;
                    Debug.Log("[QuestBrowserHeadTracking] Browser overlay: applying live center-eye pose.");
                }
                break;
            }
        }

        static bool ApplyPose(Transform head, XRNodeState node)
        {
            // Never substitute an identity/zero pose when tracking is unavailable.
            if (!node.tracked || !node.TryGetRotation(out Quaternion rotation))
                return false;

            head.localRotation = rotation;
            if (node.TryGetPosition(out Vector3 position))
                head.localPosition = position;
            return true;
        }

        void Clear()
        {
            window.Clear();
            headDriver = null;
            if (reportedTracking)
                Debug.Log("[QuestBrowserHeadTracking] Browser overlay ended; normal pose driver owns tracking.");
            reportedTracking = false;
        }

        // Kept independent of XR hardware so launch/focus/pause boundaries can
        // be regression-tested without changing any project-wide input settings.
        internal sealed class FocusWindow
        {
            const double LaunchTimeoutSeconds = 10;
            double deadline;
            bool lostFocus;
            public bool Armed { get; private set; }

            public void Arm(double now)
            {
                Armed = true;
                lostFocus = false;
                deadline = now + LaunchTimeoutSeconds;
            }

            public void NoteFocusLoss()
            {
                if (Armed)
                    lostFocus = true;
            }

            public bool Tick(double now, bool focused, bool paused)
            {
                if (!Armed)
                    return false;

                if (!lostFocus && now >= deadline)
                {
                    Clear(); // Failed/no-op launch must not affect a later system menu.
                    return false;
                }

                if (paused)
                    return false;

                if (!focused)
                    lostFocus = true;
                else if (lostFocus)
                    Clear(); // Hand back before any write on the first focused frame.

                return Armed && !focused;
            }

            public void Clear()
            {
                Armed = false;
                lostFocus = false;
            }
        }
    }
}
