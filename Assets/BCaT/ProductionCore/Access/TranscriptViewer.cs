using System.Collections.Generic;
using BCaT.Production.Shell;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BCaT.Production.Access
{
    /// <summary>
    /// Accessible transcript viewer for exhibit narrations/videos. Lists the
    /// approved SubtitleTrack assets that carry transcript text and shows the
    /// selected transcript in a keyboard-scrollable, text-scaled, high-contrast-
    /// aware panel. If no approved transcript exists for an exhibit it is
    /// simply absent — the viewer never displays placeholder or invented text,
    /// and the accessibility report lists the missing coverage.
    /// </summary>
    public static class TranscriptViewer
    {
        public static GameObject Open(System.Action onClose)
        {
            var canvas = UiFactory.CreateOverlayCanvas("BCaT_Transcripts", 31900);
            var panel = UiFactory.CreateCenterPanel(canvas.transform, "Panel", new Vector2(1040, 860));
            var column = UiFactory.CreateColumn(panel, "Column", 10f);

            UiFactory.CreateLabel(column, "Transcripts", 32f);

            var tracks = new List<SubtitleTrack>();
            if (SubtitleService.Instance != null)
                foreach (var pair in SubtitleService.Instance.AllTracks)
                    if (!string.IsNullOrWhiteSpace(pair.Value.transcript))
                        tracks.Add(pair.Value);

            ScrollRect scroll = null;

            if (tracks.Count == 0)
            {
                UiFactory.CreateLabel(column,
                    "No approved transcripts are installed yet.\n\n" +
                    "Transcript content is added by the project team as approved\n" +
                    "source material becomes available.", 22f);
            }
            else
            {
                var picker = UiFactory.CreateRect(column, "Picker");
                picker.sizeDelta = new Vector2(0, 64);
                var pickerLayout = picker.gameObject.AddComponent<HorizontalLayoutGroup>();
                pickerLayout.spacing = 8;
                pickerLayout.childControlWidth = true;
                pickerLayout.childControlHeight = true;
                pickerLayout.childForceExpandWidth = true;

                var viewport = UiFactory.CreateRect(column, "Viewport");
                viewport.sizeDelta = new Vector2(0, 560);
                viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.4f);
                viewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;

                var content = UiFactory.CreateRect(viewport, "Content");
                content.anchorMin = new Vector2(0, 1);
                content.anchorMax = new Vector2(1, 1);
                content.pivot = new Vector2(0.5f, 1f);

                var body = content.gameObject.AddComponent<TextMeshProUGUI>();
                body.fontSize = 24f * UiFactory.TextScale;
                body.color = UiFactory.TextColor;
                body.margin = new Vector4(20, 14, 20, 14);
                body.raycastTarget = false;

                content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                    ContentSizeFitter.FitMode.PreferredSize;

                scroll = viewport.gameObject.AddComponent<ScrollRect>();
                scroll.content = content;
                scroll.horizontal = false;
                scroll.scrollSensitivity = 40;

                void Show(SubtitleTrack track)
                {
                    body.text = $"<b>{track.mediaId}</b>\n\n{track.transcript}";
                    scroll.verticalNormalizedPosition = 1f;
                }

                foreach (var track in tracks)
                {
                    var captured = track;
                    UiFactory.CreateButton(picker, track.mediaId, () => Show(captured), 18f);
                }
                Show(tracks[0]);
            }

            var close = UiFactory.CreateButton(column, "Close", () =>
            {
                Object.Destroy(canvas.gameObject);
                onClose?.Invoke();
            });
            UiFactory.SelectForKeyboard(close);

            // Keyboard scrolling support.
            var keys = canvas.gameObject.AddComponent<TranscriptKeyScroller>();
            keys.scroll = scroll;
            keys.onClose = () =>
            {
                Object.Destroy(canvas.gameObject);
                onClose?.Invoke();
            };

            return canvas.gameObject;
        }

        /// <summary>Arrow/PageUp/PageDown scrolling and Escape-to-close for the viewer.</summary>
        sealed class TranscriptKeyScroller : MonoBehaviour
        {
            public ScrollRect scroll;
            public System.Action onClose;

            void Update()
            {
                if (Interaction.FocusedUiInput.CancelPressed)
                {
                    onClose?.Invoke();
                    return;
                }
                if (scroll == null) return;

                float step = Interaction.FocusedUiInput.ScrollStep();
                if (step != 0f)
                    scroll.verticalNormalizedPosition =
                        Mathf.Clamp01(scroll.verticalNormalizedPosition + step * Time.unscaledDeltaTime);
            }
        }
    }
}
