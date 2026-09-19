using System;
using UnityEngine;

namespace BCaT.Production.Media
{
    /// <summary>
    /// Owns the one foreground/narrative audio experience that may play at a
    /// time. Ambient/environmental sources do not participate.
    /// </summary>
    public sealed class ForegroundAudioCoordinator : MonoBehaviour
    {
        public static ForegroundAudioCoordinator Instance { get; private set; }

        IForegroundAudio currentAudio;
        bool transitionInProgress;

        public IForegroundAudio CurrentAudio
        {
            get
            {
                ReconcileCurrent();
                return currentAudio;
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        void Update() => ReconcileCurrent();

        void OnApplicationFocus(bool focused)
        {
            if (!focused)
                StopCurrent();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
                StopCurrent();
        }

        void OnDestroy()
        {
            if (Instance != this)
                return;

            StopCurrent();
            Instance = null;
        }

        public bool RequestPlay(IForegroundAudio requestedAudio)
        {
            ReconcileCurrent();
            if (!IsAlive(requestedAudio))
                return false;

            if (ReferenceEquals(currentAudio, requestedAudio) && requestedAudio.IsPlaying)
                return true;

            if (transitionInProgress)
            {
                Debug.LogWarning(
                    $"[ForegroundAudioCoordinator] Play request for '{Describe(requestedAudio)}' ignored while another foreground-audio transition is running.");
                return false;
            }

            transitionInProgress = true;
            try
            {
                IForegroundAudio previousAudio = currentAudio;
                if (IsAlive(previousAudio))
                {
                    TryStop(previousAudio, $"before playing '{Describe(requestedAudio)}'");
                    if (IsAlive(previousAudio) && previousAudio.IsPlaying)
                    {
                        currentAudio = previousAudio;
                        Debug.LogError(
                            $"[ForegroundAudioCoordinator] '{Describe(previousAudio)}' remained playing; '{Describe(requestedAudio)}' was not started.");
                        return false;
                    }
                }

                currentAudio = null;
                if (!IsAlive(requestedAudio))
                    return false;

                try
                {
                    requestedAudio.Play();
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"[ForegroundAudioCoordinator] '{Describe(requestedAudio)}' threw while starting playback: {exception}");
                    return false;
                }

                if (!IsAlive(requestedAudio) || !requestedAudio.IsPlaying)
                {
                    Debug.LogWarning(
                        $"[ForegroundAudioCoordinator] '{Describe(requestedAudio)}' did not report playing; no foreground audio was retained.");
                    return false;
                }

                currentAudio = requestedAudio;
                return true;
            }
            finally
            {
                transitionInProgress = false;
            }
        }

        public void StopCurrent()
        {
            IForegroundAudio audio = currentAudio;
            if (!IsAlive(audio))
            {
                currentAudio = null;
                return;
            }

            // Stop even when AudioSource.isPlaying temporarily reports false
            // during a Quest system interruption. A real Stop prevents Unity
            // from resuming that source when application audio is restored.
            TryStop(audio, "while stopping current foreground audio");
            if (!IsAlive(audio) || !audio.IsPlaying)
                currentAudio = null;
        }

        public void NotifyStopped(IForegroundAudio audio)
        {
            if (!ReferenceEquals(currentAudio, audio))
                return;

            if (IsAlive(audio) && audio.IsPlaying)
            {
                Debug.LogWarning(
                    $"[ForegroundAudioCoordinator] Ignored stop notification from '{Describe(audio)}' because it still reports playing.");
                return;
            }

            currentAudio = null;
        }

        void ReconcileCurrent()
        {
            if (currentAudio == null)
                return;

            if (IsAlive(currentAudio) && currentAudio.IsPlaying)
                return;

            object stoppedOwner = currentAudio;
            currentAudio = null;
            MediaPlaybackRegistry.NotifyStopped(stoppedOwner);
        }

        static void TryStop(IForegroundAudio audio, string context)
        {
            try
            {
                audio.Stop();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[ForegroundAudioCoordinator] Failed to stop '{Describe(audio)}' {context}: {exception}");
            }
        }

        static bool IsAlive(IForegroundAudio audio)
        {
            if (audio == null)
                return false;

            return !(audio is UnityEngine.Object unityObject) || unityObject != null;
        }

        static string Describe(IForegroundAudio audio)
        {
            if (audio == null)
                return "<null>";
            if (audio is Component component)
                return component != null ? component.gameObject.name : "<destroyed Unity object>";
            return audio.ToString();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;
    }
}
