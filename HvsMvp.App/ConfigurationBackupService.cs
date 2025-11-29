using System;
using System.IO;
using System.Text.Json;

namespace HvsMvp.App
{
    /// <summary>
    /// PR15: Service for exporting and importing application configuration.
    /// </summary>
    public class ConfigurationBackupService
    {
        private readonly string _backupDirectory;
        private const int MaxAutoBackups = 5;
        
        public ConfigurationBackupService(AppSettings? settings = null)
        {
            _backupDirectory = settings?.SessionsDirectory 
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
        }
        
        /// <summary>
        /// Exports all application settings to a JSON file.
        /// </summary>
        public string ExportConfiguration(AppSettings settings, string? customPath = null)
        {
            string exportPath = customPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"HvsMvp_Config_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            
            var exportData = new ConfigurationExport
            {
                ExportDate = DateTime.UtcNow,
                AppVersion = UpdateService.GetCurrentVersion(),
                Settings = settings
            };
            
            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            File.WriteAllText(exportPath, json);
            return exportPath;
        }
        
        /// <summary>
        /// Imports application settings from a JSON file.
        /// </summary>
        public (AppSettings? settings, string? error) ImportConfiguration(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return (null, "Arquivo não encontrado.");
            }
            
            try
            {
                var json = File.ReadAllText(filePath);
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var exportData = JsonSerializer.Deserialize<ConfigurationExport>(json, opts);
                
                if (exportData?.Settings == null)
                {
                    return (null, "Arquivo de configuração inválido ou corrompido.");
                }
                
                return (exportData.Settings, null);
            }
            catch (JsonException ex)
            {
                return (null, $"Erro ao processar JSON: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (null, $"Erro ao importar configuração: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Creates an automatic backup of current settings before major changes.
        /// </summary>
        public string? CreateAutoBackup(AppSettings settings)
        {
            try
            {
                EnsureBackupDirectory();
                
                string backupPath = Path.Combine(_backupDirectory, 
                    $"auto_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
                
                var exportData = new ConfigurationExport
                {
                    ExportDate = DateTime.UtcNow,
                    AppVersion = UpdateService.GetCurrentVersion(),
                    Settings = settings,
                    IsAutoBackup = true
                };
                
                var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                File.WriteAllText(backupPath, json);
                
                // Clean up old auto backups
                CleanupOldBackups();
                
                return backupPath;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Gets the list of available auto backups.
        /// </summary>
        public string[] GetAutoBackups()
        {
            try
            {
                EnsureBackupDirectory();
                return Directory.GetFiles(_backupDirectory, "auto_backup_*.json");
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
        
        /// <summary>
        /// Restores settings from the most recent auto backup.
        /// </summary>
        public (AppSettings? settings, string? error) RestoreLatestAutoBackup()
        {
            var backups = GetAutoBackups();
            if (backups.Length == 0)
            {
                return (null, "Nenhum backup automático encontrado.");
            }
            
            // Get most recent by filename (timestamp in name)
            Array.Sort(backups);
            Array.Reverse(backups);
            
            return ImportConfiguration(backups[0]);
        }
        
        private void EnsureBackupDirectory()
        {
            if (!Directory.Exists(_backupDirectory))
            {
                Directory.CreateDirectory(_backupDirectory);
            }
        }
        
        private void CleanupOldBackups()
        {
            try
            {
                var backups = Directory.GetFiles(_backupDirectory, "auto_backup_*.json");
                if (backups.Length <= MaxAutoBackups)
                    return;
                
                // Sort by name (contains timestamp)
                Array.Sort(backups);
                
                // Delete oldest backups
                int toDelete = backups.Length - MaxAutoBackups;
                for (int i = 0; i < toDelete; i++)
                {
                    try
                    {
                        File.Delete(backups[i]);
                    }
                    catch
                    {
                        // Skip files that can't be deleted
                    }
                }
            }
            catch
            {
                // Silent fail
            }
        }
    }
    
    /// <summary>
    /// PR15: Configuration export container.
    /// </summary>
    public class ConfigurationExport
    {
        public DateTime ExportDate { get; set; }
        public string AppVersion { get; set; } = "";
        public AppSettings? Settings { get; set; }
        public bool IsAutoBackup { get; set; }
    }
}
