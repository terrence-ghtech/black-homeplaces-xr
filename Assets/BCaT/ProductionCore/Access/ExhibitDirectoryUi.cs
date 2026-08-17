using System;
using System.Collections.Generic;
using BCaT.Production.Interaction;
using BCaT.Production.Shell;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BCaT.Production.Access
{
    /// <summary>
    /// The exhibit directory. Entries are derived from live scene content —
    /// every registered interaction target plus the Black Kitchen portal —
    /// grouped by their organizer ancestors in the scene hierarchy, so the
    /// directory can never drift from reality or present invented exhibits.
    /// Availability reflects whether the exhibit is currently present and
    /// active. No floor map is shown: no approved map asset exists, and an
    /// improvised one could mislead visitors (documented limitation).
    /// </summary>
    public static class ExhibitDirectoryUi
    {
        /// <summary>Friendly names for well-known exhibit controller types.</summary>
        static readonly Dictionary<string, string> TypeLabels = new Dictionary<string, string>
        {
            { "BlackKitchenPortalController", "Black Kitchen (portal)" },
            { "MediaVideoController", "Video exhibit" },
            { "InteractableLinkLauncher", "External resource" },
            { "SpatialAudioToggle", "Audio exhibit" },
            { "HolographicSlideshow", "Slideshow" },
            { "SimpleImagePopupInteractor", "Image exhibit" },
            { "MeshellArticleNotebookOpener", "Article notebook" },
            { "LindaLeaksPanelOpener", "Photo album" },
            { "PrivacyLawExhibitController", "Privacy Law exhibit" },
        };

        public static GameObject Open(Action onClose, Action closePauseMenu = null)
        {
            var canvas = UiFactory.CreateOverlayCanvas("BCaT_ExhibitDirectory", 31600);
            var panel = UiFactory.CreateCenterPanel(canvas.transform, "Panel", new Vector2(1000, 860));
            var column = UiFactory.CreateColumn(panel, "Column", 10f);

            UiFactory.CreateLabel(column, "Exhibit Directory", 32f);
            UiFactory.CreateLabel(column,
                SceneManager.GetActiveScene().name == SceneTransitionState.BlackKitchenSceneName
                    ? "You are in: the Black Kitchen"
                    : "You are in: the main house", 20f);

            // Scrollable list
            var viewport = UiFactory.CreateRect(column, "Viewport");
            viewport.sizeDelta = new Vector2(0, 520);
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.35f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            var content = UiFactory.CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1f);
            // A fresh RectTransform defaults to 100x100; zero it so the
            // stretched content matches the viewport width instead of
            // overhanging (and clipping) 50px on each side.
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.padding = new RectOffset(18, 18, 12, 12);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            // Clamped keeps the list bound to its actual range (no elastic
            // overscroll). The input module normalizes a wheel notch to 6
            // units, so sensitivity 8 ≈ 48 scaled px (about 1.5 rows) per notch.
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 8;

            foreach (var (room, entries) in CollectEntries())
            {
                var roomLabel = UiFactory.CreateLabel(content, room, 24f, TMPro.TextAlignmentOptions.Left);
                roomLabel.fontStyle = TMPro.FontStyles.Bold;
                foreach (var entry in entries)
                {
                    var line = UiFactory.CreateLabel(content,
                        $"   {entry.name}  —  {(entry.available ? "available" : "currently unavailable")}",
                        20f, TMPro.TextAlignmentOptions.Left);
                    if (!entry.available)
                        line.color = new Color(line.color.r, line.color.g, line.color.b, 0.55f);
                }
            }

            var footer = UiFactory.CreateRect(column, "Footer");
            footer.sizeDelta = new Vector2(0, 70);
            var footerLayout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 20;
            footerLayout.childControlWidth = true;
            footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = true;

            UiFactory.CreateButton(footer, "Return to Main Entrance", () =>
            {
                UnityEngine.Object.Destroy(canvas.gameObject);
                onClose?.Invoke();
                closePauseMenu?.Invoke();
                ResetService.ReturnToMainEntrance();
            });
            var close = UiFactory.CreateButton(footer, "Close", () =>
            {
                UnityEngine.Object.Destroy(canvas.gameObject);
                onClose?.Invoke();
            });
            UiFactory.SelectForKeyboard(close);

            return canvas.gameObject;
        }

        struct Entry
        {
            public string name;
            public bool available;
        }

        static List<(string room, List<Entry> entries)> CollectEntries()
        {
            var byRoom = new SortedDictionary<string, List<Entry>>();

            // Registered router targets (available) — enumerate through the scene
            // so inactive/unavailable exhibits are represented too.
            foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (!TypeLabels.TryGetValue(typeName, out string label))
                    continue;

                string display = $"{CleanName(mb.gameObject.name)} ({label})";
                string room = RoomOf(mb.transform);
                bool available = mb.isActiveAndEnabled;

                if (!byRoom.TryGetValue(room, out var list))
                    byRoom[room] = list = new List<Entry>();
                list.Add(new Entry { name = display, available = available });
            }

            var result = new List<(string, List<Entry>)>();
            foreach (var pair in byRoom)
            {
                pair.Value.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
                result.Add((pair.Key, pair.Value));
            }
            return result;
        }

        /// <summary>Location description from organizer ancestors (factual, scene-derived).</summary>
        static string RoomOf(Transform t)
        {
            // Walk up to the child of the content organizers; that object's name
            // is the authored room/area grouping.
            Transform current = t;
            Transform best = null;
            while (current.parent != null)
            {
                string parentName = current.parent.name;
                if (parentName == "_SceneContent" || parentName == "Home" ||
                    parentName == "ImplementedContributorInstallations" || parentName == "Environment")
                {
                    best = current;
                    break;
                }
                current = current.parent;
            }
            return best != null ? CleanName(best.name) : "Main house";
        }

        static string CleanName(string raw) =>
            raw.Replace('_', ' ').Replace("  ", " ").Trim();
    }
}
