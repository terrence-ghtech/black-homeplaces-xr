using TMPro;
using UnityEngine;

namespace BCaT.Production.Shell
{
    /// <summary>
    /// The desktop screen-space interaction prompt (the router's
    /// InteractionPromptProvider surface). One discreet bottom-center label,
    /// created on demand, styled by the accessibility settings. On Quest this
    /// class does nothing: world-space prompt objects owned by each target are
    /// used instead, so Quest never sees desktop keyboard language and desktop
    /// never sees floating world text at the wrong scale.
    /// </summary>
    public static class InteractionPromptUi
    {
        static Canvas canvas;
        static TextMeshProUGUI label;
        static CanvasGroup group;

        public static void Show(string text)
        {
            if (PlatformCapabilities.IsXRActive || PlatformCapabilities.IsQuestConfiguration)
                return;
            if (string.IsNullOrEmpty(text)) { Hide(); return; }

            EnsureCreated();
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
                typeof(Canvas), typeof(CanvasGroup));
            Object.DontDestroyOnLoad(root);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 29000; // beneath menus (30000+), above scene UI

            group = root.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;

            var textGo = new GameObject("PromptText", typeof(RectTransform));
            textGo.transform.SetParent(root.transform, false);
            var rect = textGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 90f);
            rect.sizeDelta = new Vector2(900f, 60f);

            label = textGo.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            ApplyAccessibilityStyle();

            Settings.SettingsManager.SettingsApplied += ApplyAccessibilityStyle;
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
