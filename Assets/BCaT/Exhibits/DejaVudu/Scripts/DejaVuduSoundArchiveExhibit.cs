using System.Collections.Generic;
using BCaT.Production.Addressing;
using BCaT.Production.Interaction;
using BCaT.Production.Media;
using BCaT.Production.Settings;
using BCaT.Production.Shell;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace BCaT.Exhibits.DejaVudu
{
    public sealed class DejaVuduSoundArchiveExhibit : MonoBehaviour, IInteractionTarget
    {
        const string ExhibitTitle = "DEJA VUDU SOUND ARCHIVE";
        const string SampleMarker = "SAMPLE LIST:";
        const string OwnerName = "DejaVuduSoundArchive";
        const string AudioAddress = "bcat/dejavudu/audio";
        const string CoverAddress = "bcat/dejavudu/cover";

        [SerializeField] AudioClip collageClip;
        [SerializeField] Texture2D coverArt;
        [SerializeField] TextAsset contentText;
        [SerializeField] AudioSource audioSource;
        [SerializeField] Transform focusPoint;
        [SerializeField] Collider interactionCollider;
        [SerializeField] float interactionDistance = 4.2f;
        [SerializeField] float maxViewAngle = 28f;
        [Range(0f, 1f)]
        [SerializeField] float baseVolume = 0.85f;

        readonly List<Behaviour> disabledWorldInput = new();
        Collider[] ownColliders;
        GameObject viewerRoot;
        GameObject[] pages;
        Image[] pageButtonBackgrounds;
        ScrollRect activeScroll;
        TMP_Text audioStatusText;
        TMP_Text audioButtonText;
        RectTransform coverPageRect;
        TMP_Text coverPlaceholderText;
        Button firstButton;
        Sprite coverSprite;
        AsyncOperationHandle<AudioClip> audioHandle;
        AsyncOperationHandle<Texture2D> coverHandle;
        bool playbackRegistered;
        bool inputCaptured;
        bool previousCursorVisible;
        bool closeKeyReleasedSinceOpen;
        bool mediaLoadStarted;
        bool audioHandleOwned;
        bool coverHandleOwned;
        bool audioLoadFailed;
        bool coverLoadFailed;
        bool playWhenAudioReady;
        int openedFrame = -1;
        int currentPage;
        CursorLockMode previousCursorLockState;

        public Vector3 FocusPoint => focusPoint != null ? focusPoint.position : transform.position;
        public float MaxDistance => interactionDistance;
        public float MaxViewAngle => maxViewAngle;
        public bool RequireLineOfSight => true;
        public int Priority => 4;
        public bool IsAvailable => isActiveAndEnabled && viewerRoot == null;
        public bool AllowDesktopClick => true;
        public bool Exists => this != null;

        public Collider[] OwnColliders
        {
            get
            {
                if (ownColliders == null)
                    ownColliders = GetComponentsInChildren<Collider>(true);
                return ownColliders;
            }
        }

        bool IsPlaying => audioSource != null && collageClip != null &&
                          audioSource.isPlaying && audioSource.clip == collageClip;

        public void Configure(TextAsset text, AudioSource source,
            Collider collider, Transform focus)
        {
            contentText = text;
            audioSource = source;
            interactionCollider = collider;
            focusPoint = focus;
            ownColliders = null;
            ConfigureAudioSource();
        }

        void Awake() => ConfigureAudioSource();

        void Start() => BeginAddressableMediaLoad();

        void OnEnable() => InteractionRouter.Register(this);

        void OnDisable()
        {
            InteractionRouter.Unregister(this);
            CloseViewer();
            StopPlayback("exhibit disabled");
        }

        void OnDestroy()
        {
            CloseViewer();
            StopPlayback("exhibit destroyed");
            MediaPlaybackRegistry.NotifyStopped(this);
            if (audioSource != null)
                AudioChannelService.Unregister(audioSource);
            ClearCoverSprite();
            ReleaseAddressableMedia();
        }

        void Update()
        {
            if (playbackRegistered && !IsPlaying)
                StopPlayback("collage finished");

            if (viewerRoot == null)
                return;

            if (Time.frameCount > openedFrame && !FocusedUiInput.InteractHeld)
                closeKeyReleasedSinceOpen = true;

            if (Time.frameCount <= openedFrame)
                return;

            if (FocusedUiInput.CancelPressed ||
                (closeKeyReleasedSinceOpen && FocusedUiInput.InteractPressed))
            {
                CloseViewer();
                return;
            }

            if (FocusedUiInput.NextPressed)
                SelectPage(currentPage + 1);
            else if (FocusedUiInput.PreviousPressed)
                SelectPage(currentPage - 1);

            float scrollStep = FocusedUiInput.ScrollStep();
            if (scrollStep != 0f && activeScroll != null)
                activeScroll.verticalNormalizedPosition =
                    Mathf.Clamp01(activeScroll.verticalNormalizedPosition + scrollStep * Time.unscaledDeltaTime);
        }

        public string GetPrompt(bool xr)
        {
            return SharedInteractionPrompt.Format(xr,
                IsPlaying ? SharedInteractionVerb.Stop : SharedInteractionVerb.Play,
                "Deja Vudu Sound Archive");
        }

        public void OnFocusChanged(bool focused) { }

        public void OnInteract(InteractionActivation activation)
        {
            if (IsPlaying)
            {
                StopPlayback($"toggled off via {activation}");
                return;
            }

            StartPlayback(activation.ToString());
            OpenViewer();
        }

        public void OnXRSelect()
        {
            if (InteractionRouter.Instance != null)
                InteractionRouter.Instance.RequestXRSelect(this);
            else
                OnInteract(InteractionActivation.XRSelect);
        }

        void ConfigureAudioSource()
        {
            if (audioSource == null)
                return;

            audioSource.clip = collageClip;
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.volume = baseVolume;
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 2.2f;
            audioSource.maxDistance = 14f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.dopplerLevel = 0f;
            AudioChannelService.Register(audioSource, AudioChannel.Media);
        }

        void StartPlayback(string via)
        {
            if (audioSource == null)
            {
                Debug.LogWarning("[DejaVuduSoundArchive] Playback requested with no AudioSource.");
                return;
            }

            if (collageClip == null)
            {
                if (audioLoadFailed)
                    Debug.LogWarning("[DejaVuduSoundArchive] Playback requested but Addressables audio load failed.");
                else
                {
                    BeginAddressableMediaLoad();
                    playWhenAudioReady = true;
                    Debug.Log("[DejaVuduSoundArchive] Playback queued while Addressables audio loads.");
                }

                RefreshAudioUi();
                return;
            }

            MediaPlaybackRegistry.StopAll();
            audioSource.clip = collageClip;
            audioSource.time = 0f;
            audioSource.volume = AudioChannelService.ScaledVolume(audioSource, baseVolume);
            audioSource.Play();
            playbackRegistered = true;
            MediaPlaybackRegistry.NotifyStarted(this, StopForMediaRegistry);
            RefreshAudioUi();
            Debug.Log($"[DejaVuduSoundArchive] Started collage via {via}.");
        }

        void StopPlayback(string reason)
        {
            bool wasRegistered = playbackRegistered;
            if (audioSource != null && audioSource.clip == collageClip)
                audioSource.Stop();

            playbackRegistered = false;
            MediaPlaybackRegistry.NotifyStopped(this);
            RefreshAudioUi();

            if (wasRegistered)
                Debug.Log($"[DejaVuduSoundArchive] Stopped collage ({reason}).");
        }

        void StopForMediaRegistry() => StopPlayback("media registry stop-all");

        void OpenViewer()
        {
            if (viewerRoot != null)
                return;

            openedFrame = Time.frameCount;
            closeKeyReleasedSinceOpen = !FocusedUiInput.InteractHeld;
            viewerRoot = BuildViewer();
            PositionViewerInFrontOfCamera();
            SelectPage(0);
            CaptureInput();
            InteractionState.Block(this, InteractionBlockReason.Modal, CloseViewer);
        }

        void CloseViewer()
        {
            if (viewerRoot == null)
                return;

            InteractionState.SuppressInputForCurrentFrame();
            InteractionState.Unblock(this);
            RestoreInput();
            ClearCoverSprite();
            Destroy(viewerRoot);
            viewerRoot = null;
            pages = null;
            pageButtonBackgrounds = null;
            activeScroll = null;
            coverPageRect = null;
            coverPlaceholderText = null;
            firstButton = null;
            audioStatusText = null;
            audioButtonText = null;
        }

        GameObject BuildViewer()
        {
            UiFactory.EnsureEventSystem();

            GameObject root = new GameObject("DejaVuduSoundArchiveViewer", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
            root.transform.SetParent(transform, false);
            SetLayerRecursive(root, LayerMask.NameToLayer("UI"));

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 32000;

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(1200f, 820f);
            rootRect.localScale = Vector3.one * 0.00155f;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1200f, 820f);

            RectTransform panel = Rect(rootRect, "Panel");
            Stretch(panel);
            panel.gameObject.AddComponent<Image>().color = UiFactory.PanelColor;

            TMP_Text title = Text(panel, "Title", ExhibitTitle, 34f, TextAlignmentOptions.Left);
            SetOffsets(title.rectTransform, new Vector2(36f, -82f), new Vector2(-36f, -24f), true);

            audioStatusText = Text(panel, "AudioStatus", "", 19f, TextAlignmentOptions.Left);
            SetOffsets(audioStatusText.rectTransform, new Vector2(36f, -116f), new Vector2(-36f, -84f), true);

            RectTransform nav = Rect(panel, "PageNavigation");
            SetOffsets(nav, new Vector2(36f, -178f), new Vector2(-36f, -126f), true);
            HorizontalLayoutGroup navLayout = nav.gameObject.AddComponent<HorizontalLayoutGroup>();
            navLayout.spacing = 10f;
            navLayout.childControlWidth = true;
            navLayout.childControlHeight = true;
            navLayout.childForceExpandWidth = true;
            navLayout.childForceExpandHeight = true;

            pageButtonBackgrounds = new Image[3];
            firstButton = Button(nav, "Description", () => SelectPage(0), 20f, out pageButtonBackgrounds[0]);
            Button(nav, "Sample List", () => SelectPage(1), 20f, out pageButtonBackgrounds[1]);
            Button(nav, "Cover Art", () => SelectPage(2), 20f, out pageButtonBackgrounds[2]);

            RectTransform content = Rect(panel, "ContentArea");
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(36f, 104f);
            content.offsetMax = new Vector2(-36f, -194f);

            SplitContent(contentText != null ? contentText.text : "", out string description, out string samples);
            pages = new[]
            {
                TextPage(content, "DescriptionPage", description),
                TextPage(content, "SampleListPage", samples),
                CoverPage(content),
            };

            RectTransform footer = Rect(panel, "Footer");
            footer.anchorMin = new Vector2(0f, 0f);
            footer.anchorMax = new Vector2(1f, 0f);
            footer.offsetMin = new Vector2(36f, 30f);
            footer.offsetMax = new Vector2(-36f, 86f);
            HorizontalLayoutGroup footerLayout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 14f;
            footerLayout.childControlWidth = true;
            footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = true;
            footerLayout.childForceExpandHeight = true;

            Button audioButton = Button(footer, "Stop Audio", TogglePlaybackFromViewer, 20f, out _);
            audioButtonText = audioButton.GetComponentInChildren<TMP_Text>(true);
            Button(footer, "Close", CloseViewer, 20f, out _);
            RefreshAudioUi();
            UiFactory.SelectForKeyboard(firstButton);
            return root;
        }

        GameObject TextPage(RectTransform parent, string name, string bodyText)
        {
            RectTransform viewport = Rect(parent, name);
            Stretch(viewport);
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.28f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            RectTransform content = Rect(viewport, "ScrollContent");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            TMP_Text body = content.gameObject.AddComponent<TextMeshProUGUI>();
            body.text = bodyText;
            body.fontSize = 24f * UiFactory.TextScale;
            body.color = UiFactory.TextColor;
            body.alignment = TextAlignmentOptions.TopLeft;
            body.margin = new Vector4(22f, 18f, 22f, 18f);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.raycastTarget = false;

            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 42f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return viewport.gameObject;
        }

        GameObject CoverPage(RectTransform parent)
        {
            RectTransform page = Rect(parent, "CoverArtPage");
            Stretch(page);
            coverPageRect = page;
            page.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.22f);
            PopulateCoverPage();
            return page.gameObject;
        }

        void PopulateCoverPage()
        {
            if (coverPageRect == null)
                return;

            ClearCoverSprite();
            foreach (Transform child in coverPageRect)
                Destroy(child.gameObject);

            if (coverArt == null)
            {
                string message = coverLoadFailed ? "Cover art unavailable." : "Cover art loading.";
                coverPlaceholderText = Text(coverPageRect, "CoverStatusText", message, 26f,
                    TextAlignmentOptions.Center);
                Stretch(coverPlaceholderText.rectTransform);
                return;
            }

            coverSprite = Sprite.Create(coverArt, new Rect(0f, 0f, coverArt.width, coverArt.height),
                new Vector2(0.5f, 0.5f), 100f);
            RectTransform imageRect = Rect(coverPageRect, "CoverImage");
            imageRect.anchorMin = new Vector2(0.18f, 0.05f);
            imageRect.anchorMax = new Vector2(0.82f, 0.95f);
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            Image image = imageRect.gameObject.AddComponent<Image>();
            image.sprite = coverSprite;
            image.preserveAspect = true;
        }

        void SelectPage(int pageIndex)
        {
            if (pages == null || pages.Length == 0)
                return;

            currentPage = Mathf.Clamp(pageIndex, 0, pages.Length - 1);
            activeScroll = null;

            for (int i = 0; i < pages.Length; i++)
            {
                bool selected = i == currentPage;
                if (pages[i] != null)
                {
                    pages[i].SetActive(selected);
                    if (selected)
                    {
                        activeScroll = pages[i].GetComponent<ScrollRect>();
                        if (activeScroll != null)
                            activeScroll.verticalNormalizedPosition = 1f;
                    }
                }

                if (pageButtonBackgrounds != null && i < pageButtonBackgrounds.Length &&
                    pageButtonBackgrounds[i] != null)
                {
                    pageButtonBackgrounds[i].color = selected
                        ? UiFactory.ButtonFocusColor
                        : UiFactory.ButtonColor;
                }
            }
        }

        void TogglePlaybackFromViewer()
        {
            if (IsPlaying)
                StopPlayback("viewer toggle");
            else
                StartPlayback("viewer toggle");
        }

        void RefreshAudioUi()
        {
            if (audioStatusText != null)
            {
                if (IsPlaying)
                    audioStatusText.text = "Audio playing from the radio.";
                else if (collageClip == null && audioLoadFailed)
                    audioStatusText.text = "Audio unavailable.";
                else if (collageClip == null)
                    audioStatusText.text = playWhenAudioReady
                        ? "Audio loading, playback will start shortly."
                        : "Audio loading.";
                else
                    audioStatusText.text = "Audio stopped.";
            }

            if (audioButtonText != null)
                audioButtonText.text = IsPlaying ? "Stop Audio" : "Play Audio";
        }

        void BeginAddressableMediaLoad()
        {
            if (mediaLoadStarted)
                return;

            mediaLoadStarted = true;

            audioHandle = Addressables.LoadAssetAsync<AudioClip>(AudioAddress);
            audioHandleOwned = true;
            AddressablesHandleRegistry.NotifyCreated(OwnerName, AudioAddress, audioHandle);
            audioHandle.Completed += OnAudioLoaded;

            coverHandle = Addressables.LoadAssetAsync<Texture2D>(CoverAddress);
            coverHandleOwned = true;
            AddressablesHandleRegistry.NotifyCreated(OwnerName, CoverAddress, coverHandle);
            coverHandle.Completed += OnCoverLoaded;
        }

        void OnAudioLoaded(AsyncOperationHandle<AudioClip> handle)
        {
            AddressablesHandleRegistry.NotifyCompleted(OwnerName, AudioAddress, handle.Status);
            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                collageClip = handle.Result;
                ConfigureAudioSource();
                RefreshAudioUi();

                if (playWhenAudioReady)
                {
                    playWhenAudioReady = false;
                    StartPlayback("addressables ready");
                }

                return;
            }

            audioLoadFailed = true;
            playWhenAudioReady = false;
            RefreshAudioUi();
            Debug.LogError($"[DejaVuduSoundArchive] Failed to load Addressables audio '{AudioAddress}'.");
        }

        void OnCoverLoaded(AsyncOperationHandle<Texture2D> handle)
        {
            AddressablesHandleRegistry.NotifyCompleted(OwnerName, CoverAddress, handle.Status);
            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                coverArt = handle.Result;
                PopulateCoverPage();
                return;
            }

            coverLoadFailed = true;
            if (coverPlaceholderText != null)
                coverPlaceholderText.text = "Cover art unavailable.";
            Debug.LogError($"[DejaVuduSoundArchive] Failed to load Addressables cover art '{CoverAddress}'.");
        }

        void ReleaseAddressableMedia()
        {
            if (audioHandleOwned)
            {
                if (audioHandle.IsValid())
                {
                    audioHandle.Completed -= OnAudioLoaded;
                    Addressables.Release(audioHandle);
                }

                AddressablesHandleRegistry.NotifyReleased(OwnerName, AudioAddress);
                audioHandleOwned = false;
            }

            if (coverHandleOwned)
            {
                if (coverHandle.IsValid())
                {
                    coverHandle.Completed -= OnCoverLoaded;
                    Addressables.Release(coverHandle);
                }

                AddressablesHandleRegistry.NotifyReleased(OwnerName, CoverAddress);
                coverHandleOwned = false;
            }

            collageClip = null;
            coverArt = null;
        }

        void PositionViewerInFrontOfCamera()
        {
            Camera cam = ActiveCamera();
            if (viewerRoot == null || cam == null)
                return;

            Canvas canvas = viewerRoot.GetComponent<Canvas>();
            if (canvas != null)
                canvas.worldCamera = cam;

            viewerRoot.transform.position = cam.transform.position + cam.transform.forward * 1.85f;
            Vector3 away = (viewerRoot.transform.position - cam.transform.position).normalized;
            viewerRoot.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            EnsureCameraRendersUiLayer(cam);
        }

        void CaptureInput()
        {
            if (inputCaptured)
                return;

            inputCaptured = true;
            previousCursorLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            DisableWorldInput();
        }

        void RestoreInput()
        {
            if (!inputCaptured)
                return;

            RestoreWorldInput();
            Cursor.lockState = previousCursorLockState;
            Cursor.visible = previousCursorVisible;
            inputCaptured = false;
        }

        void DisableWorldInput()
        {
            disabledWorldInput.Clear();
            foreach (Behaviour behaviour in FindObjectsByType<Behaviour>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (behaviour == null || !behaviour.enabled || behaviour == this ||
                    behaviour.transform.IsChildOf(transform))
                    continue;

                if (!ShouldDisableWhileOpen(behaviour))
                    continue;

                behaviour.enabled = false;
                disabledWorldInput.Add(behaviour);
            }
        }

        static bool ShouldDisableWhileOpen(Behaviour behaviour)
        {
            string typeName = behaviour.GetType().Name;
            string fullName = behaviour.GetType().FullName ?? typeName;

            return typeName == "FirstPersonController"
                || typeName == "StarterAssetsInputs"
                || typeName == "SimpleImagePopupInteractor"
                || typeName == "LindaLeaksPanelOpener"
                || typeName == "MediaVideoController"
                || typeName == "MeshellArticleNotebookInputRouter"
                || typeName == "MeshellArticleNotebookOpener"
                || typeName == "InteractableLinkLauncher"
                || typeName == "SpatialAudioToggle"
                || typeName == "QuiltVideoPopUp"
                || typeName == "LindaLeaksVideoPopUp"
                || fullName.Contains("ContinuousMoveProvider")
                || fullName.Contains("ContinuousTurnProvider")
                || fullName.Contains("SnapTurnProvider")
                || fullName.Contains("TeleportationProvider")
                || fullName.Contains("XRSimpleInteractable");
        }

        void RestoreWorldInput()
        {
            foreach (Behaviour behaviour in disabledWorldInput)
                if (behaviour != null)
                    behaviour.enabled = true;
            disabledWorldInput.Clear();
        }

        static void SplitContent(string fullText, out string description, out string samples)
        {
            if (string.IsNullOrWhiteSpace(fullText))
            {
                description = "Description unavailable.";
                samples = "Sample list unavailable.";
                return;
            }

            int marker = fullText.IndexOf(SampleMarker, System.StringComparison.OrdinalIgnoreCase);
            if (marker < 0)
            {
                description = fullText.Trim();
                samples = "Sample list unavailable.";
                return;
            }

            description = fullText.Substring(0, marker).Trim();
            samples = fullText.Substring(marker).Trim();
        }

        static RectTransform Rect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        static TMP_Text Text(Transform parent, string name, string value, float size,
            TextAlignmentOptions alignment)
        {
            RectTransform rect = Rect(parent, name);
            TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size * UiFactory.TextScale;
            text.color = UiFactory.TextColor;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        static Button Button(Transform parent, string label, UnityEngine.Events.UnityAction action,
            float fontSize, out Image background)
        {
            RectTransform rect = Rect(parent, "Button_" + label.Replace(" ", ""));
            background = rect.gameObject.AddComponent<Image>();
            background.color = UiFactory.ButtonColor;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(action);
            ColorBlock colors = button.colors;
            colors.normalColor = UiFactory.ButtonColor;
            colors.highlightedColor = UiFactory.ButtonFocusColor;
            colors.selectedColor = UiFactory.ButtonFocusColor;
            colors.pressedColor = Color.Lerp(UiFactory.ButtonFocusColor, Color.black, 0.3f);
            button.colors = colors;

            TMP_Text text = Text(rect, "Label", label, fontSize, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return button;
        }

        static void SetOffsets(RectTransform rect, Vector2 min, Vector2 max, bool topAnchored)
        {
            rect.anchorMin = topAnchored ? new Vector2(0f, 1f) : Vector2.zero;
            rect.anchorMax = topAnchored ? new Vector2(1f, 1f) : Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static Camera ActiveCamera()
        {
            if (Camera.main != null && Camera.main.isActiveAndEnabled)
                return Camera.main;

            foreach (Camera camera in FindObjectsByType<Camera>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (camera != null && camera.isActiveAndEnabled)
                    return camera;
            return null;
        }

        static void EnsureCameraRendersUiLayer(Camera camera)
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            if (camera == null || uiLayer < 0)
                return;

            int uiMask = 1 << uiLayer;
            if ((camera.cullingMask & uiMask) == 0)
                camera.cullingMask |= uiMask;
        }

        static void SetLayerRecursive(GameObject root, int layer)
        {
            if (root == null || layer < 0)
                return;

            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        void ClearCoverSprite()
        {
            if (coverSprite != null)
            {
                Destroy(coverSprite);
                coverSprite = null;
            }
        }
    }
}
