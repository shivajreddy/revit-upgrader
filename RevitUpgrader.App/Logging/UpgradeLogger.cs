using RevitUpgrader.Core.Models;
using RevitUpgrader.PopupHandling;
using Serilog;
using Serilog.Events;
using System.IO;

namespace RevitUpgrader.Logging;

/// <summary>
/// Structured logging service for upgrade operations
/// </summary>
public class UpgradeLogger : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _logDirectory;
    private readonly string _sessionId;

    public UpgradeLogger(string logDirectory, LogLevel logLevel = LogLevel.Info)
    {
        _logDirectory = logDirectory;
        _sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        // Ensure log directory exists
        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }

        // Configure Serilog
        var serilogLevel = ConvertLogLevel(logLevel);
        
        _logger = new LoggerConfiguration()
            .MinimumLevel.Is(serilogLevel)
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(_logDirectory, $"upgrade_{_sessionId}.txt"),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                new Serilog.Formatting.Json.JsonFormatter(),
                Path.Combine(_logDirectory, $"upgrade_{_sessionId}.json"))
            .Enrich.WithProperty("SessionId", _sessionId)
            .CreateLogger();

        Log.Logger = _logger;
    }

    #region Job-Level Events

    public void LogJobStarted(UpgradeJob job)
    {
        _logger.Information("Upgrade job started: {RootFolder}, {SourceVersion} → {TargetVersion}, {FileCount} files",
            job.RootFolder,
            job.SourceVersion.GetDisplayName(),
            job.TargetVersion.GetDisplayName(),
            job.TotalFiles);
    }

    public void LogJobCompleted(UpgradeJob job)
    {
        _logger.Information(
            "Upgrade job completed: Duration={Duration}, Succeeded={Succeeded}, Failed={Failed}, Skipped={Skipped}",
            job.TotalDuration?.ToString(@"hh\:mm\:ss"),
            job.FilesSucceeded,
            job.FilesFailed,
            job.FilesSkipped);
    }

    #endregion

    #region File-Level Events

    public void LogFileDiscovered(string filePath, RevitVersion detectedVersion, long fileSize)
    {
        _logger.Debug("File discovered: {FilePath}, Version={Version}, Size={Size}",
            filePath,
            detectedVersion.GetDisplayName(),
            FormatFileSize(fileSize));
    }

    public void LogFileUpgradeStarted(FileStatus file)
    {
        _logger.Information("Starting upgrade: {FileName} ({Version})",
            file.FileName,
            file.DetectedVersion.GetDisplayName());
    }

    public void LogFileUpgradeCompleted(FileStatus file)
    {
        _logger.Information("Upgrade completed: {FileName}, Duration={Duration}, Popups={PopupCount}",
            file.FileName,
            file.Duration?.ToString(@"mm\:ss"),
            file.PopupsEncountered.Count);
    }

    public void LogFileSkipped(FileStatus file, string reason)
    {
        _logger.Warning("File skipped: {FileName}, Reason={Reason}",
            file.FileName,
            reason);
    }

    public void LogFileFailed(FileStatus file, string error)
    {
        _logger.Error("File upgrade failed: {FileName}, Error={Error}",
            file.FileName,
            error);
    }

    #endregion

    #region Popup Events

    public void LogPopupDetected(DetectedPopup popup)
    {
        _logger.Debug("Popup detected: Title={Title}, Message={Message}, Buttons={Buttons}",
            popup.WindowTitle,
            popup.MessageText.Length > 100 ? popup.MessageText.Substring(0, 100) + "..." : popup.MessageText,
            string.Join(", ", popup.Buttons.Select(b => b.Text)));
    }

    public void LogPopupHandled(string popupId, DetectedPopup popup, PopupActionResult result)
    {
        _logger.Information("Popup handled: Id={PopupId}, Title={Title}, Actions={Actions}",
            popupId,
            popup.WindowTitle,
            string.Join(", ", result.ActionsExecuted));
    }

    public void LogPopupUnknown(DetectedPopup popup, string? screenshotPath)
    {
        _logger.Warning("Unknown popup encountered: Title={Title}, Message={Message}, Screenshot={Screenshot}",
            popup.WindowTitle,
            popup.MessageText,
            screenshotPath ?? "none");
    }

    #endregion

    #region Revit Events

    public void LogRevitLaunched(RevitVersion version, string executablePath)
    {
        _logger.Information("Revit launched: Version={Version}, Path={Path}",
            version.GetDisplayName(),
            executablePath);
    }

    public void LogRevitClosed()
    {
        _logger.Information("Revit closed");
    }

    #endregion

    #region General Events

    public void LogInfo(string message, params object[] args)
    {
        _logger.Information(message, args);
    }

    public void LogWarning(string message, params object[] args)
    {
        _logger.Warning(message, args);
    }

    public void LogError(string message, Exception? exception = null, params object[] args)
    {
        if (exception != null)
        {
            _logger.Error(exception, message, args);
        }
        else
        {
            _logger.Error(message, args);
        }
    }

    public void LogDebug(string message, params object[] args)
    {
        _logger.Debug(message, args);
    }

    #endregion

    #region Utilities

    private LogEventLevel ConvertLogLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Info => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            _ => LogEventLevel.Information
        };
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    #endregion

    public void Dispose()
    {
        Log.CloseAndFlush();
    }
}
