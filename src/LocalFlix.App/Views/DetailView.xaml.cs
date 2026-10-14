using LocalFlix.Core.Interfaces;
using LocalFlix.Core.Models;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace LocalFlix.App.Views;

public partial class DetailView : UserControl
{
    private readonly ILibraryRepository _repository;
    private TitleItem _title;
    private IReadOnlyList<Season> _seasons = Array.Empty<Season>();

    public event EventHandler? BackRequested;
    public event EventHandler<TitleItem>? PlayRequested;
    public event EventHandler<Episode>? EpisodePlayRequested;
    public event EventHandler<TitleItem>? FixMatchRequested;

    public DetailView(ILibraryRepository repository, TitleItem title)
    {
        InitializeComponent();
        _repository = repository;
        _title = title;

        Loaded += async (s, e) => await LoadDetailsAsync();
    }

    public async Task LoadDetailsAsync()
    {
        TitleName.Text = _title.Name;
        YearText.Text = _title.Year?.ToString() ?? string.Empty;
        OverviewText.Text = _title.Overview ?? "No description available.";
        GenresText.Text = _title.Genres ?? string.Empty;
        DirectorText.Text = _title.Director ?? "Unknown";
        CastText.Text = _title.CastMembers ?? "None listed";

        if (_title.RuntimeMinutes.HasValue && _title.RuntimeMinutes > 0)
        {
            var hours = _title.RuntimeMinutes.Value / 60;
            var mins = _title.RuntimeMinutes.Value % 60;
            RuntimeText.Text = hours > 0 ? $"{hours}h {mins}m" : $"{mins}m";
            RuntimeText.Visibility = Visibility.Visible;
        }
        else
        {
            RuntimeText.Visibility = Visibility.Collapsed;
        }

        if (_title.Rating.HasValue)
        {
            RatingBorder.Visibility = Visibility.Visible;
            RatingText.Text = $"★ {_title.Rating.Value:0.0}";
        }
        else
        {
            RatingBorder.Visibility = Visibility.Collapsed;
        }

        // Load artwork
        LoadImage(PosterImage, _title.PosterPath);
        LoadImage(BackdropImage, _title.BackdropPath ?? _title.PosterPath);

        // Check Watchlist status
        var isWatchlist = await _repository.IsInUserListAsync(_title.Id, UserListType.Watchlist);
        WatchlistButton.Content = isWatchlist ? "✓ In Watchlist" : "+ Watchlist";

        // Check if TV series
        if (_title.Type == MediaType.TvShow)
        {
            TvSeriesSection.Visibility = Visibility.Visible;
            var series = await _repository.GetSeriesByTitleIdAsync(_title.Id);
            if (series != null)
            {
                _seasons = await _repository.GetSeasonsBySeriesIdAsync(series.Id);
                SeasonComboBox.Items.Clear();
                foreach (var season in _seasons)
                {
                    SeasonComboBox.Items.Add($"Season {season.SeasonNumber} ({season.EpisodeCount} eps)");
                }
                if (SeasonComboBox.Items.Count > 0)
                {
                    SeasonComboBox.SelectedIndex = 0;
                }
            }
        }
        else
        {
            TvSeriesSection.Visibility = Visibility.Collapsed;
        }
    }

    private async void SeasonComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SeasonComboBox.SelectedIndex >= 0 && SeasonComboBox.SelectedIndex < _seasons.Count)
        {
            var selectedSeason = _seasons[SeasonComboBox.SelectedIndex];
            var episodes = await _repository.GetEpisodesBySeasonIdAsync(selectedSeason.Id);
            PopulateEpisodes(episodes);
        }
    }

    private void PopulateEpisodes(IReadOnlyList<Episode> episodes)
    {
        EpisodesListPanel.Children.Clear();
        foreach (var ep in episodes)
        {
            var border = new Border
            {
                Background = (System.Windows.Media.Brush)FindResource("SurfaceDarkBrush"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("BorderSubtleBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var numText = new TextBlock
            {
                Text = $"E{ep.EpisodeNumber}",
                Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(numText, 0);

            var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var nameText = new TextBlock
            {
                Text = string.IsNullOrEmpty(ep.Name) ? $"Episode {ep.EpisodeNumber}" : ep.Name,
                Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold
            };
            var overviewText = new TextBlock
            {
                Text = ep.Overview ?? string.Empty,
                Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxHeight = 36,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            };
            infoPanel.Children.Add(nameText);
            if (!string.IsNullOrEmpty(ep.Overview)) infoPanel.Children.Add(overviewText);
            Grid.SetColumn(infoPanel, 1);

            var playBtn = new Button
            {
                Content = "▶ Play",
                Style = (Style)FindResource("CinematicButton"),
                Padding = new Thickness(14, 6, 14, 6),
                VerticalAlignment = VerticalAlignment.Center
            };
            playBtn.Click += (s, e) => EpisodePlayRequested?.Invoke(this, ep);
            Grid.SetColumn(playBtn, 2);

            grid.Children.Add(numText);
            grid.Children.Add(infoPanel);
            grid.Children.Add(playBtn);
            border.Child = grid;

            EpisodesListPanel.Children.Add(border);
        }
    }

    private static void LoadImage(Image target, string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            target.Source = null;
            return;
        }

        try
        {
            var uri = new Uri(path, UriKind.RelativeOrAbsolute);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = uri;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            target.Source = bmp;
        }
        catch
        {
            target.Source = null;
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
    private void PlayButton_Click(object sender, RoutedEventArgs e) => PlayRequested?.Invoke(this, _title);
    private void FixMatchButton_Click(object sender, RoutedEventArgs e) => FixMatchRequested?.Invoke(this, _title);

    private async void WatchlistButton_Click(object sender, RoutedEventArgs e)
    {
        var inList = await _repository.IsInUserListAsync(_title.Id, UserListType.Watchlist);
        if (inList)
        {
            await _repository.RemoveFromUserListAsync(_title.Id, UserListType.Watchlist);
            WatchlistButton.Content = "+ Watchlist";
        }
        else
        {
            await _repository.AddToUserListAsync(_title.Id, UserListType.Watchlist);
            WatchlistButton.Content = "✓ In Watchlist";
        }
    }
}
