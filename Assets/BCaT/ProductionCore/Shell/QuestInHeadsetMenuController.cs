using System;
using BCaT.Production.Access;
using BCaT.Production.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace BCaT.Production.Shell
{
    /// <summary>
    /// Minimal Quest application menu. The standard OpenXR left-controller
    /// menu button toggles one stationary world-space uGUI view. Existing XR UI
    /// rays remain enabled; PlayerControlGate suspends only locomotion providers.
    /// </summary>
    public sealed class QuestInHeadsetMenuController : MonoBehaviour
    {
        const string MenuBinding = "<XRController>{LeftHand}/menuButton";
        const float ReadingDistance = 1.55f;
        const float WorldScale = 0.00125f;
        static readonly Vector2 CanvasSize = new Vector2(1200f, 760f);

        InputAction menuAction;
        GameObject menuRoot;
        RectTransform pageRoot;
        bool open;

        public bool IsOpen => open;
        internal InputAction MenuAction => menuAction;

        void Awake()
        {
            menuAction = new InputAction(
                "Quest In-Headset Menu",
                InputActionType.Button,
                MenuBinding,
                expectedControlType: "Button");
        }

        void OnEnable()
        {
            menuAction?.Enable();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Close();
            menuAction?.Disable();
        }

        void OnDestroy()
        {
            menuAction?.Dispose();
            menuAction = null;
        }

        void Update()
        {
            if (!BCaTPlatform.IsQuest || menuAction == null || !menuAction.WasPressedThisFrame())
                return;

            Toggle();
        }

        public void Toggle()
        {
            if (open)
                Close();
            else
                Open();
        }

        public void Open()
        {
            if (open || SceneTransitionState.IsTransitionInProgress)
                return;

            string scene = SceneManager.GetActiveScene().name;
            if (!CanOpenInScene(scene))
                return;
            if (InteractionState.HasReason(InteractionBlockReason.Menu))
                return;

            Camera head = ResolveHeadCamera();
            if (head == null)
            {
                Debug.LogWarning("[QuestInHeadsetMenu] No active XR head camera; menu was not opened.");
                return;
            }

            BuildView(head);
            PositionInFrontOf(head.transform);
            open = true;
            InteractionState.Block(this, InteractionBlockReason.Menu, Close);
            PlayerControlGate.Suspend(this);
            InteractionPromptUi.Hide();
            ShowDirectory();
            Debug.Log($"[QuestInHeadsetMenu] Opened from {MenuBinding} in scene '{scene}'.");
        }

        static bool CanOpenInScene(string scene) =>
            scene != ResetService.LoadingSceneName &&
            scene != PauseMenuController.MainMenuSceneName &&
            scene != SceneTransitionState.BlackKitchenSceneName;

        public void Close()
        {
            if (!open && menuRoot == null)
                return;

            open = false;
            if (menuRoot != null)
                DestroyMenuObject(menuRoot);
            menuRoot = null;
            pageRoot = null;
            InteractionState.Unblock(this);
            PlayerControlGate.Resume(this);
            Debug.Log("[QuestInHeadsetMenu] Closed; prior locomotion state restored.");
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (open || menuRoot != null)
                Close();
        }

        void BuildView(Camera head)
        {
            UiFactory.EnsureEventSystem();

            menuRoot = new GameObject(
                "BCaT_QuestInHeadsetMenu",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(TrackedDeviceGraphicRaycaster));

            RectTransform canvasRect = menuRoot.GetComponent<RectTransform>();
            canvasRect.sizeDelta = CanvasSize;
            canvasRect.localScale = Vector3.one * WorldScale;

            Canvas canvas = menuRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = head;
            canvas.sortingOrder = 32000;

            CanvasScaler scaler = menuRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.dynamicPixelsPerUnit = 10f;

            RectTransform backdrop = UiFactory.CreateRect(canvasRect, "Backdrop");
            Stretch(backdrop);
            backdrop.gameObject.AddComponent<Image>().color = UiFactory.PanelColor;

            RectTransform header = UiFactory.CreateRect(backdrop, "Header");
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = Vector2.one;
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, 92f);
            header.anchoredPosition = Vector2.zero;
            header.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.1f, 0.08f, 1f);
            TextMeshProUGUI heading = UiFactory.CreateLabel(
                header, "Black Homeplaces: The XR House", 34f, TextAlignmentOptions.Center);
            Stretch(heading.rectTransform);
            heading.fontStyle = FontStyles.Bold;

            RectTransform body = UiFactory.CreateRect(backdrop, "Body");
            Stretch(body);
            body.offsetMin = new Vector2(22f, 22f);
            body.offsetMax = new Vector2(-22f, -108f);

            RectTransform navigation = UiFactory.CreateRect(body, "Navigation");
            navigation.anchorMin = Vector2.zero;
            navigation.anchorMax = new Vector2(0.25f, 1f);
            navigation.offsetMin = Vector2.zero;
            navigation.offsetMax = new Vector2(-10f, 0f);
            navigation.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.3f);
            var navigationLayout = navigation.gameObject.AddComponent<VerticalLayoutGroup>();
            navigationLayout.padding = new RectOffset(18, 18, 18, 18);
            navigationLayout.spacing = 14f;
            navigationLayout.childControlWidth = true;
            navigationLayout.childControlHeight = true;
            navigationLayout.childForceExpandWidth = true;
            navigationLayout.childForceExpandHeight = false;

            CreateNavigationButton(navigation, "Resume", Close, 25f);
            CreateNavigationButton(navigation, "Directory", ShowDirectory, 25f);
            CreateNavigationButton(navigation, "About", ShowAbout, 25f);
            CreateNavigationButton(navigation, "Credits", ShowCredits, 25f);
            RectTransform spacer = UiFactory.CreateRect(navigation, "Spacer");
            spacer.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            CreateNavigationButton(navigation, "Main Entrance", ReturnToMainEntrance, 24f);
            CreateNavigationButton(navigation, "Quit", QuitApplication, 25f);

            pageRoot = UiFactory.CreateRect(body, "Page");
            pageRoot.anchorMin = new Vector2(0.25f, 0f);
            pageRoot.anchorMax = Vector2.one;
            pageRoot.offsetMin = new Vector2(10f, 0f);
            pageRoot.offsetMax = Vector2.zero;
            pageRoot.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.22f);
            var pageLayout = pageRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            pageLayout.padding = new RectOffset(24, 24, 20, 20);
            pageLayout.spacing = 14f;
            pageLayout.childControlWidth = true;
            pageLayout.childControlHeight = true;
            pageLayout.childForceExpandWidth = true;
            pageLayout.childForceExpandHeight = false;
        }

        static void CreateNavigationButton(
            Transform parent, string text, Action onClick, float fontSize)
        {
            Button button = UiFactory.CreateButton(parent, text, onClick, fontSize);
            var outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = UiFactory.TextColor;
            outline.effectDistance = new Vector2(4f, -4f);
            outline.useGraphicAlpha = false;
            outline.enabled = false;
            button.gameObject.AddComponent<QuestMenuHoverBorder>().Initialize(outline);
        }

        void PositionInFrontOf(Transform head)
        {
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            menuRoot.transform.SetPositionAndRotation(
                head.position + forward * ReadingDistance - Vector3.up * 0.08f,
                Quaternion.LookRotation(forward, Vector3.up));
        }

        void ShowDirectory()
        {
            RectTransform content = BeginScrollablePage("Directory", TextAlignmentOptions.Center);
            ExhibitDirectoryUi.PopulateContent(content);
        }

        void ShowAbout()
        {
            RectTransform content = BeginScrollablePage("About Black Homeplaces", TextAlignmentOptions.TopLeft);
            CreateWrappedBody(content, AboutBlackHomeplacesUi.BodyCopy, 22f);
        }

        void ShowCredits()
        {
            RectTransform content = BeginScrollablePage("Credits", TextAlignmentOptions.TopLeft);
            CreateWrappedBody(content,
                $"{Application.productName}\n{Application.companyName}\nVersion {Application.version}\n\n" +
                MainMenuController.CreditsAttributionText,
                20f);
        }

        RectTransform BeginScrollablePage(string title, TextAlignmentOptions alignment)
        {
            if (pageRoot == null)
                return null;

            for (int i = pageRoot.childCount - 1; i >= 0; i--)
            {
                pageRoot.GetChild(i).gameObject.SetActive(false);
                DestroyMenuObject(pageRoot.GetChild(i).gameObject);
            }

            TextMeshProUGUI titleLabel = UiFactory.CreateLabel(pageRoot, title, 30f);
            titleLabel.fontStyle = FontStyles.Bold;

            RectTransform viewport = UiFactory.CreateRect(pageRoot, "Viewport");
            var viewportLayout = viewport.gameObject.AddComponent<LayoutElement>();
            viewportLayout.minHeight = 1f;
            viewportLayout.flexibleHeight = 1f;
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.26f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            RectTransform content = UiFactory.CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(24, 24, 20, 20);
            contentLayout.spacing = 10f;
            contentLayout.childAlignment = alignment == TextAlignmentOptions.Center
                ? TextAnchor.UpperCenter
                : TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 10f;
            return content;
        }

        static void CreateWrappedBody(Transform parent, string text, float fontSize)
        {
            TextMeshProUGUI body = UiFactory.CreateLabel(
                parent, text, fontSize, TextAlignmentOptions.TopLeft);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.overflowMode = TextOverflowModes.Overflow;
            LayoutElement layout = body.GetComponent<LayoutElement>();
            layout.minHeight = -1f;
            layout.preferredHeight = -1f;
        }

        void ReturnToMainEntrance()
        {
            Close();
            ResetService.ReturnToMainEntrance();
        }

        void QuitApplication()
        {
            Close();
            PauseMenuController.QuitApplication();
        }

        static Camera ResolveHeadCamera()
        {
            ScenePlayerRig activeRig = ScenePlayerRigRegistry.Active;
            if (activeRig != null && activeRig.Kind == ScenePlayerRig.RigKind.XR)
            {
                foreach (Camera camera in activeRig.GetComponentsInChildren<Camera>(true))
                    if (camera != null && camera.isActiveAndEnabled && camera.CompareTag("MainCamera"))
                        return camera;
            }

            Camera main = Camera.main;
            return main != null && main.isActiveAndEnabled ? main : null;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void DestroyMenuObject(UnityEngine.Object value)
        {
            if (value == null)
                return;
            if (Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }
    }

    sealed class QuestMenuHoverBorder : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        Outline outline;

        public void Initialize(Outline targetOutline) => outline = targetOutline;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (outline != null)
                outline.enabled = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (outline != null)
                outline.enabled = false;
        }
    }
}
