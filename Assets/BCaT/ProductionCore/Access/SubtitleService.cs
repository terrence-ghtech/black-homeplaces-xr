using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BCaT.Production.Access
{
    /// <summary>
    /// A subtitle track for one exhibit narration or video. Content must come
    /// from provided/approved source material — transcripts are never invented.
    /// Assets are created by editors under Assets/BCaT/ProductionCore/Subtitles.
    /// </summary>
    [CreateAssetMenu(menuName = "BCaT/Subtitle Track", fileName = "SubtitleTrack")]
    public sealed class SubtitleTrack : ScriptableObject
    {
        [System.Serializable]
        public struct Cue
        {
            public float startSeconds;
            public float endSeconds;
            [TextArea] public string text;
        }

        [Tooltip("Media identifier this track belongs to (video file name or narrative id).")]
        public string mediaId;

        [Tooltip("Full transcript text (for the transcript viewer). Approved content only.")]
        [TextArea(6, 30)] public string transcript;

        [Tooltip("Timed cues. May be empty when only a transcript is available.")]
        public Cue[] cues = new Cue[0];
    }

    /// <summary>
    /// Global subtitle overlay, shared by desktop and (structurally) Quest.
    /// Desktop: screen-space bottom band. Quest: a camera-facing world-space
    /// canvas 2 m ahead (readability on physical hardware is deferred to the
    /// owner's headset validation). Media controllers report playback through
    /// NotifyMediaStarted/Stopped with a media id; if an approved SubtitleTrack
    /// exists for that id it plays, otherwise nothing is shown — missing
    /// transcript content is reported in the accessibility documentation, never
    /// fabricated.
    /// </summary>
    public sealed class SubtitleService : MonoBehaviour
    {
        public static SubtitleService Instance { get; private set; }

        readonly Dictionary<string, SubtitleTrack> tracks =
            new Dictionary<string, SubtitleTrack>(System.StringComparer.OrdinalIgnoreCase);

        Canvas canvas;
        TextMeshProUGUI label;
        SubtitleTrack activeTrack;
        float mediaTime;
        bool playing;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            foreach (var track in Resources.LoadAll<SubtitleTrack>("Subtitles"))
                if (track != null && !string.IsNullOrEmpty(track.mediaId))
                    tracks[track.mediaId] = track;

            Debug.Log($"[Subtitles] {tracks.Count} approved subtitle track(s) loaded.");
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool HasTrack(string mediaId) =>
            !string.IsNullOrEmpty(mediaId) && tracks.ContainsKey(mediaId);

        public SubtitleTrack GetTrack(string mediaId) =>
            !string.IsNullOrEmpty(mediaId) && tracks.TryGetValue(mediaId, out var t) ? t : null;

        public IReadOnlyDictionary<string, SubtitleTrack> AllTracks => tracks;

        public void NotifyMediaStarted(string mediaId)
        {
            activeTrack = GetTrack(mediaId);
            mediaTime = 0f;
            playing = activeTrack != null;
        }

        public void NotifyMediaTime(string mediaId, float seconds)
        {
            if (playing && activeTrack != null && activeTrack.mediaId == mediaId)
                mediaTime = seconds;
        }

        public void NotifyMediaStopped(string mediaId)
        {
            if (activeTrack != null && activeTrack.mediaId == mediaId)
            {
                playing = false;
                activeTrack = null;
                SetText(string.Empty);
            }
        }

        void Update()
        {
            if (!Settings.SettingsManager.Current.accessibility.subtitles)
            {
                SetText(string.Empty);
                return;
            }

            if (!playing || activeTrack == null || activeTrack.cues == null)
                return;

            mediaTime += Time.unscaledDeltaTime;

            string current = string.Empty;
            foreach (var cue in activeTrack.cues)
            {
                if (mediaTime >= cue.startSeconds && mediaTime <= cue.endSeconds)
                {
                    current = cue.text;
                    break;
                }
            }
            SetText(current);
        }

        void SetText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                if (canvas != null) canvas.enabled = false;
                return;
            }

            EnsureCanvas();
            canvas.enabled = true;
            label.text = text;
        }

        void EnsureCanvas()
        {
            if (canvas != null) return;

            var go = new GameObject("BCaT_Subtitles", typeof(Canvas));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();

            bool xr = PlatformCapabilities.IsXRActive || PlatformCapabilities.IsQuestConfiguration;
            if (xr)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                var rect = (RectTransform)go.transform;
                rect.sizeDelta = new Vector2(1000, 200);
                go.transform.localScale = Vector3.one * 0.002f;
                go.AddComponent<SubtitleXRFollower>();
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 29500;
            }

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRect = (RectTransform)textGo.transform;
            if (xr)
            {
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
            }
            else
            {
                textRect.anchorMin = new Vector2(0.15f, 0f);
                textRect.anchorMax = new Vector2(0.85f, 0f);
                textRect.pivot = new Vector2(0.5f, 0f);
                textRect.anchoredPosition = new Vector2(0, 150);
                textRect.sizeDelta = new Vector2(0, 140);
            }

            label = textGo.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            ApplyStyle();
            Settings.SettingsManager.SettingsApplied += ApplyStyle;
        }

        void ApplyStyle()
        {
            if (label == null) return;
            var a = Settings.SettingsManager.Current.accessibility;
            label.fontSize = 32f * a.TextScaleFactor;
            label.color = Color.white;
            label.outlineWidth = a.highContrastUi ? 0.35f : 0.25f;
            label.outlineColor = Color.black;
        }
    }

    /// <summary>Keeps the XR subtitle canvas ahead of the camera.</summary>
    sealed class SubtitleXRFollower : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            transform.position = cam.transform.position +
                                 cam.transform.forward * 2.0f +
                                 cam.transform.up * -0.55f;
            transform.rotation = Quaternion.LookRotation(
                transform.position - cam.transform.position);
        }
    }
}
