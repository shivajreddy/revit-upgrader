using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace RevitUpgrader.PopupHandling;

/// <summary>
/// Captures screenshots of popup windows
/// </summary>
public class ScreenshotCapture
{
    private readonly string _screenshotDirectory;

    public ScreenshotCapture(string screenshotDirectory)
    {
        _screenshotDirectory = screenshotDirectory;
        
        // Ensure directory exists
        if (!Directory.Exists(_screenshotDirectory))
        {
            Directory.CreateDirectory(_screenshotDirectory);
        }
    }

    /// <summary>
    /// Capture a screenshot of a specific window
    /// </summary>
    public string? CaptureWindow(IntPtr windowHandle, string fileName)
    {
        try
        {
            // Get window rectangle
            if (!GetWindowRect(windowHandle, out RECT rect))
            {
                return null;
            }

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            // Capture the window
            using var bitmap = new Bitmap(width, height);
            using var graphics = Graphics.FromImage(bitmap);
            
            graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));

            // Save to file
            var filePath = Path.Combine(_screenshotDirectory, fileName);
            bitmap.Save(filePath, ImageFormat.Png);

            return filePath;
        }
        catch (Exception ex)
        {
            // Log error but don't throw - screenshot failure shouldn't stop the process
            System.Diagnostics.Debug.WriteLine($"Failed to capture screenshot: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Capture a screenshot of a detected popup
    /// </summary>
    public string? CapturePopup(DetectedPopup popup, string fileNamePrefix)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = $"{fileNamePrefix}_{timestamp}.png";
        
        return CaptureWindow(popup.WindowHandle, fileName);
    }

    /// <summary>
    /// Generate a screenshot filename for a popup
    /// </summary>
    public static string GenerateFileName(string popupId, string? context = null)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var contextPart = string.IsNullOrEmpty(context) ? "" : $"_{context}";
        return $"{popupId}{contextPart}_{timestamp}.png";
    }

    #region Win32 Imports

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    #endregion
}
