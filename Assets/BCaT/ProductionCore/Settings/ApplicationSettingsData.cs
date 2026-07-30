using System;
using UnityEngine;

namespace BCaT.Production.Settings
{
    /// <summary>
    /// The complete serializable settings model (ApplicationSettings +
    /// SettingsDefaults of the production architecture). Persisted as JSON under
    /// Application.persistentDataPath/BCaT/settings.json by SettingsManager.
    /// Field defaults double as the reset-to-default values.
    ///
    /// Deliberately excludes player progress or session-resume state — only
    /// device/user preferences are persisted.
    /// </summary>
    [Serializable]
    public class ApplicationSettingsData
    {
        /// <summary>Increment when the schema changes; SettingsManager migrates on load.</summary>
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;

        public DisplaySettings display = new DisplaySettings();
        public GraphicsSettings graphics = new GraphicsSettings();
        public AudioSettings audio = new AudioSettings();
        public ControlSettings controls = new ControlSettings();
        public AccessibilitySettings accessibility = new AccessibilitySettings();

        [Serializable]
        public class DisplaySettings
        {
            [Tooltip("-1 means 'use the display's current resolution'.")]
            public int width = -1;
            public int height = -1;
            public bool fullscreen = true;
            public int vSyncCount = 1;
            [Tooltip("<= 0 disables the frame-rate cap (vSync then governs pacing).")]
            public int targetFrameRate = -1;
            [Tooltip("Display index for multi-monitor desktops; 0 is the primary display.")]
            public int displayIndex = 0;
        }

        [Serializable]
        public class GraphicsSettings
        {
            [Tooltip("Quality tier name: Desktop Low, Desktop Standard, Desktop High (Quest is fixed on device).")]
            public string qualityTier = "Desktop Standard";

            [Tooltip("Render scale multiplier applied to the tier's pipeline asset. 1 = tier default.")]
            public float renderScale = 1.0f;

            [Tooltip("Shadow distance multiplier applied to the tier default. 1 = tier default.")]
            public float shadowDistanceScale = 1.0f;

            [Tooltip("0 = full resolution textures, 1 = half, 2 = quarter.")]
            public int textureQuality = 0;

            [Tooltip("MSAA samples: 0/2/4. -1 = tier default.")]
            public int antiAliasing = -1;

            [Tooltip("Post-processing / ambient effects on the player camera.")]
            public bool ambientEffects = true;

            [Tooltip("Terrain basemap/detail distance multiplier. 1 = tier default.")]
            public float terrainDistanceScale = 1.0f;

            [Tooltip("Vegetation (tree/detail) distance multiplier. 1 = tier default.")]
            public float vegetationDistanceScale = 1.0f;
        }

        [Serializable]
        public class AudioSettings
        {
            [Range(0f, 1f)] public float master = 1.0f;
            [Range(0f, 1f)] public float narration = 1.0f;
            [Range(0f, 1f)] public float ambience = 1.0f;
            [Range(0f, 1f)] public float effects = 1.0f;
            [Range(0f, 1f)] public float media = 1.0f;
        }

        [Serializable]
        public class ControlSettings
        {
            [Tooltip("Mouse look sensitivity multiplier (0.2–3).")]
            public float mouseSensitivity = 1.0f;
            public bool invertY = false;
        }

        [Serializable]
        public class AccessibilitySettings
        {
            public bool subtitles = false;

            [Tooltip("0 = normal, 1 = large, 2 = extra large.")]
            public int textSize = 0;

            public bool highContrastUi = false;
            public bool reducedMotion = false;

            [Tooltip("Keep interaction prompts visible with a relaxed focus requirement.")]
            public bool persistentPrompts = false;

            public float TextScaleFactor => textSize switch
            {
                1 => 1.25f,
                2 => 1.5f,
                _ => 1f,
            };
        }
    }
}
