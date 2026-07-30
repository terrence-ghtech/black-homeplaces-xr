using System;
using UnityEngine;

/// <summary>
/// Central mapping of exhibit video files to remote (CDN) URLs plus the
/// packaged-media manifest for platforms whose StreamingAssets cannot be
/// probed with File.Exists (Android/Quest). Lives at
/// Assets/Resources/RemoteMediaConfig.asset so every scene and platform
/// resolves URLs the same way via <see cref="RuntimeMediaPaths"/>.
///
/// Native desktop/Quest resolution order per file (see RuntimeMediaPaths):
///  1. Packaged StreamingAssets file (offline-safe institutional default)
///  2. Explicit per-file remoteUrl entry (if non-empty)
///  3. remoteBaseUrl + URL-escaped file name (if remoteBaseUrl non-empty)
/// WebGL keeps its legacy remote-first order.
/// </summary>
[CreateAssetMenu(menuName = "BCaT/Remote Media Config", fileName = "RemoteMediaConfig")]
public class RemoteMediaConfig : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("File name exactly as used by the exhibit (e.g. my_video.mp4)")]
        public string fileName;
        [Tooltip("Full remote URL for this file. Leave empty to use remoteBaseUrl + fileName.")]
        public string remoteUrl;
    }

    [Tooltip("CDN prefix, e.g. https://cdn.example.org/bcat/videos/ (trailing slash required). Empty = use StreamingAssets.")]
    public string remoteBaseUrl = "";

    [Tooltip("Also use remote URLs inside the Editor. Off = editor keeps local StreamingAssets for fast iteration.")]
    public bool useRemoteInEditor;

    public Entry[] entries = Array.Empty<Entry>();

    [Tooltip("Media files packaged into StreamingAssets (maintained by BCaT > Production Setup). " +
             "Required for Android/Quest, where the package contents cannot be probed at runtime.")]
    public string[] packagedFileNames = Array.Empty<string>();

    private static RemoteMediaConfig instance;
    private static bool searched;

    public static RemoteMediaConfig Instance
    {
        get
        {
            if (!searched)
            {
                searched = true;
                instance = Resources.Load<RemoteMediaConfig>("RemoteMediaConfig");
            }
            return instance;
        }
    }

    /// <summary>True when the file ships inside StreamingAssets per the packaged manifest.</summary>
    public bool IsPackaged(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || packagedFileNames == null)
            return false;

        foreach (string packaged in packagedFileNames)
            if (string.Equals(packaged, fileName, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>Returns true and a remote URL when one is configured for this file.</summary>
    public bool TryResolveRemote(string fileName, out string url)
    {
        url = null;
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

#if UNITY_EDITOR
        if (!useRemoteInEditor)
            return false;
#endif

        if (entries != null)
        {
            foreach (Entry entry in entries)
            {
                if (entry != null && string.Equals(entry.fileName, fileName, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(entry.remoteUrl))
                {
                    url = entry.remoteUrl.Trim();
                    return true;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(remoteBaseUrl))
        {
            url = remoteBaseUrl.Trim() + Uri.EscapeDataString(fileName);
            return true;
        }

        return false;
    }
}
