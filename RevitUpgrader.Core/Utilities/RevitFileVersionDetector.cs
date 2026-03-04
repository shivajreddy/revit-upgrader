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
    /// Based on solution: Search for 'Build' keyword encoded in UTF-16-LE
    /// Revit files store version info as "Build: Autodesk Revit 2024 (Build 2024.0.0.0)"
    /// </summary>
    public static RevitVersion DetectVersion(string filePath)
    {
        if (!File.Exists(filePath))
            return RevitVersion.Unknown;

        if (!filePath.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase))
            return RevitVersion.Unknown;

        try
        {
            // CRITICAL: Open file in READ-ONLY mode ('rb' equivalent in Python)
            // This prevents any corruption or modification
            using var file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            
            // Read entire file data
            // Revit files can be large, but we need to search the whole file
            // Version info could be anywhere, not just at the beginning
            byte[] data = new byte[file.Length];
            file.Read(data, 0, data.Length);

            // Encode search string 'Build' in UTF-16-LE (Little Endian)
            // In .NET, UTF-16 Little Endian is accessed via Encoding.Unicode
            // This is the key - Revit stores text as UTF-16-LE
            var searchBytes = Encoding.Unicode.GetBytes("Build");
            
            // Find the index of 'Build' in the binary data
            var buildIndex = FindBytes(data, searchBytes);
            
            if (buildIndex >= 0)
            {
                // Extract the build string section (next 40 bytes should have version)
                // data[buildIndex:buildIndex+40] in Python
                var endIndex = Math.Min(buildIndex + 80, data.Length); // 40 chars * 2 bytes = 80 bytes
                var buildStringBytes = new byte[endIndex - buildIndex];
                Array.Copy(data, buildIndex, buildStringBytes, 0, buildStringBytes.Length);
                
                // Decode as UTF-16-LE to get the actual string
                var buildString = Encoding.Unicode.GetString(buildStringBytes);
                
                // Extract year from build string (should contain something like "Build: Autodesk Revit 2024")
                var year = ExtractYearFromText(buildString);
                if (year.HasValue)
                    return RevitVersionExtensions.FromYear(year.Value);
            }
            
            return RevitVersion.Unknown;
        }
        catch
        {
            return RevitVersion.Unknown;
        }
    }

    /// <summary>
    /// Find a byte pattern in a byte array (like data.find() in Python)
    /// </summary>
    private static int FindBytes(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    found = false;
                    break;
                }
            }
            if (found)
                return i;
        }
        return -1;
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
