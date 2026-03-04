using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using System.Diagnostics;

namespace RevitUpgrader.PopupHandling;

/// <summary>
/// Detects popup windows and dialogs in Revit
/// </summary>
public class PopupDetector : IDisposable
{
    private readonly UIA3Automation _automation;
    private readonly Process _targetProcess;

    public PopupDetector(Process targetProcess)
    {
        _targetProcess = targetProcess ?? throw new ArgumentNullException(nameof(targetProcess));
        _automation = new UIA3Automation();
    }

    /// <summary>
    /// Detect all popup windows for the target process
    /// </summary>
    public List<DetectedPopup> DetectPopups()
    {
        var popups = new List<DetectedPopup>();

        try
        {
            if (_targetProcess.HasExited)
            {
                return popups;
            }

            // Get all windows owned by the process
            var desktop = _automation.GetDesktop();
            var allWindows = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));

            foreach (var window in allWindows)
            {
                try
                {
                    // Check if this window belongs to our process
                    var processId = window.Properties.ProcessId.ValueOrDefault;
                    if (processId != _targetProcess.Id)
                    {
                        continue;
                    }

                    // Skip the main Revit window
                    if (window.Name.Contains("Autodesk Revit") && window.Properties.BoundingRectangle.Value.Width > 800)
                    {
                        continue;
                    }

                    // This is likely a dialog/popup
                    var detectedPopup = ExtractPopupInfo(window);
                    if (detectedPopup != null)
                    {
                        popups.Add(detectedPopup);
                    }
                }
                catch
                {
                    // Skip windows we can't access
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but don't throw - popup detection failing shouldn't crash the app
            Debug.WriteLine($"Error detecting popups: {ex.Message}");
        }

        return popups;
    }

    /// <summary>
    /// Extract detailed information from a popup window
    /// </summary>
    private DetectedPopup? ExtractPopupInfo(AutomationElement window)
    {
        try
        {
            var popup = new DetectedPopup
            {
                WindowHandle = window.Properties.NativeWindowHandle.ValueOrDefault,
                WindowTitle = window.Name ?? string.Empty,
                AutomationId = window.Properties.AutomationId.ValueOrDefault ?? string.Empty,
                ClassName = window.Properties.ClassName.ValueOrDefault ?? string.Empty,
                BoundingRectangle = window.Properties.BoundingRectangle.ValueOrDefault
            };

            // Extract text content
            popup.MessageText = ExtractTextContent(window);

            // Extract button information
            popup.Buttons = ExtractButtons(window);

            // Store the automation element for later interaction
            popup.AutomationElement = window;

            return popup;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extract all text content from a window
    /// </summary>
    private string ExtractTextContent(AutomationElement window)
    {
        var textParts = new List<string>();

        try
        {
            // Find all text elements
            var textElements = window.FindAllDescendants(cf => 
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));

            foreach (var textElement in textElements)
            {
                var text = textElement.Name;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    textParts.Add(text);
                }
            }

            // Also check for edit controls (sometimes dialogs have text in edit boxes)
            var editElements = window.FindAllDescendants(cf => 
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit));

            foreach (var editElement in editElements)
            {
                var text = editElement.Name;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    textParts.Add(text);
                }
            }
        }
        catch
        {
            // Ignore errors in text extraction
        }

        return string.Join(" ", textParts);
    }

    /// <summary>
    /// Extract all buttons from a window
    /// </summary>
    private List<PopupButton> ExtractButtons(AutomationElement window)
    {
        var buttons = new List<PopupButton>();

        try
        {
            var buttonElements = window.FindAllDescendants(cf => 
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));

            foreach (var buttonElement in buttonElements)
            {
                var button = new PopupButton
                {
                    Text = buttonElement.Name ?? string.Empty,
                    AutomationId = buttonElement.Properties.AutomationId.ValueOrDefault ?? string.Empty,
                    IsEnabled = buttonElement.IsEnabled,
                    AutomationElement = buttonElement
                };

                buttons.Add(button);
            }
        }
        catch
        {
            // Ignore errors in button extraction
        }

        return buttons;
    }

    public void Dispose()
    {
        _automation?.Dispose();
    }
}

/// <summary>
/// Represents a detected popup window
/// </summary>
public class DetectedPopup
{
    public IntPtr WindowHandle { get; set; }
    public string WindowTitle { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
    public string AutomationId { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public System.Drawing.Rectangle BoundingRectangle { get; set; }
    public List<PopupButton> Buttons { get; set; } = new();
    public AutomationElement? AutomationElement { get; set; }
}

/// <summary>
/// Represents a button in a popup
/// </summary>
public class PopupButton
{
    public string Text { get; set; } = string.Empty;
    public string AutomationId { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public AutomationElement? AutomationElement { get; set; }
}
