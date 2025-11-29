using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace HvsMvp.App
{
    /// <summary>
    /// PR15: Structured session logging service for traceability.
    /// Logs session start/end, analysis events, and parameters.
    /// </summary>
    public class SessionLoggerService : IDisposable
    {
        private readonly string _logsDirectory;
        private readonly long _maxLogFileSize;
        private readonly int _maxLogFiles;
        private string _currentLogPath;
        private StreamWriter? _logWriter;
        private readonly object _lock = new object();
        
        // Session state
        private Guid _currentSessionId;
        private DateTime _sessionStartUtc;
        private string _sessionMode = "";
        private OperationProfile _operationProfile;
        private bool _sessionActive;
        
        // Constants
        private const long DefaultMaxLogFileSize = 10 * 1024 * 1024; // 10 MB
        private const int DefaultMaxLogFiles = 10;
        
        public SessionLoggerService(AppSettings? settings = null)
        {
            _logsDirectory = settings?.LogsDirectory 
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            _maxLogFileSize = DefaultMaxLogFileSize;
            _maxLogFiles = DefaultMaxLogFiles;
            _currentLogPath = GetCurrentLogFilePath();
            
            EnsureLogDirectory();
        }
        
        /// <summary>
        /// Gets the current session ID, or Guid.Empty if no session is active.
        /// </summary>
        public Guid CurrentSessionId => _sessionActive ? _currentSessionId : Guid.Empty;
        
        /// <summary>
        /// Gets whether a session is currently active.
        /// </summary>
        public bool IsSessionActive => _sessionActive;
        
        /// <summary>
        /// Starts a new analysis/operation session.
        /// </summary>
        public SessionInfo StartSession(string mode, OperationProfile profile)
        {
            lock (_lock)
            {
                // End any existing session
                if (_sessionActive)
                {
                    EndSession("Replaced by new session");
                }
                
                _currentSessionId = Guid.NewGuid();
                _sessionStartUtc = DateTime.UtcNow;
                _sessionMode = mode;
                _operationProfile = profile;
                _sessionActive = true;
                
                var sessionInfo = new SessionInfo
                {
                    SessionId = _currentSessionId,
                    StartTimeUtc = _sessionStartUtc,
                    Mode = mode,
                    Profile = profile,
                    AppVersion = UpdateService.GetCurrentVersion()
                };
                
                LogEvent("SESSION_START", new Dictionary<string, object>
                {
                    ["sessionId"] = _currentSessionId.ToString(),
                    ["mode"] = mode,
                    ["profile"] = profile.ToString(),
                    ["appVersion"] = sessionInfo.AppVersion,
                    ["startTimeUtc"] = _sessionStartUtc.ToString("o")
                });
                
                return sessionInfo;
            }
        }
        
        /// <summary>
        /// Ends the current session.
        /// </summary>
        public void EndSession(string? reason = null)
        {
            lock (_lock)
            {
                if (!_sessionActive)
                    return;
                
                var endTimeUtc = DateTime.UtcNow;
                var duration = endTimeUtc - _sessionStartUtc;
                
                LogEvent("SESSION_END", new Dictionary<string, object>
                {
                    ["sessionId"] = _currentSessionId.ToString(),
                    ["endTimeUtc"] = endTimeUtc.ToString("o"),
                    ["durationSeconds"] = duration.TotalSeconds,
                    ["reason"] = reason ?? "Normal end"
                });
                
                _sessionActive = false;
            }
        }
        
        /// <summary>
        /// Logs an analysis event with parameters.
        /// </summary>
        public void LogAnalysis(AnalysisLogEntry entry)
        {
            lock (_lock)
            {
                var data = new Dictionary<string, object>
                {
                    ["sessionId"] = _currentSessionId.ToString(),
                    ["analysisId"] = entry.AnalysisId.ToString(),
                    ["imageSource"] = entry.ImageSource ?? "unknown",
                    ["cameraResolution"] = entry.CameraResolution ?? "N/A",
                    ["activeMasks"] = string.Join(",", entry.ActiveMasks ?? new List<string>()),
                    ["aiEnabled"] = entry.AiEnabled,
                    ["targetMaterial"] = entry.TargetMaterial ?? "N/A",
                    ["qualityIndex"] = entry.QualityIndex,
                    ["qualityStatus"] = entry.QualityStatus ?? "N/A",
                    ["metalsDetected"] = entry.MetalsDetected,
                    ["particlesDetected"] = entry.ParticlesDetected,
                    ["durationMs"] = entry.DurationMs
                };
                
                if (entry.AdditionalData != null)
                {
                    foreach (var kvp in entry.AdditionalData)
                    {
                        data[kvp.Key] = kvp.Value;
                    }
                }
                
                LogEvent("ANALYSIS", data);
            }
        }
        
        /// <summary>
        /// Logs a generic event.
        /// </summary>
        public void LogEvent(string eventType, Dictionary<string, object>? data = null)
        {
            lock (_lock)
            {
                EnsureLogWriter();
                RotateLogIfNeeded();
                
                var entry = new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    EventType = eventType,
                    Data = data ?? new Dictionary<string, object>()
                };
                
                try
                {
                    var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions 
                    { 
                        WriteIndented = false
                    });
                    _logWriter?.WriteLine(json);
                    _logWriter?.Flush();
                }
                catch
                {
                    // Silent fail - logging should never crash the app
                }
            }
        }
        
        /// <summary>
        /// Logs an error event.
        /// </summary>
        public void LogError(string context, Exception? ex = null)
        {
            LogEvent("ERROR", new Dictionary<string, object>
            {
                ["sessionId"] = _currentSessionId.ToString(),
                ["context"] = context,
                ["exception"] = ex?.Message ?? "N/A",
                ["exceptionType"] = ex?.GetType().Name ?? "N/A",
                ["stackTrace"] = ex?.StackTrace ?? "N/A"
            });
        }
        
        /// <summary>
        /// Logs a warning event.
        /// </summary>
        public void LogWarning(string message, Dictionary<string, object>? additionalData = null)
        {
            var data = new Dictionary<string, object>
            {
                ["sessionId"] = _currentSessionId.ToString(),
                ["message"] = message
            };
            
            if (additionalData != null)
            {
                foreach (var kvp in additionalData)
                {
                    data[kvp.Key] = kvp.Value;
                }
            }
            
            LogEvent("WARNING", data);
        }
        
        /// <summary>
        /// Exports logs for a date range to a file.
        /// </summary>
        public string ExportLogs(DateTime startDate, DateTime endDate, string format = "json")
        {
            var entries = new List<LogEntry>();
            
            // Read all log files and filter by date
            var logFiles = Directory.GetFiles(_logsDirectory, "hvs-session-*.log");
            foreach (var file in logFiles)
            {
                try
                {
                    var lines = File.ReadAllLines(file);
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        
                        try
                        {
                            var entry = JsonSerializer.Deserialize<LogEntry>(line);
                            if (entry != null && entry.Timestamp >= startDate && entry.Timestamp <= endDate)
                            {
                                entries.Add(entry);
                            }
                        }
                        catch
                        {
                            // Skip invalid lines
                        }
                    }
                }
                catch
                {
                    // Skip inaccessible files
                }
            }
            
            // Sort by timestamp
            entries.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
            
            // Export
            string exportPath = Path.Combine(_logsDirectory, $"export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{format}");
            
            if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            {
                ExportToCsv(entries, exportPath);
            }
            else
            {
                ExportToJson(entries, exportPath);
            }
            
            return exportPath;
        }
        
        /// <summary>
        /// Exports session logs for the current session.
        /// </summary>
        public string? ExportCurrentSessionLogs(string format = "json")
        {
            if (!_sessionActive)
                return null;
                
            var sessionId = _currentSessionId.ToString();
            var entries = new List<LogEntry>();
            
            // Read current log file and filter by session
            try
            {
                // Flush current writer first
                _logWriter?.Flush();
                
                var lines = File.ReadAllLines(_currentLogPath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    
                    try
                    {
                        var entry = JsonSerializer.Deserialize<LogEntry>(line);
                        if (entry?.Data != null && 
                            entry.Data.TryGetValue("sessionId", out var sid) && 
                            sid?.ToString() == sessionId)
                        {
                            entries.Add(entry);
                        }
                    }
                    catch
                    {
                        // Skip invalid lines
                    }
                }
            }
            catch
            {
                return null;
            }
            
            if (entries.Count == 0)
                return null;
            
            // Export
            string exportPath = Path.Combine(_logsDirectory, $"session_{sessionId.Substring(0, 8)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{format}");
            
            if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            {
                ExportToCsv(entries, exportPath);
            }
            else
            {
                ExportToJson(entries, exportPath);
            }
            
            return exportPath;
        }
        
        private void ExportToJson(List<LogEntry> entries, string path)
        {
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        
        private void ExportToCsv(List<LogEntry> entries, string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,EventType,SessionId,Data");
            
            foreach (var entry in entries)
            {
                string sessionId = entry.Data?.TryGetValue("sessionId", out var sid) == true 
                    ? sid?.ToString() ?? "" 
                    : "";
                string dataJson = entry.Data != null 
                    ? JsonSerializer.Serialize(entry.Data).Replace("\"", "\"\"") 
                    : "";
                
                sb.AppendLine($"\"{entry.Timestamp:o}\",\"{entry.EventType}\",\"{sessionId}\",\"{dataJson}\"");
            }
            
            File.WriteAllText(path, sb.ToString());
        }
        
        private void EnsureLogDirectory()
        {
            try
            {
                if (!Directory.Exists(_logsDirectory))
                {
                    Directory.CreateDirectory(_logsDirectory);
                }
            }
            catch
            {
                // Silent fail
            }
        }
        
        private string GetCurrentLogFilePath()
        {
            return Path.Combine(_logsDirectory, $"hvs-session-{DateTime.UtcNow:yyyyMMdd}.log");
        }
        
        private void EnsureLogWriter()
        {
            if (_logWriter == null || _currentLogPath != GetCurrentLogFilePath())
            {
                _logWriter?.Close();
                _logWriter?.Dispose();
                
                _currentLogPath = GetCurrentLogFilePath();
                _logWriter = new StreamWriter(_currentLogPath, append: true);
            }
        }
        
        private void RotateLogIfNeeded()
        {
            try
            {
                var fileInfo = new FileInfo(_currentLogPath);
                if (fileInfo.Exists && fileInfo.Length > _maxLogFileSize)
                {
                    _logWriter?.Close();
                    _logWriter?.Dispose();
                    
                    // Rename current log to archived
                    string archivedPath = Path.Combine(_logsDirectory, 
                        $"hvs-session-{DateTime.UtcNow:yyyyMMdd_HHmmss}.log.archived");
                    File.Move(_currentLogPath, archivedPath);
                    
                    // Create new log
                    _logWriter = new StreamWriter(_currentLogPath, append: true);
                    
                    // Clean up old archived logs
                    CleanupOldLogs();
                }
            }
            catch
            {
                // Silent fail
            }
        }
        
        private void CleanupOldLogs()
        {
            try
            {
                var logFiles = Directory.GetFiles(_logsDirectory, "*.log*")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .Skip(_maxLogFiles)
                    .ToList();
                
                foreach (var file in logFiles)
                {
                    try
                    {
                        file.Delete();
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
        
        public void Dispose()
        {
            lock (_lock)
            {
                if (_sessionActive)
                {
                    EndSession("Application closing");
                }
                
                _logWriter?.Flush();
                _logWriter?.Close();
                _logWriter?.Dispose();
                _logWriter = null;
            }
        }
    }
    
    /// <summary>
    /// PR15: Session information structure.
    /// </summary>
    public class SessionInfo
    {
        public Guid SessionId { get; set; }
        public DateTime StartTimeUtc { get; set; }
        public string Mode { get; set; } = "";
        public OperationProfile Profile { get; set; }
        public string AppVersion { get; set; } = "";
    }
    
    /// <summary>
    /// PR15: Log entry structure for structured logging.
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = "";
        public Dictionary<string, object>? Data { get; set; }
    }
    
    /// <summary>
    /// PR15: Analysis log entry for detailed analysis tracking.
    /// </summary>
    public class AnalysisLogEntry
    {
        public Guid AnalysisId { get; set; }
        public string? ImageSource { get; set; }
        public string? CameraResolution { get; set; }
        public List<string>? ActiveMasks { get; set; }
        public bool AiEnabled { get; set; }
        public string? TargetMaterial { get; set; }
        public double QualityIndex { get; set; }
        public string? QualityStatus { get; set; }
        public int MetalsDetected { get; set; }
        public int ParticlesDetected { get; set; }
        public long DurationMs { get; set; }
        public Dictionary<string, object>? AdditionalData { get; set; }
    }
}
