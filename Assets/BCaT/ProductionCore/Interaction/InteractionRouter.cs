using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BCaT.Production.Interaction
{
    /// <summary>
    /// The single owner of standard world interaction:
    ///   candidate collection → validation (distance, camera focus, line of
    ///   sight, blockers) → selection of one target → platform input →
    ///   one interaction event → platform prompt.
    ///
    /// Desktop input is polled from DesktopInteractionInputProvider; Quest input
    /// arrives event-driven via RequestXRSelect (wired from existing
    /// XRSimpleInteractable relays), so both platforms share the same ownership,
    /// blocking, and cooldown rules.
    /// </summary>
    public sealed class InteractionRouter : MonoBehaviour
    {
        public static InteractionRouter Instance { get; private set; }

        static readonly List<IInteractionTarget> registry = new List<IInteractionTarget>();
        static readonly List<IExclusiveInteractionZone> zones = new List<IExclusiveInteractionZone>();

        [Tooltip("Seconds after any dispatched interaction during which further interactions are ignored.")]
        public float interactionCooldown = 0.25f;

        [Tooltip("Layers blocking line of sight (defaults to everything; triggers are always ignored).")]
        public LayerMask lineOfSightMask = ~0;

        public IInteractionTarget CurrentTarget { get; private set; }

        IInteractionInputProvider input;
        Camera cachedCamera;
        float lastDispatchTime = -999f;
        bool missingCameraLogged;
        readonly RaycastHit[] losHits = new RaycastHit[16];
        readonly Dictionary<Object, IInteractionTarget> xrHoverTargets = new Dictionary<Object, IInteractionTarget>();

        public static void Register(IInteractionTarget target)
        {
            if (target != null && !registry.Contains(target))
                registry.Add(target);
        }

        public static void Unregister(IInteractionTarget target)
        {
            registry.Remove(target);
            if (Instance != null && ReferenceEquals(Instance.CurrentTarget, target))
                Instance.SetCurrentTarget(null);
        }

        public static void RegisterZone(IExclusiveInteractionZone zone)
        {
            if (zone != null && !zones.Contains(zone))
                zones.Add(zone);
        }

        public static void UnregisterZone(IExclusiveInteractionZone zone) => zones.Remove(zone);

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            input = PlatformCapabilities.IsQuestConfiguration || PlatformCapabilities.IsXRActive
                ? (IInteractionInputProvider)new QuestInteractionInputProvider()
                : new DesktopInteractionInputProvider();

            SceneManager.sceneLoaded += OnSceneLoaded;
            Debug.Log($"[InteractionRouter] Initialized in scene '{gameObject.scene.name}' with input provider '{input.GetType().Name}' ({PlatformCapabilities.Describe()}).");
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            cachedCamera = null;
            missingCameraLogged = false;
            xrHoverTargets.Clear();
            SetCurrentTarget(null);
        }

        Camera PlayerCamera
        {
            get
            {
                if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
                {
                    cachedCamera = Camera.main;
                    if (cachedCamera != null)
                    {
                        missingCameraLogged = false;
                        Debug.Log($"[InteractionRouter] Scene '{SceneManager.GetActiveScene().name}' active camera assigned: '{Path(cachedCamera.transform)}'.");
                    }
                }
                return cachedCamera;
            }
        }

        void Update()
        {
            // XR may finish initializing after startup; keep provider in sync.
            if (PlatformCapabilities.IsXRActive && input is DesktopInteractionInputProvider)
                input = new QuestInteractionInputProvider();

            bool interactPressed = input.InteractPressedThisFrame;
            bool clickPressed = input.ClickPressedThisFrame;

            if (InteractionState.IsBlocked || InteractionState.InputSuppressedThisFrame)
            {
                if (!InteractionState.InputSuppressedThisFrame &&
                    interactPressed &&
                    InteractionState.TryClose(InteractionBlockReason.Media))
                {
                    SetCurrentTarget(null);
                    xrHoverTargets.Clear();
                    foreach (var zone in zones)
                        if (zone.ZoneActive)
                            zone.ZoneSuppressPrompts();
                    return;
                }

                SetCurrentTarget(null);
                xrHoverTargets.Clear();
                foreach (var zone in zones)
                    if (zone.ZoneActive)
                        zone.ZoneSuppressPrompts();
                return;
            }

            // An active exclusive zone (Black Kitchen stations) owns selection;
            // the router still owns input, blocking, and cooldown.
            IExclusiveInteractionZone activeZone = null;
            foreach (var zone in zones)
                if (zone.ZoneActive) { activeZone = zone; break; }

            if (activeZone != null)
            {
                SetCurrentTarget(null);
                bool pressed = interactPressed && !InCooldown;
                if (pressed) lastDispatchTime = Time.unscaledTime;
                activeZone.ZoneTick(pressed);
                return;
            }

            if (PlatformCapabilities.IsQuestConfiguration || PlatformCapabilities.IsXRActive)
            {
                SetCurrentTarget(SelectBestXRHoverTarget());
                return;
            }

            var cam = PlayerCamera;
            if (cam == null)
            {
                if (!missingCameraLogged)
                {
                    missingCameraLogged = true;
                    Debug.LogWarning($"[InteractionRouter] Scene '{SceneManager.GetActiveScene().name}' has no active MainCamera; world interaction selection is disabled.");
                }
                SetCurrentTarget(null);
                return;
            }

            SetCurrentTarget(SelectBestTarget(cam));

            if (CurrentTarget == null || InCooldown)
                return;

            if (interactPressed)
                Dispatch(CurrentTarget, InteractionActivation.DesktopInteractKey);
            else if (clickPressed && CurrentTarget.AllowDesktopClick)
                Dispatch(CurrentTarget, InteractionActivation.DesktopClick);
        }

        bool InCooldown => Time.unscaledTime - lastDispatchTime < interactionCooldown;

        public void RequestXRHover(Object hoverSource, IInteractionTarget target)
        {
            if (hoverSource == null || target == null)
                return;

            xrHoverTargets[hoverSource] = target;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log($"[InteractionRouter] XR hover entered source='{hoverSource.name}' target='{TargetName(target)}'.");
#endif
        }

        public void ClearXRHover(Object hoverSource)
        {
            if (hoverSource == null)
                return;

            if (xrHoverTargets.Remove(hoverSource))
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                Debug.Log($"[InteractionRouter] XR hover exited source='{hoverSource.name}'.");
#endif
            }
        }

        IInteractionTarget SelectBestXRHoverTarget()
        {
            if (xrHoverTargets.Count == 0)
                return null;

            IInteractionTarget best = null;
            int bestPriority = int.MinValue;
            var stale = new List<Object>();
            foreach (var pair in xrHoverTargets)
            {
                var target = pair.Value;
                if (pair.Key == null || target == null || !target.Exists)
                {
                    stale.Add(pair.Key);
                    continue;
                }

                if (!target.IsAvailable)
                    continue;

                if (target.Priority >= bestPriority)
                {
                    best = target;
                    bestPriority = target.Priority;
                }
            }

            foreach (var key in stale)
                xrHoverTargets.Remove(key);

            return best;
        }

        IInteractionTarget SelectBestTarget(Camera cam)
        {
            IInteractionTarget best = null;
            float bestScore = float.MaxValue;
            int bestPriority = int.MinValue;
            Vector3 camPos = cam.transform.position;
            Vector3 camFwd = cam.transform.forward;
            bool relaxFocus = Settings.SettingsManager.Current.accessibility.persistentPrompts;

            for (int i = registry.Count - 1; i >= 0; i--)
            {
                var t = registry[i];
                if (t == null || !t.Exists) { registry.RemoveAt(i); continue; }
                if (!t.IsAvailable) continue;

                Vector3 to = t.FocusPoint - camPos;
                float distance = to.magnitude;
                if (distance > t.MaxDistance) continue;

                float angle = Vector3.Angle(camFwd, to);
                float maxAngle = t.MaxViewAngle;
                if (maxAngle > 0f)
                {
                    float allowed = relaxFocus ? maxAngle * 2f : maxAngle;
                    if (angle > allowed) continue;
                }

                if (t.RequireLineOfSight && !HasLineOfSight(camPos, t))
                    continue;

                // Priority dominates; then view angle; distance breaks ties.
                float score = angle * 10f + distance;
                if (t.Priority > bestPriority ||
                    (t.Priority == bestPriority && score < bestScore))
                {
                    best = t;
                    bestScore = score;
                    bestPriority = t.Priority;
                }
            }

            return best;
        }

        /// <summary>
        /// Line-of-sight test using the project's established pattern: foreign
        /// triggers never block, and colliders belonging to the target itself
        /// never block. Target trigger volumes still count as a valid hit so an
        /// exhibit with a trigger interaction shell is not hidden by sibling
        /// display geometry behind that shell.
        /// </summary>
        bool HasLineOfSight(Vector3 from, IInteractionTarget target)
        {
            Vector3 to = target.FocusPoint - from;
            float distance = to.magnitude;
            if (distance < 0.01f) return true;

            int count = Physics.RaycastNonAlloc(new Ray(from, to / distance), losHits,
                distance - 0.05f, lineOfSightMask, QueryTriggerInteraction.Collide);

            System.Array.Sort(losHits, 0, count, RaycastHitDistanceComparer.Instance);

            var own = target.OwnColliders;
            for (int i = 0; i < count; i++)
            {
                var hit = losHits[i].collider;
                bool isOwn = false;
                if (own != null)
                    for (int j = 0; j < own.Length; j++)
                        if (own[j] == hit) { isOwn = true; break; }
                if (isOwn)
                    return true;
                if (hit != null && hit.isTrigger)
                    continue;
                if (!isOwn)
                    return false;
            }
            return true;
        }

        sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

            public int Compare(RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance);
        }

        void SetCurrentTarget(IInteractionTarget target)
        {
            if (ReferenceEquals(CurrentTarget, target))
            {
                // Refresh the prompt text even without a focus change (dynamic verbs).
                if (target != null)
                    Shell.InteractionPromptUi.Show(target.GetPrompt(PlatformCapabilities.UseXRPrompts));
                return;
            }

            if (CurrentTarget != null && CurrentTarget.Exists)
            {
                Debug.Log($"[InteractionRouter] Scene '{SceneManager.GetActiveScene().name}' focus lost: '{TargetName(CurrentTarget)}'.");
                CurrentTarget.OnFocusChanged(false);
            }

            CurrentTarget = target;

            if (CurrentTarget != null)
            {
                CurrentTarget.OnFocusChanged(true);
                Debug.Log($"[InteractionRouter] Scene '{SceneManager.GetActiveScene().name}' focus gained: '{TargetName(CurrentTarget)}' prompt='{CurrentTarget.GetPrompt(PlatformCapabilities.UseXRPrompts)}'.");
                Shell.InteractionPromptUi.Show(CurrentTarget.GetPrompt(PlatformCapabilities.UseXRPrompts));
            }
            else
            {
                Shell.InteractionPromptUi.Hide();
            }
        }

        /// <summary>
        /// Quest entry point: XRSimpleInteractable select relays call this so XR
        /// interactions obey the same blocking and cooldown rules as desktop.
        /// Returns true when the interaction was dispatched.
        /// </summary>
        public bool RequestXRSelect(IInteractionTarget target)
        {
            InteractionState.PruneDestroyedOwners();
            if (target == null)
                return RejectXRSelect("<null>", "target is null");
            if (!target.Exists)
                return RejectXRSelect(TargetName(target), "target no longer exists");
            if (!target.IsAvailable)
                return RejectXRSelect(TargetName(target), "target is not available");
            if (InteractionState.IsBlocked)
                return RejectXRSelect(TargetName(target), $"interaction blocked ({InteractionState.ActiveReasons})");
            if (InteractionState.InputSuppressedThisFrame)
                return RejectXRSelect(TargetName(target), "input suppressed this frame");
            if (InCooldown)
                return RejectXRSelect(TargetName(target), "router cooldown active");

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log($"[InteractionRouter] XR select accepted for '{TargetName(target)}'.");
#endif
            Dispatch(target, InteractionActivation.XRSelect);
            return true;
        }

        bool RejectXRSelect(string targetName, string reason)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.LogWarning($"[InteractionRouter] XR select rejected for '{targetName}': {reason}.");
#endif
            return false;
        }

        /// <summary>Programmatic activation (smoke tests, exhibit directory).</summary>
        public bool RequestProgrammatic(IInteractionTarget target)
        {
            if (target == null || !target.Exists || !target.IsAvailable) return false;
            if (InteractionState.IsBlocked || InteractionState.InputSuppressedThisFrame) return false;
            Dispatch(target, InteractionActivation.Programmatic);
            return true;
        }

        void Dispatch(IInteractionTarget target, InteractionActivation activation)
        {
            lastDispatchTime = Time.unscaledTime;
            Debug.Log($"[InteractionRouter] Scene '{SceneManager.GetActiveScene().name}' invoking '{TargetName(target)}' via {activation}.");
            try
            {
                target.OnInteract(activation);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[InteractionRouter] Target '{target}' threw during OnInteract: {e}");
            }
        }

        static string TargetName(IInteractionTarget target)
        {
            if (target is Component component)
                return Path(component.transform);
            return target != null ? target.ToString() : "(null)";
        }

        static string Path(Transform transform)
        {
            if (transform == null)
                return "(null)";

            string path = transform.name;
            Transform parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            registry.Clear();
            zones.Clear();
        }
    }
}
