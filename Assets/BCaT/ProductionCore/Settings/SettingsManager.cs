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
