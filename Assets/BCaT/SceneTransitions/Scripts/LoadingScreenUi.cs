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
    const float EllipsisInterval = 0.45f;

    static readonly Color BackdropColor = new Color(0.07f, 0.06f, 0.05f, 1f);

    TextMeshProUGUI loadingLabel;
    TextMeshProUGUI progressLabel;
    float ellipsisTimer;
    int ellipsisDots = 3;
    bool animateEllipsis = true;

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

        var backdrop = UiFactory.CreateFullScreenPanel(contentParent, "Backdrop");
        backdrop.GetComponent<Image>().color = BackdropColor;

        var content = UiFactory.CreateRect(contentParent, "Content");
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
        content.pivot = new Vector2(0.5f, 0.5f);
        content.sizeDelta = new Vector2(1200f, 420f);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var ui = canvasObject.AddComponent<LoadingScreenUi>();

        UiFactory.CreateLabel(content, "Black Homeplaces: The XR House", 40f);

        ui.loadingLabel = UiFactory.CreateLabel(content, "Loading…", 30f);
        ui.loadingLabel.color = UiFactory.ButtonFocusColor;

        var waitLabel = UiFactory.CreateLabel(content,
            "Please wait while the next space is prepared.", 22f);
        waitLabel.color = new Color(
            waitLabel.color.r, waitLabel.color.g, waitLabel.color.b, 0.85f);

        ui.progressLabel = UiFactory.CreateLabel(content, "", 22f);
        ui.progressLabel.color = new Color(
            ui.progressLabel.color.r, ui.progressLabel.color.g, ui.progressLabel.color.b, 0.7f);

        return ui;
    }

    /// <summary>Real load progress in [0,1]; hidden until it becomes meaningful.</summary>
    public void SetProgress(float progress)
    {
        if (progressLabel != null && progress > 0.01f)
            progressLabel.text = $"{Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f)}%";
    }

    /// <summary>Replace the loading line (used by the failure-recovery path).</summary>
    public void SetStatus(string message)
    {
        animateEllipsis = false;
        if (loadingLabel != null)
            loadingLabel.text = message;
        if (progressLabel != null)
            progressLabel.text = string.Empty;
    }

    void Update()
    {
        if (!animateEllipsis || loadingLabel == null)
            return;

        ellipsisTimer += Time.unscaledDeltaTime;
        if (ellipsisTimer < EllipsisInterval)
            return;

        ellipsisTimer = 0f;
        ellipsisDots = ellipsisDots % 3 + 1;
        loadingLabel.text = "Loading" + new string('.', ellipsisDots);
    }
}
