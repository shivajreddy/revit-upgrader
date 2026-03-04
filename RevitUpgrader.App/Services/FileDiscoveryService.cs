using RevitUpgrader.Core.Models;
using RevitUpgrader.Core.Utilities;
using System.IO;
using System.Text.RegularExpressions;

namespace RevitUpgrader.Services;

/// <summary>
/// Service for discovering and filtering Revit files
/// </summary>
public class FileDiscoveryService
{
    // Regex pattern for backup files: .0001.rvt, .0002.rvt, etc.
    private static readonly Regex BackupFilePattern = new Regex(@"\.\d{4}\.rvt$", RegexOptions.IgnoreCase);

    /// <summary>
    /// Discover all .rvt files in a directory tree
    /// </summary>
    /// <param name="rootPath">Root directory to search</param>
    /// <param name="sourceVersion">Expected source version (files must match this version)</param>
    /// <param name="progress">Optional progress callback</param>
    /// <returns>List of discovered files with their status</returns>
    public async Task<List<FileStatus>> DiscoverFilesAsync(
        string rootPath,
        RevitVersion sourceVersion,
        IProgress<FileDiscoveryProgress>? progress = null)
    {
        var discoveredFiles = new List<FileStatus>();

        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Root path not found: {rootPath}");
        }

        await Task.Run(() =>
        {
            var allRvtFiles = Directory.GetFiles(rootPath, "*.rvt", SearchOption.AllDirectories);
            var totalFiles = allRvtFiles.Length;
            var processedCount = 0;

            foreach (var filePath in allRvtFiles)
            {
                processedCount++;

                // Report progress
                progress?.Report(new FileDiscoveryProgress
                {
                    CurrentFile = filePath,
                    ProcessedCount = processedCount,
                    TotalCount = totalFiles,
                    Message = $"Scanning: {Path.GetFileName(filePath)}"
                });

                // Skip backup files
                if (IsBackupFile(filePath))
                {
                    continue;
                }

                // Get file info
                var fileInfo = new FileInfo(filePath);

                // Detect version
                var detectedVersion = RevitFileVersionDetector.DetectVersion(filePath);

                // Only include files that match the source version
                if (detectedVersion != sourceVersion)
                {
                    continue;
                }

                // Create FileStatus entry
                var fileStatus = new FileStatus
                {
                    FilePath = filePath,
                    FileName = fileInfo.Name,
                    FileSizeBytes = fileInfo.Length,
                    DetectedVersion = detectedVersion,
                    Status = UpgradeStatus.Pending,
                    IsExcluded = false
                };

                discoveredFiles.Add(fileStatus);
            }
        });

        return discoveredFiles;
    }

    /// <summary>
    /// Discover files synchronously (for simpler scenarios)
    /// </summary>
    public List<FileStatus> DiscoverFiles(string rootPath, RevitVersion sourceVersion)
    {
        return DiscoverFilesAsync(rootPath, sourceVersion).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Check if a file is a Revit backup file
    /// Backup files have patterns like: file.0001.rvt, file.0002.rvt
    /// </summary>
    private bool IsBackupFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return BackupFilePattern.IsMatch(fileName);
    }

    /// <summary>
    /// Validate that a file still exists and is accessible
    /// </summary>
    public bool ValidateFile(string filePath)
    {
        try
        {
            return File.Exists(filePath) && new FileInfo(filePath).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get file size in human-readable format
    /// </summary>
    public static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>
/// Progress information for file discovery
/// </summary>
public class FileDiscoveryProgress
{
    public string CurrentFile { get; set; } = string.Empty;
    public int ProcessedCount { get; set; }
    public int TotalCount { get; set; }
    public string Message { get; set; } = string.Empty;
    public double PercentComplete => TotalCount > 0 ? (ProcessedCount * 100.0 / TotalCount) : 0;
}
