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
            if (e.OldValue is TitleCardViewModel old) old.PropertyChanged -= control.Item_PropertyChanged;
            item.PropertyChanged += control.Item_PropertyChanged;
            control.UpdateVisuals(item);
        }
    }

    private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (Item == null) return;
        MissingDriveOverlay.Visibility = Item.IsDriveConnected ? Visibility.Collapsed : Visibility.Visible;
        WatchProgressBar.Visibility = Item.ProgressPercentage > 0 ? Visibility.Visible : Visibility.Collapsed;
        WatchProgressBar.Value = Item.ProgressPercentage;
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
                bmp.DecodePixelWidth = 340;
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

    private void OnCardKeyDown(object sender, KeyEventArgs e)
    {
        if ((e.Key == Key.Enter || e.Key == Key.Space) && Item != null)
        {
            CardClicked?.Invoke(this, Item);
            e.Handled = true;
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
