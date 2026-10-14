using LocalFlix.Core.Interfaces;
using LocalFlix.Core.Models;
using LocalFlix.Playback;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace LocalFlix.App.Views;

public partial class PlayerWindow : Window
{
    private readonly LibVlcPlayerService _playerService;
    private readonly ILibraryRepository _repository;
    private readonly MediaFile _mediaFile;
    private readonly string _fullPath;
    private readonly string _title;
    private readonly Episode? _nextEpisode;

    private readonly DispatcherTimer _hudTimer;
    private readonly DispatcherTimer _saveProgressTimer;
    private readonly DispatcherTimer _nextEpisodeTimer;
    private int _countdownSeconds = 10;
    private bool _isUserDraggingSlider = false;
    private bool _isNextEpisodeCountdownRunning = false;
    private PlaybackState? _initialState;

    public PlayerWindow(
        ILibraryRepository repository,
        MediaFile mediaFile,
        string fullPath,
        string title,
        Episode? nextEpisode = null)
    {
        InitializeComponent();
        _repository = repository;
        _mediaFile = mediaFile;
        _fullPath = fullPath;
        _title = title;
        _nextEpisode = nextEpisode;

        _playerService = new LibVlcPlayerService(enableHardwareAcceleration: true);
        if (_playerService.MediaPlayer != null)
        {
            VideoViewControl.MediaPlayer = _playerService.MediaPlayer;
        }

        MediaTitleText.Text = _title;

        // HUD auto-hide timer (3 seconds)
        _hudTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _hudTimer.Tick += (s, e) =>
        {
            if (!_isUserDraggingSlider && ResumePromptModal.Visibility != Visibility.Visible && NextEpisodePromptModal.Visibility != Visibility.Visible)
            {
                HudOverlay.Visibility = Visibility.Collapsed;
                Mouse.OverrideCursor = Cursors.None;
            }
        };

        // Progress save timer (every 5 seconds)
        _saveProgressTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _saveProgressTimer.Tick += async (s, e) => await SavePlaybackProgressAsync();

        // Next episode countdown timer (1 second ticks)
        _nextEpisodeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _nextEpisodeTimer.Tick += (s, e) => OnNextEpisodeTick();

        HookPlayerEvents();
        Loaded += PlayerWindow_Loaded;
        Closing += PlayerWindow_Closing;
    }

    private async void PlayerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _hudTimer.Start();
        _saveProgressTimer.Start();

