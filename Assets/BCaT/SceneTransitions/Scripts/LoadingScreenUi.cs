using BCaT.Production.Shell;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Branded loading presentation for the LoadingScene. The scene previously
/// rendered nothing (solid-black desktop camera, bare Quest rig), so every
/// transition looked like a frozen black screen. This builds the loading UI
/// at runtime in the project's UiFactory style, so the player always sees
/// that Black Homeplaces is running and the next space is being prepared.
///
/// Desktop uses a ScreenSpaceOverlay canvas. On Quest an overlay canvas is
/// never composited into the headset view (see FadeOverlayBuilder), so a
/// WorldSpace canvas is parented to the headset camera instead, and the
/// camera's clear color is matched to the panel so the periphery blends in.
/// The canvas lives in the LoadingScene and is destroyed with it when the
/// destination scene activates — no persistent object, no interaction, and
/// no cleanup path needed.
/// </summary>
public sealed class LoadingScreenUi : MonoBehaviour
{
    const string BackgroundResourceName = "BHXR_LoadingBackground_3840x2160";
    const float BackgroundAspect = 16f / 9f;

    static readonly Color BackdropColor = new Color(0.015f, 0.013f, 0.011f, 1f);
    static readonly Color IvoryText = new Color(0.94f, 0.91f, 0.84f, 1f);
    static readonly Color Bronze = new Color(0.89f, 0.57f, 0.20f, 1f);
    static readonly Color TrackColor = new Color(0.32f, 0.27f, 0.20f, 0.62f);
    static readonly Color FillColor = new Color(1f, 0.72f, 0.34f, 1f);

    TextMeshProUGUI loadingLabel;
    TextMeshProUGUI progressLabel;
    Image progressFill;

    public static LoadingScreenUi Create()
    {
        Camera xrCamera = BCaT.Production.BCaTPlatform.IsQuest ? Camera.main : null;

        GameObject canvasObject;
        RectTransform contentParent;
        if (xrCamera != null)
        {
            canvasObject = new GameObject("BCaT_LoadingScreen",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var rect = (RectTransform)canvasObject.transform;
            rect.SetParent(xrCamera.transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = xrCamera;
            canvas.sortingOrder = 32000;

            // 4 m of canvas 1.2 m ahead fills ~120 degrees horizontally at a
            // comfortable reading distance; both lengths inherit the rig scale
            // together, so angular size is scale-independent (same approach as
            // FadeOverlayBuilder, pulled back for text legibility).
            rect.sizeDelta = new Vector2(1920f, 1080f);
            rect.localPosition = new Vector3(0f, 0f, 1.2f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * (4f / 1920f);

            // Blend the periphery beyond the canvas into the panel color so
            // the headset never shows a raw black void around the message.
            xrCamera.clearFlags = CameraClearFlags.SolidColor;
            xrCamera.backgroundColor = BackdropColor;

            contentParent = rect;
        }
        else
        {
            var canvas = UiFactory.CreateOverlayCanvas("BCaT_LoadingScreen", 32000);
            canvasObject = canvas.gameObject;
            contentParent = (RectTransform)canvas.transform;
        }

        CreateBackground(contentParent);

        var content = UiFactory.CreateRect(contentParent, "Content");
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
        content.pivot = new Vector2(0.5f, 0.5f);
        content.sizeDelta = new Vector2(1240f, 430f);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 21f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var ui = canvasObject.AddComponent<LoadingScreenUi>();

        var title = UiFactory.CreateLabel(content, "BLACK HOMEPLACES: THE XR HOUSE", 34f);
        title.color = IvoryText;
        title.characterSpacing = 17f;

        var studio = UiFactory.CreateLabel(content, "BCAT LAB", 18f);
        studio.color = Bronze;
        studio.characterSpacing = 42f;

        ui.loadingLabel = UiFactory.CreateLabel(content, "LOADING EXPERIENCE", 15f);
        ui.loadingLabel.color = IvoryText;
        ui.loadingLabel.characterSpacing = 34f;

        ui.progressFill = CreateProgressBar(content);

        ui.progressLabel = UiFactory.CreateLabel(content, "", 22f);
        ui.progressLabel.color = Bronze;
        ui.progressLabel.characterSpacing = 13f;

        var waitLabel = UiFactory.CreateLabel(content,
            "PREPARING THE SPACE. PLEASE WAIT.", 15f);
        waitLabel.color = Bronze;
        waitLabel.characterSpacing = 38f;

        return ui;
    }

    static void CreateBackground(RectTransform parent)
    {
        var backdrop = UiFactory.CreateFullScreenPanel(parent, "Backdrop");
        backdrop.GetComponent<Image>().color = BackdropColor;

        var frame = UiFactory.CreateRect(backdrop, "BackgroundAspectFillFrame");
        frame.anchorMin = Vector2.zero;
        frame.anchorMax = Vector2.one;
        frame.offsetMin = Vector2.zero;
        frame.offsetMax = Vector2.zero;
        frame.gameObject.AddComponent<RectMask2D>();

        var imageRect = UiFactory.CreateRect(frame, "SharedLoadingBackground");
        imageRect.anchorMin = imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.sizeDelta = new Vector2(1920f, 1080f);

        var rawImage = imageRect.gameObject.AddComponent<RawImage>();
        rawImage.texture = Resources.Load<Texture2D>(BackgroundResourceName);
        rawImage.color = rawImage.texture == null ? Color.clear : Color.white;
        rawImage.raycastTarget = false;

        var fitter = imageRect.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = BackgroundAspect;
    }

    static Image CreateProgressBar(RectTransform parent)
    {
        var slot = UiFactory.CreateRect(parent, "ProgressBarSlot");
        var slotLayout = slot.gameObject.AddComponent<LayoutElement>();
        slotLayout.preferredHeight = 12f;
        slotLayout.minHeight = 12f;

        var track = UiFactory.CreateRect(slot, "ProgressBar");
        track.anchorMin = track.anchorMax = new Vector2(0.5f, 0.5f);
        track.pivot = new Vector2(0.5f, 0.5f);
        track.sizeDelta = new Vector2(560f, 12f);

        var trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = TrackColor;
        trackImage.raycastTarget = false;

        var fillRect = UiFactory.CreateRect(track, "ProgressFill");
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        var fill = fillRect.gameObject.AddComponent<Image>();
        fill.color = FillColor;
        fill.raycastTarget = false;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0f;
        return fill;
    }

    /// <summary>Real load progress in [0,1]; hidden until it becomes meaningful.</summary>
    public void SetProgress(float progress)
    {
        float clampedProgress = Mathf.Clamp01(progress);
        if (progressFill != null)
            progressFill.fillAmount = clampedProgress;
        if (progressLabel != null && progress > 0.01f)
            progressLabel.text = $"{Mathf.RoundToInt(clampedProgress * 100f)}%";
    }

    /// <summary>Replace the loading line (used by the failure-recovery path).</summary>
    public void SetStatus(string message)
    {
        if (loadingLabel != null)
            loadingLabel.text = message;
        if (progressLabel != null)
            progressLabel.text = string.Empty;
    }
}
