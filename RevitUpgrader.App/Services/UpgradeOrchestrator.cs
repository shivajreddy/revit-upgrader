using RevitUpgrader.Core.Models;
using RevitUpgrader.Core.Utilities;
using RevitUpgrader.Logging;
using RevitUpgrader.PopupHandling;
using Newtonsoft.Json;
using System.IO;

namespace RevitUpgrader.Services;

/// <summary>
/// Main orchestrator for the upgrade process
/// Coordinates all services and manages the workflow
/// </summary>
public class UpgradeOrchestrator : IDisposable
{
    private readonly UpgradeLogger _logger;
    private readonly FileDiscoveryService _fileDiscovery;
    private RevitAutomationService? _revitAutomation;
    private PopupDetector? _popupDetector;
    private PopupMatchingService? _popupMatcher;
    private PopupActionExecutor? _popupActionExecutor;
    private ScreenshotCapture? _screenshotCapture;
    private PopupHandlerConfig? _popupConfig;

    private bool _isRunning;
    private CancellationTokenSource? _cancellationTokenSource;

    public UpgradeOrchestrator(UpgradeJobSettings settings)
    {
        _logger = new UpgradeLogger(settings.LogDirectory, settings.LogLevel);
        _fileDiscovery = new FileDiscoveryService();
        
        // Load popup handlers configuration
        LoadPopupHandlers(settings.PopupHandlersConfigPath);
        
        // Setup screenshot capture
        _screenshotCapture = new ScreenshotCapture(settings.ScreenshotDirectory);
        _popupActionExecutor = new PopupActionExecutor(_screenshotCapture);
    }

    /// <summary>
    /// Execute the upgrade job
    /// </summary>
    public async Task<UpgradeJob> ExecuteUpgradeAsync(
        UpgradeJob job,
        IProgress<UpgradeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            throw new InvalidOperationException("Upgrade is already running");
        }

        _isRunning = true;
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        job.StartTime = DateTime.Now;
        _logger.LogJobStarted(job);

        try
        {
            // Step 1: Discover files (if not already done)
            if (job.Files.Count == 0)
            {
                await DiscoverFilesAsync(job, progress);
            }

            // Step 2: Launch Revit
            await LaunchRevitAsync(job);

            // Step 3: Process each file
            await ProcessFilesAsync(job, progress, _cancellationTokenSource.Token);

            // Step 4: Close Revit
            await CloseRevitAsync();

            job.EndTime = DateTime.Now;
            _logger.LogJobCompleted(job);
        }
        catch (Exception ex)
        {
            _logger.LogError("Upgrade job failed with exception", ex);
            job.EndTime = DateTime.Now;
            throw;
        }
        finally
        {
            _isRunning = false;
        }

