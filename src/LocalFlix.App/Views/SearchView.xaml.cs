using LocalFlix.App.UserControls;
using LocalFlix.App.ViewModels;
using LocalFlix.Core.Interfaces;
using LocalFlix.Core.Models;
using System.Windows;
using System.Windows.Controls;

namespace LocalFlix.App.Views;

public partial class SearchView : UserControl
{
    private readonly ILibraryRepository _repository;
    private MediaType? _currentFilterType = null;
    private string _currentQuery = string.Empty;
    private string? _currentGenre = null;
    private readonly bool _watchlistOnly;
    private int _page;
    private int _generation;
    private const int PageSize = 60;

    public event EventHandler<TitleItem>? TitleSelected;

    public SearchView(ILibraryRepository repository, string initialQuery = "", MediaType? initialType = null, bool watchlistOnly = false)
    {
        InitializeComponent();
        _repository = repository;
        _watchlistOnly = watchlistOnly;
        _currentQuery = initialQuery;
        _currentFilterType = initialType;

        Loaded += async (s, e) =>
        {
            SetupGenres();
            await RefreshResultsAsync();
        };
    }

    private void SetupGenres()
    {
        GenreComboBox.Items.Clear();
        GenreComboBox.Items.Add("All Genres");
        var genres = new[] { "Action", "Adventure", "Animation", "Comedy", "Crime", "Documentary", "Drama", "Family", "Fantasy", "Horror", "Mystery", "Romance", "Sci-Fi", "Thriller" };
        foreach (var g in genres) GenreComboBox.Items.Add(g);
        GenreComboBox.SelectedIndex = 0;
    }

    public async Task UpdateSearchQueryAsync(string query)
    {
        _page = 0;
        _currentQuery = query;
        await RefreshResultsAsync();
    }

    public async Task RefreshResultsAsync()
    {
        var generation = ++_generation;
        SearchHeaderTitle.Text = string.IsNullOrWhiteSpace(_currentQuery)
            ? (_currentFilterType.HasValue ? $"{_currentFilterType} Titles" : "Explore Library")
            : $"Search results for \"{_currentQuery}\"";

        IReadOnlyList<TitleItem> items;
        if (string.IsNullOrWhiteSpace(_currentQuery))
        {
            items = await _repository.GetAllTitlesAsync(_currentFilterType);
            if (!string.IsNullOrEmpty(_currentGenre) && _currentGenre != "All Genres")
            {
                items = items.Where(i => i.Genres?.Contains(_currentGenre) == true).ToList();
            }
        }
        else
        {
            items = await _repository.SearchTitlesAsync(_currentQuery, _currentFilterType, _currentGenre == "All Genres" ? null : _currentGenre);
        }

        if (_watchlistOnly)
        {
            var ids = (await _repository.GetUserListTitlesAsync(UserListType.Watchlist)).Select(t => t.Id).ToHashSet();
            items = items.Where(t => ids.Contains(t.Id)).ToList();
            SearchHeaderTitle.Text = "Your Watchlist";
        }
        if (generation != _generation) return;
        var pageCount = Math.Max(1, (items.Count + PageSize - 1) / PageSize);
        _page = Math.Clamp(_page, 0, pageCount - 1);
        PageLabel.Text = $"{items.Count} titles | Page {_page + 1} of {pageCount}";
        PreviousPage.IsEnabled = _page > 0;
        NextPage.IsEnabled = _page + 1 < pageCount;
        items = items.Skip(_page * PageSize).Take(PageSize).ToList();
        ResultsWrapPanel.Children.Clear();
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
                IsDriveConnected = false
            };

            _ = UpdateAvailabilityAsync(vm, item);
            var card = new TitleCardControl { Item = vm, Margin = new Thickness(0, 0, 16, 20) };
            card.CardClicked += (s, clickedVm) => TitleSelected?.Invoke(this, item);
            ResultsWrapPanel.Children.Add(card);
        }
    }

    private async Task UpdateAvailabilityAsync(TitleCardViewModel vm, TitleItem item)
    {
        if (!item.MediaFileId.HasValue) return;
        var file = await _repository.GetMediaFileByIdAsync(item.MediaFileId.Value);
        if (file == null) return;
        var drive = await _repository.GetDriveByIdAsync(file.DriveId);
        vm.IsDriveConnected = drive?.IsConnected == true;
        var progress = await _repository.GetPlaybackStateAsync(file.Id);
        if (progress != null && progress.DurationSeconds > 0)
            vm.ProgressPercentage = 100 * progress.PositionSeconds / progress.DurationSeconds;
    }

    private async void PreviousPage_Click(object sender, RoutedEventArgs e) { _page--; await RefreshResultsAsync(); }
    private async void NextPage_Click(object sender, RoutedEventArgs e) { _page++; await RefreshResultsAsync(); }

    private async void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            FilterAll.Tag = null;
            FilterMovies.Tag = null;
            FilterSeries.Tag = null;
            FilterDocs.Tag = null;
            btn.Tag = "Active";

            _page = 0;
            _currentFilterType = btn.Name switch
            {
                nameof(FilterMovies) => MediaType.Movie,
                nameof(FilterSeries) => MediaType.TvShow,
                nameof(FilterDocs) => MediaType.Documentary,
                _ => null
            };

            await RefreshResultsAsync();
        }
    }

    private async void GenreComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_repository != null && GenreComboBox.SelectedItem is string genre)
        {
            _page = 0;
            _currentGenre = genre;
            await RefreshResultsAsync();
        }
    }
}
