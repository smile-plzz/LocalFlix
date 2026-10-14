using LocalFlix.Core.Models;

namespace LocalFlix.Core.Interfaces;

public record ParsedMediaInfo
{
    public string RawTitle { get; init; } = string.Empty;
    public string CleanTitle { get; init; } = string.Empty;
    public int? Year { get; init; }
    public int? SeasonNumber { get; init; }
    public int? EpisodeNumber { get; init; }
    public string? Resolution { get; init; }
    public string? Source { get; init; }
    public string? VideoCodec { get; init; }
    public string? AudioCodec { get; init; }
    public string? Language { get; init; }
    public bool IsMultiEpisode { get; init; }
    public int? SecondEpisodeNumber { get; init; }
    public MediaType InferredType { get; init; } = MediaType.Unknown;
}

public record ScanProgress(
    string CurrentPath,
    int FilesDiscovered,
    int FilesProcessed,
    int FilesMatched,
    int FilesNeedingReview,
    string? Message = null);

public interface IFilenameParser
{
    ParsedMediaInfo Parse(string filename, string? parentDirectoryName = null);
}

public interface IMediaClassifier
{
    MediaType Classify(string fullPath, long fileSizeBytes, double durationSeconds, ParsedMediaInfo parsedInfo);
    bool IsPersonalVideo(string fullPath, long fileSizeBytes, double durationSeconds, ParsedMediaInfo parsedInfo, out string reason);
    bool IsNoise(string fullPath, long fileSizeBytes);
}

public interface IFileScanner
{
    IAsyncEnumerable<string> DiscoverFilesAsync(string rootPath, CancellationToken cancellationToken = default);
    Task ScanDirectoryAsync(
        string rootPath,
        int driveId,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
