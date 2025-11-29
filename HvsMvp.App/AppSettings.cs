using System;
using System.Collections.Generic;
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
        public string BiExportDirectory { get; set; } = "";
        public string DatasetIaDirectory { get; set; } = "";

        // ===== Laudo (Report) =====
        public bool GeneratePdfByDefault { get; set; } = false;
        public bool GenerateTxtByDefault { get; set; } = true;
        public string DefaultSampleName { get; set; } = "";
        public string DefaultClientProject { get; set; } = "";

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

        // ===== Interface (UI) =====
        /// <summary>
        /// PR10: If true, skips the welcome screen on startup (operator mode).
        /// </summary>
        public bool SkipWelcomeScreen { get; set; } = false;

        /// <summary>
        /// PR15: Operation profile (Basic or Advanced).
        /// </summary>
        public OperationProfile CurrentProfile { get; set; } = OperationProfile.Basic;

        /// <summary>
        /// PR15: If true, shows checklist before Live/Analysis operations.
        /// </summary>
        public bool ShowPreOperationChecklist { get; set; } = true;

        /// <summary>
        /// PR15: If true, enables structured session logging.
        /// </summary>
        public bool EnableSessionLogging { get; set; } = true;

        // ===== PR16: New Settings =====

        /// <summary>
        /// PR16: Current image preset for brightness/contrast/etc.
        /// </summary>
        public string ImagePreset { get; set; } = "Standard";

        /// <summary>
        /// PR16: Custom brightness value (-1.0 to 1.0).
        /// </summary>
        public double CustomBrightness { get; set; } = 0;

        /// <summary>
        /// PR16: Custom contrast value (-1.0 to 1.0).
        /// </summary>
        public double CustomContrast { get; set; } = 0;

        /// <summary>
        /// PR16: Custom gamma value (0.1 to 3.0).
        /// </summary>
        public double CustomGamma { get; set; } = 1.0;

        /// <summary>
        /// PR16: Custom saturation value (-1.0 to 1.0).
        /// </summary>
        public double CustomSaturation { get; set; } = 0;

        /// <summary>
        /// PR16: Whether UV mode is enabled by default.
        /// </summary>
        public bool UvModeEnabled { get; set; } = false;

        /// <summary>
        /// PR16: Default UV mode type (when enabled).
        /// </summary>
        public string UvModeType { get; set; } = "Simulated";

        /// <summary>
        /// PR16: Whether to show ROI selection controls.
        /// </summary>
        public bool ShowRoiControls { get; set; } = true;

        /// <summary>
        /// PR16: Whether to auto-apply image adjustments.
        /// </summary>
        public bool AutoApplyImageAdjustments { get; set; } = true;

        /// <summary>
        /// PR16: Default zoom level (1.0 = 100%).
        /// </summary>
        public double DefaultZoomLevel { get; set; } = 1.0;

        /// <summary>
        /// PR16: Confidence threshold for metal detection (higher = stricter).
        /// </summary>
        public double MetalConfidenceThreshold { get; set; } = 0.5;

        /// <summary>
        /// PR13: Recent files list for quick access to previously analyzed images.
        /// Maximum 10 files stored.
        /// </summary>
        public List<string> RecentFiles { get; set; } = new List<string>();

        /// <summary>
        /// PR13: Maximum number of recent files to store.
        /// </summary>
        private const int MaxRecentFiles = 10;

        /// <summary>
        /// PR13: Add a file to the recent files list.
        /// </summary>
        public void AddRecentFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            // Remove if already exists (will be moved to top)
            RecentFiles.RemoveAll(f => string.Equals(f, filePath, StringComparison.OrdinalIgnoreCase));
            
            // Insert at beginning
            RecentFiles.Insert(0, filePath);
            
            // Trim to max size
            while (RecentFiles.Count > MaxRecentFiles)
            {
                RecentFiles.RemoveAt(RecentFiles.Count - 1);
            }
        }

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
                BiExportDirectory = Path.Combine(baseDir, "exports", "bi"),
                DatasetIaDirectory = Path.Combine(baseDir, "exports", "dataset-ia"),
                GeneratePdfByDefault = false,
                GenerateTxtByDefault = true,
                DefaultSampleName = "",
                DefaultClientProject = "",
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
                WhatsAppContact = "",
                SkipWelcomeScreen = false,
                CurrentProfile = OperationProfile.Basic,
                ShowPreOperationChecklist = true,
                EnableSessionLogging = true,
                // PR16 defaults
                ImagePreset = "Standard",
                CustomBrightness = 0,
                CustomContrast = 0,
                CustomGamma = 1.0,
                CustomSaturation = 0,
                UvModeEnabled = false,
                UvModeType = "Simulated",
                ShowRoiControls = true,
                AutoApplyImageAdjustments = true,
                DefaultZoomLevel = 1.0,
                MetalConfidenceThreshold = 0.5
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
            BiExportDirectory = defaults.BiExportDirectory;
            DatasetIaDirectory = defaults.DatasetIaDirectory;
            GeneratePdfByDefault = defaults.GeneratePdfByDefault;
            GenerateTxtByDefault = defaults.GenerateTxtByDefault;
            DefaultSampleName = defaults.DefaultSampleName;
            DefaultClientProject = defaults.DefaultClientProject;
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
            SkipWelcomeScreen = defaults.SkipWelcomeScreen;
            CurrentProfile = defaults.CurrentProfile;
            ShowPreOperationChecklist = defaults.ShowPreOperationChecklist;
            EnableSessionLogging = defaults.EnableSessionLogging;
            // PR16 defaults
            ImagePreset = defaults.ImagePreset;
            CustomBrightness = defaults.CustomBrightness;
            CustomContrast = defaults.CustomContrast;
            CustomGamma = defaults.CustomGamma;
            CustomSaturation = defaults.CustomSaturation;
            UvModeEnabled = defaults.UvModeEnabled;
            UvModeType = defaults.UvModeType;
            ShowRoiControls = defaults.ShowRoiControls;
            AutoApplyImageAdjustments = defaults.AutoApplyImageAdjustments;
            DefaultZoomLevel = defaults.DefaultZoomLevel;
            MetalConfidenceThreshold = defaults.MetalConfidenceThreshold;
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
