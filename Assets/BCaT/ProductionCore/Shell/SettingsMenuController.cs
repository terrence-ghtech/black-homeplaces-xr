using System;
using System.Collections.Generic;
using System.Linq;
using BCaT.Production.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace BCaT.Production.Shell
{
    /// <summary>
    /// The shared settings panel used by both the main menu and the pause menu.
    /// Builds a vertically navigated UI (Display / Graphics / Audio / Controls)
    /// from the runtime UiFactory. Every control writes into
    /// SettingsManager.Current, applies immediately, and persists on close.
    /// In kiosk mode the Display, Graphics, and Controls sections are withheld
    /// (the administrator fixes those), while Audio remains visitor-adjustable.
    /// </summary>
    public static class SettingsMenuController
    {
        const int SortingOrder = 31500;

        public static GameObject Open(Action onClose, int initialTab = 0) =>
            Open(onClose, initialTab, ApplicationModeService.IsKiosk);

        /// <summary>Full panel for kiosk administrators (bypasses visitor restrictions).</summary>
        public static GameObject OpenUnrestricted(Action onClose) =>
            Open(onClose, 0, kioskRestricted: false);

        static GameObject Open(Action onClose, int initialTab, bool kioskRestricted)
        {
            var canvas = UiFactory.CreateOverlayCanvas("BCaT_SettingsMenu", SortingOrder);
            var panel = UiFactory.CreateFullScreenPanel(canvas.transform, "Panel");

            var frame = UiFactory.CreateCenterPanel(panel, "Frame", new Vector2(1100, 860));
            var column = UiFactory.CreateColumn(frame, "Column", 10f);
            UiFactory.CreateLabel(column, "Settings", 34f);

            var sections = new List<(string name, Action<Transform> build)>();
            if (!kioskRestricted)
            {
                sections.Add(("Display", BuildDisplaySection));
                sections.Add(("Graphics", BuildGraphicsSection));
            }
            sections.Add(("Audio", BuildAudioSection));
            if (!kioskRestricted)
                sections.Add(("Controls", BuildControlsSection));

            // Body: vertical section selector on the left, section rows on the right.
            var body = UiFactory.CreateRect(column, "Body");
            body.sizeDelta = new Vector2(0, 600);

            var nav = UiFactory.CreateRect(body, "SectionNav");
            nav.anchorMin = new Vector2(0, 0);
            nav.anchorMax = new Vector2(0, 1);
            nav.pivot = new Vector2(0, 0.5f);
            nav.sizeDelta = new Vector2(230, 0);
            nav.anchoredPosition = Vector2.zero;
            var navLayout = nav.gameObject.AddComponent<VerticalLayoutGroup>();
            navLayout.spacing = 8;
            navLayout.childAlignment = TextAnchor.UpperCenter;
            navLayout.childControlWidth = true;
            navLayout.childControlHeight = true;
            navLayout.childForceExpandWidth = true;
            navLayout.childForceExpandHeight = false;

            var content = UiFactory.CreateRect(body, "SectionContent");
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(254, 0);
            content.offsetMax = Vector2.zero;

            var navButtons = new List<Button>();

            void ShowSection(int index)
            {
                index = Mathf.Clamp(index, 0, sections.Count - 1);
                foreach (Transform child in content)
                    UnityEngine.Object.Destroy(child.gameObject);
                var rows = UiFactory.CreateColumn(content, "Rows", 10f);
                sections[index].build(rows);

                for (int i = 0; i < navButtons.Count; i++)
                {
                    var colors = navButtons[i].colors;
                    colors.normalColor = i == index ? UiFactory.ButtonFocusColor : UiFactory.ButtonColor;
                    navButtons[i].colors = colors;
                }
            }

            for (int i = 0; i < sections.Count; i++)
            {
                int captured = i;
                navButtons.Add(UiFactory.CreateButton(nav, sections[i].name, () => ShowSection(captured), 24f));
            }

            var footer = UiFactory.CreateRect(column, "Footer");
            footer.sizeDelta = new Vector2(0, 70);
            var footerLayout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 24;
            footerLayout.childControlWidth = true;
            footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = true;

            if (!kioskRestricted)
            {
                UiFactory.CreateButton(footer, "Reset All To Defaults", () =>
                {
                    SettingsManager.ResetToDefaults();
                    // Rebuild so widgets reflect the defaults.
                    UnityEngine.Object.Destroy(canvas.gameObject);
                    Open(onClose, initialTab);
                });
            }

            var closeButton = UiFactory.CreateButton(footer, "Close", () =>
            {
                SettingsManager.Save();
                UnityEngine.Object.Destroy(canvas.gameObject);
                onClose?.Invoke();
            });
            UiFactory.SelectForKeyboard(closeButton);

            ShowSection(initialTab);
            return canvas.gameObject;
        }

        static void ApplyNow()
        {
            SettingsManager.ApplyAll();
        }

        // ---- Sections --------------------------------------------------------

        static void BuildDisplaySection(Transform parent)
        {
            var d = SettingsManager.Current.display;

            var resolutions = Screen.resolutions
                .Select(r => (width: r.width, height: r.height))
                .Distinct()
                .OrderBy(r => r.width * r.height)
                .ToList();
            if (resolutions.Count == 0)
                resolutions.Add((Display.main.systemWidth, Display.main.systemHeight));

            var labels = resolutions.Select(r => $"{r.width} × {r.height}").ToList();
            labels.Add("Native");
            int currentIndex = labels.Count - 1;
            for (int i = 0; i < resolutions.Count; i++)
                if (resolutions[i].width == d.width && resolutions[i].height == d.height)
                    currentIndex = i;

            UiFactory.CreateCycler(parent, "Resolution", labels, currentIndex, i =>
            {
                if (i >= resolutions.Count) { d.width = -1; d.height = -1; }
                else { d.width = resolutions[i].width; d.height = resolutions[i].height; }
                ApplyNow();
            });

            UiFactory.CreateToggle(parent, "Fullscreen", d.fullscreen, v =>
            {
                d.fullscreen = v;
                ApplyNow();
            });
        }

        static void BuildGraphicsSection(Transform parent)
        {
            var g = SettingsManager.Current.graphics;

            var tiers = new List<string> { "Desktop Low", "Desktop Standard", "Desktop High" };
            int tierIndex = Mathf.Max(0, tiers.IndexOf(g.qualityTier));
            UiFactory.CreateCycler(parent, "Quality tier", tiers, tierIndex, i =>
            {
                g.qualityTier = tiers[i];
                ApplyNow();
            });

            UiFactory.CreateSlider(parent, "Shadow distance", 0.5f, 1.5f, g.shadowDistanceScale, v =>
            {
                g.shadowDistanceScale = v;
                ApplyNow();
            });

            UiFactory.CreateSlider(parent, "Terrain distance", 0.5f, 1.5f, g.terrainDistanceScale, v =>
            {
                g.terrainDistanceScale = v;
                ApplyNow();
            });
        }

        static void BuildAudioSection(Transform parent)
        {
            var a = SettingsManager.Current.audio;
            UiFactory.CreateSlider(parent, "Master volume", 0f, 1f, a.master, v => { a.master = v; ApplyNow(); });
        }

        static void BuildControlsSection(Transform parent)
        {
            var c = SettingsManager.Current.controls;
            UiFactory.CreateSlider(parent, "Mouse sensitivity", 0.2f, 3f, c.mouseSensitivity, v =>
            {
                c.mouseSensitivity = v;
                ApplyNow();
            });
            UiFactory.CreateToggle(parent, "Invert Y axis", c.invertY, v =>
            {
                c.invertY = v;
                ApplyNow();
            });
            UiFactory.CreateLabel(parent,
                "Move: WASD   Look: Mouse   Interact: E   Pause: Esc", 20f);
        }
    }
}