        // Check if there is an existing resume point
        _initialState = await _repository.GetPlaybackStateAsync(_mediaFile.Id);
        if (_initialState != null && _initialState.PositionSeconds > 30 && !_initialState.IsWatched)
        {
            var ts = TimeSpan.FromSeconds(_initialState.PositionSeconds);
            ResumeTimeText.Text = $"You were watching at {ts:hh\\:mm\\:ss}";
            ResumePromptModal.Visibility = Visibility.Visible;
        }
        else
        {
            await _playerService.PlayAsync(_fullPath, 0);
        }
    }

    private void HookPlayerEvents()
    {
        _playerService.PositionChanged += (s, pos) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (!_isUserDraggingSlider)
                {
                    SeekSlider.Value = pos;
                    CurrentTimeText.Text = FormatTime(pos);
                }

                // Check for end of episode to trigger next episode countdown
                var dur = _playerService.DurationSeconds;
                if (dur > 60 && pos >= (dur - 90) && _nextEpisode != null && !_isNextEpisodeCountdownRunning)
                {
                    ShowNextEpisodePrompt();
                }
            });
        };

        _playerService.DurationChanged += (s, dur) =>
        {
            Dispatcher.Invoke(() =>
            {
                SeekSlider.Maximum = dur;
                TotalTimeText.Text = FormatTime(dur);
                PopulateTracks();
            });
        };

        _playerService.EndReached += (s, e) =>
        {
            Dispatcher.Invoke(async () =>
            {
                await MarkAsWatchedAsync();
                if (_nextEpisode != null && !_isNextEpisodeCountdownRunning)
                {
                    ShowNextEpisodePrompt();
                }
            });
        };
    }

    private void PopulateTracks()
    {
        // Audio Tracks
        var audioTracks = _playerService.GetAudioTracks();
        AudioTracksCombo.Items.Clear();
        foreach (var t in audioTracks)
        {
            AudioTracksCombo.Items.Add(string.IsNullOrEmpty(t.Name) ? $"Track {t.Id}" : t.Name);
        }
        if (AudioTracksCombo.Items.Count > 0) AudioTracksCombo.SelectedIndex = 0;

        // Subtitle Tracks
        var subTracks = _playerService.GetSubtitleTracks();
        SubtitlesCombo.Items.Clear();
        SubtitlesCombo.Items.Add("Subtitles: Off");
        foreach (var s in subTracks)
        {
            SubtitlesCombo.Items.Add(string.IsNullOrEmpty(s.Name) ? $"Track {s.Id}" : s.Name);
        }
        if (SubtitlesCombo.Items.Count > 0) SubtitlesCombo.SelectedIndex = 0;
    }

    private void ShowNextEpisodePrompt()
    {
        _isNextEpisodeCountdownRunning = true;
        _countdownSeconds = 10;
        NextEpisodeTitleText.Text = string.IsNullOrEmpty(_nextEpisode?.Name)
            ? $"Episode {_nextEpisode?.EpisodeNumber}"
            : _nextEpisode.Name;
        NextCountdownText.Text = $"Playing in {_countdownSeconds}s...";
        NextEpisodePromptModal.Visibility = Visibility.Visible;
        _nextEpisodeTimer.Start();
        ShowHud();
    }

    private void OnNextEpisodeTick()
    {
        _countdownSeconds--;
        if (_countdownSeconds <= 0)
        {
            _nextEpisodeTimer.Stop();
            NextEpisodePromptModal.Visibility = Visibility.Collapsed;
            // Play next episode
            PlayNextEpisode();
        }
        else
        {
            NextCountdownText.Text = $"Playing in {_countdownSeconds}s...";
        }
    }

    private void PlayNextEpisode()
    {
        // Play next episode transition
        Close();
    }

    private async Task SavePlaybackProgressAsync()
    {
        if (_playerService.DurationSeconds <= 0) return;

        var pos = _playerService.CurrentPositionSeconds;
        var dur = _playerService.DurationSeconds;
        bool isWatched = pos >= (dur * 0.90) || (dur - pos) < 180;

        await _repository.UpsertPlaybackStateAsync(new PlaybackState
        {
            MediaFileId = _mediaFile.Id,
            PositionSeconds = pos,
            DurationSeconds = dur,
            IsWatched = isWatched,
            LastPlayedUtc = DateTime.UtcNow
        });
    }

    private async Task MarkAsWatchedAsync()
    {
        await _repository.UpsertPlaybackStateAsync(new PlaybackState
        {
            MediaFileId = _mediaFile.Id,
            PositionSeconds = _playerService.DurationSeconds,
            DurationSeconds = _playerService.DurationSeconds,
            IsWatched = true,
            LastPlayedUtc = DateTime.UtcNow
        });
    }

    private async void PlayerWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _hudTimer.Stop();
        _saveProgressTimer.Stop();
        _nextEpisodeTimer.Stop();
        await SavePlaybackProgressAsync();
        _playerService.Dispose();
        Mouse.OverrideCursor = null;
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        ShowHud();
    }

    private void ShowHud()
    {
        HudOverlay.Visibility = Visibility.Visible;
        Mouse.OverrideCursor = null;
        _hudTimer.Stop();
        _hudTimer.Start();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        ShowHud();
        switch (e.Key)
        {
            case Key.Space:
                TogglePlayPause();
                e.Handled = true;
                break;
            case Key.Left:
                _playerService.SeekAsync(Math.Max(0, _playerService.CurrentPositionSeconds - 10));
                e.Handled = true;
                break;
            case Key.Right:
                _playerService.SeekAsync(_playerService.CurrentPositionSeconds + 10);
                e.Handled = true;
                break;
            case Key.Up:
                VolumeSlider.Value = Math.Min(100, VolumeSlider.Value + 5);
                e.Handled = true;
                break;
            case Key.Down:
                VolumeSlider.Value = Math.Max(0, VolumeSlider.Value - 5);
                e.Handled = true;
                break;
            case Key.F:
            case Key.F11:
                ToggleFullscreen();
                e.Handled = true;
                break;
            case Key.Escape:
                if (WindowState == WindowState.Maximized && WindowStyle == WindowStyle.None)
                {
                    ToggleFullscreen();
                }
                else
                {
                    Close();
                }
                e.Handled = true;
                break;
            case Key.M:
                VolumeSlider.Value = VolumeSlider.Value > 0 ? 0 : 100;
                e.Handled = true;
                break;
        }
    }

    private void TogglePlayPause()
    {
        if (_playerService.IsPlaying)
        {
            _playerService.PauseAsync();
            PlayPauseButton.Content = "▶";
        }
        else
        {
            _playerService.ResumeAsync();
            PlayPauseButton.Content = "⏸";
        }
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e) => TogglePlayPause();
    private void SkipBackBtn_Click(object sender, RoutedEventArgs e) => _playerService.SeekAsync(Math.Max(0, _playerService.CurrentPositionSeconds - 10));
    private void SkipForwardBtn_Click(object sender, RoutedEventArgs e) => _playerService.SeekAsync(_playerService.CurrentPositionSeconds + 10);

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _playerService.Volume = (int)e.NewValue;
    }

    private void SeekSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e) => _isUserDraggingSlider = true;
    private void SeekSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isUserDraggingSlider = false;
        _playerService.SeekAsync(SeekSlider.Value);
    }
    private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUserDraggingSlider)
        {
            CurrentTimeText.Text = FormatTime(e.NewValue);
        }
    }

    private void AudioTracksCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var tracks = _playerService.GetAudioTracks();
        if (AudioTracksCombo.SelectedIndex >= 0 && AudioTracksCombo.SelectedIndex < tracks.Count)
        {
            _playerService.SetAudioTrack(tracks[AudioTracksCombo.SelectedIndex].Id);
        }
    }

    private void SubtitlesCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SubtitlesCombo.SelectedIndex == 0)
        {
            _playerService.SetSubtitleTrack(-1); // Disabled
        }
        else
        {
            var tracks = _playerService.GetSubtitleTracks();
            var targetIdx = SubtitlesCombo.SelectedIndex - 1;
            if (targetIdx >= 0 && targetIdx < tracks.Count)
            {
                _playerService.SetSubtitleTrack(tracks[targetIdx].Id);
            }
        }
    }

    private void SpeedButton_Click(object sender, RoutedEventArgs e)
    {
        var rate = _playerService.PlaybackRate;
        var nextRate = rate switch
        {
            1.0f => 1.25f,
            1.25f => 1.5f,
            1.5f => 2.0f,
            2.0f => 0.5f,
            _ => 1.0f
        };
        _playerService.PlaybackRate = nextRate;
        SpeedButton.Content = $"{nextRate:0.0}x";
    }

    private void ToggleFullscreen()
    {
        if (WindowStyle == WindowStyle.None)
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = WindowState.Normal;
            FullscreenButton.Content = "⛶";
        }
        else
        {
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            FullscreenButton.Content = "🗗";
        }
    }

    private void FullscreenButton_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();
    private void BackButton_Click(object sender, RoutedEventArgs e) => Close();

    private async void ResumeButton_Click(object sender, RoutedEventArgs e)
    {
        ResumePromptModal.Visibility = Visibility.Collapsed;
        var pos = _initialState?.PositionSeconds ?? 0;
        await _playerService.PlayAsync(_fullPath, pos);
    }

    private async void RestartButton_Click(object sender, RoutedEventArgs e)
    {
        ResumePromptModal.Visibility = Visibility.Collapsed;
        await _playerService.PlayAsync(_fullPath, 0);
    }

    private void PlayNextNowButton_Click(object sender, RoutedEventArgs e)
    {
        _nextEpisodeTimer.Stop();
        NextEpisodePromptModal.Visibility = Visibility.Collapsed;
        PlayNextEpisode();
    }

    private void CancelNextButton_Click(object sender, RoutedEventArgs e)
    {
        _nextEpisodeTimer.Stop();
        NextEpisodePromptModal.Visibility = Visibility.Collapsed;
        _isNextEpisodeCountdownRunning = true; // Prevent prompt re-showing for this session
    }

    private static string FormatTime(double seconds)
    {
        if (double.IsNaN(seconds) || seconds < 0) return "00:00";
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1 ? ts.ToString(@"hh\:mm\:ss") : ts.ToString(@"mm\:ss");
    }
}
