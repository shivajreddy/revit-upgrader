using RevitUpgrader.Core.Models;
using System.Diagnostics;
using System.IO;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace RevitUpgrader.Services;

/// <summary>
/// Service for automating Revit operations (launch, open files, close)
/// </summary>
public class RevitAutomationService : IDisposable
{
    private Process? _revitProcess;
    private readonly RevitVersion _targetVersion;
    private readonly string _revitExecutablePath;
    private UIA3Automation? _automation;

    public bool IsRevitRunning => _revitProcess != null && !_revitProcess.HasExited;

    public RevitAutomationService(RevitVersion targetVersion, string? customRevitPath = null)
    {
        _targetVersion = targetVersion;
        _revitExecutablePath = customRevitPath ?? _targetVersion.GetDefaultInstallPath();

        if (!File.Exists(_revitExecutablePath))
        {
            throw new FileNotFoundException(
                $"Revit executable not found at: {_revitExecutablePath}. " +
                $"Please ensure Revit {_targetVersion.GetYear()} is installed.");
        }

        _automation = new UIA3Automation();
    }

    /// <summary>
    /// Launch Revit application
    /// </summary>
    public async Task<bool> LaunchRevitAsync()
    {
        if (IsRevitRunning)
        {
            return true;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _revitExecutablePath,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            _revitProcess = Process.Start(startInfo);

            if (_revitProcess == null)
            {
                return false;
            }

            // Wait for Revit to be ready (wait for main window)
            await WaitForRevitToBeReadyAsync();

            return true;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to launch Revit: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Wait for Revit to fully launch and be ready
    /// </summary>
    private async Task WaitForRevitToBeReadyAsync(int timeoutSeconds = 60)
    {
        var startTime = DateTime.Now;
        
        while ((DateTime.Now - startTime).TotalSeconds < timeoutSeconds)
        {
            if (_revitProcess == null || _revitProcess.HasExited)
            {
                throw new Exception("Revit process terminated unexpectedly during startup");
            }

            // Check if main window is available
            _revitProcess.Refresh();
            if (_revitProcess.MainWindowHandle != IntPtr.Zero)
            {
                // Wait a bit more for Revit to fully initialize
                await Task.Delay(3000);
                return;
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Revit did not start within {timeoutSeconds} seconds");
    }

    /// <summary>
    /// Open a file in Revit using command line
    /// </summary>
    public async Task<bool> OpenFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        if (!IsRevitRunning)
        {
            throw new InvalidOperationException("Revit is not running. Call LaunchRevitAsync first.");
        }

        try
        {
            // Create a journal file to open the file
            var journalPath = await CreateOpenFileJournalAsync(filePath);

            // Execute the journal
            await ExecuteJournalAsync(journalPath);

            // Wait for file to open
            await Task.Delay(2000);

            // Clean up journal
            if (File.Exists(journalPath))
            {
                File.Delete(journalPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to open file {filePath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Close the currently open file in Revit
    /// </summary>
    public async Task<bool> CloseFileAsync(bool saveChanges = true)
    {
        if (!IsRevitRunning)
        {
            return false;
        }

        try
        {
            // Create a journal file to close the file
            var journalPath = await CreateCloseFileJournalAsync(saveChanges);

            // Execute the journal
            await ExecuteJournalAsync(journalPath);

            // Wait for file to close
            await Task.Delay(1000);

            // Clean up journal
            if (File.Exists(journalPath))
            {
                File.Delete(journalPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to close file: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Create a Revit journal file to open a specific file
    /// </summary>
    private async Task<string> CreateOpenFileJournalAsync(string filePath)
    {
        var journalPath = Path.Combine(Path.GetTempPath(), $"RevitUpgrader_Open_{Guid.NewGuid()}.txt");
        
        var journalContent = $@"' 0:< ' Journal created by RevitUpgrader
Jrn.Command ""Ribbon"" , ""Open file:ID_REVIT_FILE_OPEN""
Jrn.Data ""File Name"" , ""IDOK"" , ""{filePath}""
Jrn.Data ""WorksetConfig"" , ""All"" , 0
";

        await File.WriteAllTextAsync(journalPath, journalContent);
        return journalPath;
    }

    /// <summary>
    /// Create a Revit journal file to close the current file
    /// </summary>
    private async Task<string> CreateCloseFileJournalAsync(bool saveChanges)
    {
        var journalPath = Path.Combine(Path.GetTempPath(), $"RevitUpgrader_Close_{Guid.NewGuid()}.txt");
        
        var saveCommand = saveChanges ? "ID_REVIT_SAVE_AS_FAMILY" : "ID_FILE_CLOSE";
        
        var journalContent = $@"' 0:< ' Journal created by RevitUpgrader
Jrn.Command ""Ribbon"" , ""{saveCommand}""
";

        await File.WriteAllTextAsync(journalPath, journalContent);
        return journalPath;
    }

    /// <summary>
    /// Execute a Revit journal file
    /// Note: This is a simplified approach. Full implementation would need
    /// to launch Revit with journal file as parameter or use Revit API.
    /// </summary>
    private async Task ExecuteJournalAsync(string journalPath)
    {
        // For now, this is a placeholder
        // Real implementation would involve:
        // 1. Launching Revit with journal file parameter, OR
        // 2. Using UI automation to send File > Open commands, OR
        // 3. Using Revit API if available
        await Task.Delay(100);
    }

    /// <summary>
    /// Close Revit application
    /// </summary>
    public async Task CloseRevitAsync()
    {
        if (_revitProcess != null && !_revitProcess.HasExited)
        {
            try
            {
                // Try graceful close first
                _revitProcess.CloseMainWindow();
                
                // Wait up to 10 seconds for graceful exit
                if (!_revitProcess.WaitForExit(10000))
                {
                    // Force kill if needed
                    _revitProcess.Kill();
                }

                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to close Revit: {ex.Message}", ex);
            }
            finally
            {
                _revitProcess?.Dispose();
                _revitProcess = null;
            }
        }
    }

    /// <summary>
    /// Get the main Revit window for UI automation
    /// </summary>
    public AutomationElement? GetRevitMainWindow()
    {
        if (!IsRevitRunning || _revitProcess == null || _automation == null)
        {
            return null;
        }

        try
        {
            var window = _automation.FromHandle(_revitProcess.MainWindowHandle);
            return window;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _automation?.Dispose();
        _revitProcess?.Dispose();
    }
}
