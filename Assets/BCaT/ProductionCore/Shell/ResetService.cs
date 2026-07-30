using System.Collections.Generic;
using BCaT.Production.Interaction;
using BCaT.Production.Media;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BCaT.Production.Shell
{
    /// <summary>
    /// Reset-position / unstuck / return-to-main-entrance flows.
    ///
    /// The main scene has no authored "MainEntrance" SceneSpawnPoint, so the
    /// service captures the active rig's authored starting pose on every scene
    /// load — that pose *is* the main entrance in the main house — and uses it
    /// as the reset fallback alongside any SceneSpawnPoints in the scene.
    /// Teleporting reuses SceneArrivalController.PlaceActivePlayerAt, which
    /// already disables the CharacterController, clears fall velocity, and
    /// restores control state safely.
    /// </summary>
    public static class ResetService
    {
        public const string MainSceneName = "BH_XR_MainScene";
        public const string LoadingSceneName = "LoadingScene";
        public const string MainEntranceSpawnId = "MainEntrance";

        static Vector3 entryPosition;
        static Quaternion entryRotation;
        static bool entryCaptured;
        static string capturedScene;

        /// <summary>Called by the bootstrap after each scene load.</summary>
        public static void CaptureSceneEntryPose(Scene scene)
        {
            entryCaptured = false;
            capturedScene = scene.name;

            var rig = FindActiveRigTransform();
            if (rig != null)
            {
                entryPosition = rig.position;
                entryRotation = rig.rotation;
                entryCaptured = true;
            }
        }

        static Transform FindActiveRigTransform()
        {
            foreach (var rig in Object.FindObjectsByType<ScenePlayerRig>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                return rig.transform;

            var controller = Object.FindFirstObjectByType<CharacterController>(FindObjectsInactive.Exclude);
            return controller != null ? controller.transform : null;
        }

        /// <summary>
        /// Move the player to the nearest safe location (any SceneSpawnPoint or
        /// the captured scene entry pose). Does not reset exhibits or media.
        /// </summary>
        public static void ResetPosition()
        {
            var rig = FindActiveRigTransform();
            if (rig == null)
            {
                Debug.LogWarning("[ResetService] No active player rig found; cannot reset position.");
                return;
            }

            Transform best = null;
            float bestDistance = float.MaxValue;
            GameObject temp = null;

            foreach (var spawn in Object.FindObjectsByType<SceneSpawnPoint>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                float d = (spawn.transform.position - rig.position).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = spawn.transform; }
            }

            if (entryCaptured && capturedScene == SceneManager.GetActiveScene().name)
            {
                float d = (entryPosition - rig.position).sqrMagnitude;
                if (best == null || d < bestDistance)
                {
                    temp = new GameObject("BCaT_TempResetPose");
                    temp.transform.SetPositionAndRotation(entryPosition, entryRotation);
                    best = temp.transform;
                }
            }

            if (best == null)
            {
                Debug.LogWarning("[ResetService] No spawn point or captured entry pose available.");
                return;
            }

            SceneArrivalController.PlaceActivePlayerAt(best);
            if (temp != null)
                Object.Destroy(temp, 1f);
            Debug.Log($"[ResetService] Player reset to '{best.name}'.");
        }

        /// <summary>Unstuck is reset-position with the same safety path (documented alias).</summary>
        public static void Unstuck() => ResetPosition();

        /// <summary>
        /// Full return-to-main-entrance flow: stop media, close focused
        /// interfaces, then either teleport (already in the main house) or run a
        /// real scene transition through the shared loading flow (from Black
        /// Kitchen or any other loaded scene).
        /// </summary>
        public static void ReturnToMainEntrance()
        {
            if (SceneTransitionState.IsTransitionInProgress)
            {
                Debug.LogWarning("[ResetService] Transition already in progress; ignoring return request.");
                return;
            }

            // 1) Stop media and close all blocking interfaces.
            MediaPlaybackRegistry.StopAll();
            InteractionState.ForceCloseAll();
            PrepareExhibitAudioForExit();

            string current = SceneManager.GetActiveScene().name;
            if (current == MainSceneName)
            {
                // Already home: teleport to the captured entrance pose.
                if (entryCaptured)
                {
                    var temp = new GameObject("BCaT_TempEntrancePose");
                    temp.transform.SetPositionAndRotation(entryPosition, entryRotation);
                    SceneArrivalController.PlaceActivePlayerAt(temp.transform);
                    Object.Destroy(temp, 1f);
                }
                else
                {
                    ResetPosition();
                }
                PlayerControlGate.ForceResumeAll();
                return;
            }

            // 2) From any other scene, use the shared transition lifecycle.
            // The 'MainEntrance' spawn id has no SceneSpawnPoint in the main
            // scene, so arrival intentionally leaves the player at the rig's
            // authored start position — which is the main entrance.
            SceneTransitionState.RequestTransition(MainSceneName, MainEntranceSpawnId, current);
            SceneManager.LoadSceneAsync(LoadingSceneName, LoadSceneMode.Single);
        }

        /// <summary>Give scene-audio authorities their exit notification (Black Kitchen).</summary>
        static void PrepareExhibitAudioForExit()
        {
            foreach (var coordinator in Object.FindObjectsByType<BlackKitchenAudioCoordinator>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                try { coordinator.PrepareForSceneExit(); }
                catch (System.Exception e)
                {
                    Debug.LogError($"[ResetService] Audio exit preparation failed: {e}");
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            entryCaptured = false;
            capturedScene = null;
        }
    }
}
