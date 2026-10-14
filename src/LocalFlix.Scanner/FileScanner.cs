using LocalFlix.Core.Interfaces;
using LocalFlix.Core.Models;
using System.IO;

namespace LocalFlix.Scanner;

public class CandidateFolder
{
    public string Path { get; set; } = string.Empty;
    public int VideoFileCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public string SuggestedType { get; set; } = "Mixed";
}

public class FileScanner : IFileScanner
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".m4v",
        ".ts", ".m2ts", ".webm", ".mpg", ".mpeg"
    };

    private readonly ILibraryRepository _repository;
    private readonly IFilenameParser _parser;
    private readonly IMediaClassifier _classifier;

    public FileScanner(
        ILibraryRepository repository,
        IFilenameParser parser,
        IMediaClassifier classifier)
    {
        _repository = repository;
        _parser = parser;
        _classifier = classifier;
    }

    public async IAsyncEnumerable<string> DiscoverFilesAsync(
        string rootPath,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(rootPath)) yield break;

        var dirQueue = new Queue<string>();
        dirQueue.Enqueue(rootPath);

        while (dirQueue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentDir = dirQueue.Dequeue();

            string[] subDirs;
            try
            {
                subDirs = Directory.GetDirectories(currentDir);
            }
            catch
            {
                continue; // Skip inaccessible directories without failing scan
            }

            foreach (var sub in subDirs)
            {
                var dirName = Path.GetFileName(sub).ToLowerInvariant();
                if (dirName.StartsWith(".") || dirName == "$recycle.bin" || dirName == "system volume information")
                {
                    continue;
                }
                dirQueue.Enqueue(sub);
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(currentDir);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file);
                if (VideoExtensions.Contains(ext))
                {
                    yield return file;
                }
            }

            // HDD friendliness: slight async throttle so mechanical drives are not overwhelmed
            await Task.Yield();
        }
    }

    public async Task ScanDirectoryAsync(
        string rootPath,
        int driveId,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(rootPath)) return;

        int discovered = 0;
        int processed = 0;
        int matched = 0;
        int needingReview = 0;

        await foreach (var file in DiscoverFilesAsync(rootPath, cancellationToken))
        {
            discovered++;
            cancellationToken.ThrowIfCancellationRequested();

            var fileInfo = new FileInfo(file);
            if (_classifier.IsNoise(file, fileInfo.Length))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(rootPath, file);
            var parentDir = Path.GetFileName(Path.GetDirectoryName(file));

            // Incremental rescan check: if existing file has same size and modified date, skip reprocessing
            var existing = await _repository.GetMediaFileByRelativePathAsync(driveId, relativePath);
            if (existing != null && existing.FileSizeBytes == fileInfo.Length && existing.DateModifiedUtc == fileInfo.LastWriteTimeUtc)
            {
                processed++;
                progress?.Report(new ScanProgress(file, discovered, processed, matched, needingReview, "Skipping unchanged file"));
                continue;
            }

            var parsed = _parser.Parse(fileInfo.Name, parentDir);
            var classification = _classifier.Classify(file, fileInfo.Length, 0, parsed);

            var mediaFile = existing ?? new MediaFile
            {
                DriveId = driveId,
                RelativePath = relativePath
            };

            mediaFile.FileSizeBytes = fileInfo.Length;
            mediaFile.DateModifiedUtc = fileInfo.LastWriteTimeUtc;
            mediaFile.Classification = classification;
            mediaFile.VideoCodec = parsed.VideoCodec ?? string.Empty;
            mediaFile.AudioCodec = parsed.AudioCodec ?? string.Empty;

            if (classification == MediaType.Personal)
            {
                mediaFile.Status = FileStatus.Excluded;
                var fileId = await _repository.UpsertMediaFileAsync(mediaFile);

                _classifier.IsPersonalVideo(file, fileInfo.Length, 0, parsed, out var reason);
                await _repository.UpsertReviewItemAsync(new ReviewItem
                {
                    MediaFileId = fileId,
                    Reason = $"Excluded personal video: {reason}",
                    CandidateMatchesJson = "[]"
                });

                needingReview++;
            }
            else
            {
                mediaFile.Status = FileStatus.Active;
                await _repository.UpsertMediaFileAsync(mediaFile);
                matched++;
            }

            processed++;
            progress?.Report(new ScanProgress(file, discovered, processed, matched, needingReview));
        }
    }

    public async Task<IReadOnlyList<CandidateFolder>> DiscoverCandidateFoldersAsync(
        string driveRoot,
        CancellationToken cancellationToken = default)
    {
        var candidates = new Dictionary<string, (int count, long size)>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(driveRoot)) return Array.Empty<CandidateFolder>();

        await foreach (var file in DiscoverFilesAsync(driveRoot, cancellationToken))
        {
            var dir = Path.GetDirectoryName(file);
            if (string.IsNullOrEmpty(dir)) continue;

            // Roll up to top-level or second-level directory under drive root
            var relative = Path.GetRelativePath(driveRoot, dir);
            var topSegment = relative.Split(Path.DirectorySeparatorChar)[0];
            var candidatePath = Path.Combine(driveRoot, topSegment);

            var fileLen = new FileInfo(file).Length;
            if (candidates.TryGetValue(candidatePath, out var val))
            {
                candidates[candidatePath] = (val.count + 1, val.size + fileLen);
            }
            else
            {
                candidates[candidatePath] = (1, fileLen);
            }
        }

        return candidates
            .Where(c => c.Value.count >= 2)
            .OrderByDescending(c => c.Value.count)
            .Select(c => new CandidateFolder
            {
                Path = c.Key,
                VideoFileCount = c.Value.count,
                TotalSizeBytes = c.Value.size,
                SuggestedType = GuessFolderType(c.Key)
            })
            .ToList();
    }

    private static string GuessFolderType(string folderPath)
    {
        var lower = Path.GetFileName(folderPath).ToLowerInvariant();
        if (lower.Contains("movie") || lower.Contains("film")) return "Movies";
        if (lower.Contains("tv") || lower.Contains("series") || lower.Contains("show")) return "TV Shows";
        if (lower.Contains("doc")) return "Documentaries";
        if (lower.Contains("anime")) return "Anime";
        return "Entertainment";
    }
}
