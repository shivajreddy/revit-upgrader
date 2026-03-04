using RevitUpgrader.Core.Models;
using FlaUI.Core.AutomationElements;
using System.Threading;

namespace RevitUpgrader.PopupHandling;

/// <summary>
/// Executes actions on detected popups
/// </summary>
public class PopupActionExecutor
{
    private readonly ScreenshotCapture _screenshotCapture;

    public PopupActionExecutor(ScreenshotCapture screenshotCapture)
    {
        _screenshotCapture = screenshotCapture;
    }

    /// <summary>
    /// Execute a sequence of actions on a popup
    /// </summary>
    public async Task<PopupActionResult> ExecuteActionsAsync(
        DetectedPopup popup, 
        List<PopupAction> actions,
        string popupId)
    {
        var result = new PopupActionResult
        {
            Success = true,
            PopupId = popupId,
            ActionsExecuted = new List<string>()
        };

        foreach (var action in actions)
        {
            try
            {
                var actionDescription = await ExecuteActionAsync(popup, action, popupId);
                result.ActionsExecuted.Add(actionDescription);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Failed to execute action {action.Type}: {ex.Message}";
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Execute a single action
    /// </summary>
    private async Task<string> ExecuteActionAsync(DetectedPopup popup, PopupAction action, string popupId)
    {
        switch (action.Type)
        {
            case PopupActionType.Click:
                return await ClickButtonAsync(popup, action.Target ?? "OK");

            case PopupActionType.Wait:
                await Task.Delay(action.Milliseconds);
                return $"Waited {action.Milliseconds}ms";

            case PopupActionType.Type:
                return await TypeTextAsync(popup, action.Target ?? "");

            case PopupActionType.Screenshot:
                var screenshotPath = _screenshotCapture.CapturePopup(popup, popupId);
                return $"Screenshot saved: {screenshotPath ?? "failed"}";

            case PopupActionType.Log:
                return $"Log: {action.Message ?? ""}";

            default:
                return $"Unknown action type: {action.Type}";
        }
    }

    /// <summary>
    /// Click a button on the popup
    /// </summary>
    private async Task<string> ClickButtonAsync(DetectedPopup popup, string buttonTarget)
    {
        // Try to find button by text
        var button = popup.Buttons.FirstOrDefault(b => 
            b.Text.Equals(buttonTarget, StringComparison.OrdinalIgnoreCase));

        if (button?.AutomationElement == null)
        {
            // Try to find by automation ID
            button = popup.Buttons.FirstOrDefault(b => 
                b.AutomationId.Equals(buttonTarget, StringComparison.OrdinalIgnoreCase));
        }

        if (button?.AutomationElement == null)
        {
            throw new Exception($"Button '{buttonTarget}' not found on popup");
        }

        if (!button.IsEnabled)
        {
            throw new Exception($"Button '{buttonTarget}' is disabled");
        }

        // Click the button
        button.AutomationElement.AsButton().Invoke();
        
        // Wait a bit for the click to process
        await Task.Delay(300);

        return $"Clicked button: {button.Text}";
    }

    /// <summary>
    /// Type text into a control on the popup
    /// </summary>
    private async Task<string> TypeTextAsync(DetectedPopup popup, string text)
    {
        if (popup.AutomationElement == null)
        {
            throw new Exception("Popup automation element not available");
        }

        // Find first text box / edit control
        var editControl = popup.AutomationElement.FindFirstDescendant(cf => 
            cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit));

        if (editControl == null)
        {
            throw new Exception("No text input control found on popup");
        }

        // Focus and type
        editControl.Focus();
        await Task.Delay(100);
        
        // Clear existing text
        editControl.AsTextBox().Text = text;
        
        await Task.Delay(200);

        return $"Typed text: {text}";
    }

    /// <summary>
    /// Close the popup window
    /// </summary>
    public async Task<bool> ClosePopupAsync(DetectedPopup popup)
    {
        try
        {
            if (popup.AutomationElement == null)
            {
                return false;
            }

            // Try to close using the window pattern
            var window = popup.AutomationElement.AsWindow();
            window?.Close();

            await Task.Delay(300);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Result of executing actions on a popup
/// </summary>
public class PopupActionResult
{
    public bool Success { get; set; }
    public string PopupId { get; set; } = string.Empty;
    public List<string> ActionsExecuted { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? ScreenshotPath { get; set; }
}
