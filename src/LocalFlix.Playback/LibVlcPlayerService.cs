using LibVLCSharp.Shared;
using LocalFlix.Core.Interfaces;
using System.IO;

namespace LocalFlix.Playback;

public class LibVlcPlayerService : IPlayerService
{
    private static bool _initialized = false;
    private static readonly object _initLock = new();

    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private string? _currentFilePath;

    public event EventHandler<double>? PositionChanged;
    public event EventHandler<double>? DurationChanged;
    public event EventHandler? EndReached;
    public event EventHandler<string>? ErrorOccurred;

    public bool IsPlaying => _mediaPlayer?.IsPlaying ?? false;
    public double CurrentPositionSeconds => (_mediaPlayer?.Time ?? 0) / 1000.0;
    public double DurationSeconds => (_mediaPlayer?.Length ?? 0) / 1000.0;

    public int Volume
    {
        get => _mediaPlayer?.Volume ?? 100;
        set
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Volume = Math.Clamp(value, 0, 100);
            }
        }
    }

    public float PlaybackRate
    {
        get => _mediaPlayer?.Rate ?? 1.0f;
        set
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.SetRate(value);
            }
        }
    }

    public MediaPlayer? MediaPlayer => _mediaPlayer;

    public LibVlcPlayerService(bool enableHardwareAcceleration = true, string subtitleLanguage = "English")
    {
        EnsureInitialized();

        var options = new List<string>
        {
            "--no-video-title-show",
            "--mouse-hide-timeout=2000"
        };

        var language = subtitleLanguage switch
        {
            "Bengali" => "ben", "Spanish" => "spa", "French" => "fra", "German" => "deu",
            "Hindi" => "hin", "Japanese" => "jpn", _ => "eng"
        };
        options.Add("--sub-language=" + language);
        if (!enableHardwareAcceleration) options.Add("--avcodec-hw=none");
        if (enableHardwareAcceleration)
        {
            options.Add("--avcodec-hw=any");
        }

        try
        {
            _libVLC = new LibVLC(options.ToArray());
            _mediaPlayer = new MediaPlayer(_libVLC);
            HookEvents();
        }
        catch (Exception ex)
        {
            // LibVLC initialization fallback (e.g. headless or mock test environments)
            System.Diagnostics.Debug.WriteLine($"LibVLC initialization notice: {ex.Message}");
        }
    }

    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            lock (_initLock)
            {
                if (!_initialized)
                {
                    try
                    {
                        LibVLCSharp.Shared.Core.Initialize();
                    }
                    catch
                    {
                        // In some CLI or test environments, native library resolution may already be handled or mock
                    }
                    _initialized = true;
                }
            }
        }
    }

    private void HookEvents()
    {
        if (_mediaPlayer == null) return;

        _mediaPlayer.TimeChanged += (s, e) =>
        {
            PositionChanged?.Invoke(this, e.Time / 1000.0);
        };

        _mediaPlayer.LengthChanged += (s, e) =>
        {
            DurationChanged?.Invoke(this, e.Length / 1000.0);
        };

        _mediaPlayer.EndReached += (s, e) =>
        {
            EndReached?.Invoke(this, EventArgs.Empty);
        };

        _mediaPlayer.EncounteredError += (s, e) =>
        {
            ErrorOccurred?.Invoke(this, "LibVLC encountered a playback error.");
        };
    }

    public Task PlayAsync(string mediaPath, double startPositionSeconds = 0)
    {
        _currentFilePath = mediaPath;

        if (_libVLC == null || _mediaPlayer == null)
        {
            ErrorOccurred?.Invoke(this, "The native player could not load. Reinstall the complete LocalFlix package.");
            return Task.CompletedTask;
        }

        try
        {
            using var media = new Media(_libVLC, mediaPath, FromType.FromPath);
            if (startPositionSeconds > 0) media.AddOption($":start-time={startPositionSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            _mediaPlayer.Play(media);

            // Auto-detect beside external subtitles (.srt, .ass, .vtt)
            AttachExternalSubtitles(mediaPath);


        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex.Message);
        }

        return Task.CompletedTask;
    }

    private void AttachExternalSubtitles(string videoPath)
    {
        if (_mediaPlayer == null) return;

        var dir = Path.GetDirectoryName(videoPath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

        var baseName = Path.GetFileNameWithoutExtension(videoPath);
        var subExtensions = new[] { ".srt", ".ass", ".ssa", ".vtt", ".sub" };

        try
        {
            foreach (var ext in subExtensions)
            {
                var candidate = Path.Combine(dir, $"{baseName}{ext}");
                if (File.Exists(candidate))
                {
                    _mediaPlayer.AddSlave(MediaSlaveType.Subtitle, candidate, select: true);
                }

                // Check language tags like movie.en.srt
                var langFiles = Directory.GetFiles(dir, $"{baseName}.*{ext}");
                foreach (var lf in langFiles)
                {
                    if (lf != candidate)
                    {
                        _mediaPlayer.AddSlave(MediaSlaveType.Subtitle, lf, select: false);
                    }
                }
            }
        }
        catch
        {
        }
    }

    public Task PauseAsync()
    {
        _mediaPlayer?.Pause();
        return Task.CompletedTask;
    }

    public Task ResumeAsync()
    {
        _mediaPlayer?.Play();
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _mediaPlayer?.Stop();
        return Task.CompletedTask;
    }

    public Task SeekAsync(double positionSeconds)
    {
        if (_mediaPlayer != null)
        {
            var targetMs = (long)(positionSeconds * 1000);
            _mediaPlayer.Time = Math.Max(0, targetMs);
        }
        return Task.CompletedTask;
    }

    public IReadOnlyList<AudioTrackInfo> GetAudioTracks()
    {
        if (_mediaPlayer == null) return Array.Empty<AudioTrackInfo>();

        var tracks = new List<AudioTrackInfo>();
        var count = _mediaPlayer.AudioTrackCount;
        var descriptions = _mediaPlayer.AudioTrackDescription;

        if (descriptions != null)
        {
            foreach (var desc in descriptions)
            {
                tracks.Add(new AudioTrackInfo(desc.Id, desc.Name, string.Empty));
            }
        }

        return tracks;
    }

    public void SetAudioTrack(int trackId)
    {
        if (_mediaPlayer != null)
        {
            _mediaPlayer.SetAudioTrack(trackId);
        }
    }

    public IReadOnlyList<SubtitleTrackInfo> GetSubtitleTracks()
    {
        if (_mediaPlayer == null) return Array.Empty<SubtitleTrackInfo>();

        var tracks = new List<SubtitleTrackInfo>();
        var descriptions = _mediaPlayer.SpuDescription;

        if (descriptions != null)
        {
            foreach (var desc in descriptions)
            {
                tracks.Add(new SubtitleTrackInfo(desc.Id, desc.Name, string.Empty));
            }
        }

        return tracks;
    }

    public void SetSubtitleTrack(int trackId)
    {
        if (_mediaPlayer != null)
        {
            _mediaPlayer.SetSpu(trackId);
        }
    }

    public void SetSubtitleDelay(long delayMs)
    {
        if (_mediaPlayer != null)
        {
            _mediaPlayer.SetSpuDelay(delayMs * 1000); // LibVLC takes microseconds
        }
    }

    public void Dispose()
    {
        try
        {
            _mediaPlayer?.Stop();
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
        }
        catch
        {
        }
    }
}
