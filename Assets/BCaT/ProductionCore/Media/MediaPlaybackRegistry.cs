using System;
using System.Collections.Generic;
using UnityEngine;

namespace BCaT.Production.Media
{
    /// <summary>
    /// Tracks currently playing long-form media (exhibit videos and narrations)
    /// so the shell can (a) defer the kiosk inactivity reset while a visitor is
    /// intentionally watching or listening, and (b) stop everything in one call
    /// during kiosk resets, return-to-entrance, and scene transitions.
    /// Media controllers register when playback starts and unregister when it
    /// stops; the stop action must be safe to call redundantly.
    /// </summary>
    public static class MediaPlaybackRegistry
    {
        static readonly Dictionary<object, Action> active = new Dictionary<object, Action>();

        public static bool IsAnyMediaPlaying => active.Count > 0;

        public static int ActiveCount => active.Count;

        public static void NotifyStarted(object owner, Action stopAction)
        {
            if (owner == null) return;
            active[owner] = stopAction;
        }

        public static void NotifyStopped(object owner)
        {
            if (owner == null) return;
            active.Remove(owner);
        }

        /// <summary>Stop all registered media. Defensive: one failure cannot stop the sweep.</summary>
        public static void StopAll()
        {
            var snapshot = new List<KeyValuePair<object, Action>>(active);
            active.Clear();
            foreach (var pair in snapshot)
            {
                try { pair.Value?.Invoke(); }
                catch (Exception e)
                {
                    Debug.LogError($"[MediaRegistry] Stopping media owned by '{pair.Key}' failed: {e}");
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();
    }

    /// <summary>
    /// Structured media failure logging (Phase 6 requirement). Keeps messages
    /// visitor-safe elsewhere; the technical detail lands in the player log.
    /// Never log credential-bearing query strings from remote URLs.
    /// </summary>
    public static class MediaErrorLog
    {
        public static void LogFailure(string exhibitName, string requestedPath, string errorMessage,
            bool remoteAttempted, bool recovered)
        {
            string sanitizedPath = Sanitize(requestedPath);
            Debug.LogError(
                $"[MediaError] exhibit='{exhibitName}' path='{sanitizedPath}' platform={Application.platform} " +
                $"error='{errorMessage}' remoteAttempted={remoteAttempted} recovered={recovered}");
        }

        /// <summary>Strip query strings so signed/tokenized URL parameters never reach logs.</summary>
        static string Sanitize(string path)
        {
            if (string.IsNullOrEmpty(path)) return "(empty)";
            int q = path.IndexOf('?');
            return q >= 0 ? path.Substring(0, q) + "?…" : path;
        }
    }
}
