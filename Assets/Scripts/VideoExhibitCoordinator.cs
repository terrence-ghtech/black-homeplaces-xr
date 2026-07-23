using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Ensures only one video exhibit is open/buffering at a time. Each popup
/// registers on open with a callback that closes it; opening another exhibit
/// closes the previous one first so two large videos never stream at once.
/// </summary>
public static class VideoExhibitCoordinator
{
    private static object activeOwner;
    private static Action activeCloser;

    public static void NotifyOpened(object owner, Action closer)
    {
        if (activeOwner != null && !ReferenceEquals(activeOwner, owner))
        {
            try { activeCloser?.Invoke(); }
            catch (Exception e) { Debug.LogWarning($"[VideoExhibitCoordinator] Closing previous exhibit failed: {e.Message}"); }
        }
        activeOwner = owner;
        activeCloser = closer;
    }

    public static void NotifyClosed(object owner)
    {
        if (ReferenceEquals(activeOwner, owner))
        {
            activeOwner = null;
            activeCloser = null;
        }
    }
}

/// <summary>
/// Minimal code-created loading/error label for video popups, so remote
/// videos show progress feedback instead of a black frame. No scene or
/// prefab edits required — attaches under the popup at runtime.
/// </summary>
public static class VideoLoadingIndicator
{
    public static GameObject Show(Transform parent, string message)
    {
        if (parent == null)
            return null;

        var go = new GameObject("VideoLoadingIndicator", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(600, 90);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = message;
        text.fontSize = 30;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        return go;
    }

    public static void SetMessage(GameObject indicator, string message)
    {
        if (indicator == null) return;
        var text = indicator.GetComponent<TextMeshProUGUI>();
        if (text != null) text.text = message;
    }

    public static void Hide(ref GameObject indicator)
    {
        if (indicator == null) return;
        UnityEngine.Object.Destroy(indicator);
        indicator = null;
    }
}
