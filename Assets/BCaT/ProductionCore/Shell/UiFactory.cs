using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BCaT.Production.Shell
{
    /// <summary>
    /// Runtime uGUI builders for the desktop application shell (menus, settings,
    /// directory, dialogs). The project builds its modal UI in code by
    /// convention (see the Black Kitchen exit modal); this factory centralizes
    /// that pattern with a consistent theme, keyboard focus states, and
    /// accessibility-aware sizing/contrast read from the settings at build time.
    /// </summary>
    public static class UiFactory
    {
        // ---- Theme -------------------------------------------------------

        public static float TextScale =>
            Settings.SettingsManager.Current.accessibility.TextScaleFactor;

        public static bool HighContrast =>
            Settings.SettingsManager.Current.accessibility.highContrastUi;

        public static Color PanelColor =>
            HighContrast ? new Color(0f, 0f, 0f, 0.97f) : new Color(0.07f, 0.06f, 0.05f, 0.92f);

        public static Color ButtonColor =>
            HighContrast ? new Color(0.05f, 0.05f, 0.05f, 1f) : new Color(0.18f, 0.15f, 0.12f, 0.95f);

        public static Color ButtonFocusColor =>
            HighContrast ? new Color(1f, 0.95f, 0.2f, 1f) : new Color(0.45f, 0.38f, 0.28f, 1f);

        public static Color TextColor =>
            HighContrast ? Color.white : new Color(0.95f, 0.93f, 0.88f, 1f);

        // ---- Canvas / structure -------------------------------------------

        public static Canvas CreateOverlayCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();
            return canvas;
        }

        /// <summary>
        /// Menus require an active EventSystem carrying the input module the
        /// active platform profile specifies.
        ///
        /// Deliberately never activates an inactive EventSystem it happens to
        /// find. An earlier revision did, and because the platform layer
        /// authors one EventSystem per platform, FindFirstObjectByType(Include)
        /// could resurrect the *other* platform's EventSystem and then stack a
        /// second input module on it — a platform leak whose trigger was scene
        /// serialization order. ScenePlatformBinding owns EventSystem
        /// activation; this method only fills in a missing module, or creates a
        /// correctly configured EventSystem when a scene genuinely has none.
        /// </summary>
        public static void EnsureEventSystem()
        {
            bool wantsXr = BCaTPlatform.UiInputModule == BCaTUiInputModuleKind.XRUI;

            var current = EventSystem.current;
            if (current == null || !current.isActiveAndEnabled)
            {
                // Only consider ACTIVE candidates: an inactive EventSystem
                // belongs to the platform layer, not to us.
                foreach (var candidate in UnityEngine.Object.FindObjectsByType<EventSystem>(
                             FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (candidate != null && candidate.isActiveAndEnabled)
                    {
                        current = candidate;
                        break;
                    }
                }
            }

            if (current == null)
            {
                var created = new GameObject("EventSystem", typeof(EventSystem));
                if (wantsXr)
                    created.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>();
                else
                    created.AddComponent<InputSystemUIInputModule>();
                Debug.Log($"[UiFactory] No active EventSystem in scene " +
                          $"'{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}'; " +
                          $"created one with the {BCaTPlatform.UiInputModule} module.");
                return;
            }

            bool hasModule = current.GetComponents<BaseInputModule>().Length > 0;
            if (hasModule)
                return;

            if (wantsXr)
                current.gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>();
            else
                current.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        public static RectTransform CreateFullScreenPanel(Transform parent, string name)
        {
            var rect = CreateRect(parent, name);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = PanelColor;
            return rect;
        }

        public static RectTransform CreateCenterPanel(Transform parent, string name, Vector2 size)
        {
            var rect = CreateRect(parent, name);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = PanelColor;
            return rect;
        }

        /// <summary>Vertical stack with padding for menu content.</summary>
        public static RectTransform CreateColumn(Transform parent, string name, float spacing = 14f)
        {
            var rect = CreateRect(parent, name);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(40, 32);
            rect.offsetMax = new Vector2(-40, -32);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rect;
        }

        public static RectTransform CreateRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            if (parent != null)
                rect.SetParent(parent, false);
            return rect;
        }

        // ---- Widgets -------------------------------------------------------

        public static TextMeshProUGUI CreateLabel(Transform parent, string text, float size,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var rect = CreateRect(parent, "Label_" + Sanitize(text));
            float height = size * TextScale * 1.6f;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(0f, height);

            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 1f;
            layout.preferredWidth = 560f;
            layout.minHeight = height;
            layout.preferredHeight = height;

            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size * TextScale;
            label.color = TextColor;
            label.alignment = alignment;
            label.enableAutoSizing = false;
            label.raycastTarget = false;
            return label;
        }

        public static Button CreateButton(Transform parent, string text, Action onClick,
            float fontSize = 26f)
        {
            var rect = CreateRect(parent, "Button_" + Sanitize(text));
            float height = 30f + fontSize * TextScale * 1.4f;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(0f, height);

            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 1f;
            layout.preferredWidth = 560f;
            layout.minHeight = height;
            layout.preferredHeight = height;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = ButtonColor;

            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = ButtonFocusColor;
            colors.selectedColor = ButtonFocusColor;
            colors.pressedColor = Color.Lerp(ButtonFocusColor, Color.black, 0.3f);
            colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.4f);
            button.colors = colors;
            if (onClick != null)
                button.onClick.AddListener(() => onClick());

            var label = CreateLabel(rect, text, fontSize);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.color = HighContrast ? Color.white : TextColor;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;

            return button;
        }

        public static Slider CreateSlider(Transform parent, string title, float min, float max,
            float value, Action<float> onChanged, bool wholeNumbers = false)
        {
            var row = CreateRow(parent, "Slider_" + Sanitize(title), out var labelArea, out var controlArea);
            CreateLabel(labelArea, title, 22f, TextAlignmentOptions.Left);

            var sliderRect = CreateRect(controlArea, "Slider");
            sliderRect.anchorMin = Vector2.zero;
            sliderRect.anchorMax = Vector2.one;
            sliderRect.offsetMin = new Vector2(0, 12);
            sliderRect.offsetMax = new Vector2(-90, -12);

            var bg = CreateRect(sliderRect, "Background");
            Stretch(bg);
            bg.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            var fillArea = CreateRect(sliderRect, "FillArea");
            Stretch(fillArea);
            var fill = CreateRect(fillArea, "Fill");
            Stretch(fill);
            fill.gameObject.AddComponent<Image>().color = ButtonFocusColor;

            var handleArea = CreateRect(sliderRect, "HandleArea");
            Stretch(handleArea);
            var handle = CreateRect(handleArea, "Handle");
            handle.sizeDelta = new Vector2(24, 0);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = HighContrast ? Color.white : new Color(0.9f, 0.85f, 0.75f, 1f);

            var slider = sliderRect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.value = value;

            var valueLabel = CreateLabel(controlArea, FormatValue(value, wholeNumbers), 20f);
            var valueRect = valueLabel.rectTransform;
            valueRect.anchorMin = new Vector2(1, 0);
            valueRect.anchorMax = new Vector2(1, 1);
            valueRect.pivot = new Vector2(1, 0.5f);
            valueRect.sizeDelta = new Vector2(84, 0);
            valueRect.anchoredPosition = Vector2.zero;

            slider.onValueChanged.AddListener(v =>
            {
                valueLabel.text = FormatValue(v, wholeNumbers);
                onChanged?.Invoke(v);
            });
            return slider;
        }

        public static Toggle CreateToggle(Transform parent, string title, bool value, Action<bool> onChanged)
        {
            var row = CreateRow(parent, "Toggle_" + Sanitize(title), out var labelArea, out var controlArea);
            CreateLabel(labelArea, title, 22f, TextAlignmentOptions.Left);

            var box = CreateRect(controlArea, "Box");
            box.anchorMin = new Vector2(0, 0.5f);
            box.anchorMax = new Vector2(0, 0.5f);
            box.pivot = new Vector2(0, 0.5f);
            box.sizeDelta = new Vector2(34, 34);
            var boxImage = box.gameObject.AddComponent<Image>();
            boxImage.color = new Color(0f, 0f, 0f, 0.6f);

            var check = CreateRect(box, "Check");
            Stretch(check);
            check.offsetMin = new Vector2(6, 6);
            check.offsetMax = new Vector2(-6, -6);
            var checkImage = check.gameObject.AddComponent<Image>();
            checkImage.color = ButtonFocusColor;

            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = boxImage;
            toggle.graphic = checkImage;
            toggle.isOn = value;
            if (onChanged != null)
                toggle.onValueChanged.AddListener(v => onChanged(v));
            return toggle;
        }

        /// <summary>
        /// Keyboard-friendly "&lt; option &gt;" cycler used instead of dropdowns.
        /// </summary>
        public static void CreateCycler(Transform parent, string title, IReadOnlyList<string> options,
            int index, Action<int> onChanged)
        {
            CreateRow(parent, "Cycler_" + Sanitize(title), out var labelArea, out var controlArea);
            CreateLabel(labelArea, title, 22f, TextAlignmentOptions.Left);

            int current = Mathf.Clamp(index, 0, Mathf.Max(0, options.Count - 1));

            var valueLabel = CreateLabel(controlArea, options.Count > 0 ? options[current] : "—", 22f);
            var valueRect = valueLabel.rectTransform;
            valueRect.anchorMin = new Vector2(0.5f, 0);
            valueRect.anchorMax = new Vector2(0.5f, 1);
            valueRect.pivot = new Vector2(0.5f, 0.5f);
            valueRect.sizeDelta = new Vector2(280, 0);

            void Move(int delta)
            {
                if (options.Count == 0) return;
                current = (current + delta + options.Count) % options.Count;
                valueLabel.text = options[current];
                onChanged?.Invoke(current);
            }

            var prev = CreateButton(controlArea, "<", () => Move(-1), 22f);
            var prevRect = (RectTransform)prev.transform;
            prevRect.anchorMin = new Vector2(0, 0.5f);
            prevRect.anchorMax = new Vector2(0, 0.5f);
            prevRect.pivot = new Vector2(0, 0.5f);
            prevRect.sizeDelta = new Vector2(52, 44);

            var next = CreateButton(controlArea, ">", () => Move(1), 22f);
            var nextRect = (RectTransform)next.transform;
            nextRect.anchorMin = new Vector2(1, 0.5f);
            nextRect.anchorMax = new Vector2(1, 0.5f);
            nextRect.pivot = new Vector2(1, 0.5f);
            nextRect.sizeDelta = new Vector2(52, 44);
        }

        /// <summary>Simple confirmation dialog. Returns its root so callers can close it early.</summary>
        public static GameObject CreateConfirmDialog(string message, string confirmText,
            Action onConfirm, Action onCancel = null)
        {
            var canvas = CreateOverlayCanvas("BCaT_ConfirmDialog", 32500);
            var panel = CreateCenterPanel(canvas.transform, "Panel", new Vector2(620, 300));
            var column = CreateColumn(panel, "Column", 22f);
            CreateLabel(column, message, 26f);

            var buttons = CreateRect(column, "Buttons");
            buttons.sizeDelta = new Vector2(0, 70);
            var layout = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 24;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            void Close()
            {
                if (canvas != null)
                    UnityEngine.Object.Destroy(canvas.gameObject);
            }

            CreateButton(buttons, confirmText, () => { Close(); onConfirm?.Invoke(); });
            var cancel = CreateButton(buttons, "Cancel", () => { Close(); onCancel?.Invoke(); });
            SelectForKeyboard(cancel);
            return canvas.gameObject;
        }

        /// <summary>Give keyboard focus to a selectable (visible focus state).</summary>
        public static void SelectForKeyboard(Selectable selectable)
        {
            if (selectable != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }

        // ---- helpers -------------------------------------------------------

        static RectTransform CreateRow(Transform parent, string name,
            out RectTransform labelArea, out RectTransform controlArea)
        {
            var row = CreateRect(parent, name);
            row.sizeDelta = new Vector2(0, 40f + 18f * TextScale);

            labelArea = CreateRect(row, "LabelArea");
            labelArea.anchorMin = new Vector2(0, 0);
            labelArea.anchorMax = new Vector2(0.45f, 1);
            labelArea.offsetMin = Vector2.zero;
            labelArea.offsetMax = Vector2.zero;

            controlArea = CreateRect(row, "ControlArea");
            controlArea.anchorMin = new Vector2(0.45f, 0);
            controlArea.anchorMax = new Vector2(1, 1);
            controlArea.offsetMin = Vector2.zero;
            controlArea.offsetMax = Vector2.zero;
            return row;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static string FormatValue(float v, bool whole) => whole ? ((int)v).ToString() : v.ToString("0.00");

        static string Sanitize(string s) =>
            string.IsNullOrEmpty(s) ? "?" : s.Length > 24 ? s.Substring(0, 24) : s;
    }
}
