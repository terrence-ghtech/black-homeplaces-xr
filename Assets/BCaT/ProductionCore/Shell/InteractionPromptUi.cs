using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BCaT.Production.Shell
{
    /// <summary>
    /// Shared interaction prompt surface. Desktop uses screen-space overlay; Quest
    /// uses a camera-attached world-space HUD so the same bottom prompt is visible
    /// in headset without attaching text to exhibit objects.
    /// </summary>
    public static class InteractionPromptUi
    {
        static Canvas canvas;
        static TextMeshProUGUI label;
        static CanvasGroup group;
        static RectTransform rootRect;
        static RectTransform panelRect;
        static Image panelImage;
        static Camera boundXrCamera;
        static bool sizingLogged;

        public static void Show(string text)
        {
            if (string.IsNullOrEmpty(text)) { Hide(); return; }

            EnsureCreated();
            ConfigureForCurrentPlatform();
            label.text = text;
            group.alpha = 1f;
            LogSizingOnce();
        }

        /// <summary>
        /// One line, the first time the prompt is shown, recording what the prompt
        /// actually measures at runtime. The Game view and the standalone player
        /// render at different resolutions, so this is the only way to confirm the
        /// two now match without eyeballing screenshots. Read it from the player
        /// log after a standalone run.
        /// </summary>
        static void LogSizingOnce()
        {
            if (sizingLogged || canvas == null || label == null)
                return;

            sizingLogged = true;
            Debug.Log($"[InteractionPromptUi] sizing: screen={Screen.width}x{Screen.height} " +
                      $"dpi={Screen.dpi} renderMode={canvas.renderMode} " +
                      $"canvas.scaleFactor={canvas.scaleFactor:0.###} " +
                      $"panel={panelRect.rect.size} " +
                      $"panelScreenPx={panelRect.rect.size * canvas.scaleFactor} " +
                      $"fontSize={label.fontSize:0.##} " +
                      $"fontScreenPx={label.fontSize * canvas.scaleFactor:0.##} " +
                      $"textScaleSetting={Settings.SettingsManager.Current.accessibility.TextScaleFactor:0.##}.");
        }

        public static void Hide()
        {
            if (group != null)
                group.alpha = 0f;
        }

        static void EnsureCreated()
        {
            if (canvas != null) return;

            var root = new GameObject("BCaT_InteractionPrompt",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            Object.DontDestroyOnLoad(root);
            rootRect = root.GetComponent<RectTransform>();
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 29000; // beneath menus (30000+), above scene UI

            // Resolution independence. Without a CanvasScaler a Canvas behaves as
            // ConstantPixelSize with scaleFactor 1, so the panel's 900x62 and the
            // label's 30pt are literal SCREEN PIXELS. That is why this prompt
            // looked right in the Game view and far too small in the standalone
            // player: identical pixel size, very different screen size. A ~1280
            // wide Game view gives the panel ~70% of the width; a Retina desktop
            // window at ~2880 gives it ~31%, and the text shrinks to match.
            // These are the same values UiFactory.Canvas uses, so the prompt now
            // scales like every other shared surface.
            //
            // Quest is deliberately unaffected: Unity's CanvasScaler short-circuits
            // to HandleWorldCanvas for a WorldSpace canvas, which applies
            // dynamicPixelsPerUnit (1) and ignores uiScaleMode entirely. The XR
            // branch below therefore keeps the exact scaleFactor it had before.
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            group = root.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;

            var panelGo = new GameObject("PromptPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelGo.transform.SetParent(root.transform, false);
            panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 78f);
            panelRect.sizeDelta = new Vector2(900f, 62f);
            panelImage = panelGo.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.42f);
            panelImage.raycastTarget = false;

            var textGo = new GameObject("PromptText", typeof(RectTransform));
            textGo.transform.SetParent(panelGo.transform, false);
            var rect = textGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(22f, 0f);
            rect.offsetMax = new Vector2(-22f, 0f);

            label = textGo.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            ApplyAccessibilityStyle();

            Settings.SettingsManager.SettingsApplied += ApplyAccessibilityStyle;
            SceneManager.sceneLoaded += (_, _) => Hide();
        }

        static void ConfigureForCurrentPlatform()
        {
            bool xr = PlatformCapabilities.IsXRActive || PlatformCapabilities.IsQuestConfiguration;
            if (!xr)
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.worldCamera = null;
                    rootRect.SetParent(null, false);
                    Object.DontDestroyOnLoad(rootRect.gameObject);
                    rootRect.localScale = Vector3.one;
                    rootRect.localPosition = Vector3.zero;
                    rootRect.localRotation = Quaternion.identity;
                }

                panelRect.anchorMin = new Vector2(0.5f, 0f);
                panelRect.anchorMax = new Vector2(0.5f, 0f);
                panelRect.pivot = new Vector2(0.5f, 0f);
                panelRect.anchoredPosition = new Vector2(0f, 78f);
                panelRect.sizeDelta = new Vector2(900f, 62f);
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
                return;

            if (canvas.renderMode != RenderMode.WorldSpace || boundXrCamera != cam)
            {
                boundXrCamera = cam;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = cam;
                rootRect.SetParent(cam.transform, false);
                rootRect.sizeDelta = new Vector2(1.2f, 0.18f);
            }

            rootRect.localPosition = new Vector3(0f, -0.34f, 1.15f);
            rootRect.localRotation = Quaternion.identity;
            rootRect.localScale = Vector3.one * 0.0012f;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(900f, 62f);
        }

        static void ApplyAccessibilityStyle()
        {
            if (label == null) return;
            var a = Settings.SettingsManager.Current.accessibility;
            label.fontSize = 30f * a.TextScaleFactor;
            if (a.highContrastUi)
            {
                label.color = Color.white;
                label.outlineWidth = 0.3f;
                label.outlineColor = Color.black;
            }
            else
            {
                label.color = new Color(1f, 1f, 1f, 0.92f);
                label.outlineWidth = 0.15f;
                label.outlineColor = new Color(0f, 0f, 0f, 0.75f);
            }
        }
    }
}
