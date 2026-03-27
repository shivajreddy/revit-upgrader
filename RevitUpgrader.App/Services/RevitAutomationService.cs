using RevitUpgrader.Core.Models;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
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

    // Win32 — force a window to the foreground regardless of focus-lock
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    private const int SW_RESTORE = 9;

    /// <summary>
    /// Reliably brings a window to the foreground by attaching our thread's
    /// input queue to the target window's thread before calling SetForegroundWindow.
    /// Plain SetForegroundWindow is blocked by Windows when called from a process
    /// that doesn't currently own the foreground.
    /// </summary>
    private void ForceForeground(IntPtr hwnd)
    {
        uint currentThread = GetCurrentThreadId();
        uint targetThread = GetWindowThreadProcessId(hwnd, out _);

        AttachThreadInput(currentThread, targetThread, true);
        ShowWindow(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);
        AttachThreadInput(currentThread, targetThread, false);
    }

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
    /// Launch a fresh Revit process dedicated to upgrading.
    /// Any Revit instances already open by the user are untouched.
    /// </summary>
    public async Task<bool> LaunchRevitAsync()
    {
        if (IsRevitRunning)
            return true;

        var startInfo = new ProcessStartInfo
        {
            FileName = _revitExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = false
        };

        _revitProcess = Process.Start(startInfo)
            ?? throw new Exception("Failed to start Revit process.");

        await WaitForRevitToBeReadyAsync();
        return true;
    }

    /// <summary>
    /// Poll until Revit's home screen is fully loaded and ready for input.
    /// We check MainWindowTitle for "Autodesk Revit" — this string only appears
    /// once the home screen is rendered, not during the splash screen.
    /// Polls every second for up to 2 minutes (Revit is slow to start).
    /// </summary>
    private async Task WaitForRevitToBeReadyAsync(int timeoutSeconds = 120)
    {
        var deadline = DateTime.Now.AddSeconds(timeoutSeconds);

        while (DateTime.Now < deadline)
        {
            if (_revitProcess == null || _revitProcess.HasExited)
                throw new Exception("Revit process terminated unexpectedly during startup.");

            _revitProcess.Refresh();

            var title = _revitProcess.MainWindowTitle;
            if (_revitProcess.MainWindowHandle != IntPtr.Zero &&
                title.Contains("Autodesk Revit", StringComparison.OrdinalIgnoreCase))
            {
                // Brief buffer for the ribbon to finish initialising
                await Task.Delay(2000);
                return;
            }

            await Task.Delay(1000); // check once per second
        }

        throw new TimeoutException($"Revit did not become ready within {timeoutSeconds} seconds.");
    }

    /// <summary>
    /// Open a .rvt file in our running Revit instance using FlaUI.
    /// Sends Ctrl+O, waits for Revit's file dialog to open, then types
    /// the path directly into the focused filename box and confirms.
    /// We don't search for the dialog via UIA because Autodesk's dialog
    /// is parented to the main window in a way that makes UIA enumeration
    /// unreliable. The filename box is focused by default when the dialog
    /// opens, so typing immediately after the delay is sufficient.
    /// </summary>
    public async Task<bool> OpenFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        if (!IsRevitRunning)
            throw new InvalidOperationException("Revit is not running. Call LaunchRevitAsync first.");

        // Bring our Revit window to the foreground.
        // FlaUI's Focus() is blocked by Windows focus-lock when called from another process,
        // so we use SetForegroundWindow directly via Win32.
        _revitProcess!.Refresh();
        var hwnd = _revitProcess.MainWindowHandle;
        if (hwnd == IntPtr.Zero)
            throw new Exception("Revit main window handle is not available — Revit may still be loading.");

        ForceForeground(hwnd);
        await Task.Delay(1000); // ensure Revit is in the foreground before sending keys

        // Ctrl+O → opens Revit's file dialog
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_O);

        // Wait for Revit's dialog to fully open.
        // Autodesk's dialog focuses the filename box by default (as seen in testing),
        // so we type straight into it without needing to locate it via UIA.
        await Task.Delay(2000);

        // Clear any pre-filled text, then type the full path
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        Keyboard.Type(filePath);
        await Task.Delay(300);

        // Press Enter to confirm — equivalent to clicking the "Open" button
        Keyboard.Type(VirtualKeyShort.RETURN);

        // Give Revit time to dismiss the dialog and begin loading the file
        await Task.Delay(1000);

        return true;
    }

    /// <summary>
    /// Save and/or close the currently open file.
    /// saveChanges = true  → Ctrl+S then Ctrl+W
    /// saveChanges = false → Ctrl+W, then dismiss any "save?" prompt with Don't Save
    /// </summary>
    public async Task<bool> CloseFileAsync(bool saveChanges = true)
    {
        if (!IsRevitRunning)
            return false;

        _revitProcess!.Refresh();
        var closeHwnd = _revitProcess.MainWindowHandle;
        if (closeHwnd != IntPtr.Zero)
            ForceForeground(closeHwnd);
        await Task.Delay(300);

        if (saveChanges)
        {
            // Ctrl+S — save (overwrites the original file with the upgraded version)
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            await Task.Delay(3000); // saving large files can take a moment
        }

        // Ctrl+W — close active document
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
        await Task.Delay(1000);

        // If Revit asks "Save changes?" and we chose not to save, press N to dismiss
        if (!saveChanges)
        {
            await Task.Delay(1000); // give the prompt time to appear
            Keyboard.Type(VirtualKeyShort.KEY_N); // "No" / "Don't Save"
            await Task.Delay(500);
        }

        return true;
    }

    /// <summary>
    /// Close the Revit process we spawned. Tries graceful shutdown first,
    /// force-kills if Revit doesn't respond within 10 seconds.
    /// </summary>
    public async Task CloseRevitAsync()
    {
        if (_revitProcess == null || _revitProcess.HasExited)
            return;

        try
        {
            _revitProcess.CloseMainWindow();

            if (!_revitProcess.WaitForExit(10000))
                _revitProcess.Kill();

            await Task.Delay(1000);
        }
        finally
        {
            _revitProcess?.Dispose();
            _revitProcess = null;
        }
    }

    /// <summary>
    /// Get the main window of our spawned Revit process via its window handle.
    /// </summary>
    public AutomationElement? GetRevitMainWindow()
    {
        if (!IsRevitRunning || _revitProcess == null || _automation == null)
            return null;

        try
        {
            return _automation.FromHandle(_revitProcess.MainWindowHandle);
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
