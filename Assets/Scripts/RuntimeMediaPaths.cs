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
    /// Preferred resolver for exhibit media.
    ///
    /// Native desktop and Quest (institutional editions): packaged
    /// StreamingAssets media is preferred so the application works fully
    /// offline; the remote CDN URL is the fallback for files that are not
    /// packaged. On Windows/macOS a packaged file is verified with
    /// File.Exists; on Android/Quest StreamingAssets lives inside the APK, so
    /// packaged files are declared in RemoteMediaConfig.packagedFileNames
    /// (maintained by the editor media sync tool) and addressed by the jar URL.
    ///
    /// WebGL (legacy remnant): remote-first, exactly as shipped previously.
    ///
    /// Returns string.Empty when nothing can play so callers skip playback
    /// instead of handing the decoder a dead path.
    /// </summary>
    public static string ResolveMediaUrl(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            Debug.LogError("[RuntimeMediaPaths] ResolveMediaUrl called with an empty file name; nothing will load.");
            return string.Empty;
        }

        RemoteMediaConfig config = RemoteMediaConfig.Instance;

#if UNITY_WEBGL && !UNITY_EDITOR
        // Legacy WebGL behavior: remote CDN first, raw StreamingAssets URL as
        // the fallback (File.Exists does not apply to HTTP paths).
        if (config != null && config.TryResolveRemote(fileName, out string webRemote))
        {
            Debug.Log($"[RuntimeMediaPaths] '{fileName}' resolved to remote URL: {webRemote}");
            return webRemote;
        }
        return StreamingAssetUrl(fileName);
#else
        string localPath = StreamingAssetUrl(fileName);

#if UNITY_ANDROID && !UNITY_EDITOR
        // Quest: StreamingAssets is inside the APK; existence is declared by
        // the packaged-media manifest instead of the file system.
        bool localAvailable = config != null && config.IsPackaged(fileName);
#else
        bool localAvailable = File.Exists(localPath);
#endif

        if (localAvailable)
        {
            Debug.Log($"[RuntimeMediaPaths] '{fileName}' resolved to packaged StreamingAssets file: {localPath}");
            return localPath;
        }

        if (config != null && config.TryResolveRemote(fileName, out string remote))
        {
            Debug.Log($"[RuntimeMediaPaths] '{fileName}' not packaged; resolved to remote URL: {remote}");
            return remote;
        }

        Debug.LogError(
            $"[RuntimeMediaPaths] '{fileName}' not found in StreamingAssets ({localPath}) and no remote URL is configured for it. " +
            "Package the file (BCaT > Production Setup) or set remoteBaseUrl/entries in Assets/Resources/RemoteMediaConfig.asset. " +
            "Skipping this item.");
        return string.Empty;
#endif
    }
}
