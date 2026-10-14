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
        Closing += (_, e) =>
        {
            if (!_isScanning) return;
            _cts?.Cancel();
            e.Cancel = true;
            StatusMessageText.Text = "Cancelling safely. Close once scanning stops.";
        };
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

        var roots = new List<string> { path };
        if (SmartScanCheckBox.IsChecked == true && _scanner is LocalFlix.Scanner.FileScanner smartScanner)
        {
            StartScanButton.IsEnabled = false;
            _isScanning = true;
            _cts = new CancellationTokenSource();
            try
            {
                var candidates = await Task.Run(() => smartScanner.DiscoverCandidateFoldersAsync(path, _cts.Token));
                roots.Clear();
                foreach (var candidate in candidates)
                    if (MessageBox.Show(this, $"Add this folder?\n{candidate.Path}\n{candidate.VideoFileCount} videos", "Confirm discovered folder", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        roots.Add(candidate.Path);
                if (roots.Count == 0) return;
            }
            catch (OperationCanceledException) { StatusMessageText.Text = "Discovery cancelled."; return; }
            catch (Exception ex) { StatusMessageText.Text = "Discovery failed: " + ex.Message; return; }
            finally { _isScanning = false; _cts?.Dispose(); _cts = null; StartScanButton.IsEnabled = true; }
        }
        PauseButton.IsEnabled = true;
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
                CountsText.Text = $"Discovered: {p.FilesDiscovered} | Processed: {p.FilesProcessed} | Indexed: {p.FilesMatched} | Review: {p.FilesNeedingReview}";
                CurrentFileText.Text = Path.GetFileName(p.CurrentPath);
                StatusMessageText.Text = $"Processing: {Path.GetFileName(p.CurrentPath)}";
            });

            foreach (var root in roots)
                await Task.Run(() => _scanner.ScanDirectoryAsync(root, driveId, progress, _cts.Token));

            // Run metadata enrichment for newly added active files
            PauseButton.IsEnabled = false;
            StatusMessageText.Text = "Enriching metadata and posters...";
            var files = await _repository.GetFilesByDriveIdAsync(driveId);
            int enriched = 0;

            var titles = await _repository.GetAllTitlesAsync();
            var matchedIds = titles.Where(t => t.MediaFileId.HasValue).Select(t => t.MediaFileId!.Value).ToHashSet();
            foreach (var f in files.Where(x => x.Status == FileStatus.Active && !matchedIds.Contains(x.Id)))
            {
                _cts.Token.ThrowIfCancellationRequested();

                var fullPath = Path.Combine(drive.LastKnownLetter, f.RelativePath);
                if (!roots.Any(root => fullPath.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) continue;
                var parsed = _parser.Parse(Path.GetFileName(f.RelativePath), Path.GetFileName(Path.GetDirectoryName(f.RelativePath)));
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
            PauseButton.IsEnabled = false;
            PauseButton.Content = "Pause";
            if (_scanner is LocalFlix.Scanner.FileScanner fs) fs.IsPaused = false;
            _cts?.Dispose();
            _cts = null;
            _isScanning = false;
            StartScanButton.Content = "Start Scan";
            StartScanButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
            ScanProgressBar.IsIndeterminate = false;
        }
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_scanner is LocalFlix.Scanner.FileScanner scanner)
        {
            scanner.IsPaused = !scanner.IsPaused;
            PauseButton.Content = scanner.IsPaused ? "Resume" : "Pause";
            StatusMessageText.Text = scanner.IsPaused ? "Paused. Your progress is saved." : "Scanning...";
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