        return job;
    }

    /// <summary>
    /// Discover files for the job
    /// </summary>
    private async Task DiscoverFilesAsync(UpgradeJob job, IProgress<UpgradeProgress>? progress)
    {
        progress?.Report(new UpgradeProgress
        {
            Stage = UpgradeStage.Discovery,
            Message = "Discovering Revit files..."
        });

        var fileProgress = new Progress<FileDiscoveryProgress>(p =>
        {
            progress?.Report(new UpgradeProgress
            {
                Stage = UpgradeStage.Discovery,
                CurrentFile = p.CurrentFile,
                ProcessedCount = p.ProcessedCount,
                TotalCount = p.TotalCount,
                Message = p.Message
            });
        });

        job.Files = await _fileDiscovery.DiscoverFilesAsync(
            job.RootFolder,
            job.SourceVersion,
            fileProgress);

        _logger.LogInfo("File discovery completed: {Count} files found", job.Files.Count);
    }

    /// <summary>
    /// Launch Revit application
    /// </summary>
    private async Task LaunchRevitAsync(UpgradeJob job)
    {
        var revitPath = job.Settings.CustomRevitExecutablePath 
            ?? job.TargetVersion.GetDefaultInstallPath();

        _revitAutomation = new RevitAutomationService(job.TargetVersion, revitPath);
        
        _logger.LogInfo("Launching Revit {Version}...", job.TargetVersion.GetDisplayName());
        
        await _revitAutomation.LaunchRevitAsync();
        
        _logger.LogRevitLaunched(job.TargetVersion, revitPath);

        // Initialize popup detection
        if (_revitAutomation.IsRevitRunning)
        {
            _popupDetector = new PopupDetector(_revitAutomation.GetType()
                .GetField("_revitProcess", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .GetValue(_revitAutomation) as System.Diagnostics.Process 
                ?? throw new Exception("Could not get Revit process"));
        }
    }

    /// <summary>
    /// Process all files
    /// </summary>
    private async Task ProcessFilesAsync(
        UpgradeJob job, 
        IProgress<UpgradeProgress>? progress,
        CancellationToken cancellationToken)
    {
        var filesToProcess = job.Files.Where(f => !f.IsExcluded && f.Status == UpgradeStatus.Pending).ToList();
        
        for (int i = 0; i < filesToProcess.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Upgrade cancelled by user");
                break;
            }

            var file = filesToProcess[i];

            progress?.Report(new UpgradeProgress
            {
                Stage = UpgradeStage.Processing,
                CurrentFile = file.FilePath,
                ProcessedCount = i,
                TotalCount = filesToProcess.Count,
                Message = $"Processing {file.FileName}..."
            });

            await ProcessSingleFileAsync(file, job.Settings);
        }
    }

    /// <summary>
    /// Process a single file
    /// </summary>
    private async Task ProcessSingleFileAsync(FileStatus file, UpgradeJobSettings settings)
    {
        file.StartTime = DateTime.Now;
        file.Status = UpgradeStatus.InProgress;
        
        _logger.LogFileUpgradeStarted(file);

        try
        {
            // Step 1: Open file in Revit
            if (_revitAutomation == null)
            {
                throw new Exception("Revit automation service not initialized");
            }

            await _revitAutomation.OpenFileAsync(file.FilePath);
            
            // Step 2: Monitor for popups
            var popupMonitorResult = await MonitorAndHandlePopupsAsync(file, settings);

            if (!popupMonitorResult.Success)
            {
                // Unknown popup encountered - skip file
                file.Status = UpgradeStatus.Skipped;
                file.SkipReason = popupMonitorResult.SkipReason;
                _logger.LogFileSkipped(file, popupMonitorResult.SkipReason ?? "Unknown");
                
                // Close without saving
                await _revitAutomation.CloseFileAsync(saveChanges: false);
                return;
            }

            // Step 3: Save and close
            await _revitAutomation.CloseFileAsync(saveChanges: true);
            
            file.Status = UpgradeStatus.Success;
            file.EndTime = DateTime.Now;
            
            _logger.LogFileUpgradeCompleted(file);
        }
        catch (Exception ex)
        {
            file.Status = UpgradeStatus.Failed;
            file.ErrorMessage = ex.Message;
            file.EndTime = DateTime.Now;
            
            _logger.LogFileFailed(file, ex.Message);
        }
    }

    /// <summary>
    /// Monitor for popups and handle them
    /// </summary>
    private async Task<PopupMonitorResult> MonitorAndHandlePopupsAsync(
        FileStatus file, 
        UpgradeJobSettings settings)
    {
        var result = new PopupMonitorResult { Success = true };
        var startTime = DateTime.Now;
        var maxWaitTime = TimeSpan.FromSeconds(settings.MaxPopupWaitTimeSeconds);

        // Initial delay before checking
        await Task.Delay(settings.PopupDetectionDelayMs);

        while ((DateTime.Now - startTime) < maxWaitTime)
        {
            // Detect popups
            var popups = _popupDetector?.DetectPopups() ?? new List<DetectedPopup>();

            foreach (var popup in popups)
            {
                _logger.LogPopupDetected(popup);

                // Try to match to a handler
                var handler = _popupMatcher?.FindMatchingHandler(popup);

                if (handler == null)
                {
                    // Unknown popup - capture screenshot and skip file
                    var screenshot = _screenshotCapture?.CapturePopup(popup, "unknown_popup");
                    _logger.LogPopupUnknown(popup, screenshot);

                    result.Success = false;
                    result.SkipReason = $"Unknown popup: {popup.WindowTitle}";
                    
                    // Record the encounter
                    file.PopupsEncountered.Add(new PopupEncounter
                    {
                        PopupId = "unknown",
                        WindowTitle = popup.WindowTitle,
                        MessageText = popup.MessageText,
                        EncounteredAt = DateTime.Now,
                        ActionTaken = "File skipped",
                        ScreenshotPath = screenshot,
                        WasHandled = false
                    });

                    return result;
                }

                // Execute handler actions
                var actionResult = await _popupActionExecutor!.ExecuteActionsAsync(
                    popup, 
                    handler.Actions, 
                    handler.Id);

                // Capture screenshot if configured
                string? screenshotPath = null;
                if (handler.CaptureScreenshot)
                {
                    screenshotPath = _screenshotCapture?.CapturePopup(popup, handler.Id);
                }

                _logger.LogPopupHandled(handler.Id, popup, actionResult);

                // Record the encounter
                file.PopupsEncountered.Add(new PopupEncounter
                {
                    PopupId = handler.Id,
                    WindowTitle = popup.WindowTitle,
                    MessageText = popup.MessageText,
                    EncounteredAt = DateTime.Now,
                    ActionTaken = string.Join(", ", actionResult.ActionsExecuted),
                    ScreenshotPath = screenshotPath,
                    WasHandled = true
                });
            }

            // Wait before next check
            await Task.Delay(settings.PopupPollingIntervalMs);
        }

        return result;
    }

    /// <summary>
    /// Close Revit application
    /// </summary>
    private async Task CloseRevitAsync()
    {
        if (_revitAutomation != null)
        {
            await _revitAutomation.CloseRevitAsync();
            _logger.LogRevitClosed();
        }
    }

    /// <summary>
    /// Load popup handlers from configuration file
    /// </summary>
    private void LoadPopupHandlers(string configPath)
    {
        try
        {
            if (!File.Exists(configPath))
            {
                _logger.LogWarning("Popup handlers config not found: {Path}", configPath);
                _popupConfig = new PopupHandlerConfig();
                return;
            }

            var json = File.ReadAllText(configPath);
            _popupConfig = JsonConvert.DeserializeObject<PopupHandlerConfig>(json) 
                ?? new PopupHandlerConfig();

            _popupMatcher = new PopupMatchingService(_popupConfig.PopupHandlers);
            
            _logger.LogInfo("Loaded {Count} popup handlers from config", 
                _popupConfig.PopupHandlers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to load popup handlers config", ex);
            _popupConfig = new PopupHandlerConfig();
        }
    }

    /// <summary>
    /// Cancel the current upgrade operation
    /// </summary>
    public void CancelUpgrade()
    {
        _cancellationTokenSource?.Cancel();
    }

    public void Dispose()
    {
        _revitAutomation?.Dispose();
        _popupDetector?.Dispose();
        _logger?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}

/// <summary>
/// Progress information for upgrade process
/// </summary>
public class UpgradeProgress
{
    public UpgradeStage Stage { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    public int ProcessedCount { get; set; }
    public int TotalCount { get; set; }
    public string Message { get; set; } = string.Empty;
    public double PercentComplete => TotalCount > 0 ? (ProcessedCount * 100.0 / TotalCount) : 0;
}

public enum UpgradeStage
{
    Discovery,
    LaunchingRevit,
    Processing,
    Completed
}

/// <summary>
/// Result of popup monitoring
/// </summary>
internal class PopupMonitorResult
{
    public bool Success { get; set; }
    public string? SkipReason { get; set; }
}
