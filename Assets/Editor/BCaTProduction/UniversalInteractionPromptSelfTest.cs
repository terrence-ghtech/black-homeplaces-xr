using System;
using System.IO;
using System.Reflection;
using BCaT.Production;
using BCaT.Production.Interaction;
using BCaT.Production.Shell;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace BCaT.EditorTools
{
    /// <summary>Checks same-focus prompt refresh and XR hover recovery.</summary>
    public static class UniversalInteractionPromptSelfTest
    {
        [MenuItem("BCaT/Diagnostics/Universal Interaction Prompt Self Test")]
        public static void Run()
        {
            GameObject routerObject = null;
            GameObject targetObject = null;
            GameObject hoverSource = null;
            GameObject spatialObject = null;
            GameObject focusedVideoObject = null;
            GameObject replacementVideoObject = null;
            GameObject coordinatorObject = null;
            try
            {
                coordinatorObject = new GameObject("UniversalInteractionPromptSelfTest_Coordinator");
                FocusedExhibitCoordinator coordinator =
                    coordinatorObject.AddComponent<FocusedExhibitCoordinator>();
                Invoke(coordinator, "Awake");

                routerObject = new GameObject("UniversalInteractionPromptSelfTest_Router");
                InteractionRouter router = routerObject.AddComponent<InteractionRouter>();
                Invoke(router, "Awake");
                router.interactionCooldown = 0f;

                targetObject = new GameObject("UniversalInteractionPromptSelfTest_Target");
                DynamicPromptSelfTestTarget target =
                    targetObject.AddComponent<DynamicPromptSelfTestTarget>();

                Invoke(router, "SetCurrentTarget", target);
                Require(DisplayedPrompt() == target.ExpectedPrompt,
                    "a stopped target should initially display Play");

                Invoke(router, "Dispatch", target, InteractionActivation.DesktopInteractKey);
                Require(target.IsPlaying && DisplayedPrompt() == target.ExpectedPrompt,
                    "activation should display Stop in the same frame without a focus change");

                target.StopExternally();
                Invoke(router, "SetCurrentTarget", target);
                Require(!target.IsPlaying && DisplayedPrompt() == target.ExpectedPrompt,
                    "an external stop should display Play while the same target remains focused");

                hoverSource = new GameObject("UniversalInteractionPromptSelfTest_XRHover");
                router.RequestXRHover(hoverSource, target);
                InteractionState.Block(target, InteractionBlockReason.Modal, null);
                Invoke(router, "Update");
                Require(router.CurrentTarget == null,
                    "a blocker should suppress the universal prompt");

                InteractionState.Unblock(target);
                IInteractionTarget recovered =
                    Invoke(router, "SelectBestXRHoverTarget") as IInteractionTarget;
                Require(ReferenceEquals(recovered, target),
                    "XR hover should survive prompt suppression without another hover-enter event");
                Invoke(router, "SetCurrentTarget", recovered);
                Require(DisplayedPrompt() == target.ExpectedPrompt,
                    "the XR prompt should recover from retained physical hover state");

                spatialObject = new GameObject("UniversalInteractionPromptSelfTest_SpatialAudio");
                AudioSource source = spatialObject.AddComponent<AudioSource>();
                SpatialAudioToggle spatial = spatialObject.AddComponent<SpatialAudioToggle>();
                SetField(spatial, "audioSource", source);
                SetField(spatial, "displayName", "Nine Night");
                SetField(spatial, "prompt", new SharedInteractionPromptConfig
                {
                    desktopPrompt = "Legacy fixed open prompt",
                    xrPrompt = "Legacy fixed open prompt",
                    objectName = "Nine Night",
                });
                Require(spatial.GetPrompt(false) == "Press E to play Nine Night" &&
                        spatial.GetPrompt(true) == "Play Nine Night",
                    "SpatialAudioToggle should ignore fixed overrides and derive Play from its stopped AudioSource");

                focusedVideoObject = new GameObject("UniversalInteractionPromptSelfTest_FocusedVideo");
                MediaVideoController focusedVideo = focusedVideoObject.AddComponent<MediaVideoController>();
                SetField(focusedVideo, "title", "Such lovely gravy");
                SetField(focusedVideo, "showClosePromptWhileOpen", true);
                replacementVideoObject = new GameObject("UniversalInteractionPromptSelfTest_ReplacementVideo");
                MediaVideoController replacementVideo =
                    replacementVideoObject.AddComponent<MediaVideoController>();
                SetField(replacementVideo, "title", "Cooperative Hall of Fame");
                SetField(replacementVideo, "showClosePromptWhileOpen", true);

                Invoke(router, "SetCurrentTarget", focusedVideo);
                Require(DisplayedPrompt() == Expected("play", "Play", "Such lovely gravy"),
                    "an opted-in focused video should advertise Play before opening");

                Require(router.RequestXRSelect(focusedVideo),
                    "the router should accept a closed video XR selection");
                Require(focusedVideo.IsOpen && ReferenceEquals(router.CurrentTarget, focusedVideo) &&
                        DisplayedPrompt() == Expected("close", "Close", "Such lovely gravy"),
                    "an open focused video should remain actionable and advertise Close");

                Require(router.RequestXRSelect(focusedVideo),
                    "the router should accept the current open video's XR selection");
                Require(!focusedVideo.IsOpen &&
                        DisplayedPrompt() == Expected("play", "Play", "Such lovely gravy"),
                    "a second interaction should close the video and restore Play");

                ResetSuppressedInputFrame();
                Require(router.RequestXRSelect(focusedVideo) && focusedVideo.IsOpen,
                    "Video A should reopen for the replacement check");
                Invoke(router, "SetCurrentTarget", replacementVideo);
                Require(router.RequestXRSelect(replacementVideo),
                    "the router should accept Video B while Video A is open");
                Require(!focusedVideo.IsOpen && replacementVideo.IsOpen &&
                        ReferenceEquals(coordinator.CurrentExhibit, replacementVideo),
                    "selecting Video B should close Video A and retain Video B");
                Require(DisplayedPrompt() == Expected("close", "Close", "Cooperative Hall of Fame"),
                    "the replacement video should advertise its actionable Close state");

                VerifyProductionVideoClosePromptConfiguration();

                Debug.Log("[UniversalInteractionPromptSelfTest] PASS");
            }
            finally
            {
                InteractionState.ForceCloseAll();
                CleanupPromptUi();
                if (hoverSource != null)
                    UnityEngine.Object.DestroyImmediate(hoverSource);
                if (spatialObject != null)
                    UnityEngine.Object.DestroyImmediate(spatialObject);
                if (focusedVideoObject != null)
                    UnityEngine.Object.DestroyImmediate(focusedVideoObject);
                if (replacementVideoObject != null)
                    UnityEngine.Object.DestroyImmediate(replacementVideoObject);
                if (targetObject != null)
                    UnityEngine.Object.DestroyImmediate(targetObject);
                if (routerObject != null)
                    UnityEngine.Object.DestroyImmediate(routerObject);
                if (coordinatorObject != null)
                    UnityEngine.Object.DestroyImmediate(coordinatorObject);
            }
        }

        static void VerifyProductionVideoClosePromptConfiguration()
        {
            string sceneText = File.ReadAllText(
                Path.Combine(Application.dataPath, "BH_XR_MainScene.unity"));
            Require(CountOccurrences(sceneText, "showClosePromptWhileOpen: 1") == 6 &&
                    !sceneText.Contains("showClosePromptWhileOpen: 0"),
                "all six scene-authored production videos should opt into the Close prompt");

            const string lindaPath =
                "Assets/BCaT_assets/LindaLeaks/Prefabs/LindaLeaks_Exhibit_VintageCamera.prefab";
            GameObject lindaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(lindaPath);
            MediaVideoController lindaVideo =
                lindaPrefab != null ? lindaPrefab.GetComponentInChildren<MediaVideoController>(true) : null;
            Require(lindaVideo != null && GetField<bool>(lindaVideo, "showClosePromptWhileOpen"),
                "the production Linda Leaks video should opt into the Close prompt");
        }

        static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }

        static void ResetSuppressedInputFrame()
        {
            typeof(InteractionState)
                .GetField("suppressedInputFrame", BindingFlags.Static | BindingFlags.NonPublic)
                ?.SetValue(null, -1);
        }

        static string DisplayedPrompt()
        {
            FieldInfo labelField = typeof(InteractionPromptUi)
                .GetField("label", BindingFlags.Static | BindingFlags.NonPublic);
            return (labelField?.GetValue(null) as TMP_Text)?.text;
        }

        static string Expected(string desktopAction, string xrAction, string title) =>
            PlatformCapabilities.UseXRPrompts
                ? $"{xrAction} {title}"
                : $"Press E to {desktopAction} {title}";

        static void CleanupPromptUi()
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            Type type = typeof(InteractionPromptUi);
            FieldInfo rootField = type.GetField("rootRect", flags);
            RectTransform root = rootField?.GetValue(null) as RectTransform;
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root.gameObject);

            foreach (string fieldName in new[]
                     { "canvas", "label", "group", "rootRect", "panelRect", "panelImage", "boundXrCamera" })
            {
                type.GetField(fieldName, flags)?.SetValue(null, null);
            }
        }

        static object Invoke(object target, string method, params object[] arguments)
        {
            MethodInfo match = null;
            foreach (MethodInfo candidate in target.GetType().GetMethods(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (candidate.Name == method && candidate.GetParameters().Length == arguments.Length)
                {
                    match = candidate;
                    break;
                }
            }

            return match?.Invoke(target, arguments);
        }

        static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }

        static T GetField<T>(object target, string fieldName)
        {
            object value = target.GetType().GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(target);
            return value is T typed ? typed : default;
        }

        static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(
                    "[UniversalInteractionPromptSelfTest] FAIL: " + message);
        }
    }

    internal sealed class DynamicPromptSelfTestTarget : MonoBehaviour, IInteractionTarget
    {
        public bool IsPlaying { get; private set; }
        public string ExpectedPrompt => PlatformCapabilities.UseXRPrompts
            ? (IsPlaying ? "Stop Prompt Target" : "Play Prompt Target")
            : (IsPlaying ? "Press E to stop Prompt Target" : "Press E to play Prompt Target");

        public Vector3 FocusPoint => transform.position;
        public float MaxDistance => 5f;
        public float MaxViewAngle => 0f;
        public bool RequireLineOfSight => false;
        public int Priority => 0;
        public bool IsAvailable => isActiveAndEnabled;
        public bool AllowDesktopClick => true;
        public bool Exists => this != null;
        public Collider[] OwnColliders => null;

        public string GetPrompt(bool xr) => SharedInteractionPrompt.Format(xr,
            IsPlaying ? SharedInteractionVerb.Stop : SharedInteractionVerb.Play,
            "Prompt Target");

        public void OnFocusChanged(bool focused) { }

        public void OnInteract(InteractionActivation activation) => IsPlaying = !IsPlaying;

        public void StopExternally() => IsPlaying = false;
    }
}
