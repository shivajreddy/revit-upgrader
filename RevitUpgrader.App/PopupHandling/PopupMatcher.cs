using RevitUpgrader.Core.Models;
using System.Text.RegularExpressions;

namespace RevitUpgrader.PopupHandling;

/// <summary>
/// Matches detected popups against configured handlers
/// </summary>
public class PopupMatchingService
{
    private readonly List<PopupHandler> _handlers;

    public PopupMatchingService(List<PopupHandler> handlers)
    {
        _handlers = handlers?.Where(h => h.Enabled).OrderBy(h => h.Priority).ToList() 
            ?? new List<PopupHandler>();
    }

    /// <summary>
    /// Find the best matching handler for a detected popup
    /// </summary>
    /// <returns>The matching handler, or null if no match found</returns>
    public PopupHandler? FindMatchingHandler(DetectedPopup popup)
    {
        foreach (var handler in _handlers)
        {
            if (IsMatch(popup, handler.Matchers))
            {
                return handler;
            }
        }

        return null;
    }

    /// <summary>
    /// Check if a popup matches the given matcher criteria
    /// </summary>
    private bool IsMatch(DetectedPopup popup, Core.Models.PopupMatcher matchers)
    {
        var matchCount = 0;
        var totalCriteria = 0;

        // Check window title (exact match)
        if (!string.IsNullOrEmpty(matchers.WindowTitle))
        {
            totalCriteria++;
            if (popup.WindowTitle.Equals(matchers.WindowTitle, StringComparison.OrdinalIgnoreCase))
            {
                matchCount++;
            }
        }

        // Check window title (regex)
        if (!string.IsNullOrEmpty(matchers.WindowTitleRegex))
        {
            totalCriteria++;
            try
            {
                if (Regex.IsMatch(popup.WindowTitle, matchers.WindowTitleRegex, RegexOptions.IgnoreCase))
                {
                    matchCount++;
                }
            }
            catch
            {
                // Invalid regex - don't match
            }
        }

        // Check message contains
        if (!string.IsNullOrEmpty(matchers.MessageContains))
        {
            totalCriteria++;
            if (popup.MessageText.Contains(matchers.MessageContains, StringComparison.OrdinalIgnoreCase))
            {
                matchCount++;
            }
        }

        // Check message regex
        if (!string.IsNullOrEmpty(matchers.MessageRegex))
        {
            totalCriteria++;
            try
            {
                if (Regex.IsMatch(popup.MessageText, matchers.MessageRegex, RegexOptions.IgnoreCase))
                {
                    matchCount++;
                }
            }
            catch
            {
                // Invalid regex - don't match
            }
        }

        // Check buttons
        if (matchers.Buttons != null && matchers.Buttons.Count > 0)
        {
            totalCriteria++;
            var popupButtonTexts = popup.Buttons.Select(b => b.Text.ToLowerInvariant()).ToList();
            var requiredButtons = matchers.Buttons.Select(b => b.ToLowerInvariant()).ToList();

            // Check if all required buttons exist
            if (requiredButtons.All(rb => popupButtonTexts.Contains(rb)))
            {
                matchCount++;
            }
        }

        // Check control ID
        if (!string.IsNullOrEmpty(matchers.ControlId))
        {
            totalCriteria++;
            if (popup.AutomationId.Equals(matchers.ControlId, StringComparison.OrdinalIgnoreCase))
            {
                matchCount++;
            }
        }

        // Match if ALL specified criteria match
        return totalCriteria > 0 && matchCount == totalCriteria;
    }

    /// <summary>
    /// Get match score (for debugging/logging)
    /// </summary>
    public double GetMatchScore(DetectedPopup popup, PopupHandler handler)
    {
        // Implementation similar to IsMatch but returns a percentage
        return IsMatch(popup, handler.Matchers) ? 100.0 : 0.0;
    }
}
