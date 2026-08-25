using System.Collections.Generic;
using BCaT.Production.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace BCaT.Production.Shell
{
    /// <summary>
    /// Session-only first-landing overlay shown after the main house has loaded
    /// and the arrival fade has completed.
    /// </summary>
    public sealed class OpeningOnboardingController : MonoBehaviour
    {
        const int SortingOrder = 32650;
        const string ContinueLabel = "Begin Exploring";
        const float DesktopButtonSpacerHeight = 33f;
        const float QuestOverlayDistance = 1.35f;
        const float QuestOverlayWorldWidth = 3.25f;
        const float QuestOverlayReferenceWidth = 1360f;
        const float SpawnHoldTimeoutSeconds = 30f;
        static bool shownThisSession;
        static OpeningOnboardingController activeController;
        static GameObject activeOverlayRoot;

        readonly List<BehaviourState> questLocomotionStates = new List<BehaviourState>();

        GameObject overlayRoot;
        InputAction questDismissAction;
        bool open;

        // Separate gate owner from the panel itself: the panel takes the hold
        // over without the gate's count ever reaching zero.
        readonly object spawnHoldToken = new object();
        bool spawnHoldActive;
        Coroutine spawnHoldWatchdog;


        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneArrivalController.ArrivalCompleted += OnArrivalCompleted;
        }

        void Start()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name != ResetService.MainSceneName || SceneTransitionState.IsTransitionInProgress)
                return;

            if (!shownThisSession && !open)
                AcquireSpawnHold();
            StartCoroutine(ShowAfterFrame(scene));
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneArrivalController.ArrivalCompleted -= OnArrivalCompleted;
            if (open)
                Close();
            ReleaseSpawnHold();
        }

        void Update()
        {
            if (!open || !BCaTPlatform.IsQuest)
                return;

            if (questDismissAction != null && questDismissAction.WasPressedThisFrame())
                Close();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single)
                return;

            if (scene.name != ResetService.MainSceneName)
            {
                ReleaseSpawnHold();
                if (open)
                    Close();
                return;
            }

            // Hold the player at the authored spawn from the main scene's FIRST
            // frame, not from the moment this panel appears. Between activation
            // and the panel there is the arrival placement plus the arrival
            // fade: on Quest only the fade was covered (and the hold was
            // released a frame before the panel took over), and on desktop
            // nothing held the controls at all -- so the player could walk off
            // the authored spawn, or be carried off it by gravity, before ever
            // seeing the instructions.
            if (!shownThisSession && !open)
                AcquireSpawnHold();
        }

        void OnArrivalCompleted(Scene scene)
        {
            if (scene.name != ResetService.MainSceneName || shownThisSession)
                return;

            StartCoroutine(ShowAfterFrame(scene));
        }

        System.Collections.IEnumerator ShowAfterFrame(Scene scene)
        {
            yield return null;

            if (shownThisSession || open || scene.name != SceneManager.GetActiveScene().name)
                yield break;

            Show();
        }

        void Show()
        {
            if (activeController != null && activeController != this)
                activeController.Close();
            if (activeOverlayRoot != null)
            {
                Destroy(activeOverlayRoot);
                activeOverlayRoot = null;
            }

            shownThisSession = true;
            open = true;
            activeController = this;

            InteractionState.Block(this, InteractionBlockReason.Menu, Close);
            PlayerControlGate.Suspend(this);
            // Taken in this order so the gate's hold count never drops to zero
            // between the spawn hold and the panel's own hold.
            ReleaseSpawnHold();
            if (BCaTPlatform.IsQuest)
                SuspendQuestLocomotion();

            PresentOverlay();
        }

        void PresentOverlay()
        {
            overlayRoot = BCaTPlatform.IsQuest ? BuildQuestOverlay() : BuildDesktopOverlay();
            activeOverlayRoot = overlayRoot;
            if (BCaTPlatform.IsQuest)
                EnableQuestDismissAction();
        }

        GameObject BuildDesktopOverlay()
        {
            var canvas = UiFactory.CreateOverlayCanvas("BCaT_OpeningOnboarding", SortingOrder);
            BuildPanel(canvas.transform, quest: false, worldSpace: false);
            return canvas.gameObject;
        }

        GameObject BuildQuestOverlay()
        {
            Camera cam = Camera.main;
            var canvasObject = new GameObject("BCaT_OpeningOnboarding",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster), typeof(CanvasGroup));

            var rect = (RectTransform)canvasObject.transform;

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            canvas.sortingOrder = SortingOrder;

            rect.sizeDelta = new Vector2(QuestOverlayReferenceWidth, 820f);
            rect.localScale = Vector3.one * (QuestOverlayWorldWidth / QuestOverlayReferenceWidth);
            PlaceQuestOverlayOnce(rect, cam);

            BuildPanel(rect, quest: true, worldSpace: true);
            return canvasObject;
        }

        /// <summary>
        /// Place the panel from the starting frame the arrival established, not
        /// from the live head pose. The fade runs for the best part of a second
        /// after that frame is set, so reading the camera here plants the panel
        /// wherever the player happened to be looking — off to one side, and
        /// tilted by their head roll, permanently, because the canvas is
        /// world-space. Rotation is yaw-only for the same reason.
        /// </summary>
        static void PlaceQuestOverlayOnce(RectTransform rect, Camera cam)
        {
            if (XrArrivalAlignment.HasEstablishedFrame)
            {
                Vector3 forward = XrArrivalAlignment.EstablishedForward;
                Vector3 anchored = XrArrivalAlignment.EstablishedHeadPoint + forward * QuestOverlayDistance;
                rect.SetPositionAndRotation(anchored, Quaternion.LookRotation(forward, Vector3.up));
                return;
            }

            if (cam == null)
            {
                rect.SetPositionAndRotation(new Vector3(0f, 0f, QuestOverlayDistance), Quaternion.identity);
                return;
            }

            // No established frame (tracking never reported): fall back to the
            // head, but keep the panel upright rather than inheriting head tilt.
            Transform cameraTransform = cam.transform;
            Vector3 flat = cameraTransform.forward;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.000001f)
                flat = Vector3.forward;
            flat.Normalize();
            rect.SetPositionAndRotation(cameraTransform.position + flat * QuestOverlayDistance,
                                        Quaternion.LookRotation(flat, Vector3.up));
        }

        void BuildPanel(Transform parent, bool quest, bool worldSpace)
        {
            var panel = UiFactory.CreateCenterPanel(parent, "Panel",
                worldSpace ? new Vector2(1120f, 690f) : new Vector2(1080f, 690f));
            var column = UiFactory.CreateColumn(panel, "Column", 18f);

            OpeningInstructionsUi.AddTo(column, quest, worldSpace ? 19f : 20f, 510f);

            if (!quest)
                AddDesktopButtonSpacer(column);
            var button = UiFactory.CreateButton(column, ContinueLabel, Close, 27f);

            if (BCaTPlatform.IsQuest)
                UiFactory.CreateLabel(column, "Press either trigger to continue.", 18f);

            if (!BCaTPlatform.IsQuest)
                UiFactory.SelectForKeyboard(button);
        }

        static void AddDesktopButtonSpacer(Transform parent)
        {
            var spacer = UiFactory.CreateRect(parent, "DesktopButtonSpacer");
            spacer.sizeDelta = new Vector2(0f, DesktopButtonSpacerHeight);
            var layout = spacer.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = DesktopButtonSpacerHeight;
            layout.preferredHeight = DesktopButtonSpacerHeight;
            layout.flexibleHeight = 0f;
        }

        /// <summary>
        /// Freeze the player where the scene authored them until the
        /// instructions are dismissed. This is the shared control gate, not a
        /// second lock: head tracking on Quest and the cursor on desktop are
        /// untouched, so the player can still look around and click Begin
        /// Exploring.
        /// </summary>
        void AcquireSpawnHold()
        {
            if (spawnHoldActive)
                return;

            spawnHoldActive = true;
            PlayerControlGate.Suspend(spawnHoldToken);
            spawnHoldWatchdog = StartCoroutine(SpawnHoldWatchdog());
        }

        void ReleaseSpawnHold()
        {
            if (spawnHoldWatchdog != null)
            {
                StopCoroutine(spawnHoldWatchdog);
                spawnHoldWatchdog = null;
            }

            if (!spawnHoldActive)
                return;

            spawnHoldActive = false;
            PlayerControlGate.Resume(spawnHoldToken);
        }

        /// <summary>
        /// The hold must never outlive what it is waiting for. If the panel
        /// never arrives -- an arrival coroutine that dies before
        /// ArrivalCompleted, say -- give the player their controls back rather
        /// than leaving them frozen for the session.
        /// </summary>
        System.Collections.IEnumerator SpawnHoldWatchdog()
        {
            yield return new WaitForSecondsRealtime(SpawnHoldTimeoutSeconds);

            if (!spawnHoldActive || open)
                yield break;

            Debug.LogWarning("[OpeningOnboardingController] The opening instructions did not appear within " +
                             $"{SpawnHoldTimeoutSeconds:0}s; releasing the spawn hold so controls are never " +
                             "locked permanently.");
            ReleaseSpawnHold();
        }

        void SuspendQuestLocomotion()
        {
            questLocomotionStates.Clear();
            foreach (Behaviour behaviour in FindObjectsByType<Behaviour>(FindObjectsInactive.Exclude))
            {
                if (behaviour == null || !behaviour.enabled || !IsQuestLocomotionBehaviour(behaviour))
                    continue;

                questLocomotionStates.Add(new BehaviourState(behaviour, behaviour.enabled));
                behaviour.enabled = false;
            }
        }

        static bool IsQuestLocomotionBehaviour(Behaviour behaviour)
        {
            // Kept in step with PlayerControlGate.IsXRLocomotionBehaviour,
            // including GravityProvider, which moves the rig root too.
            string name = behaviour.GetType().Name;
            return name.Contains("MoveProvider") ||
                   name.Contains("TurnProvider") ||
                   name.Contains("TeleportationProvider") ||
                   name.Contains("ClimbProvider") ||
                   name.Contains("JumpProvider") ||
                   name.Contains("GravityProvider");
        }

        void RestoreQuestLocomotion()
        {
            foreach (var state in questLocomotionStates)
                if (state.Behaviour != null)
                    state.Behaviour.enabled = state.Enabled;
            questLocomotionStates.Clear();
        }

        void EnableQuestDismissAction()
        {
            questDismissAction = new InputAction("OpeningOnboardingQuestDismiss", InputActionType.Button);
            questDismissAction.AddBinding("<XRController>{LeftHand}/{TriggerButton}");
            questDismissAction.AddBinding("<XRController>{RightHand}/{TriggerButton}");
            questDismissAction.Enable();
        }

        void DisableQuestDismissAction()
        {
            if (questDismissAction == null)
                return;

            questDismissAction.Disable();
            questDismissAction.Dispose();
            questDismissAction = null;
        }

        void Close()
        {
            if (!open)
                return;

            open = false;
            DisableQuestDismissAction();
            RestoreQuestLocomotion();

            GameObject rootToDestroy = overlayRoot;
            if (rootToDestroy != null)
                Destroy(rootToDestroy);
            overlayRoot = null;
            if (activeOverlayRoot != null && activeOverlayRoot == rootToDestroy)
                activeOverlayRoot = null;
            if (activeController == this)
                activeController = null;

            ReleaseSpawnHold();
            InteractionState.Unblock(this);
            PlayerControlGate.Resume(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            shownThisSession = false;
            activeController = null;
            activeOverlayRoot = null;
        }

        readonly struct BehaviourState
        {
            public readonly Behaviour Behaviour;
            public readonly bool Enabled;

            public BehaviourState(Behaviour behaviour, bool enabled)
            {
                Behaviour = behaviour;
                Enabled = enabled;
            }
        }
    }
}
