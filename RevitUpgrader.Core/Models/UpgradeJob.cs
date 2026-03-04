namespace RevitUpgrader.Core.Models;

/// <summary>
/// Represents a complete upgrade job configuration
/// </summary>
public class UpgradeJob
{
    public string RootFolder { get; set; } = string.Empty;
    public RevitVersion SourceVersion { get; set; } = RevitVersion.Revit2024;
    public RevitVersion TargetVersion { get; set; } = RevitVersion.Revit2026;
    public List<FileStatus> Files { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? TotalDuration => EndTime.HasValue ? EndTime.Value - StartTime : null;
    
    public UpgradeJobSettings Settings { get; set; } = new();
    
    // Statistics
    public int TotalFiles => Files.Count;
    public int FilesSucceeded => Files.Count(f => f.Status == UpgradeStatus.Success);
    public int FilesSkipped => Files.Count(f => f.Status == UpgradeStatus.Skipped);
    public int FilesFailed => Files.Count(f => f.Status == UpgradeStatus.Failed);
    public int FilesExcluded => Files.Count(f => f.Status == UpgradeStatus.Excluded || f.IsExcluded);
    public int FilesPending => Files.Count(f => f.Status == UpgradeStatus.Pending);
}

/// <summary>
/// Settings for an upgrade job
/// </summary>
public class UpgradeJobSettings
{
    public string PopupHandlersConfigPath { get; set; } = "popup-handlers.json";
    public string LogDirectory { get; set; } = "Logs";
    public string ScreenshotDirectory { get; set; } = "Screenshots";
    public LogLevel LogLevel { get; set; } = LogLevel.Info;
    
    /// <summary>
    /// Custom Revit executable path (if null, auto-detects based on target version)
    /// </summary>
    public string? CustomRevitExecutablePath { get; set; }
    
    /// <summary>
    /// Time to wait after opening file before checking for popups (milliseconds)
    /// </summary>
    public int PopupDetectionDelayMs { get; set; } = 500;
    
    /// <summary>
    /// Polling interval for popup detection (milliseconds)
    /// </summary>
    public int PopupPollingIntervalMs { get; set; } = 200;
    
    /// <summary>
    /// Maximum time to wait for a popup to appear after opening file (seconds)
    /// </summary>
    public int MaxPopupWaitTimeSeconds { get; set; } = 30;
    
    /// <summary>
    /// Save upgraded files in place (overwrite originals)
    /// </summary>
    public bool SaveInPlace { get; set; } = true;
}

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}
