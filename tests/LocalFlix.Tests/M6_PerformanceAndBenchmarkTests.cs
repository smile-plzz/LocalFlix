using LocalFlix.Core.Models;
using LocalFlix.Data;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace LocalFlix.Tests;

public class M6_PerformanceAndBenchmarkTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqliteLibraryRepository _repository;

    public M6_PerformanceAndBenchmarkTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"LocalFlix_PerfTest_{Guid.NewGuid():N}.db");
        var migrator = new DatabaseMigrator(_tempDbPath);
        migrator.Migrate();

        _repository = new SqliteLibraryRepository(_tempDbPath);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
            var bak = $"{_tempDbPath}.bak";
            if (File.Exists(bak)) File.Delete(bak);
        }
        catch
        {
        }
    }

    [Fact]
    public async Task Benchmark_5000Titles_LoadsAndSearchesWellUnderTwoSeconds()
    {
        var drive = new Drive { VolumeGuid = @"\\?\Volume{perf-guid}\", Label = "LargeMediaDisk", LastKnownLetter = "E:" };
        var driveId = await _repository.UpsertDriveAsync(drive);

        // Batch populate 5,000 synthetic titles
        var sw = Stopwatch.StartNew();
        for (int i = 1; i <= 5000; i++)
        {
            var title = new TitleItem
            {
                Type = (i % 3 == 0) ? MediaType.TvShow : ((i % 5 == 0) ? MediaType.Documentary : MediaType.Movie),
                Name = $"Synthetic Movie Title {i}",
                Year = 1970 + (i % 54),
                Rating = 6.0 + (i % 40) / 10.0,
                Genres = (i % 2 == 0) ? "Action, Drama" : "Sci-Fi, Adventure",
                Director = $"Director {(i % 50)}",
                CastMembers = $"Actor A{(i % 100)}, Actor B{(i % 200)}",
                Overview = $"Detailed overview description for movie number {i} in the library.",
                PosterPath = $"/posters/synth_{i}.jpg",
                BackdropPath = $"/backdrops/synth_{i}.jpg"
            };
            await _repository.UpsertTitleAsync(title);
        }
        sw.Stop();

        // 1. Benchmark: Home screen queries (Recently Added, Continue Watching, Movies, TV, Docs)
        var homeSw = Stopwatch.StartNew();
        var recentlyAdded = await _repository.GetRecentlyAddedTitlesAsync(20);
        var continueWatching = await _repository.GetContinueWatchingTitlesAsync(10);
        var movies = await _repository.GetAllTitlesAsync(MediaType.Movie);
        var tvShows = await _repository.GetAllTitlesAsync(MediaType.TvShow);
        var docs = await _repository.GetAllTitlesAsync(MediaType.Documentary);
        homeSw.Stop();

        Assert.NotEmpty(recentlyAdded);
        Assert.NotEmpty(movies);
        Assert.NotEmpty(tvShows);
        Assert.NotEmpty(docs);

        // Verification: Must load well under 2,000 milliseconds (2.0s)
        Assert.True(homeSw.ElapsedMilliseconds < 2000, $"Home queries took {homeSw.ElapsedMilliseconds}ms, expected < 2000ms");

        // 2. Benchmark: Instant title search across 5,000 items
        var searchSw = Stopwatch.StartNew();
        var searchResults = await _repository.SearchTitlesAsync("Title 2500");
        searchSw.Stop();

        Assert.NotEmpty(searchResults);
        Assert.True(searchSw.ElapsedMilliseconds < 200, $"Search took {searchSw.ElapsedMilliseconds}ms, expected < 200ms");
    }
}
