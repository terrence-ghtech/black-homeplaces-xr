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
}
