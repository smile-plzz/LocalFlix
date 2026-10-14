using LocalFlix.Core.Interfaces;
using LocalFlix.Core.Models;
using LocalFlix.Metadata;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace LocalFlix.App.Views;

public class CandidateSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string Type { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
}

public partial class NeedsReviewView : UserControl
{
    private readonly ILibraryRepository _repository;
    private readonly MetadataOrchestrator _orchestrator;
    private readonly IMetadataProvider _primaryProvider;

    public event EventHandler? ReviewResolved;

    public NeedsReviewView(
        ILibraryRepository repository,
        MetadataOrchestrator orchestrator,
        IMetadataProvider primaryProvider)
    {
        InitializeComponent();
        _repository = repository;
        _orchestrator = orchestrator;
        _primaryProvider = primaryProvider;

        Loaded += async (s, e) => await LoadReviewItemsAsync();
    }

    public async Task LoadReviewItemsAsync()
    {
        ReviewItemsContainer.Children.Clear();
        var pendingItems = await _repository.GetPendingReviewItemsAsync();

        if (pendingItems.Count == 0)
        {
            var emptyPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 60, 0, 0) };
            emptyPanel.Children.Add(new TextBlock { Text = "✨", FontSize = 36, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10) });
            emptyPanel.Children.Add(new TextBlock { Text = "Everything is matched!", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), HorizontalAlignment = HorizontalAlignment.Center });
            emptyPanel.Children.Add(new TextBlock { Text = "No files currently need review.", FontSize = 13, Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"), HorizontalAlignment = HorizontalAlignment.Center });
            ReviewItemsContainer.Children.Add(emptyPanel);
            return;
        }

        foreach (var item in pendingItems)
        {
            var mediaFile = await _repository.GetMediaFileByIdAsync(item.MediaFileId);
            var card = CreateReviewCard(item, mediaFile);
            ReviewItemsContainer.Children.Add(card);
        }
    }

    private Border CreateReviewCard(ReviewItem item, MediaFile? file)
    {
        var border = new Border
        {
            Background = (System.Windows.Media.Brush)FindResource("SurfaceDarkBrush"),
            BorderBrush = (System.Windows.Media.Brush)FindResource("BorderSubtleBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 0, 0, 16)
        };

        var mainStack = new StackPanel();

        // Top info row
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var fileInfo = new StackPanel();
        var filePathText = new TextBlock
        {
            Text = file?.RelativePath ?? "Unknown File",
            Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
            FontSize = 15,
            FontWeight = FontWeights.SemiBold
        };
        var reasonText = new TextBlock
        {
            Text = item.Reason,
            Foreground = (System.Windows.Media.Brush)FindResource("AccentRedBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0)
        };
        fileInfo.Children.Add(filePathText);
        fileInfo.Children.Add(reasonText);
        Grid.SetColumn(fileInfo, 0);

        var dismissBtn = new Button
        {
            Content = "Mark Resolved",
            Style = (Style)FindResource("CinematicButton"),
            Padding = new Thickness(12, 6, 12, 6),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        dismissBtn.Click += async (s, e) =>
        {
            await _repository.ResolveReviewItemAsync(item.Id);
            await LoadReviewItemsAsync();
            ReviewResolved?.Invoke(this, EventArgs.Empty);
        };
        Grid.SetColumn(dismissBtn, 1);

        headerGrid.Children.Add(fileInfo);
        headerGrid.Children.Add(dismissBtn);
        mainStack.Children.Add(headerGrid);

        // Candidate suggestions if available
        if (!string.IsNullOrEmpty(item.CandidateMatchesJson) && item.CandidateMatchesJson != "[]")
        {
            try
            {
                var candidates = JsonSerializer.Deserialize<List<CandidateSummaryDto>>(item.CandidateMatchesJson);
                if (candidates != null && candidates.Count > 0)
                {
                    var candLabel = new TextBlock
                    {
                        Text = "SUGGESTED CANDIDATES:",
                        Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 14, 0, 8)
                    };
                    mainStack.Children.Add(candLabel);

                    var candidatesStack = new StackPanel();
                    foreach (var cand in candidates)
                    {
                        var candRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                        candRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        candRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                        var candTitle = new TextBlock
                        {
                            Text = $"{cand.Title} ({cand.Year}) - Confidence: {cand.ConfidenceScore:P0}",
                            Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
                            FontSize = 13,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        Grid.SetColumn(candTitle, 0);

                        var selectBtn = new Button
                        {
                            Content = "Accept This Match",
                            Style = (Style)FindResource("CinematicButton"),
                            Padding = new Thickness(10, 4, 10, 4),
                            FontSize = 11,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        selectBtn.Click += async (s, e) =>
                        {
                            await _orchestrator.ApplyManualMatchAsync(
                                item.MediaFileId,
                                cand.Id,
                                _primaryProvider,
                                cand.Type == "TvShow" ? MediaType.TvShow : MediaType.Movie,
                                item.Id);
                            await LoadReviewItemsAsync();
                            ReviewResolved?.Invoke(this, EventArgs.Empty);
                        };
                        Grid.SetColumn(selectBtn, 1);

                        candRow.Children.Add(candTitle);
                        candRow.Children.Add(selectBtn);
                        candidatesStack.Children.Add(candRow);
                    }
                    mainStack.Children.Add(candidatesStack);
                }
            }
            catch
            {
            }
        }

        // Custom ID lookup input
        var customRow = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        customRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        customRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var idInput = new TextBox
        {
            Height = 32,
            Background = (System.Windows.Media.Brush)FindResource("BackgroundDarkBrush"),
            BorderBrush = (System.Windows.Media.Brush)FindResource("BorderSubtleBrush"),
            Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
            Padding = new Thickness(8, 6, 8, 6),
            FontSize = 12
        };
        Grid.SetColumn(idInput, 0);

        var matchIdBtn = new Button
        {
            Content = "Apply ID",
            Style = (Style)FindResource("AccentButton"),
            Padding = new Thickness(14, 6, 14, 6),
            FontSize = 12,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        matchIdBtn.Click += async (s, e) =>
        {
            var enteredId = idInput.Text.Trim();
            if (!string.IsNullOrEmpty(enteredId))
            {
                try
                {
                    await _orchestrator.ApplyManualMatchAsync(
                        item.MediaFileId,
                        enteredId,
                        _primaryProvider,
                        MediaType.Movie,
                        item.Id);
                    await LoadReviewItemsAsync();
                    ReviewResolved?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Match Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        };
        Grid.SetColumn(matchIdBtn, 1);

        customRow.Children.Add(idInput);
        customRow.Children.Add(matchIdBtn);
        mainStack.Children.Add(customRow);

        border.Child = mainStack;
        return border;
    }
}
