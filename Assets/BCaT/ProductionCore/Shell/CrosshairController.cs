using BCaT.Production.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace BCaT.Production.Shell
{
    /// <summary>
    /// Desktop focus indicator: a deliberately small, unobtrusive center dot
    /// that communicates the interaction state —
    ///   no target: faint dot · valid target: bright dot with ring ·
    ///   blocked/menu/loading/focused exhibit: hidden.
    /// Sizing and contrast follow the accessibility settings. Not created on
    /// Quest (XR uses ray interactors instead).
    /// </summary>
    public sealed class CrosshairController : MonoBehaviour
    {
        Canvas canvas;
        Image dot;
        Image ring;

        void Start()
        {
            if (PlatformCapabilities.IsQuestConfiguration || PlatformCapabilities.IsXRActive)
            {
                enabled = false;
                return;
            }
            Build();
            Settings.SettingsManager.SettingsApplied += ApplyStyle;
        }

        void OnDestroy()
        {
            Settings.SettingsManager.SettingsApplied -= ApplyStyle;
        }

        void Build()
        {
            var go = new GameObject("BCaT_Crosshair", typeof(Canvas));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 28000;

            ring = CreateImage(go.transform, "Ring", 26f);
            dot = CreateImage(go.transform, "Dot", 6f);
            ApplyStyle();
        }

        Image CreateImage(Transform parent, string name, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.sprite = null; // plain square scaled small reads as a dot
            return image;
        }

        void ApplyStyle()
        {
            if (dot == null) return;
            float scale = Settings.SettingsManager.Current.accessibility.TextScaleFactor;
            ((RectTransform)dot.transform).sizeDelta = new Vector2(6f * scale, 6f * scale);
            ((RectTransform)ring.transform).sizeDelta = new Vector2(26f * scale, 26f * scale);
        }

        void Update()
        {
            if (canvas == null) return;

            bool hidden = InteractionState.IsBlocked || PlayerControlGate.IsSuspended;
            canvas.enabled = !hidden;
            if (hidden) return;

            bool highContrast = Settings.SettingsManager.Current.accessibility.highContrastUi;
            bool hasTarget = InteractionRouter.Instance != null &&
                             InteractionRouter.Instance.CurrentTarget != null;

            if (hasTarget)
            {
                dot.color = highContrast ? Color.yellow : new Color(1f, 1f, 1f, 0.95f);
                ring.color = highContrast
                    ? new Color(1f, 0.92f, 0.2f, 0.5f)
                    : new Color(1f, 1f, 1f, 0.25f);
                ring.enabled = true;
            }
            else
            {
                dot.color = highContrast
                    ? new Color(1f, 1f, 1f, 0.8f)
                    : new Color(1f, 1f, 1f, 0.35f);
                ring.enabled = false;
            }
        }
    }
}
