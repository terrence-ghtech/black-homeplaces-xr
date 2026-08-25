using UnityEditor;
using UnityEngine;

namespace BCaT.EditorTools
{
	public static class OpeningOnboardingIconImportSettings
	{
		private static readonly string[] IconPaths = new string[4] { "Assets/BCaT/ProductionCore/Shell/Resources/OpeningOnboardingIcons/onboarding_move.png", "Assets/BCaT/ProductionCore/Shell/Resources/OpeningOnboardingIcons/onboarding_look.png", "Assets/BCaT/ProductionCore/Shell/Resources/OpeningOnboardingIcons/onboarding_interact.png", "Assets/BCaT/ProductionCore/Shell/Resources/OpeningOnboardingIcons/onboarding_return.png" };

		[MenuItem("BCaT/Diagnostics/Apply Opening Onboarding Icon Import Settings")]
		public static void Apply()
		{
			string[] iconPaths = IconPaths;
			foreach (string text in iconPaths)
			{
				TextureImporter textureImporter = AssetImporter.GetAtPath(text) as TextureImporter;
				if (textureImporter == null)
				{
					Debug.LogWarning("[OpeningOnboardingIconImportSettings] Missing icon texture importer: " + text);
					continue;
				}
				textureImporter.textureType = TextureImporterType.Sprite;
				textureImporter.spriteImportMode = SpriteImportMode.Single;
				textureImporter.alphaIsTransparency = true;
				textureImporter.mipmapEnabled = false;
				textureImporter.wrapMode = TextureWrapMode.Clamp;
				textureImporter.textureCompression = TextureImporterCompression.CompressedHQ;
				textureImporter.maxTextureSize = 256;
				TextureImporterSettings textureImporterSettings = new TextureImporterSettings();
				textureImporter.ReadTextureSettings(textureImporterSettings);
				textureImporterSettings.spriteMeshType = SpriteMeshType.FullRect;
				textureImporter.SetTextureSettings(textureImporterSettings);
				textureImporter.SaveAndReimport();
				Debug.Log("[OpeningOnboardingIconImportSettings] Applied UI sprite import settings: " + text);
			}
			if (Application.isBatchMode)
			{
				EditorApplication.Exit(0);
			}
		}
	}
}
