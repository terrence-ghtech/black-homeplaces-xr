using System;
using System.Reflection;
using BCaT.Exhibits.DejaVudu;
using BCaT.Production.Media;
using UnityEditor;
using UnityEngine;

namespace BCaT.EditorTools
{
    /// <summary>Edit-time transition checks suitable for Unity -executeMethod.</summary>
    public static class ForegroundAudioCoordinatorSelfTest
    {
        [MenuItem("BCaT/Diagnostics/Foreground Audio Coordinator Self Test")]
        public static void Run()
        {
            GameObject coordinatorObject = null;
            GameObject videoObject = null;
            GameObject dejaObject = null;
            try
            {
                coordinatorObject = new GameObject("ForegroundAudioCoordinatorSelfTest");
                ForegroundAudioCoordinator coordinator =
                    coordinatorObject.AddComponent<ForegroundAudioCoordinator>();
                Invoke(coordinator, "Awake");
                Require(ReferenceEquals(ForegroundAudioCoordinator.Instance, coordinator),
                    "coordinator should publish its service instance");

                VerifyReplacement(coordinator);
                VerifyExternalStopReconciles(coordinator);
                VerifyApplicationInterruptionStops(coordinator);

                ForegroundAudioSelfTestOwner audio = CreateOwner("VideoPredecessor");
                coordinator.RequestPlay(audio);
                videoObject = new GameObject("ForegroundAudioSelfTest_Video");
                MediaVideoController video = videoObject.AddComponent<MediaVideoController>();
                Invoke(video, "OpenInternal");
                Require(!audio.IsPlaying && coordinator.CurrentAudio == null,
                    "opening MediaVideoController should stop and clear foreground audio");

                dejaObject = new GameObject("ForegroundAudioSelfTest_Deja");
                AudioSource source = dejaObject.AddComponent<AudioSource>();
                DejaVuduSoundArchiveExhibit deja =
                    dejaObject.AddComponent<DejaVuduSoundArchiveExhibit>();
                SetField(deja, "audioSource", source);
                SetField(deja, "playWhenAudioReady", true);
                Invoke(deja, "CloseViewer");
                Require(!deja.IsOpen && !deja.IsPlaying,
                    "Deja Close should close the viewer and report audio stopped");
                Require(!(bool)GetField(deja, "playWhenAudioReady"),
                    "Deja Close should cancel queued playback");

                Debug.Log("[ForegroundAudioCoordinatorSelfTest] PASS");
            }
            finally
            {
                foreach (ForegroundAudioSelfTestOwner owner in
                         UnityEngine.Object.FindObjectsByType<ForegroundAudioSelfTestOwner>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (owner != null)
                        UnityEngine.Object.DestroyImmediate(owner.gameObject);
                }

                if (videoObject != null)
                    UnityEngine.Object.DestroyImmediate(videoObject);
                if (dejaObject != null)
                    UnityEngine.Object.DestroyImmediate(dejaObject);
                if (coordinatorObject != null)
                    UnityEngine.Object.DestroyImmediate(coordinatorObject);
            }
        }

        static void VerifyReplacement(ForegroundAudioCoordinator coordinator)
        {
            ForegroundAudioSelfTestOwner a = CreateOwner("NineNight");
            ForegroundAudioSelfTestOwner b = CreateOwner("Duppy");
            ForegroundAudioSelfTestOwner c = CreateOwner("Deja");

            Require(coordinator.RequestPlay(a) && a.IsPlaying,
                "Nine Night should begin playing");
            Require(coordinator.RequestPlay(b) && !a.IsPlaying && b.IsPlaying,
                "Nine Night -> Duppy should stop Nine Night before Duppy plays");
            Require(a.StopCalls == 1 && ReferenceEquals(coordinator.CurrentAudio, b),
                "Duppy should be the sole retained owner");

            Require(coordinator.RequestPlay(a) && !b.IsPlaying && a.IsPlaying,
                "Duppy -> Nine Night should stop Duppy before Nine Night plays");
            Require(coordinator.RequestPlay(c) && !a.IsPlaying && c.IsPlaying,
                "Nine Night -> Deja should stop Nine Night before Deja plays");
            Require(coordinator.RequestPlay(b) && !c.IsPlaying && b.IsPlaying,
                "Deja -> Duppy should stop Deja before Duppy plays");
            Require(coordinator.RequestPlay(c) && !b.IsPlaying && c.IsPlaying,
                "Duppy -> Deja should stop Duppy before Deja plays");
            Require(coordinator.RequestPlay(a) && !c.IsPlaying && a.IsPlaying,
                "Deja -> Nine Night should stop Deja before Nine Night plays");

            coordinator.StopCurrent();
            Require(!a.IsPlaying && coordinator.CurrentAudio == null,
                "StopCurrent should stop and clear Nine Night");
            Destroy(a);
            Destroy(b);
            Destroy(c);
        }

        static void VerifyExternalStopReconciles(ForegroundAudioCoordinator coordinator)
        {
            ForegroundAudioSelfTestOwner audio = CreateOwner("ExternallyStopped");
            coordinator.RequestPlay(audio);
            audio.ExternalStop();

            Require(coordinator.CurrentAudio == null,
                "actual stopped state should clear stale ownership without a notification");
            Destroy(audio);
        }

        static void VerifyApplicationInterruptionStops(ForegroundAudioCoordinator coordinator)
        {
            ForegroundAudioSelfTestOwner audio = CreateOwner("QuestSystemInterruption");
            coordinator.RequestPlay(audio);
            audio.ExternalStop(); // mirrors the temporary isPlaying=false system-audio window
            Invoke(coordinator, "OnApplicationFocus", false);

            Require(!audio.IsPlaying && audio.StopCalls == 1 && coordinator.CurrentAudio == null,
                "loss of application focus should issue a real stop even during a temporary system pause");
            Destroy(audio);
        }

        static ForegroundAudioSelfTestOwner CreateOwner(string name)
        {
            return new GameObject("ForegroundAudioSelfTest_" + name)
                .AddComponent<ForegroundAudioSelfTestOwner>();
        }

        static void Destroy(UnityEngine.Object value)
        {
            if (value != null)
                UnityEngine.Object.DestroyImmediate(value is Component component
                    ? component.gameObject
                    : value);
        }

        static void Invoke(object target, string method, params object[] arguments)
        {
            target.GetType().GetMethod(method,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.Invoke(target, arguments);
        }

        static void SetField(object target, string field, object value)
        {
            target.GetType().GetField(field,
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }

        static object GetField(object target, string field)
        {
            return target.GetType().GetField(field,
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(target);
        }

        static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(
                    "[ForegroundAudioCoordinatorSelfTest] FAIL: " + message);
        }
    }

    internal sealed class ForegroundAudioSelfTestOwner : MonoBehaviour, IForegroundAudio
    {
        public bool IsPlaying { get; private set; }
        public int StopCalls { get; private set; }

        public void Play() => IsPlaying = true;

        public void Stop()
        {
            StopCalls++;
            IsPlaying = false;
        }

        public void ExternalStop() => IsPlaying = false;
    }
}
