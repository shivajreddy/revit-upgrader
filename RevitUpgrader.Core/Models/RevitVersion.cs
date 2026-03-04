namespace RevitUpgrader.Core.Models;

/// <summary>
/// Represents a Revit version
/// </summary>
public enum RevitVersion
{
    Unknown = 0,
    Revit2019 = 2019,
    Revit2020 = 2020,
    Revit2021 = 2021,
    Revit2022 = 2022,
    Revit2023 = 2023,
    Revit2024 = 2024,
    Revit2025 = 2025,
    Revit2026 = 2026,
    Revit2027 = 2027
}

/// <summary>
/// Extension methods and utilities for RevitVersion
/// </summary>
public static class RevitVersionExtensions
{
    /// <summary>
    /// Get the default installation path for a Revit version
    /// </summary>
    public static string GetDefaultInstallPath(this RevitVersion version)
    {
        return version switch
        {
            RevitVersion.Revit2019 => @"C:\Program Files\Autodesk\Revit 2019\Revit.exe",
            RevitVersion.Revit2020 => @"C:\Program Files\Autodesk\Revit 2020\Revit.exe",
            RevitVersion.Revit2021 => @"C:\Program Files\Autodesk\Revit 2021\Revit.exe",
            RevitVersion.Revit2022 => @"C:\Program Files\Autodesk\Revit 2022\Revit.exe",
            RevitVersion.Revit2023 => @"C:\Program Files\Autodesk\Revit 2023\Revit.exe",
            RevitVersion.Revit2024 => @"C:\Program Files\Autodesk\Revit 2024\Revit.exe",
            RevitVersion.Revit2025 => @"C:\Program Files\Autodesk\Revit 2025\Revit.exe",
            RevitVersion.Revit2026 => @"C:\Program Files\Autodesk\Revit 2026\Revit.exe",
            RevitVersion.Revit2027 => @"C:\Program Files\Autodesk\Revit 2027\Revit.exe",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Get the year number from version
    /// </summary>
    public static int GetYear(this RevitVersion version)
    {
        return (int)version;
    }

    /// <summary>
    /// Get display name for the version
    /// </summary>
    public static string GetDisplayName(this RevitVersion version)
    {
        return version == RevitVersion.Unknown 
            ? "Unknown" 
            : $"Revit {version.GetYear()}";
    }

    /// <summary>
    /// Get all supported versions for UI display
    /// </summary>
    public static IEnumerable<RevitVersion> GetSupportedVersions()
    {
        return Enum.GetValues<RevitVersion>()
            .Where(v => v != RevitVersion.Unknown)
            .OrderBy(v => (int)v);
    }

    /// <summary>
    /// Parse version from year number
    /// </summary>
    public static RevitVersion FromYear(int year)
    {
        return Enum.IsDefined(typeof(RevitVersion), year) 
            ? (RevitVersion)year 
            : RevitVersion.Unknown;
    }
}
