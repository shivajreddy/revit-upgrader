using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using RevitUpgrader.Core.Models;
using RevitUpgrader.Services;

namespace RevitUpgrader;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private UpgradeJob? _currentJob;
    private UpgradeOrchestrator? _orchestrator;
    private ObservableCollection<FileViewModel> _files = new();
    private CancellationTokenSource? _cancellationTokenSource;

    public MainWindow()
    {
        InitializeComponent();
        InitializeUI();
    }

    private void InitializeUI()
    {
        // Populate version combo boxes
        var versions = RevitVersionExtensions.GetSupportedVersions().ToList();
        
        SourceVersionComboBox.ItemsSource = versions;
        TargetVersionComboBox.ItemsSource = versions;

        // Set default values
        SourceVersionComboBox.SelectedItem = RevitVersion.Revit2024;
        TargetVersionComboBox.SelectedItem = RevitVersion.Revit2026;

        // Setup DataGrid
        FilesDataGrid.ItemsSource = _files;

        // Enable folder selection
        BrowseFolderButton.IsEnabled = true;
    }

    #region Event Handlers

    private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Root Folder Containing Revit Files"
        };

        if (dialog.ShowDialog() == true)
        {
            RootFolderTextBox.Text = dialog.FolderName;
            DiscoverFilesButton.IsEnabled = true;
            UpdateStatus("Folder selected. Click 'Discover Files' to scan.");
        }
    }

    private async void DiscoverFilesButton_Click(object sender, RoutedEventArgs e)
    {
        if (SourceVersionComboBox.SelectedItem is not RevitVersion sourceVersion ||
            TargetVersionComboBox.SelectedItem is not RevitVersion targetVersion)
        {
            MessageBox.Show("Please select source and target versions.", "Validation Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrEmpty(RootFolderTextBox.Text))
        {
            MessageBox.Show("Please select a root folder.", "Validation Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            SetUIEnabled(false);
            UpdateStatus("Discovering files...");

            _currentJob = new UpgradeJob
            {
                RootFolder = RootFolderTextBox.Text,
                SourceVersion = sourceVersion,
                TargetVersion = targetVersion,
                Settings = new UpgradeJobSettings
                {
                    LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs"),
                    ScreenshotDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots"),
                    PopupHandlersConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                        "Config", "popup-handlers.json")
                }
            };

            var fileDiscovery = new FileDiscoveryService();
            var progress = new Progress<FileDiscoveryProgress>(p =>
            {
                ProgressBar.Value = p.PercentComplete;
                ProgressText.Text = p.Message;
            });

            var discoveredFiles = await fileDiscovery.DiscoverFilesAsync(
                _currentJob.RootFolder,
                _currentJob.SourceVersion,
                progress);

            _currentJob.Files = discoveredFiles;

            // Convert to view models
            _files.Clear();
            foreach (var file in discoveredFiles)
            {
                _files.Add(new FileViewModel(file));
            }

            FilesHeaderText.Text = $"Files ({_files.Count})";
            UpdateStatus($"Discovered {_files.Count} files matching {sourceVersion.GetDisplayName()}");
            StartUpgradeButton.IsEnabled = _files.Count > 0;
            ProgressBar.Value = 0;
            ProgressText.Text = $"Found {_files.Count} files";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error discovering files: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateStatus("Error during file discovery");
        }
        finally
        {
            SetUIEnabled(true);
        }
    }

    private async void StartUpgradeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentJob == null || _files.Count == 0)
        {
            MessageBox.Show("No files to upgrade. Please discover files first.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Start upgrading {_files.Count(f => f.IsIncluded)} files from " +
            $"{_currentJob.SourceVersion.GetDisplayName()} to {_currentJob.TargetVersion.GetDisplayName()}?\n\n" +
            "This will:\n" +
            "• Launch Revit\n" +
            "• Open and upgrade each file\n" +
            "• Handle popups automatically\n" +
            "• Save files in place (overwriting originals)\n\n" +
            "Make sure you have backups!",
            "Confirm Upgrade",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            SetUIEnabled(false, isUpgrading: true);
            _cancellationTokenSource = new CancellationTokenSource();

            // Mark excluded files
            foreach (var fileVM in _files)
            {
                fileVM.FileStatus.IsExcluded = !fileVM.IsIncluded;
            }

            _orchestrator = new UpgradeOrchestrator(_currentJob.Settings);

            var progress = new Progress<UpgradeProgress>(p =>
            {
                ProgressBar.Value = p.PercentComplete;
                ProgressText.Text = p.Message;
                UpdateStatus($"Processing: {p.CurrentFile}");

                // Update stats
                UpdateStats();
            });

            await _orchestrator.ExecuteUpgradeAsync(_currentJob, progress, _cancellationTokenSource.Token);

            MessageBox.Show(
                $"Upgrade completed!\n\n" +
                $"Succeeded: {_currentJob.FilesSucceeded}\n" +
                $"Failed: {_currentJob.FilesFailed}\n" +
                $"Skipped: {_currentJob.FilesSkipped}\n" +
                $"Duration: {_currentJob.TotalDuration?.ToString(@"hh\:mm\:ss")}",
                "Upgrade Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            UpdateStatus("Upgrade completed");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error during upgrade: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateStatus("Upgrade failed");
        }
        finally
        {
            SetUIEnabled(true);
            _orchestrator?.Dispose();
            _orchestrator = null;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_orchestrator != null && _cancellationTokenSource != null)
        {
            var result = MessageBox.Show(
                "Are you sure you want to cancel the upgrade?\n\n" +
                "The current file will finish processing, but remaining files will be skipped.",
                "Confirm Cancel",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _orchestrator.CancelUpgrade();
                UpdateStatus("Cancelling upgrade...");
            }
        }
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var file in _files)
        {
            file.IsIncluded = true;
        }
    }

    private void DeselectAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var file in _files)
        {
            file.IsIncluded = false;
        }
    }

    private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        var logsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        
        if (Directory.Exists(logsPath))
        {
            Process.Start("explorer.exe", logsPath);
        }
        else
        {
            MessageBox.Show("No logs found yet.", "Information", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    #endregion

    #region Helper Methods

    private void SetUIEnabled(bool enabled, bool isUpgrading = false)
    {
        SourceVersionComboBox.IsEnabled = enabled;
        TargetVersionComboBox.IsEnabled = enabled;
        BrowseFolderButton.IsEnabled = enabled;
        DiscoverFilesButton.IsEnabled = enabled && !string.IsNullOrEmpty(RootFolderTextBox.Text);
        StartUpgradeButton.IsEnabled = enabled && _files.Count > 0;
        CancelButton.IsEnabled = isUpgrading;
        SelectAllButton.IsEnabled = enabled;
        DeselectAllButton.IsEnabled = enabled;
    }

    private void UpdateStatus(string message)
    {
        StatusText.Text = message;
    }

    private void UpdateStats()
    {
        if (_currentJob == null) return;

        StatsText.Text = $"✓ {_currentJob.FilesSucceeded}  " +
                        $"✗ {_currentJob.FilesFailed}  " +
                        $"⊘ {_currentJob.FilesSkipped}";
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_orchestrator != null)
        {
            var result = MessageBox.Show(
                "An upgrade is in progress. Are you sure you want to exit?",
                "Confirm Exit",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }

            _orchestrator.CancelUpgrade();
        }

        base.OnClosing(e);
    }

    #endregion
}

/// <summary>
/// View model for displaying file information in the DataGrid
/// </summary>
public class FileViewModel : INotifyPropertyChanged
{
    public FileStatus FileStatus { get; }
    
    private bool _isIncluded = true;

    public FileViewModel(FileStatus fileStatus)
    {
        FileStatus = fileStatus;
    }

    public bool IsIncluded
    {
        get => _isIncluded;
        set
        {
            if (_isIncluded != value)
            {
                _isIncluded = value;
                OnPropertyChanged(nameof(IsIncluded));
            }
        }
    }

    public string FileName => FileStatus.FileName;
    public string FilePath => FileStatus.FilePath;
    public string VersionDisplay => FileStatus.DetectedVersion.GetDisplayName();
    public string FileSizeDisplay => FormatFileSize(FileStatus.FileSizeBytes);
    public string StatusDisplay => FileStatus.Status.ToString();
    public string DurationDisplay => FileStatus.Duration?.ToString(@"mm\:ss") ?? "-";

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}