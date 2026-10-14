using LocalFlix.App.ViewModels;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace LocalFlix.App.UserControls;

public partial class TitleCardControl : UserControl
{
    public static readonly DependencyProperty ItemProperty =
        DependencyProperty.Register(nameof(Item), typeof(TitleCardViewModel), typeof(TitleCardControl),
            new PropertyMetadata(null, OnItemChanged));

    public TitleCardViewModel? Item
    {
        get => (TitleCardViewModel?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public event EventHandler<TitleCardViewModel>? CardClicked;

    public TitleCardControl()
    {
        InitializeComponent();
    }

    private static void OnItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TitleCardControl control && e.NewValue is TitleCardViewModel item)
        {
            control.UpdateVisuals(item);
        }
    }

    private void UpdateVisuals(TitleCardViewModel item)
    {
        TitleText.Text = item.Name;
        YearText.Text = item.DisplayYear;
        TypeBadgeText.Text = item.TypeBadge;

        if (item.Rating.HasValue)
        {
            RatingBadge.Visibility = Visibility.Visible;
            RatingText.Text = item.DisplayRating;
        }
        else
        {
            RatingBadge.Visibility = Visibility.Collapsed;
        }

        MissingDriveOverlay.Visibility = item.IsDriveConnected ? Visibility.Collapsed : Visibility.Visible;

        if (item.ProgressPercentage > 0)
        {
            WatchProgressBar.Visibility = Visibility.Visible;
            WatchProgressBar.Value = item.ProgressPercentage;
        }
        else
        {
            WatchProgressBar.Visibility = Visibility.Collapsed;
        }

        // Load poster image
        if (!string.IsNullOrEmpty(item.PosterPath))
        {
            try
            {
                var uri = new Uri(item.PosterPath, UriKind.RelativeOrAbsolute);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = uri;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                PosterImage.Source = bmp;
            }
            catch
            {
                PosterImage.Source = null;
            }
        }
        else
        {
            PosterImage.Source = null;
        }
    }

    private void OnCardClicked(object sender, MouseButtonEventArgs e)
    {
        if (Item != null)
        {
            CardClicked?.Invoke(this, Item);
        }
    }
}
