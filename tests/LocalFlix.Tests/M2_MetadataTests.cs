using LocalFlix.Core.Interfaces;
using LocalFlix.Core.Models;
using LocalFlix.Data;
using LocalFlix.Metadata;
using System.IO;
using Xunit;

namespace LocalFlix.Tests;

public class M2_MetadataTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqliteLibraryRepository _repository;
    private readonly FakeMetadataProvider _fakeProvider;
    private readonly ImageCacheService _imageCache;
    private readonly MetadataOrchestrator _orchestrator;

    public M2_MetadataTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"LocalFlix_MetaTest_{Guid.NewGuid():N}.db");
        var migrator = new DatabaseMigrator(_tempDbPath);
        migrator.Migrate();

        _repository = new SqliteLibraryRepository(_tempDbPath);
        _fakeProvider = new FakeMetadataProvider();
        _imageCache = new ImageCacheService(Path.Combine(Path.GetTempPath(), $"LocalFlix_ImgCache_{Guid.NewGuid():N}"));
        _orchestrator = new MetadataOrchestrator(_repository, _imageCache, _fakeProvider);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
            var bak = $"{_tempDbPath}.bak";
            if (File.Exists(bak)) File.Delete(bak);
            if (Directory.Exists(_imageCache.GetLocalCacheDirectory()))
            {
                Directory.Delete(_imageCache.GetLocalCacheDirectory(), true);
            }
        }
        catch
        {
        }
    }

    [Theory]
    [InlineData("The Dark Knight", "The Dark Knight", 1.0)]
    [InlineData("Dark Knight", "The Dark Knight", 0.85)]
    [InlineData("Inception", "Inception", 1.0)]
    [InlineData("Breaking Bad", "Breaking Bad", 1.0)]
    [InlineData("Totally Different Movie", "The Shawshank Redemption", 0.30)]
    public void FuzzyMatcher_AccuratelyEvaluatesTitleSimilarity(string input, string candidate, double minExpected)
    {
        var similarity = FuzzyMatcher.CalculateTitleSimilarity(input, candidate);
        if (minExpected >= 0.80)
        {
            Assert.True(similarity >= minExpected, $"Expected similarity >= {minExpected}, got {similarity}");
        }
        else
        {
            Assert.True(similarity < 0.40, $"Expected similarity < 0.40, got {similarity}");
        }
    }

    [Fact]
    public async Task MetadataOrchestrator_AutoAcceptsHighConfidenceMatch()
    {
        var drive = new Drive { VolumeGuid = @"\\?\Volume{m2-guid}\", Label = "MediaDrive", LastKnownLetter = "D:" };
        var driveId = await _repository.UpsertDriveAsync(drive);

        var file = new MediaFile
        {
            DriveId = driveId,
            RelativePath = @"Movies\The.Dark.Knight.2008.1080p.mkv",
            Classification = MediaType.Movie
        };
        var fileId = await _repository.UpsertMediaFileAsync(file);
        file.Id = fileId;

        var parsed = new ParsedMediaInfo
        {
            CleanTitle = "The Dark Knight",
            Year = 2008,
            InferredType = MediaType.Movie
        };

        var title = await _orchestrator.MatchMediaFileAsync(file, parsed, @"D:\Movies\The.Dark.Knight.2008.1080p.mkv");

        Assert.NotNull(title);
        Assert.Equal("The Dark Knight", title.Name);
        Assert.Equal(2008, title.Year);
        Assert.Equal(MatchMethod.Auto, title.MatchedBy);
        Assert.True(title.ConfidenceScore >= 0.85);

        // Verify title in database
        var dbTitle = await _repository.GetTitleByIdAsync(title.Id);
        Assert.NotNull(dbTitle);
        Assert.Equal(9.0, dbTitle.Rating);

        // Review queue should be empty
        var reviews = await _repository.GetPendingReviewItemsAsync();
        Assert.Empty(reviews);
    }

    [Fact]
    public async Task MetadataOrchestrator_RoutesAmbiguousOrMissingMatchToNeedsReviewQueue()
    {
        var drive = new Drive { VolumeGuid = @"\\?\Volume{m2-guid-2}\", Label = "MediaDrive", LastKnownLetter = "D:" };
        var driveId = await _repository.UpsertDriveAsync(drive);

        var file = new MediaFile
        {
            DriveId = driveId,
            RelativePath = @"Movies\Mysterious.Uncataloged.Film.2024.mkv",
            Classification = MediaType.Movie
        };
        var fileId = await _repository.UpsertMediaFileAsync(file);
        file.Id = fileId;

        var parsed = new ParsedMediaInfo
        {
            CleanTitle = "Mysterious Uncataloged Film",
            Year = 2024,
            InferredType = MediaType.Movie
        };

        var title = await _orchestrator.MatchMediaFileAsync(file, parsed, @"D:\Movies\Mysterious.Uncataloged.Film.2024.mkv");

        Assert.Null(title);

        // Review queue should have pending item
        var reviews = await _repository.GetPendingReviewItemsAsync();
        Assert.Single(reviews);
        Assert.Equal(fileId, reviews[0].MediaFileId);
        Assert.False(reviews[0].IsResolved);
    }

    [Fact]
    public async Task MetadataOrchestrator_AppliesManualMatchAndResolvesReviewItem()
    {
        var drive = new Drive { VolumeGuid = @"\\?\Volume{m2-guid-3}\", Label = "MediaDrive", LastKnownLetter = "D:" };
        var driveId = await _repository.UpsertDriveAsync(drive);

        var file = new MediaFile
        {
            DriveId = driveId,
            RelativePath = @"Movies\Unknown.File.mkv",
            Classification = MediaType.Movie
        };
        var fileId = await _repository.UpsertMediaFileAsync(file);

        var reviewId = await _repository.UpsertReviewItemAsync(new ReviewItem
        {
            MediaFileId = fileId,
            Reason = "User manual review required"
        });

        // User chooses to match with Inception
        var manualTitle = await _orchestrator.ApplyManualMatchAsync(
            fileId,
            "tmdb-103",
            _fakeProvider,
            MediaType.Movie,
            reviewId);

        Assert.NotNull(manualTitle);
        Assert.Equal("Inception", manualTitle.Name);
        Assert.Equal(MatchMethod.Manual, manualTitle.MatchedBy);

        // Review item should now be marked resolved
        var pendingReviews = await _repository.GetPendingReviewItemsAsync();
        Assert.Empty(pendingReviews);
    }
}
