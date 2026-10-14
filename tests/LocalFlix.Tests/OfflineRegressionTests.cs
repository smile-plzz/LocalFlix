using LocalFlix.Core.Interfaces;
using LocalFlix.Core.Models;
using LocalFlix.Data;
using LocalFlix.Metadata;
using LocalFlix.Scanner;
using LocalFlix.Drives;
using System.IO;
using System.Net;
using System.Net.Http;
using Xunit;

namespace LocalFlix.Tests;

public class OfflineRegressionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "LocalFlix_Regression_" + Guid.NewGuid().ToString("N"));
    private readonly SqliteLibraryRepository _repo;
    public OfflineRegressionTests()
    {
        Directory.CreateDirectory(_root);
        var db = Path.Combine(_root, "library.db");
        new DatabaseMigrator(db).Migrate();
        _repo = new SqliteLibraryRepository(db);
    }
    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_root, true);
    }
    private async Task<MediaFile> AddFile(string path = "movie.mkv")
    {
        var drive = new Drive { VolumeGuid = "test-volume", LastKnownLetter = Path.GetPathRoot(_root)! };
        await _repo.UpsertDriveAsync(drive);
        var file = new MediaFile { DriveId = drive.Id, RelativePath = path };
        await _repo.UpsertMediaFileAsync(file);
        return file;
    }
    [Fact]
    public async Task RescanUsesVolumeRelativePathsAndKeepsOverlappingRootsDistinct()
    {
        var file = await AddFile();
        var folder = Path.Combine(_root, "Movies");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "Film.2020.mkv");
        using (var stream = File.Create(path)) stream.SetLength(40 * 1024 * 1024);
        var scanner = new FileScanner(_repo, new FilenameParser(), new MediaClassifier());
        await scanner.ScanDirectoryAsync(folder, file.DriveId);
        await scanner.ScanDirectoryAsync(_root, file.DriveId);
        var files = await _repo.GetFilesByDriveIdAsync(file.DriveId);
        var scanned = Assert.Single(files, f => f.RelativePath.EndsWith("Film.2020.mkv"));
        Assert.Equal(path, Path.Combine(Path.GetPathRoot(_root)!, scanned.RelativePath));
    }
    [Fact]
    public async Task ManualCorrectionKeepsIdentityWatchlistAndWinsOverRescan()
    {
        var file = await AddFile();
        var original = new TitleItem { Name = "Wrong", MediaFileId = file.Id };
        await _repo.UpsertTitleAsync(original);
        await _repo.AddToUserListAsync(original.Id, UserListType.Watchlist);
        var manual = new TitleItem { Name = "Correct", MediaFileId = file.Id, MatchedBy = MatchMethod.Manual };
        await _repo.UpsertTitleAsync(manual);
        await _repo.UpsertTitleAsync(new TitleItem { Name = "Wrong again", MediaFileId = file.Id });
        Assert.Equal(original.Id, manual.Id);
        Assert.Equal("Correct", Assert.Single(await _repo.GetAllTitlesAsync()).Name);
        Assert.Equal("Correct", Assert.Single(await _repo.GetUserListTitlesAsync(UserListType.Watchlist)).Name);
    }
    [Fact]
    public async Task ReviewQueueIsIdempotent()
    {
        var file = await AddFile();
        await _repo.UpsertReviewItemAsync(new ReviewItem { MediaFileId = file.Id, Reason = "First" });
        await _repo.UpsertReviewItemAsync(new ReviewItem { MediaFileId = file.Id, Reason = "Retry" });
        Assert.Equal("Retry", Assert.Single(await _repo.GetPendingReviewItemsAsync()).Reason);
    }
    [Fact]
    public async Task EpisodesShareOneSeriesTitle()
    {
        var first = await AddFile("S01E01.mkv");
        var second = await AddFile("S01E02.mkv");
        var one = new TitleItem { Type = MediaType.TvShow, Name = "Series", TmdbId = "42", MediaFileId = first.Id };
        var two = new TitleItem { Type = MediaType.TvShow, Name = "Series", TmdbId = "42", MediaFileId = second.Id };
        await _repo.UpsertTitleAsync(one);
        await _repo.UpsertTitleAsync(two);
        Assert.Equal(one.Id, two.Id);
        Assert.Single(await _repo.GetAllTitlesAsync());
    }
    [Theory]
    [InlineData("Spider-Man.1080p.BluRay.mkv", "Spider-Man")]
    [InlineData("The.Matrix.1080p.WEB-DL.mkv", "The Matrix")]
    [InlineData("1917.2019.1080p.mkv", "1917")]
    public void ParserPreservesTitlesAndStripsQualityWithoutYear(string input, string title)
        => Assert.Equal(title, new FilenameParser().Parse(input).CleanTitle);
    [Theory]
    [InlineData("Modern Family", false)]
    [InlineData("Phone Booth", false)]
    [InlineData("DCIM", true)]
    public void PersonalFolderDetectionUsesSegments(string folder, bool personal)
    {
        var path = Path.Combine(_root, folder, "Film.2020.mkv");
        Assert.Equal(personal, new MediaClassifier().IsPersonalVideo(path, 100000000, 1000, new FilenameParser().Parse("Film.2020.mkv"), out _));
    }
    [Fact]
    public void SameLetterOrUnknownSerialCannotImpersonateDrive()
    {
        var stored = new Drive { VolumeGuid = "volume-one", SerialNumber = "SN-UNKNOWN", LastKnownLetter = "E:" };
        Assert.False(WindowsDriveService.SameVolume(stored, "volume-two", "SN-UNKNOWN"));
        Assert.True(WindowsDriveService.SameVolume(stored, "volume-one", "1234"));
        stored.SerialNumber = "1234";
        Assert.False(WindowsDriveService.SameVolume(stored, "volume-one", "5678"));
    }
    [Fact]
    public async Task TvMetadataWithoutDirectorStillLoads()
    {
        using var client = new HttpClient(new JsonHandler("""{"name":"Example","genres":[],"credits":{"crew":[],"cast":[]}}"""));
        var provider = new TmdbMetadataProvider(client, "test");
        var details = await provider.GetDetailsAsync("42", MediaType.TvShow);
        Assert.NotNull(details);
        Assert.Equal("Example", details.Title);
    }
    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }
}
