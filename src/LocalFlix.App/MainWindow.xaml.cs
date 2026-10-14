using LocalFlix.Core.Interfaces;
using LocalFlix.Data;
using System.Windows;
using System.Windows.Controls;

namespace LocalFlix.App;

public partial class MainWindow : Window
{
    private readonly ILibraryRepository _repository;
    private readonly IImageCacheService _imageCache;
    private readonly IMetadataProvider _metadataProvider;

    public MainWindow(
        ILibraryRepository repository,
        IImageCacheService imageCache,
        IMetadataProvider metadataProvider)
    {
        InitializeComponent();
        _repository = repository;
        _imageCache = imageCache;
        _metadataProvider = metadataProvider;

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
        // Placeholder simple text block until view controls are loaded in M3
        var panel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var title = new TextBlock
        {
            Text = $"LocalFlix: {pageName}",
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var subtitle = new TextBlock
        {
            Text = "Ready to scan folders and browse titles.",
            FontSize = 14,
            Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        panel.Children.Add(title);
        panel.Children.Add(subtitle);

        MainContentHost.Content = panel;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Will trigger search in M3
    }

    private void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        // Will open folder scan dialog in M1/M3
    }
}