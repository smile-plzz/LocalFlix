using LocalFlix.App.UserControls;
using LocalFlix.App.ViewModels;
using LocalFlix.Core.Interfaces;
using LocalFlix.Core.Models;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace LocalFlix.App.Views;

public partial class HomeView : UserControl
{
    private readonly ILibraryRepository _repository;
    private TitleItem? _currentHeroTitle;

    public event EventHandler<TitleItem>? TitleSelected;
    public event EventHandler<TitleItem>? PlayRequested;

    public HomeView(ILibraryRepository repository)
    {
        InitializeComponent();
        _repository = repository;
        Loaded += async (s, e) => await LoadDataAsync();
    }

    public async Task LoadDataAsync()
    {
        var allTitles = await _repository.GetAllTitlesAsync();
        var recentlyAdded = await _repository.GetRecentlyAddedTitlesAsync(15);
        var continueWatching = await _repository.GetContinueWatchingTitlesAsync(10);
        var movies = allTitles.Where(t => t.Type == MediaType.Movie).Take(15).ToList();
        var tvShows = allTitles.Where(t => t.Type == MediaType.TvShow).Take(15).ToList();
        var docs = allTitles.Where(t => t.Type == MediaType.Documentary).Take(15).ToList();

        // Hero Banner Setup
        _currentHeroTitle = continueWatching.FirstOrDefault() ?? recentlyAdded.FirstOrDefault() ?? allTitles.FirstOrDefault();
        if (_currentHeroTitle != null)
        {
            HeroContainer.Visibility = Visibility.Visible;
            HeroTitle.Text = _currentHeroTitle.Name;
            HeroYear.Text = _currentHeroTitle.Year?.ToString() ?? string.Empty;
            HeroRating.Text = _currentHeroTitle.Rating.HasValue ? $"★ {_currentHeroTitle.Rating.Value:0.0}" : string.Empty;
            HeroGenres.Text = _currentHeroTitle.Genres ?? string.Empty;
            HeroOverview.Text = _currentHeroTitle.Overview ?? "Browse your personal offline library.";

            var backdropPath = _currentHeroTitle.BackdropPath ?? _currentHeroTitle.PosterPath;
            if (!string.IsNullOrEmpty(backdropPath))
            {
                try
                {
                    var uri = new Uri(backdropPath, UriKind.RelativeOrAbsolute);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = uri;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    HeroBackdrop.Source = bmp;
                }
                catch
                {
                    HeroBackdrop.Source = null;
                }
            }
        }
        else
        {
            HeroContainer.Visibility = Visibility.Collapsed;
        }

        // Continue Watching Row
        if (continueWatching.Count > 0)
        {
            ContinueWatchingSection.Visibility = Visibility.Visible;
            PopulateRow(ContinueWatchingPanel, continueWatching);
        }
        else
        {
            ContinueWatchingSection.Visibility = Visibility.Collapsed;
        }

        // Recently Added Row
        PopulateRow(RecentlyAddedPanel, recentlyAdded);

        // Movies Row
        PopulateRow(MoviesPanel, movies);

        // TV Shows Row
        PopulateRow(TvShowsPanel, tvShows);

        // Documentaries Row
        PopulateRow(DocsPanel, docs);
    }

    private void PopulateRow(StackPanel targetPanel, IReadOnlyList<TitleItem> items)
    {
        targetPanel.Children.Clear();
        foreach (var item in items)
        {
            var vm = new TitleCardViewModel
            {
                Id = item.Id,
                Name = item.Name,
                Year = item.Year,
                Rating = item.Rating,
                PosterPath = item.PosterPath,
                BackdropPath = item.BackdropPath,
                Type = item.Type,
                IsDriveConnected = true
            };

            var card = new TitleCardControl { Item = vm };
            card.CardClicked += (s, clickedVm) =>
            {
                var found = items.FirstOrDefault(i => i.Id == clickedVm.Id);
                if (found != null) TitleSelected?.Invoke(this, found);
            };

            targetPanel.Children.Add(card);
        }
    }

    private void HeroPlayBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentHeroTitle != null)
        {
            PlayRequested?.Invoke(this, _currentHeroTitle);
        }
    }

    private async void HeroWatchlistBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentHeroTitle != null)
        {
            var inList = await _repository.IsInUserListAsync(_currentHeroTitle.Id, UserListType.Watchlist);
            if (inList)
            {
                await _repository.RemoveFromUserListAsync(_currentHeroTitle.Id, UserListType.Watchlist);
                HeroWatchlistBtn.Content = "+ Watchlist";
            }
            else
            {
                await _repository.AddToUserListAsync(_currentHeroTitle.Id, UserListType.Watchlist);
                HeroWatchlistBtn.Content = "✓ In Watchlist";
            }
        }
    }
}
