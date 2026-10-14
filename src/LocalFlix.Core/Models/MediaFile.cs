namespace LocalFlix.Core.Models;

public class MediaFile
{
    public int Id { get; set; }
    public int DriveId { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime DateModifiedUtc { get; set; }
    public double DurationSeconds { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string VideoCodec { get; set; } = string.Empty;
    public string AudioCodec { get; set; } = string.Empty;
    public string AudioChannels { get; set; } = string.Empty;
    public string SubtitleTracksJson { get; set; } = string.Empty;
    public string IdentityHash { get; set; } = string.Empty;
    public MediaType Classification { get; set; } = MediaType.Unknown;
    public FileStatus Status { get; set; } = FileStatus.Active;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    // Navigation / resolved properties (non-persisted directly or joined)
    public string? FullPath { get; set; }
}
