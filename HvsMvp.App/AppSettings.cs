using System;
using System.IO;
using System.Text.Json;

namespace HvsMvp.App
{
    /// <summary>
    /// Application settings that are persisted to JSON file.
    /// </summary>
    public class AppSettings
    {
        private static readonly string SettingsFileName = "app-settings.json";
        private static string SettingsFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);

        // ===== Geral (General) =====
        public string DefaultImagesDirectory { get; set; } = "";
        public string ReportsDirectory { get; set; } = "";
        public string SessionsDirectory { get; set; } = "";
        public string LogsDirectory { get; set; } = "";

        // ===== Câmera (Camera) =====
        public int DefaultCameraIndex { get; set; } = 0;
        public string PreferredResolution { get; set; } = "1920x1080";

        // ===== Análise (Analysis) =====
        public double MaskSensitivity { get; set; } = 0.3;
        public double FocusThreshold { get; set; } = 0.15;
        public double ClippingThreshold { get; set; } = 0.025;
        public bool StrongWarningsEnabled { get; set; } = false;

        // ===== Atualizações (Updates) =====
        public bool AutoUpdateCheckEnabled { get; set; } = true;
        public int UpdateCheckFrequencyHours { get; set; } = 24;
        public DateTime? LastUpdateCheck { get; set; }
        public string LastUpdateResult { get; set; } = "NotChecked";
        public string? LatestVersionFound { get; set; }

        // ===== Perfil (Profile) =====
        public string LabName { get; set; } = "Trilha Gold Capital";
        public string LogoPath { get; set; } = "";
        public string DefaultOperator { get; set; } = "";
        public string WhatsAppContact { get; set; } = "";

        /// <summary>
        /// Load settings from JSON file. If file doesn't exist, returns default settings.
        /// </summary>
        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, opts);
                    return settings ?? CreateDefault();
                }
            }
            catch
            {
                // If load fails, return defaults
            }

            return CreateDefault();
        }

        /// <summary>
        /// Save settings to JSON file.
        /// </summary>
        public void Save()
        {
            try
            {
                var opts = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(this, opts);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
                // Silent fail on save error
            }
        }

        /// <summary>
        /// Create default settings with sensible defaults.
        /// </summary>
        public static AppSettings CreateDefault()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return new AppSettings
            {
                DefaultImagesDirectory = Path.Combine(baseDir, "images"),
                ReportsDirectory = Path.Combine(baseDir, "exports"),
                SessionsDirectory = Path.Combine(baseDir, "sessions"),
                LogsDirectory = Path.Combine(baseDir, "logs"),
                DefaultCameraIndex = 0,
                PreferredResolution = "1920x1080",
                MaskSensitivity = 0.3,
                FocusThreshold = 0.15,
                ClippingThreshold = 0.025,
                StrongWarningsEnabled = false,
                AutoUpdateCheckEnabled = true,
                UpdateCheckFrequencyHours = 24,
                LastUpdateCheck = null,
                LastUpdateResult = "NotChecked",
                LatestVersionFound = null,
                LabName = "Trilha Gold Capital",
                LogoPath = "",
                DefaultOperator = "",
                WhatsAppContact = ""
            };
        }

        /// <summary>
        /// Reset all settings to defaults.
        /// </summary>
        public void RestoreDefaults()
        {
            var defaults = CreateDefault();
            DefaultImagesDirectory = defaults.DefaultImagesDirectory;
            ReportsDirectory = defaults.ReportsDirectory;
            SessionsDirectory = defaults.SessionsDirectory;
            LogsDirectory = defaults.LogsDirectory;
            DefaultCameraIndex = defaults.DefaultCameraIndex;
            PreferredResolution = defaults.PreferredResolution;
            MaskSensitivity = defaults.MaskSensitivity;
            FocusThreshold = defaults.FocusThreshold;
            ClippingThreshold = defaults.ClippingThreshold;
            StrongWarningsEnabled = defaults.StrongWarningsEnabled;
            AutoUpdateCheckEnabled = defaults.AutoUpdateCheckEnabled;
            UpdateCheckFrequencyHours = defaults.UpdateCheckFrequencyHours;
            LastUpdateCheck = defaults.LastUpdateCheck;
            LastUpdateResult = defaults.LastUpdateResult;
            LatestVersionFound = defaults.LatestVersionFound;
            LabName = defaults.LabName;
            LogoPath = defaults.LogoPath;
            DefaultOperator = defaults.DefaultOperator;
            WhatsAppContact = defaults.WhatsAppContact;
        }

        /// <summary>
        /// Get resolution width from PreferredResolution string.
        /// </summary>
        public int GetResolutionWidth()
        {
            var parts = PreferredResolution?.Split('x');
            if (parts != null && parts.Length == 2 && int.TryParse(parts[0], out int w))
                return w;
            return 1920;
        }

        /// <summary>
        /// Get resolution height from PreferredResolution string.
        /// </summary>
        public int GetResolutionHeight()
        {
            var parts = PreferredResolution?.Split('x');
            if (parts != null && parts.Length == 2 && int.TryParse(parts[1], out int h))
                return h;
            return 1080;
        }
    }
}
