using UnityEngine;
using UnityEngine.UI;

namespace BCaT.Production.Shell
{
    /// <summary>
    /// Builds the full-view black fade canvas used around scene transitions.
    ///
    /// A ScreenSpaceOverlay canvas is never composited into the headset view,
    /// so on Quest the old overlays were invisible: every transition was a
    /// hard cut, and the arrival fade-from-black silently did nothing. On
    /// Quest this builder instead parents a WorldSpace canvas to the main
    /// camera (the same pattern InteractionPromptUi and the Black Kitchen
    /// exit modal already use), close enough and large enough to cover the
    /// full field of view. Desktop keeps the overlay canvas unchanged.
    ///
    /// Callers only ever touch the returned CanvasGroup's alpha, so fade
    /// logic is identical on both platforms. The canvas lives in the caller's
    /// scene (camera or supplied parent), so scene unload disposes it exactly
    /// as before.
    /// </summary>
    public static class FadeOverlayBuilder
    {
        public static CanvasGroup Create(string name, int sortingOrder, Transform desktopParent = null)
        {
            GameObject canvasObject = new GameObject(
                name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.sortingOrder = sortingOrder;

            Camera xrCamera = BCaTPlatform.IsQuest ? Camera.main : null;
            if (xrCamera != null)
            {
                RectTransform rect = (RectTransform)canvasObject.transform;
                rect.SetParent(xrCamera.transform, false);
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = xrCamera;
                // 3 m of quad 0.32 m ahead covers >150 degrees in every
                // direction; both distances inherit the rig scale together, so
                // angular coverage is scale-independent.
                rect.sizeDelta = new Vector2(3f, 3f);
                rect.localPosition = new Vector3(0f, 0f, 0.32f);
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;
            }
            else
            {
                if (desktopParent != null)
                    canvasObject.transform.SetParent(desktopParent, false);
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            GameObject imageObject = new GameObject(
                "Black", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);
            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            imageObject.GetComponent<Image>().color = Color.black;

            return canvasObject.GetComponent<CanvasGroup>();
        }
    }
}
