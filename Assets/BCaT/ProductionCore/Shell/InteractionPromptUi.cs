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

        public static void Show(string text)
        {
            if (string.IsNullOrEmpty(text)) { Hide(); return; }

            EnsureCreated();
            ConfigureForCurrentPlatform();
            label.text = text;
            group.alpha = 1f;
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
                typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            Object.DontDestroyOnLoad(root);
            rootRect = root.GetComponent<RectTransform>();
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 29000; // beneath menus (30000+), above scene UI

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
