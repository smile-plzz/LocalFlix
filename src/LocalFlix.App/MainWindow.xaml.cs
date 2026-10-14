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

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        NavigateTo("Home");
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
                var watchlistView = new SearchView(_repository);
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

    private void ShowTitleDetails(TitleItem title)
    {
        var detailView = new DetailView(_repository, title);
        detailView.BackRequested += (s, e) => NavigateTo("Home");
        detailView.PlayRequested += (s, t) => OnPlayTitle(t);
        detailView.EpisodePlayRequested += (s, ep) => OnPlayEpisode(ep);
        detailView.FixMatchRequested += (s, t) =>
        {
            NavigateTo("NeedsReview");
        };
        MainContentHost.Content = detailView;
    }

    private void OnPlayTitle(TitleItem title)
    {
        // Handled in M4 (Playback)
    }

    private void OnPlayEpisode(Episode episode)
    {
        // Handled in M4 (Playback)
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