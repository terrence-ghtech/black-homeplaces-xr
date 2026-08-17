using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Adds the general instruction panel beside the contributor credits panel.
    ///
    /// It is signage, not an interactable: no collider, no IInteractionTarget, no
    /// XR select surface, and every graphic has raycastTarget off, so neither the
    /// desktop camera ray nor the Quest gaze test can ever pick it up. The credits
    /// panel is read for its transform and canvas settings but is not modified.
    ///
    ///   Unity -executeMethod BCaT.EditorTools.BlackKitchenInstructionPanelSetup.Apply
    /// </summary>
    public static class BlackKitchenInstructionPanelSetup
    {
        const string ScenePath =
            "Assets/BCaT/Exhibits/BlackKitchen/Scenes/BlackKitchen_MemoryScene.unity";

        const string PanelName = "InstructionPanel";
        const string CreditsName = "CreditsPanel";

        const string TitleText = "Explore the Black Kitchen";

        const string BodyText =
            "Move through the space and look closely at objects around you. " +
            "Interactive works will display an on-screen prompt when available.";

        // Placed along the credits panel's own right axis so the two stay coplanar,
        // reading as a pair of plaques rather than two unrelated floating cards.
        const float SideOffset = 1.65f;

        [MenuItem("BCaT/Black Kitchen/Set Up Instruction Panel")]
        public static void Apply()
        {
            var log = new StringBuilder();
            log.AppendLine("[BlackKitchenInstructionPanelSetup] START");

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Fail(log, $"could not open '{ScenePath}'.");
                return;
            }

            Transform credits = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == CreditsName);
            if (credits == null)
            {
                Fail(log, $"no '{CreditsName}' in the scene to sit beside.");
                return;
            }

            var creditsCanvas = credits.GetComponent<Canvas>();
            var creditsRect = credits as RectTransform;
            if (creditsCanvas == null || creditsRect == null)
            {
                Fail(log, $"'{CreditsName}' is not a Canvas with a RectTransform.");
                return;
            }

            Transform existing = credits.parent != null
                ? credits.parent.Find(PanelName)
                : null;
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
                log.AppendLine("  removed previous InstructionPanel (rebuilding)");
            }

            // --- canvas, matching the credits plaque exactly ---
            var panelObject = new GameObject(PanelName,
                typeof(RectTransform), typeof(Canvas));
            panelObject.transform.SetParent(credits.parent, false);

            var canvas = panelObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = creditsCanvas.sortingOrder;

            var rect = panelObject.GetComponent<RectTransform>();
            rect.sizeDelta = creditsRect.sizeDelta;
            rect.localScale = creditsRect.localScale;
            rect.localRotation = creditsRect.localRotation;
            rect.localPosition = creditsRect.localPosition + credits.right * SideOffset;

            // --- background, borrowing the credits plaque's colour ---
            var creditsBackground = credits.GetComponentsInChildren<Image>(true)
                .FirstOrDefault();

            var backgroundObject = new GameObject("InstructionBackground",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            backgroundObject.transform.SetParent(panelObject.transform, false);
            var backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.sizeDelta = new Vector2(rect.sizeDelta.x - 20f, rect.sizeDelta.y - 20f);
            var backgroundImage = backgroundObject.GetComponent<Image>();
            backgroundImage.color = creditsBackground != null
                ? creditsBackground.color
                : new Color(0.02f, 0.025f, 0.028f, 0.88f);
            backgroundImage.raycastTarget = false;

            // --- title + body ---
            TMP_Text titleLabel = CreateText(panelObject.transform, "InstructionTitle", TitleText,
                34f, FontStyles.Bold, TextAlignmentOptions.Top,
                new Vector2(rect.sizeDelta.x - 80f, 70f), new Vector2(0f, 96f));

            TMP_Text bodyLabel = CreateText(panelObject.transform, "InstructionBody", BodyText,
                24f, FontStyles.Normal, TextAlignmentOptions.Top,
                new Vector2(rect.sizeDelta.x - 80f, 170f), new Vector2(0f, -30f));

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Fail(log, "SaveScene returned false.");
                return;
            }

            log.AppendLine($"  InstructionPanel created under '{credits.parent.name}'");
            log.AppendLine($"    credits  localPos={creditsRect.localPosition} size={creditsRect.sizeDelta} " +
                           $"scale={creditsRect.localScale}");
            log.AppendLine($"    panel    localPos={rect.localPosition} size={rect.sizeDelta} " +
                           $"scale={rect.localScale} world={rect.position}");
            log.AppendLine($"    title='{titleLabel.text}'");
            log.AppendLine($"    body='{bodyLabel.text}'");
            log.AppendLine($"    colliders={panelObject.GetComponentsInChildren<Collider>(true).Length} " +
                           $"(must be 0) " +
                           $"raycastTargets={panelObject.GetComponentsInChildren<Graphic>(true).Count(g => g.raycastTarget)} " +
                           $"(must be 0)");
            log.AppendLine("[BlackKitchenInstructionPanelSetup] DONE");
            Debug.Log(log.ToString());

            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        static TMP_Text CreateText(Transform parent, string name, string value, float fontSize,
            FontStyles style, TextAlignmentOptions alignment, Vector2 size, Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            TMP_Text text = go.GetComponent<TMP_Text>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = new Color(0.94f, 0.92f, 0.87f, 1f);
            text.enableWordWrapping = true;
            text.raycastTarget = false; // informational only
            return text;
        }

        static void Fail(StringBuilder log, string message)
        {
            log.AppendLine($"  FAILED: {message}");
            Debug.LogError(log.ToString());
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }
}
