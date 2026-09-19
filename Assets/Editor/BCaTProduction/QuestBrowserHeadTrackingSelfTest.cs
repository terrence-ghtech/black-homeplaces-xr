using System;
using System.Reflection;
using BCaT.Production;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;

namespace BCaT.EditorTools
{
    /// <summary>Hardware-independent regression checks for the browser-only tracking scope.</summary>
    public static class QuestBrowserHeadTrackingSelfTest
    {
        public static void RunWithArchitectureValidation()
        {
            Run();
            BCaTArchitectureValidator.RunBatch();
        }

        [MenuItem("BCaT/Diagnostics/Quest Browser Head Tracking Self Test")]
        public static void Run()
        {
            CheckFocusLifecycle();
            CheckPoseSpace();
            Debug.Log("[QuestBrowserHeadTrackingSelfTest] PASS: focus lifecycle, pause/resume, " +
                      "failed launch, repeat launch, pose direction, rig preservation, and invalid tracking.");
        }

        static void CheckFocusLifecycle()
        {
            // The production state machine is internal; reflection lets the
            // separate Editor assembly exercise it without expanding its API.
            Type type = typeof(QuestBrowserHeadTracking).GetNestedType("FocusWindow", BindingFlags.NonPublic);
            object window = Activator.CreateInstance(type, true);
            void Arm(double now) => type.GetMethod("Arm").Invoke(window, new object[] { now });
            void Lost() => type.GetMethod("NoteFocusLoss").Invoke(window, null);
            bool Tick(double now, bool focused, bool paused = false) =>
                (bool)type.GetMethod("Tick").Invoke(window, new object[] { now, focused, paused });
            bool Armed() => (bool)type.GetProperty("Armed").GetValue(window);

            Require(!Tick(0, true) && !Tick(0, false), "Unarmed gameplay/system menu must be untouched.");
            Arm(1);
            Require(!Tick(2, true) && Armed(), "Launching must not override focused gameplay.");
            Require(Tick(3, false), "Browser focus loss must start tracking.");
            Require(Tick(100, false), "An open browser must not time out.");
            Require(!Tick(101, true) && !Armed(), "First focused frame must stop the fallback.");
            Require(!Tick(102, false), "A later unrelated menu must remain untouched.");

            Arm(200);
            Require(!Tick(210, false) && !Armed(), "A failed launch must expire before a later focus loss.");

            Arm(300);
            Lost(); // Application pause/focus callback can precede the next rendered frame.
            Require(!Tick(301, false, true) && Armed(), "Paused applications must not receive pose writes.");
            Require(Tick(400, false), "Resume behind the browser must still track after a long pause.");
            Require(!Tick(401, true) && !Armed(), "Resume into gameplay must disarm.");

            Arm(500);
            Lost();
            Require(!Tick(501, true, true), "Pause must suppress writes even with stale XR focus.");
            Require(!Tick(600, true) && !Armed(), "Resume focused must not override the pose driver.");
            Arm(700);
            Require(Tick(701, false), "A repeated browser launch must work.");
            type.GetMethod("Clear").Invoke(window, null);
            Require(!Tick(702, false) && !Armed(), "Cleanup must stop tracking.");
        }

        static void CheckPoseSpace()
        {
            var rig = new GameObject("BrowserTrackingTestRig");
            var head = new GameObject("BrowserTrackingTestHead");
            try
            {
                rig.transform.SetPositionAndRotation(new Vector3(9, 2, -4), Quaternion.Euler(0, 137, 0));
                head.transform.SetParent(rig.transform, false);
                MethodInfo apply = typeof(QuestBrowserHeadTracking).GetMethod("ApplyPose",
                    BindingFlags.NonPublic | BindingFlags.Static);
                bool Apply(XRNodeState pose) => (bool)apply.Invoke(null, new object[] { head.transform, pose });
                var position = new Vector3(0.2f, 1.7f, -0.3f);
                foreach (Vector3 angles in new[] {
                    new Vector3(0, 35, 0), new Vector3(0, -35, 0),
                    new Vector3(25, 0, 0), new Vector3(-25, 0, 0) })
                {
                    var pose = new XRNodeState { nodeType = XRNode.CenterEye, tracked = true,
                        position = position, rotation = Quaternion.Euler(angles) };
                    Require(Apply(pose), "Valid headset pose must apply.");
                    Require(Quaternion.Angle(head.transform.localRotation, Quaternion.Euler(angles)) < 0.001f,
                        "Head direction must not invert or accumulate.");
                    Require(Vector3.Distance(head.transform.localPosition, position) < 0.0001f,
                        "Head position must remain in tracking space.");
                    Require(Quaternion.Angle(head.transform.rotation,
                        rig.transform.rotation * Quaternion.Euler(angles)) < 0.01f,
                        "An aligned/turned rig must retain its world heading.");
                }

                Vector3 previousPosition = head.transform.localPosition;
                Quaternion previousRotation = head.transform.localRotation;
                Require(!Apply(new XRNodeState { tracked = false, rotation = Quaternion.identity }),
                    "Untracked poses must not apply.");
                Require(!Apply(new XRNodeState { tracked = true }), "Missing rotation must not apply.");
                Require(head.transform.localPosition == previousPosition &&
                        head.transform.localRotation == previousRotation, "Invalid tracking must preserve the pose.");
                Require(Apply(new XRNodeState { tracked = true, rotation = Quaternion.Euler(5, 10, 0) }),
                    "Rotation-only tracking must remain usable.");
                Require(head.transform.localPosition == previousPosition,
                    "Missing position must preserve height/translation.");
                Require(rig.transform.position == new Vector3(9, 2, -4) &&
                        Quaternion.Angle(rig.transform.rotation, Quaternion.Euler(0, 137, 0)) < 0.001f,
                    "The fallback must never modify the rig.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(head);
                UnityEngine.Object.DestroyImmediate(rig);
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("[QuestBrowserHeadTrackingSelfTest] " + message);
        }
    }
}
