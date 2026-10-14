using LocalFlix.Core.Interfaces;
using LocalFlix.Core.Models;
using LocalFlix.Metadata;
using System.IO;
using System.Windows;

namespace LocalFlix.App.Views;

public partial class ScanDialog : Window
{
    private readonly IFileScanner _scanner;
    private readonly ILibraryRepository _repository;
    private readonly MetadataOrchestrator _orchestrator;
    private readonly IFilenameParser _parser;
    private readonly IDriveService _driveService;
    private CancellationTokenSource? _cts;
    private bool _isScanning = false;

    public event EventHandler? ScanCompleted;

    public ScanDialog(
        IFileScanner scanner,
        ILibraryRepository repository,
        MetadataOrchestrator orchestrator,
        IFilenameParser parser,
        IDriveService driveService)
    {
        InitializeComponent();
        _scanner = scanner;
        _repository = repository;
        _orchestrator = orchestrator;
        _parser = parser;
        _driveService = driveService;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        // Use folder browser or input dialog
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Media Folder to Scan",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            FolderPathBox.Text = dialog.FolderName;
        }
    }

    private async void StartScanButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isScanning)
        {
            // Cancel scan
            _cts?.Cancel();
            StartScanButton.IsEnabled = false;
            StatusMessageText.Text = "Cancelling scan...";
            return;
        }

        var path = FolderPathBox.Text.Trim();
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            MessageBox.Show("Please choose a valid existing folder path.", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _isScanning = true;
        _cts = new CancellationTokenSource();
        StartScanButton.Content = "Stop Scan";
        CancelButton.IsEnabled = false;
        ScanProgressBar.IsIndeterminate = true;
        StatusMessageText.Text = "Scanning directory...";

        try
        {
            var volumeGuid = _driveService.GetVolumeGuidForPath(path);
            var serial = _driveService.GetVolumeSerialForPath(path);
            var label = _driveService.GetVolumeLabelForPath(path);

            var drive = new Drive
            {
                VolumeGuid = volumeGuid,
                SerialNumber = serial,
                Label = label,
                LastKnownLetter = Path.GetPathRoot(path) ?? string.Empty,
                LastSeenUtc = DateTime.UtcNow,
                IsConnected = true
            };
            var driveId = await _repository.UpsertDriveAsync(drive);

            var progress = new Progress<ScanProgress>(p =>
            {
                CountsText.Text = $"Discovered: {p.FilesDiscovered} | Processed: {p.FilesProcessed} | Matched: {p.FilesMatched} | Review: {p.FilesNeedingReview}";
                CurrentFileText.Text = Path.GetFileName(p.CurrentPath);
                StatusMessageText.Text = $"Processing: {Path.GetFileName(p.CurrentPath)}";
            });

            await _scanner.ScanDirectoryAsync(path, driveId, progress, _cts.Token);

            // Run metadata enrichment for newly added active files
            StatusMessageText.Text = "Enriching metadata and posters...";
            var files = await _repository.GetFilesByDriveIdAsync(driveId);
            int enriched = 0;

            foreach (var f in files.Where(x => x.Status == FileStatus.Active))
            {
                if (_cts.IsCancellationRequested) break;

                var fullPath = Path.Combine(path, f.RelativePath);
                var parsed = _parser.Parse(Path.GetFileName(f.RelativePath));
                await _orchestrator.MatchMediaFileAsync(f, parsed, fullPath, _cts.Token);
                enriched++;
                CountsText.Text = $"Enriched metadata: {enriched}/{files.Count}";
            }

            StatusMessageText.Text = "Scan completed successfully!";
            ScanProgressBar.IsIndeterminate = false;
            ScanProgressBar.Value = 100;
            ScanCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            StatusMessageText.Text = "Scan cancelled by user.";
        }
        catch (Exception ex)
        {
            StatusMessageText.Text = $"Scan encountered an error: {ex.Message}";
        }
        finally
        {
            _isScanning = false;
            StartScanButton.Content = "Start Scan";
            StartScanButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
            ScanProgressBar.IsIndeterminate = false;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
