using LocalFlix.Core.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LocalFlix.App.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public class TitleCardViewModel : ViewModelBase
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int? Year { get; init; }
    public double? Rating { get; init; }
    public string? PosterPath { get; init; }
    public string? BackdropPath { get; init; }
    public MediaType Type { get; init; }
    private bool _isDriveConnected = true;
    public bool IsDriveConnected { get => _isDriveConnected; set => SetField(ref _isDriveConnected, value); }
    private double _progressPercentage;
    public double ProgressPercentage { get => _progressPercentage; set => SetField(ref _progressPercentage, value); }

    public string DisplayYear => Year.HasValue ? Year.Value.ToString() : string.Empty;
    public string DisplayRating => Rating.HasValue ? $"★ {Rating.Value:0.0}" : string.Empty;
    public string TypeBadge => Type switch
    {
        MediaType.Movie => "MOVIE",
        MediaType.TvShow => "SERIES",
        MediaType.Documentary => "DOC",
        _ => "VIDEO"
    };
}
