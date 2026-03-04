namespace RevitUpgrader.Core.Models;

/// <summary>
/// Represents the processing status of a single Revit file
/// </summary>
public class FileStatus
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public RevitVersion DetectedVersion { get; set; } = RevitVersion.Unknown;
    public UpgradeStatus Status { get; set; } = UpgradeStatus.Pending;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration => EndTime.HasValue && StartTime.HasValue 
        ? EndTime.Value - StartTime.Value 
        : null;
    
    public List<PopupEncounter> PopupsEncountered { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SkipReason { get; set; }
    
    /// <summary>
    /// Indicates if user manually excluded this file from upgrade
    /// </summary>
    public bool IsExcluded { get; set; }
}

/// <summary>
/// Status of the upgrade process for a file
/// </summary>
public enum UpgradeStatus
{
    Pending,        // Not yet started
    InProgress,     // Currently being processed
    Success,        // Successfully upgraded and saved
    Skipped,        // Skipped due to unknown popup or user exclusion
    Failed,         // Failed due to error
    Excluded        // User manually excluded from upgrade
}

/// <summary>
/// Record of a popup that was encountered during file upgrade
/// </summary>
public class PopupEncounter
{
    public string PopupId { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
    public DateTime EncounteredAt { get; set; }
    public string ActionTaken { get; set; } = string.Empty;
    public string? ScreenshotPath { get; set; }
    public bool WasHandled { get; set; }
}
