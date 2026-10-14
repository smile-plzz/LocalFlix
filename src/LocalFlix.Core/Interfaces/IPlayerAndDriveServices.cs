using LocalFlix.Core.Models;

namespace LocalFlix.Core.Interfaces;

public record DriveChangedEventArgs(Drive Drive, bool IsArrival);

public interface IDriveService
{
    event EventHandler<DriveChangedEventArgs>? DriveChanged;
    IReadOnlyList<Drive> GetConnectedDrives();
    string? ResolveFullPath(Drive drive, string relativePath);
    string? ResolveFullPath(MediaFile mediaFile);
    string GetVolumeGuidForPath(string path);
    string GetVolumeSerialForPath(string path);
    string GetVolumeLabelForPath(string path);
}

public record AudioTrackInfo(int Id, string Name, string Language);
public record SubtitleTrackInfo(int Id, string Name, string Language);

public interface IPlayerService : IDisposable
{
    event EventHandler<double>? PositionChanged;
    event EventHandler<double>? DurationChanged;
    event EventHandler? EndReached;
    event EventHandler<string>? ErrorOccurred;

    bool IsPlaying { get; }
    double CurrentPositionSeconds { get; }
    double DurationSeconds { get; }
    int Volume { get; set; }
    float PlaybackRate { get; set; }

    Task PlayAsync(string mediaPath, double startPositionSeconds = 0);
    Task PauseAsync();
    Task ResumeAsync();
    Task StopAsync();
    Task SeekAsync(double positionSeconds);

    IReadOnlyList<AudioTrackInfo> GetAudioTracks();
    void SetAudioTrack(int trackId);

    IReadOnlyList<SubtitleTrackInfo> GetSubtitleTracks();
    void SetSubtitleTrack(int trackId);
    void SetSubtitleDelay(long delayMs);
}

public interface IContentSource
{
    string SourceName { get; }
    bool IsOnline { get; }
    Task<IReadOnlyList<TitleItem>> GetHomeRowsAsync();
}
