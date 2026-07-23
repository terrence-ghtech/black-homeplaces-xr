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
    /// Resources/RemoteMediaConfig, otherwise local StreamingAssets (unchanged
    /// shipped behavior).
    /// </summary>
    public static string ResolveMediaUrl(string fileName)
    {
        RemoteMediaConfig config = RemoteMediaConfig.Instance;
        if (config != null && config.TryResolveRemote(fileName, out string remote))
            return remote;

        return StreamingAssetUrl(fileName);
    }
}
