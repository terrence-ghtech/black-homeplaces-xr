using System.IO;
using BCaT.Production;
using UnityEditor;
using UnityEngine;

namespace BCaT.EditorTools
{
    /// <summary>
    /// Creates the two platform profile assets in a Resources folder so
    /// BCaTPlatform can load them without a scene reference. Idempotent: an
    /// existing asset is left exactly as authored, so tuning a profile in the
    /// Inspector is never overwritten by a re-run.
    ///
    ///   Unity -executeMethod BCaT.EditorTools.BCaTPlatformProfileSetup.CreateMissingProfiles
    /// </summary>
    public static class BCaTPlatformProfileSetup
    {
        const string ResourcesRoot = "Assets/BCaT/ProductionCore/Platform/Resources";
        const string ProfileFolder = ResourcesRoot + "/" + BCaTPlatformProfile.ResourcesFolder;

        [MenuItem("BCaT/Architecture/Create Missing Platform Profiles")]
        public static void CreateMissingProfiles()
        {
            Directory.CreateDirectory(ProfileFolder);
            AssetDatabase.Refresh();

            Create(BCaTPlatformId.Desktop, "BCaTPlatformProfile_Desktop");
            Create(BCaTPlatformId.Quest, "BCaTPlatformProfile_Quest");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        static void Create(BCaTPlatformId id, string fileName)
        {
            string path = ProfileFolder + "/" + fileName + ".asset";
            if (File.Exists(path))
            {
                Debug.Log($"[BCaTPlatformProfileSetup] {path} already exists; left unchanged.");
                return;
            }

            BCaTPlatformProfile profile = BCaTPlatformProfile.CreateFallback(id);
            profile.name = fileName;
            AssetDatabase.CreateAsset(profile, path);
            Debug.Log($"[BCaTPlatformProfileSetup] Created {path}.");
        }
    }
}
