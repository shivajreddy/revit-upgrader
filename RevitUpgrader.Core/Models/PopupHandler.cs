namespace RevitUpgrader.Core.Models;

/// <summary>
/// Configuration for handling specific popup dialogs
/// </summary>
public class PopupHandlerConfig
{
    public List<PopupHandler> PopupHandlers { get; set; } = new();
    public UnknownPopupBehavior UnknownPopupBehavior { get; set; } = new();
}

/// <summary>
/// Defines how to identify and handle a specific popup
/// </summary>
public class PopupHandler
{
    public string Id { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 100;
    public PopupMatcher Matchers { get; set; } = new();
    public List<PopupAction> Actions { get; set; } = new();
    public bool CaptureScreenshot { get; set; } = true;
}

/// <summary>
/// Criteria for matching a popup dialog
/// </summary>
public class PopupMatcher
{
    public string? WindowTitle { get; set; }
    public string? WindowTitleRegex { get; set; }
    public string? MessageContains { get; set; }
    public string? MessageRegex { get; set; }
    public List<string>? Buttons { get; set; }
    public string? ControlId { get; set; }
}

/// <summary>
/// Action to take when popup is matched
/// </summary>
public class PopupAction
{
    public PopupActionType Type { get; set; }
    public string? Target { get; set; }
    public int Milliseconds { get; set; }
    public string? Message { get; set; }
}

public enum PopupActionType
{
    Click,      // Click a button
    Wait,       // Wait for specified time
    Type,       // Type text
    Log,        // Log a message
    Screenshot  // Take screenshot
}

/// <summary>
/// Behavior when encountering an unknown popup
/// </summary>
public class UnknownPopupBehavior
{
    public UnknownPopupAction Action { get; set; } = UnknownPopupAction.Skip;
    public bool CaptureScreenshot { get; set; } = true;
    public bool LogDetails { get; set; } = true;
}

public enum UnknownPopupAction
{
    Skip,       // Skip the file and continue
    Pause,      // Pause and alert user
    Abort       // Stop entire process
}
