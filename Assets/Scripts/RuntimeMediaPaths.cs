using System.IO;
using UnityEngine;

public static class RuntimeMediaPaths
{
    public static string StreamingAssetUrl(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return string.Empty;

        return Path.Combine(Application.streamingAssetsPath, fileName);
    }

    /// <summary>
    /// Preferred resolver for exhibit media: remote CDN URL when configured in
    /// Resources/RemoteMediaConfig, otherwise local StreamingAssets. Local
    /// StreamingAssets is optional: when the file is absent (media migrated to
    /// remote hosting) this logs the missing path and returns string.Empty so
    /// callers can skip playback instead of handing the decoder a dead path.
    /// </summary>
    public static string ResolveMediaUrl(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            Debug.LogError("[RuntimeMediaPaths] ResolveMediaUrl called with an empty file name; nothing will load.");
            return string.Empty;
        }

        RemoteMediaConfig config = RemoteMediaConfig.Instance;
        if (config != null && config.TryResolveRemote(fileName, out string remote))
        {
            Debug.Log($"[RuntimeMediaPaths] '{fileName}' resolved to remote URL: {remote}");
            return remote;
        }

        string localPath = StreamingAssetUrl(fileName);

#if !UNITY_WEBGL || UNITY_EDITOR
        // streamingAssetsPath is a real folder on these platforms, so a missing
        // file can be detected up front. (On WebGL players it is an HTTP URL and
        // File.Exists does not apply; the request itself reports failure.)
        if (!File.Exists(localPath))
        {
            Debug.LogError(
                $"[RuntimeMediaPaths] '{fileName}' not found in StreamingAssets ({localPath}) and no remote URL is configured for it. " +
                "Large media was migrated to remote hosting - set remoteBaseUrl/entries in Assets/Resources/RemoteMediaConfig.asset " +
                "(and enable useRemoteInEditor for Editor Play Mode). Skipping this item.");
            return string.Empty;
        }
#endif

        Debug.Log($"[RuntimeMediaPaths] '{fileName}' resolved to local StreamingAssets file: {localPath}");
        return localPath;
    }
}
