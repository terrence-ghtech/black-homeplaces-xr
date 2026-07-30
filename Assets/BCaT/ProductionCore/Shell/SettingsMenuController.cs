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
    /// Builds a tabbed UI (Display / Graphics / Audio / Controls /
    /// Accessibility) from the runtime UiFactory. Every control writes into
    /// SettingsManager.Current, applies immediately, and persists on close.
    /// In kiosk mode the Display and Graphics tabs are withheld (the
    /// administrator fixes those), while Audio and Accessibility remain
    /// visitor-adjustable.
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

            // Tab strip
            var tabs = new List<(string name, Action<Transform> build)>();
            if (!kioskRestricted)
            {
                tabs.Add(("Display", BuildDisplayTab));
                tabs.Add(("Graphics", BuildGraphicsTab));
            }
            tabs.Add(("Audio", BuildAudioTab));
            if (!kioskRestricted)
                tabs.Add(("Controls", BuildControlsTab));
            tabs.Add(("Accessibility", BuildAccessibilityTab));

            var tabBar = UiFactory.CreateRect(column, "TabBar");
            tabBar.sizeDelta = new Vector2(0, 64);
            var tabLayout = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 8;
            tabLayout.childControlWidth = true;
            tabLayout.childControlHeight = true;
            tabLayout.childForceExpandWidth = true;

            var content = UiFactory.CreateRect(column, "TabContent");
            content.sizeDelta = new Vector2(0, 560);

            void ShowTab(int index)
            {
                foreach (Transform child in content)
                    UnityEngine.Object.Destroy(child.gameObject);
                var tabColumn = UiFactory.CreateColumn(content, "Rows", 10f);
                tabs[Mathf.Clamp(index, 0, tabs.Count - 1)].build(tabColumn);
            }

            for (int i = 0; i < tabs.Count; i++)
            {
                int captured = i;
                UiFactory.CreateButton(tabBar, tabs[i].name, () => ShowTab(captured), 22f);
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

            ShowTab(Mathf.Clamp(initialTab, 0, tabs.Count - 1));
            return canvas.gameObject;
        }

        static void ApplyNow()
        {
            SettingsManager.ApplyAll();
        }

        // ---- Tabs ----------------------------------------------------------

        static void BuildDisplayTab(Transform parent)
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

            if (Display.displays.Length > 1)
            {
                var displayLabels = new List<string>();
                for (int i = 0; i < Display.displays.Length; i++)
                    displayLabels.Add($"Display {i + 1}");
                UiFactory.CreateCycler(parent, "Display", displayLabels,
                    Mathf.Clamp(d.displayIndex, 0, Display.displays.Length - 1), i =>
                    {
                        d.displayIndex = i;
                        ApplyNow();
                    });
            }

            UiFactory.CreateToggle(parent, "VSync", d.vSyncCount > 0, v =>
            {
                d.vSyncCount = v ? 1 : 0;
                ApplyNow();
            });

            var fpsOptions = new List<string> { "Uncapped", "30 FPS", "60 FPS", "120 FPS" };
            int fpsIndex = d.targetFrameRate switch { 30 => 1, 60 => 2, 120 => 3, _ => 0 };
            UiFactory.CreateCycler(parent, "Frame-rate limit", fpsOptions, fpsIndex, i =>
            {
                d.targetFrameRate = i switch { 1 => 30, 2 => 60, 3 => 120, _ => -1 };
                ApplyNow();
            });
        }

        static void BuildGraphicsTab(Transform parent)
        {
            var g = SettingsManager.Current.graphics;

            var tiers = new List<string> { "Desktop Low", "Desktop Standard", "Desktop High" };
            int tierIndex = Mathf.Max(0, tiers.IndexOf(g.qualityTier));
            UiFactory.CreateCycler(parent, "Quality tier", tiers, tierIndex, i =>
            {
                g.qualityTier = tiers[i];
                ApplyNow();
            });

            UiFactory.CreateSlider(parent, "Render scale", 0.5f, 1.5f, g.renderScale, v =>
            {
                g.renderScale = v;
                ApplyNow();
            });

            UiFactory.CreateSlider(parent, "Shadow distance", 0.5f, 1.5f, g.shadowDistanceScale, v =>
            {
                g.shadowDistanceScale = v;
                ApplyNow();
            });

            var texOptions = new List<string> { "Full", "Half", "Quarter" };
            UiFactory.CreateCycler(parent, "Texture quality", texOptions,
                Mathf.Clamp(SettingsManager.Current.graphics.textureQuality, 0, 2), i =>
                {
                    g.textureQuality = i;
                    ApplyNow();
                });

            var aaOptions = new List<string> { "Tier default", "Off", "2x MSAA", "4x MSAA" };
            int aaIndex = g.antiAliasing switch { 1 => 1, 2 => 2, 4 => 3, _ => 0 };
            UiFactory.CreateCycler(parent, "Anti-aliasing", aaOptions, aaIndex, i =>
            {
                g.antiAliasing = i switch { 1 => 1, 2 => 2, 3 => 4, _ => -1 };
                ApplyNow();
            });

            UiFactory.CreateToggle(parent, "Ambient effects (post-processing)", g.ambientEffects, v =>
            {
                g.ambientEffects = v;
                ApplyNow();
            });

            UiFactory.CreateSlider(parent, "Terrain distance", 0.5f, 1.5f, g.terrainDistanceScale, v =>
            {
                g.terrainDistanceScale = v;
                ApplyNow();
            });

            UiFactory.CreateSlider(parent, "Vegetation distance", 0.5f, 1.5f, g.vegetationDistanceScale, v =>
            {
                g.vegetationDistanceScale = v;
                ApplyNow();
            });
        }

        static void BuildAudioTab(Transform parent)
        {
            var a = SettingsManager.Current.audio;
            UiFactory.CreateSlider(parent, "Master", 0f, 1f, a.master, v => { a.master = v; ApplyNow(); });
            UiFactory.CreateSlider(parent, "Narration", 0f, 1f, a.narration, v => { a.narration = v; ApplyNow(); });
            UiFactory.CreateSlider(parent, "Ambience", 0f, 1f, a.ambience, v => { a.ambience = v; ApplyNow(); });
            UiFactory.CreateSlider(parent, "Effects", 0f, 1f, a.effects, v => { a.effects = v; ApplyNow(); });
            UiFactory.CreateSlider(parent, "Media / video", 0f, 1f, a.media, v => { a.media = v; ApplyNow(); });
        }

        static void BuildControlsTab(Transform parent)
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

        static void BuildAccessibilityTab(Transform parent)
        {
            var a = SettingsManager.Current.accessibility;

            UiFactory.CreateToggle(parent, "Subtitles", a.subtitles, v =>
            {
                a.subtitles = v;
                ApplyNow();
            });

            var sizes = new List<string> { "Normal", "Large", "Extra large" };
            UiFactory.CreateCycler(parent, "Text size", sizes, Mathf.Clamp(a.textSize, 0, 2), i =>
            {
                a.textSize = i;
                ApplyNow();
            });

            UiFactory.CreateToggle(parent, "High-contrast interface", a.highContrastUi, v =>
            {
                a.highContrastUi = v;
                ApplyNow();
            });

            UiFactory.CreateToggle(parent, "Reduced motion", a.reducedMotion, v =>
            {
                a.reducedMotion = v;
                ApplyNow();
            });

            UiFactory.CreateToggle(parent, "Persistent interaction prompts", a.persistentPrompts, v =>
            {
                a.persistentPrompts = v;
                ApplyNow();
            });

            UiFactory.CreateLabel(parent,
                "Text size and contrast apply to menus, prompts, and subtitles.\n" +
                "Reopen this panel to see it restyled.", 18f);
        }
    }
}
