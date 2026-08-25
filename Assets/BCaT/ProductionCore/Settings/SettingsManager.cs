using System;
using System.IO;
using UnityEngine;

namespace BCaT.Production.Settings
{
    /// <summary>
    /// Central settings service: durable versioned JSON persistence plus the
    /// single ApplyAll entry point that pushes settings into the runtime
    /// controllers. Exhibit scripts must not read or write PlayerPrefs for
    /// settings; everything goes through this class.
    /// </summary>
    public static class SettingsManager
    {
        static ApplicationSettingsData current;

        public static string SettingsDirectory =>
            Path.Combine(Application.persistentDataPath, "BCaT");

        public static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

        /// <summary>Raised after settings are applied so UI/services can restyle.</summary>
        public static event Action SettingsApplied;

        public static ApplicationSettingsData Current
        {
            get
            {
                if (current == null)
                    current = Load();
                return current;
            }
        }

        static ApplicationSettingsData Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var data = new ApplicationSettingsData();
                    JsonUtility.FromJsonOverwrite(json, data);
                    Migrate(data);
                    SanitizeUnsupported(data);
                    return data;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Settings] Failed to read settings ({e.Message}); " +
                               "backing up the corrupt file and using defaults.");
                TryBackupCorruptFile();
            }
            return new ApplicationSettingsData();
        }

        static void Migrate(ApplicationSettingsData data)
        {
            if (data.schemaVersion == ApplicationSettingsData.CurrentSchemaVersion)
                return;

            // Future schema migrations belong here, oldest first.
            Debug.Log($"[Settings] Migrating settings schema " +
                      $"{data.schemaVersion} -> {ApplicationSettingsData.CurrentSchemaVersion}.");
            data.schemaVersion = ApplicationSettingsData.CurrentSchemaVersion;
        }

        /// <summary>
        /// Fields that are still applied at runtime but no longer have a
        /// user-facing control are pinned to their defaults on load, so a
        /// persisted legacy value (e.g. an extreme render scale or text
        /// scale) can never leave the user stuck with no way back. Fields
        /// the settings UI still exposes are left untouched.
        /// </summary>
        static void SanitizeUnsupported(ApplicationSettingsData data)
        {
            var defaults = new ApplicationSettingsData();
            bool changed = false;

            changed |= data.display.vSyncCount != defaults.display.vSyncCount;
            data.display.vSyncCount = defaults.display.vSyncCount;
            changed |= data.display.targetFrameRate != defaults.display.targetFrameRate;
            data.display.targetFrameRate = defaults.display.targetFrameRate;
            changed |= data.display.displayIndex != defaults.display.displayIndex;
            data.display.displayIndex = defaults.display.displayIndex;

            changed |= data.graphics.renderScale != defaults.graphics.renderScale;
            data.graphics.renderScale = defaults.graphics.renderScale;
            changed |= data.graphics.textureQuality != defaults.graphics.textureQuality;
            data.graphics.textureQuality = defaults.graphics.textureQuality;
            changed |= data.graphics.antiAliasing != defaults.graphics.antiAliasing;
            data.graphics.antiAliasing = defaults.graphics.antiAliasing;
            changed |= data.graphics.ambientEffects != defaults.graphics.ambientEffects;
            data.graphics.ambientEffects = defaults.graphics.ambientEffects;
            changed |= data.graphics.vegetationDistanceScale != defaults.graphics.vegetationDistanceScale;
            data.graphics.vegetationDistanceScale = defaults.graphics.vegetationDistanceScale;

            changed |= data.audio.narration != defaults.audio.narration;
            data.audio.narration = defaults.audio.narration;
            changed |= data.audio.ambience != defaults.audio.ambience;
            data.audio.ambience = defaults.audio.ambience;
            changed |= data.audio.effects != defaults.audio.effects;
            data.audio.effects = defaults.audio.effects;
            changed |= data.audio.media != defaults.audio.media;
            data.audio.media = defaults.audio.media;

            changed |= data.accessibility.textSize != defaults.accessibility.textSize;
            changed |= data.accessibility.subtitles != defaults.accessibility.subtitles;
            changed |= data.accessibility.highContrastUi != defaults.accessibility.highContrastUi;
            changed |= data.accessibility.reducedMotion != defaults.accessibility.reducedMotion;
            changed |= data.accessibility.persistentPrompts != defaults.accessibility.persistentPrompts;
            data.accessibility = defaults.accessibility;

            if (changed)
                Debug.Log("[Settings] Reset persisted values for settings without a user-facing control to defaults.");
        }

        static void TryBackupCorruptFile()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    File.Copy(SettingsPath, SettingsPath + ".corrupt", overwrite: true);
            }
            catch { /* backup is best-effort */ }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                File.WriteAllText(SettingsPath, JsonUtility.ToJson(Current, true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Settings] Failed to save settings: {e.Message}");
            }
        }

        public static void ResetToDefaults()
        {
            current = new ApplicationSettingsData();
            Save();
            ApplyAll();
        }

        /// <summary>
        /// Apply every settings section to the running application. Safe to call
        /// at startup, after scene loads, and whenever the user changes a value.
        /// </summary>
        public static void ApplyAll()
        {
            var s = Current;

            // Kiosk mode locks the quality tier to the administrator's choice.
            if (ApplicationModeService.IsKiosk &&
                !string.IsNullOrEmpty(ApplicationModeService.Kiosk.fixedQualityTier))
            {
                s.graphics.qualityTier = ApplicationModeService.Kiosk.fixedQualityTier;
            }

            try { DisplaySettingsController.Apply(s.display); }
            catch (Exception e) { Debug.LogError($"[Settings] Display apply failed: {e}"); }

            try { GraphicsSettingsController.Apply(s.graphics); }
            catch (Exception e) { Debug.LogError($"[Settings] Graphics apply failed: {e}"); }

            try { AudioChannelService.Apply(s.audio); }
            catch (Exception e) { Debug.LogError($"[Settings] Audio apply failed: {e}"); }

            try { ControlSettingsController.Apply(s.controls); }
            catch (Exception e) { Debug.LogError($"[Settings] Controls apply failed: {e}"); }

            SettingsApplied?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            current = null;
            SettingsApplied = null;
        }
    }
}
