namespace RevitUpgrader.Core.Models;

/// <summary>
/// Structured log entry for upgrade events
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogLevel Level { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, object>? Details { get; set; }
    public string? Exception { get; set; }
}

/// <summary>
/// Common event types for logging
/// </summary>
public static class LogEventTypes
{
    public const string JobStarted = "JobStarted";
    public const string JobCompleted = "JobCompleted";
    public const string FileDiscovered = "FileDiscovered";
    public const string FileUpgradeStarted = "FileUpgradeStarted";
    public const string FileUpgradeCompleted = "FileUpgradeCompleted";
    public const string FileSkipped = "FileSkipped";
    public const string FileFailed = "FileFailed";
    public const string PopupDetected = "PopupDetected";
    public const string PopupHandled = "PopupHandled";
    public const string PopupUnknown = "PopupUnknown";
    public const string RevitLaunched = "RevitLaunched";
    public const string RevitClosed = "RevitClosed";
    public const string Error = "Error";
}
