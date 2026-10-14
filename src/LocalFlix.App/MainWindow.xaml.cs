using LocalFlix.App.Views;
using LocalFlix.Core.Interfaces;
using LocalFlix.Core.Models;
using LocalFlix.Metadata;
using System.Windows;
using System.Windows.Controls;

namespace LocalFlix.App;

public partial class MainWindow : Window
{
    private readonly ILibraryRepository _repository;
    private readonly IImageCacheService _imageCache;
    private readonly IMetadataProvider _metadataProvider;
    private readonly MetadataOrchestrator _orchestrator;
    private readonly IFileScanner _scanner;
    private readonly IFilenameParser _parser;
    private readonly IDriveService _driveService;

    private HomeView? _homeView;
    private SearchView? _searchView;
    private NeedsReviewView? _needsReviewView;

    public MainWindow(
        ILibraryRepository repository,
        IImageCacheService imageCache,
        IMetadataProvider metadataProvider,
        MetadataOrchestrator orchestrator,
        IFileScanner scanner,
        IFilenameParser parser,
        IDriveService driveService)
    {
        InitializeComponent();
        _repository = repository;
        _imageCache = imageCache;
        _metadataProvider = metadataProvider;
        _orchestrator = orchestrator;
        _scanner = scanner;
        _parser = parser;
        _driveService = driveService;

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        NavigateTo("Home");

        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var source = System.Windows.Interop.HwndSource.FromHwnd(hwnd);
            if (_driveService is LocalFlix.Drives.WindowsDriveService winDrives)
            {
                source?.AddHook(winDrives.HandleWndProc);
                winDrives.DriveChanged += async (s, args) =>
                {
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        await Task.Run(() => winDrives.SynchronizeDrivesAsync(_repository));
                        DriveStatusText.Text = args.IsArrival
                            ? $"Drive Connected ({args.Drive.LastKnownLetter})"
                            : "Drive Disconnected";
                        if (_homeView != null) await _homeView.LoadDataAsync();
                    });
                };

