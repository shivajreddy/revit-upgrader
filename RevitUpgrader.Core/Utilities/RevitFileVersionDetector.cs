using RevitUpgrader.Core.Models;
using System.Text;

namespace RevitUpgrader.Core.Utilities;

/// <summary>
/// Detects the Revit version of .rvt files by reading the file header
/// </summary>
public static class RevitFileVersionDetector
{
    /// <summary>
    /// Detect the Revit version from a .rvt file
    /// Revit files are OLE compound documents with version info in the header
    /// The version is stored in UTF-16 format (appears as "B u i l d" or "F o r m a t" in notepad)
    /// </summary>
    public static RevitVersion DetectVersion(string filePath)
    {
        if (!File.Exists(filePath))
            return RevitVersion.Unknown;

        if (!filePath.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase))
            return RevitVersion.Unknown;

        try
        {
            // CRITICAL: Open file in READ-ONLY mode to prevent any corruption
            // FileMode.Open = Open existing file (don't create/truncate)
            // FileAccess.Read = Read-only access (cannot write)
            // FileShare.ReadWrite = Allow other processes to read/write while we read
            // This ensures the file is NEVER modified or locked exclusively
            using var fs = new FileStream(
                filePath, 
                FileMode.Open,           // Open existing, don't create
                FileAccess.Read,         // READ ONLY - cannot write
                FileShare.ReadWrite);    // Allow others to use file
            
            // Read a larger chunk to ensure we capture the version info
            // Version info can be anywhere in the first 64KB
            var buffer = new byte[65536]; // 64KB
            var bytesRead = fs.Read(buffer, 0, buffer.Length);

            // Revit files store strings in UTF-16 (Unicode) format
            // This is why you see "B u i l d" or "F o r m a t" when opening in notepad
            var headerText = Encoding.Unicode.GetString(buffer, 0, bytesRead);

            // Try to find version using UTF-16 decoded text
            var version = ExtractVersionFromHeader(headerText);
            
            if (version == RevitVersion.Unknown)
            {
                // Fallback: Try ASCII encoding as well (for older formats)
                headerText = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                version = ExtractVersionFromHeader(headerText);
            }
            
            return version;
        }
        catch
        {
            return RevitVersion.Unknown;
        }
    }

    /// <summary>
    /// Extract version from header text
    /// Looks for "Build" or "Format" keywords followed by the year
    /// </summary>
    private static RevitVersion ExtractVersionFromHeader(string headerText)
    {
        // Method 1: Look for "Build: " followed by version
        // Pattern in file: "Build: Autodesk Revit 2024" or just "Build: 2024"
        var buildIndex = headerText.IndexOf("Build", StringComparison.OrdinalIgnoreCase);
        if (buildIndex >= 0)
        {
            // Extract substring after "Build" (next 100 characters should contain the year)
            var buildSection = headerText.Substring(buildIndex, Math.Min(100, headerText.Length - buildIndex));
            var year = ExtractYearFromText(buildSection);
            if (year.HasValue)
                return RevitVersionExtensions.FromYear(year.Value);
        }

        // Method 2: Look for "Format: " followed by version
        // Pattern in file: "Format: 2024"
        var formatIndex = headerText.IndexOf("Format", StringComparison.OrdinalIgnoreCase);
        if (formatIndex >= 0)
        {
            var formatSection = headerText.Substring(formatIndex, Math.Min(50, headerText.Length - formatIndex));
            var year = ExtractYearFromText(formatSection);
            if (year.HasValue)
                return RevitVersionExtensions.FromYear(year.Value);
        }

        // Method 3: Look for "Autodesk Revit YYYY" patterns
        var patterns = new[]
        {
            "Autodesk Revit 2027",
            "Autodesk Revit 2026",
            "Autodesk Revit 2025",
            "Autodesk Revit 2024",
            "Autodesk Revit 2023",
            "Autodesk Revit 2022",
            "Autodesk Revit 2021",
            "Autodesk Revit 2020",
            "Autodesk Revit 2019"
        };

        foreach (var pattern in patterns)
        {
            if (headerText.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                var yearStr = pattern.Split(' ').Last();
                if (int.TryParse(yearStr, out int year))
                {
                    return RevitVersionExtensions.FromYear(year);
                }
            }
        }

        return RevitVersion.Unknown;
    }

    /// <summary>
    /// Extract a year (2019-2027) from a text string
    /// </summary>
    private static int? ExtractYearFromText(string text)
    {
        // Look for 4-digit numbers that could be years
        for (int year = 2027; year >= 2019; year--)
        {
            if (text.Contains(year.ToString()))
            {
                return year;
            }
        }
        return null;
    }

    /// <summary>
    /// Check if a file matches the expected version
    /// </summary>
    public static bool IsFileVersion(string filePath, RevitVersion expectedVersion)
    {
        var detectedVersion = DetectVersion(filePath);
        return detectedVersion == expectedVersion;
    }
}
