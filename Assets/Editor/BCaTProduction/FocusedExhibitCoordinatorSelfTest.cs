using System;
using System.Collections.Generic;
using System.Reflection;
using BCaT.Exhibits.DejaVudu;
using BCaT.Production.Interaction;
using BCaT.Production.Shell;
using UnityEditor;
using UnityEngine;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Lightweight edit-time behavioral check for the focused-exhibit state
    /// transitions. Suitable for headed use or Unity -executeMethod.
    /// </summary>
    public static class FocusedExhibitCoordinatorSelfTest
    {
        [MenuItem("BCaT/Diagnostics/Focused Exhibit Coordinator Self Test")]
        public static void Run()
        {
            GameObject coordinatorObject = null;
            try
            {
                coordinatorObject = new GameObject("FocusedExhibitCoordinatorSelfTest");
                var coordinator = coordinatorObject.AddComponent<FocusedExhibitCoordinator>();
                // executeMethod runs outside Play Mode, so Unity does not invoke
                // MonoBehaviour.Awake for this temporary component.
                typeof(FocusedExhibitCoordinator)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(coordinator, null);
                Require(ReferenceEquals(FocusedExhibitCoordinator.Instance, coordinator),
                    "coordinator should publish its service instance");

                VerifyReplacementAndSameExhibit(coordinator);
                VerifyOwnCloseNotification(coordinator);
                VerifyDestroyedCurrentIsCleared(coordinator);
                VerifyCloseFailureStopsReplacement(coordinator);
                VerifyOpenFailureIsNotRetained(coordinator);
                VerifyMediaVideoActivation(coordinator);
                VerifyPlayerControlGateOwnership();
                VerifyProductionFamilyReplacement(coordinator);
                VerifyProductionDisableCleanup(coordinator);

                Debug.Log("[FocusedExhibitCoordinatorSelfTest] PASS");
            }
            finally
            {
                if (coordinatorObject != null)
                    UnityEngine.Object.DestroyImmediate(coordinatorObject);

                foreach (FocusedExhibitCoordinatorSelfTestExhibit exhibit in
                         UnityEngine.Object.FindObjectsByType<FocusedExhibitCoordinatorSelfTestExhibit>(
                             FindObjectsInactive.Include))
                {
                    if (exhibit != null)
                        UnityEngine.Object.DestroyImmediate(exhibit.gameObject);
                }

                foreach (MediaVideoController video in
                         UnityEngine.Object.FindObjectsByType<MediaVideoController>(FindObjectsInactive.Include))
                {
                    if (video != null && video.gameObject.name.StartsWith("FocusedExhibitSelfTest_"))
                        UnityEngine.Object.DestroyImmediate(video.gameObject);
                }
            }
        }

        static void VerifyReplacementAndSameExhibit(FocusedExhibitCoordinator coordinator)
        {
            FocusedExhibitCoordinatorSelfTestExhibit a = Create("A");
            FocusedExhibitCoordinatorSelfTestExhibit b = Create("B");

            coordinator.RequestOpen(null);
            Require(coordinator.CurrentExhibit == null, "null request should retain no current exhibit");

            coordinator.RequestOpen(a);
            Require(ReferenceEquals(coordinator.CurrentExhibit, a) && a.IsOpen,
                "null -> A should open and retain A");

            coordinator.RequestOpen(a);
            Require(a.OpenCalls == 1 && a.CloseCalls == 0,
                "A -> A should do nothing");

            coordinator.RequestOpen(b);
            Require(!a.IsOpen && b.IsOpen && ReferenceEquals(coordinator.CurrentExhibit, b),
                "A -> B should close A and retain open B");

            coordinator.RequestOpen(a);
            Require(!b.IsOpen && a.IsOpen && ReferenceEquals(coordinator.CurrentExhibit, a),
                "B -> A should close B and retain open A");

            coordinator.RequestClose(a);
            Destroy(a.gameObject);
            Destroy(b.gameObject);
        }

        static void VerifyOwnCloseNotification(FocusedExhibitCoordinator coordinator)
        {
            FocusedExhibitCoordinatorSelfTestExhibit a = Create("OwnClose");
            coordinator.RequestOpen(a);
            a.Close();
            coordinator.NotifyClosed(a);

            Require(coordinator.CurrentExhibit == null,
                "an exhibit closed through its own UI should clear CurrentExhibit");
            Destroy(a.gameObject);
        }

        static void VerifyDestroyedCurrentIsCleared(FocusedExhibitCoordinator coordinator)
        {
            FocusedExhibitCoordinatorSelfTestExhibit a = Create("Destroyed");
            coordinator.RequestOpen(a);
            UnityEngine.Object.DestroyImmediate(a.gameObject);

            Require(coordinator.CurrentExhibit == null,
                "a destroyed Unity exhibit should not remain current");
        }

        static void VerifyCloseFailureStopsReplacement(FocusedExhibitCoordinator coordinator)
        {
            FocusedExhibitCoordinatorSelfTestExhibit a = Create("CannotClose");
            FocusedExhibitCoordinatorSelfTestExhibit b = Create("BlockedRequest");
            coordinator.RequestOpen(a);
            a.FailClose = true;

            coordinator.RequestOpen(b);

            Require(a.IsOpen && b.OpenCalls == 0 && ReferenceEquals(coordinator.CurrentExhibit, a),
                "failed close must prevent the requested exhibit from opening");

            a.FailClose = false;
            coordinator.RequestClose(a);
            Destroy(a.gameObject);
            Destroy(b.gameObject);
        }

        static void VerifyOpenFailureIsNotRetained(FocusedExhibitCoordinator coordinator)
        {
            FocusedExhibitCoordinatorSelfTestExhibit b = Create("CannotOpen");
            b.FailOpen = true;

            coordinator.RequestOpen(b);

            Require(b.OpenCalls == 1 && !b.IsOpen && coordinator.CurrentExhibit == null,
                "an exhibit that does not report open must not be retained");
            Destroy(b.gameObject);
        }

        static void VerifyMediaVideoActivation(FocusedExhibitCoordinator coordinator)
        {
            var aObject = new GameObject("FocusedExhibitSelfTest_VideoA");
            var bObject = new GameObject("FocusedExhibitSelfTest_VideoB");
            var a = aObject.AddComponent<MediaVideoController>();
            var b = bObject.AddComponent<MediaVideoController>();

            a.OnInteract(InteractionActivation.DesktopInteractKey);
            Require(a.IsOpen && ReferenceEquals(coordinator.CurrentExhibit, a),
                "desktop video activation should open through the coordinator");

            b.OnInteract(InteractionActivation.DesktopInteractKey);
            Require(!a.IsOpen && b.IsOpen && ReferenceEquals(coordinator.CurrentExhibit, b),
                "one desktop activation of Video B should replace Video A");
            Require(!InteractionState.HasReason(InteractionBlockReason.Media),
                "migrated videos should not register a Media blocker");

            // With no router in this isolated check, OnXRSelect takes its guarded
            // fallback into the same coordinator path.
            a.OnXRSelect();
            Require(!b.IsOpen && a.IsOpen && ReferenceEquals(coordinator.CurrentExhibit, a),
                "one XR video activation should replace the current video");

            coordinator.RequestClose(a);
            Destroy(aObject);
            Destroy(bObject);
        }

        static void VerifyProductionFamilyReplacement(FocusedExhibitCoordinator coordinator)
        {
            var roots = new List<GameObject>();
            try
            {
                MediaVideoController video = Add<MediaVideoController>(roots, "Video");
                MuralExhibitController mural = Add<MuralExhibitController>(roots, "Mural");
                SimpleImagePopupController image = Add<SimpleImagePopupController>(roots, "ImagePopup");
                SimpleImagePopupInteractor imageTarget = Add<SimpleImagePopupInteractor>(roots, "ImageTarget");
                HolographicSlideshow slideshow = Add<HolographicSlideshow>(roots, "Slideshow");
                LindaLeaksPanelOpener slideshowTarget = slideshow.gameObject.AddComponent<LindaLeaksPanelOpener>();
                MeshellArticleReaderController article = Add<MeshellArticleReaderController>(roots, "Article");
                MeshellArticleNotebookOpener notebook = Add<MeshellArticleNotebookOpener>(roots, "Notebook");
                LindaLeaksPanelOpener articleTarget = notebook.gameObject.AddComponent<LindaLeaksPanelOpener>();
                AdinkraSymbolExhibit adinkra = Add<AdinkraSymbolExhibit>(roots, "Adinkra");
                DejaVuduSoundArchiveExhibit deja = Add<DejaVuduSoundArchiveExhibit>(roots, "Deja");
                PrivacyLawExhibitController privacy = Add<PrivacyLawExhibitController>(roots, "Privacy");

                SetField(article, "articles", new List<MeshellArticleDocument>
                {
                    new MeshellArticleDocument { title = "Self Test Article" }
                });
                SetField(privacy, "pageButtonBackgrounds", Array.Empty<UnityEngine.UI.Image>());
                SetField(imageTarget, "popup", image);
                SetField(slideshowTarget, "photoAlbum", slideshow);
                SetEnumField(slideshowTarget, "target", 1);
                SetField(notebook, "reader", article);
                SetField(articleTarget, "meshellArticleReader", notebook);
                SetEnumField(articleTarget, "target", 2);

                video.OnInteract(InteractionActivation.DesktopInteractKey);
                RequireCurrent(coordinator, video, "initial video");

                mural.OnInteract(InteractionActivation.DesktopInteractKey);
                Require(!video.IsOpen, "video -> mural should close video");
                RequireCurrent(coordinator, mural, "video -> mural");

                video.OnInteract(InteractionActivation.DesktopInteractKey);
                Require(!mural.IsOpen, "mural -> video should close mural");
                RequireCurrent(coordinator, video, "mural -> video");

                imageTarget.OnInteract(InteractionActivation.DesktopInteractKey);
                Require(!video.IsOpen, "video -> image popup should close video");
                RequireCurrent(coordinator, image, "video -> image popup");
                Require(!imageTarget.IsAvailable,
                    "image popup adapter should be unavailable for same-session re-entry");

                slideshowTarget.OnInteract(InteractionActivation.DesktopInteractKey);
                Require(!image.IsOpen, "image popup -> slideshow should close image popup");
                RequireCurrent(coordinator, slideshow, "image popup -> slideshow");
                Require(!slideshowTarget.IsAvailable,
                    "slideshow adapter should be unavailable for same-session re-entry");

                articleTarget.OnInteract(InteractionActivation.DesktopInteractKey);
                Require(!slideshow.IsOpen, "slideshow -> article should close slideshow");
                RequireCurrent(coordinator, article, "slideshow -> article");
                Require(!articleTarget.IsAvailable,
                    "article adapter should be unavailable for same-session re-entry");

                adinkra.OnInteract(InteractionActivation.DesktopInteractKey);
                Require(!article.IsOpen, "article -> Adinkra should close article");
                RequireCurrent(coordinator, adinkra, "article -> Adinkra");

                deja.OnInteract(InteractionActivation.DesktopInteractKey);
                Require(!adinkra.IsOpen, "Adinkra -> Deja should close Adinkra");
                RequireCurrent(coordinator, deja, "Adinkra -> Deja");

                privacy.OpenExhibit();
                Require(!deja.IsOpen, "Deja -> Privacy should close Deja synchronously");
                RequireCurrent(coordinator, privacy, "Deja -> Privacy");

                video.OnInteract(InteractionActivation.DesktopInteractKey);
                Require(!privacy.IsOpen, "Privacy -> video should close Privacy");
                RequireCurrent(coordinator, video, "Privacy -> video");

                Require(!InteractionState.HasReason(InteractionBlockReason.Modal) &&
                        !InteractionState.HasReason(InteractionBlockReason.Media),
                    "migrated focused families should not retain Modal or Media blockers");

                coordinator.RequestClose(video);
                Require(!PlayerControlGate.IsSuspended,
                    "cross-family replacement should release every PlayerControlGate hold");
            }
            finally
            {
                coordinator.CloseCurrent();
                foreach (GameObject root in roots)
                    Destroy(root);
                PlayerControlGate.ForceResumeAll();
                InteractionState.ForceCloseAll();
            }
        }

        static void VerifyPlayerControlGateOwnership()
        {
            var root = new GameObject("FocusedExhibitSelfTest_ControlGate");
            var initiallyEnabled = root.AddComponent<StarterAssets.StarterAssetsInputs>();
            var initiallyDisabled = root.AddComponent<StarterAssets.StarterAssetsInputs>();
            initiallyDisabled.enabled = false;
            object firstOwner = new object();
            object secondOwner = new object();

            try
            {
                PlayerControlGate.Suspend(firstOwner);
                PlayerControlGate.Suspend(secondOwner);
                Require(!initiallyEnabled.enabled && !initiallyDisabled.enabled,
                    "control gate should suspend enabled input without touching pre-disabled input");

                PlayerControlGate.Resume(firstOwner);
                Require(!initiallyEnabled.enabled,
                    "control gate should remain suspended while another owner holds it");

                PlayerControlGate.Resume(secondOwner);
                Require(initiallyEnabled.enabled && !initiallyDisabled.enabled,
                    "control gate should restore only the component it disabled");
            }
            finally
            {
                PlayerControlGate.ForceResumeAll();
                Destroy(root);
            }
        }

        static void VerifyProductionDisableCleanup(FocusedExhibitCoordinator coordinator)
        {
            var root = new GameObject("FocusedExhibitSelfTest_DisableCleanup");
            var popup = root.AddComponent<SimpleImagePopupController>();
            popup.Open();
            RequireCurrent(coordinator, popup, "disable cleanup setup");
            Require(PlayerControlGate.IsSuspended, "open image popup should own a control hold");

            InvokeNonPublic(popup, "OnDisable");
            Require(!popup.IsOpen && coordinator.CurrentExhibit == null,
                "OnDisable should close and clear the focused owner");
            Require(!PlayerControlGate.IsSuspended,
                "OnDisable should release the focused owner's control hold");
            Destroy(root);
        }

        static T Add<T>(List<GameObject> roots, string name) where T : MonoBehaviour
        {
            var root = new GameObject("FocusedExhibitSelfTest_" + name);
            roots.Add(root);
            return root.AddComponent<T>();
        }

        static void RequireCurrent(FocusedExhibitCoordinator coordinator, IFocusedExhibit expected,
            string transition)
        {
            Require(expected.IsOpen && ReferenceEquals(coordinator.CurrentExhibit, expected),
                transition + " should leave the requested exhibit as the only current owner");
        }

        static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(field != null, $"missing self-test field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        static void SetEnumField(object target, string fieldName, int value)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(field != null && field.FieldType.IsEnum,
                $"missing self-test enum field '{fieldName}' on {target.GetType().Name}");
            field.SetValue(target, Enum.ToObject(field.FieldType, value));
        }

        static void InvokeNonPublic(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(method != null, $"missing self-test method '{methodName}' on {target.GetType().Name}");
            method.Invoke(target, null);
        }

        static FocusedExhibitCoordinatorSelfTestExhibit Create(string name)
        {
            var gameObject = new GameObject("FocusedExhibitSelfTest_" + name);
            return gameObject.AddComponent<FocusedExhibitCoordinatorSelfTestExhibit>();
        }

        static void Destroy(GameObject gameObject)
        {
            if (gameObject != null)
                UnityEngine.Object.DestroyImmediate(gameObject);
        }

        static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("[FocusedExhibitCoordinatorSelfTest] FAIL: " + message);
        }
    }

    internal sealed class FocusedExhibitCoordinatorSelfTestExhibit : MonoBehaviour, IFocusedExhibit
    {
        public bool IsOpen { get; private set; }
        public bool FailOpen { get; set; }
        public bool FailClose { get; set; }
        public int OpenCalls { get; private set; }
        public int CloseCalls { get; private set; }

        public void Open()
        {
            OpenCalls++;
            if (!FailOpen)
                IsOpen = true;
        }

        public void Close()
        {
            CloseCalls++;
            if (!FailClose)
                IsOpen = false;
        }
    }
}