                await winDrives.SynchronizeDrivesAsync(_repository);
                var connectedCount = winDrives.GetConnectedDrives().Count;
                DriveStatusText.Text = $"Ready ({connectedCount} drives)";
            }
        }
        catch
        {
        }
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            ResetNavButtons();
            btn.Tag = "Active";

            var target = btn.Name switch
            {
                nameof(NavHome) => "Home",
                nameof(NavMovies) => "Movies",
                nameof(NavTvShows) => "TV Shows",
                nameof(NavDocs) => "Documentaries",
                nameof(NavWatchlist) => "Watchlist",
                nameof(NavReview) => "NeedsReview",
                nameof(NavDrives) => "Drives",
                nameof(NavSettings) => "Settings",
                _ => "Home"
            };

            NavigateTo(target);
        }
    }

    private void ResetNavButtons()
    {
        NavHome.Tag = null;
        NavMovies.Tag = null;
        NavTvShows.Tag = null;
        NavDocs.Tag = null;
        NavWatchlist.Tag = null;
        NavReview.Tag = null;
        NavDrives.Tag = null;
        NavSettings.Tag = null;
    }

    public void NavigateTo(string pageName)
    {
        switch (pageName)
        {
            case "Home":
                _homeView = new HomeView(_repository);
                _homeView.TitleSelected += (s, title) => ShowTitleDetails(title);
                _homeView.PlayRequested += (s, title) => OnPlayTitle(title);
                MainContentHost.Content = _homeView;
                break;

            case "Movies":
                var moviesView = new SearchView(_repository, string.Empty, MediaType.Movie);
                moviesView.TitleSelected += (s, title) => ShowTitleDetails(title);
                MainContentHost.Content = moviesView;
                break;

            case "TV Shows":
                var tvView = new SearchView(_repository, string.Empty, MediaType.TvShow);
                tvView.TitleSelected += (s, title) => ShowTitleDetails(title);
                MainContentHost.Content = tvView;
                break;

            case "Documentaries":
                var docsView = new SearchView(_repository, string.Empty, MediaType.Documentary);
                docsView.TitleSelected += (s, title) => ShowTitleDetails(title);
                MainContentHost.Content = docsView;
                break;

            case "Watchlist":
                var watchlistView = new SearchView(_repository, watchlistOnly: true);
                watchlistView.TitleSelected += (s, title) => ShowTitleDetails(title);
                MainContentHost.Content = watchlistView;
                break;

            case "NeedsReview":
                _needsReviewView = new NeedsReviewView(_repository, _orchestrator, _metadataProvider);
                _needsReviewView.ReviewResolved += async (s, e) =>
                {
                    if (_homeView != null) await _homeView.LoadDataAsync();
                };
                MainContentHost.Content = _needsReviewView;
                break;

            case "Drives":
                ShowDrivesAsync();
                break;

            case "Settings":
                MainContentHost.Content = new SettingsView(_imageCache);
                break;

            default:
                var panel = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                panel.Children.Add(new TextBlock
                {
                    Text = pageName,
                    FontSize = 24,
                    FontWeight = FontWeights.Bold,
                    Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                });
                panel.Children.Add(new TextBlock
                {
                    Text = "Configure preferences, metadata keys, and view drive mappings.",
                    FontSize = 14,
                    Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                MainContentHost.Content = panel;
                break;
        }
    }

    private async void ShowDrivesAsync()
    {
        var panel = new StackPanel { Margin = new Thickness(32) };
        panel.Children.Add(new TextBlock { Text = "Your media drives", FontSize = 24, Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush") });
        var drives = await _repository.GetAllDrivesAsync();
        foreach (var drive in drives)
            panel.Children.Add(new TextBlock
            {
                Text = $"{drive.Label}  |  {(drive.IsConnected ? "Connected" : "Drive not connected")}  |  {drive.LastKnownLetter}\n{drive.VolumeGuid}",
                Margin = new Thickness(0, 20, 0, 0), TextWrapping = TextWrapping.Wrap,
                Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush")
            });
        if (drives.Count == 0) panel.Children.Add(new TextBlock { Text = "Add a media folder to register your first drive.", Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"), Margin = new Thickness(0, 20, 0, 0) });
        MainContentHost.Content = new ScrollViewer { Content = panel };
    }

    private void ShowTitleDetails(TitleItem title)
    {
        var detailView = new DetailView(_repository, title);
        detailView.BackRequested += (s, e) => NavigateTo("Home");
        detailView.PlayRequested += (s, t) => OnPlayTitle(t);
        detailView.EpisodePlayRequested += (s, ep) => OnPlayEpisode(ep);
        detailView.FixMatchRequested += async (s, t) =>
        {
            if (t.MediaFileId.HasValue)
                await _repository.UpsertReviewItemAsync(new ReviewItem { MediaFileId = t.MediaFileId.Value, Reason = "Manual correction requested" });
            NavigateTo("NeedsReview");
        };
        MainContentHost.Content = detailView;
    }

    private async void OnPlayTitle(TitleItem title)
    {
        if (title.Type == MediaType.TvShow)
        {
            var series = await _repository.GetSeriesByTitleIdAsync(title.Id);
            if (series != null)
                foreach (var season in await _repository.GetSeasonsBySeriesIdAsync(series.Id))
                    foreach (var episode in await _repository.GetEpisodesBySeasonIdAsync(season.Id))
                    {
                        if (!episode.MediaFileId.HasValue) continue;
                        var state = await _repository.GetPlaybackStateAsync(episode.MediaFileId.Value);
                        if (state?.IsWatched != true) { OnPlayEpisode(episode); return; }
                    }
        }
        if (!title.MediaFileId.HasValue)
        {
            MessageBox.Show("No media file linked to this title.", "Playback Error", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var file = await _repository.GetMediaFileByIdAsync(title.MediaFileId.Value);
        if (file == null)
        {
            MessageBox.Show("Media file record not found in library.", "Playback Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var drive = await _repository.GetDriveByIdAsync(file.DriveId);
        if (drive == null)
        {
            MessageBox.Show("Drive record not found.", "Playback Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var resolvedPath = _driveService.ResolveFullPath(drive, file.RelativePath);
        if (string.IsNullOrEmpty(resolvedPath) || !System.IO.File.Exists(resolvedPath))
        {
            MessageBox.Show(
                $"Please connect drive '{drive.Label}' ({drive.LastKnownLetter}) to play this title.\n\nRelative path: {file.RelativePath}",
                "Drive Not Connected",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var playerWin = new PlayerWindow(_repository, file, resolvedPath, title.Name)
        {
            Owner = this
        };
        playerWin.ShowDialog();
    }

    private async void OnPlayEpisode(Episode episode)
    {
        if (!episode.MediaFileId.HasValue)
        {
            MessageBox.Show("No media file linked to this episode.", "Playback Error", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var file = await _repository.GetMediaFileByIdAsync(episode.MediaFileId.Value);
        if (file == null) return;

        var drive = await _repository.GetDriveByIdAsync(file.DriveId);
        if (drive == null) return;

        var resolvedPath = _driveService.ResolveFullPath(drive, file.RelativePath);
        if (string.IsNullOrEmpty(resolvedPath) || !System.IO.File.Exists(resolvedPath))
        {
            MessageBox.Show(
                $"Please connect drive '{drive.Label}' ({drive.LastKnownLetter}) to play this episode.",
                "Drive Not Connected",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var epTitle = string.IsNullOrEmpty(episode.Name) ? $"Episode {episode.EpisodeNumber}" : episode.Name;
        Episode? nextEpisode = null;
        var currentSeason = episode.SeasonId;
        foreach (var title in await _repository.GetAllTitlesAsync(MediaType.TvShow))
        {
            var series = await _repository.GetSeriesByTitleIdAsync(title.Id);
            if (series == null) continue;
            var seasons = await _repository.GetSeasonsBySeriesIdAsync(series.Id);
            if (!seasons.Any(s => s.Id == currentSeason)) continue;
            var episodes = new List<Episode>();
            foreach (var season in seasons.OrderBy(s => s.SeasonNumber))
                episodes.AddRange(await _repository.GetEpisodesBySeasonIdAsync(season.Id));
            nextEpisode = episodes.SkipWhile(ep => ep.Id != episode.Id).Skip(1)
                .FirstOrDefault(ep => ep.MediaFileId.HasValue && ep.MediaFileId != episode.MediaFileId);
            break;
        }
        var playerWin = new PlayerWindow(_repository, file, resolvedPath, epTitle, nextEpisode)
        {
            Owner = this
        };
        playerWin.ShowDialog();
        if (playerWin.PlayNextRequested && nextEpisode != null)
            _ = Dispatcher.InvokeAsync(() => OnPlayEpisode(nextEpisode));
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text.Trim();
        if (string.IsNullOrEmpty(query))
        {
            NavigateTo("Home");
        }
        else
        {
            if (_searchView == null)
            {
                _searchView = new SearchView(_repository, query);
                _searchView.TitleSelected += (s, title) => ShowTitleDetails(title);
                MainContentHost.Content = _searchView;
            }
            else
            {
                MainContentHost.Content = _searchView;
                await _searchView.UpdateSearchQueryAsync(query);
            }
        }
    }

    private void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ScanDialog(_scanner, _repository, _orchestrator, _parser, _driveService)
        {
            Owner = this
        };
        dialog.ScanCompleted += async (s, args) =>
        {
            if (_homeView != null) await _homeView.LoadDataAsync();
        };
        dialog.ShowDialog();
    }
}