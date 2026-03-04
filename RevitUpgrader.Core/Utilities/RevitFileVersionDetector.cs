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
    /// </summary>
    public static RevitVersion DetectVersion(string filePath)
    {
        if (!File.Exists(filePath))
            return RevitVersion.Unknown;

        if (!filePath.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase))
            return RevitVersion.Unknown;

        try
        {
            // Read the first 4KB of the file (version info is in the header)
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = new byte[4096];
            var bytesRead = fs.Read(buffer, 0, buffer.Length);

            // Convert to string to search for version markers
            var headerText = Encoding.ASCII.GetString(buffer, 0, bytesRead);

            // Revit files contain format strings like "Autodesk Revit 2024"
            // Try to find these patterns
            var version = ExtractVersionFromHeader(headerText);
            
            return version;
        }
        catch
        {
            return RevitVersion.Unknown;
        }
    }

    /// <summary>
    /// Extract version from header text
    /// </summary>
    private static RevitVersion ExtractVersionFromHeader(string headerText)
    {
        // Look for patterns like "Autodesk Revit 2024", "Revit 2024", etc.
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

        // Fallback: look for just year numbers (2019-2027)
        for (int year = 2027; year >= 2019; year--)
        {
            var yearPattern = $"Format: {year}";
            if (headerText.Contains(yearPattern))
            {
                return RevitVersionExtensions.FromYear(year);
            }
        }

        return RevitVersion.Unknown;
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
