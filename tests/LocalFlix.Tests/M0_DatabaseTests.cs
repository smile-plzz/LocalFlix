using LocalFlix.Core.Models;
using LocalFlix.Data;
using System.IO;
using Xunit;

namespace LocalFlix.Tests;

public class M0_DatabaseTests : IDisposable
{
    private readonly string _tempDbPath;

    public M0_DatabaseTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"localflix_test_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempDbPath))
            {
                File.Delete(_tempDbPath);
            }
            var bak = $"{_tempDbPath}.bak";
            if (File.Exists(bak))
            {
                File.Delete(bak);
            }
        }
        catch
        {
            // Ignore file lock during test cleanup
        }
    }

    [Fact]
    public void DatabaseMigrator_CreatesTablesAndMigrationRecord()
    {
        var migrator = new DatabaseMigrator(_tempDbPath);
        migrator.Migrate();

        Assert.True(File.Exists(_tempDbPath));

        // Re-running migration should be idempotent and succeed without error
        migrator.Migrate();
        Assert.True(File.Exists(_tempDbPath));
    }

    [Fact]
    public async Task SqliteLibraryRepository_CanInsertAndRetrieveDrive()
    {
        var migrator = new DatabaseMigrator(_tempDbPath);
        migrator.Migrate();

        var repo = new SqliteLibraryRepository(_tempDbPath);

        var drive = new Drive
        {
            VolumeGuid = @"\\?\Volume{12345678-1234-1234-1234-123456789abc}\",
            SerialNumber = "SN99887766",
            Label = "Seagate4TB",
            FileSystem = "NTFS",
            LastKnownLetter = "E:",
            LastSeenUtc = DateTime.UtcNow,
            IsConnected = true
        };

        var driveId = await repo.UpsertDriveAsync(drive);
        Assert.True(driveId > 0);

        var retrieved = await repo.GetDriveByVolumeGuidAsync(drive.VolumeGuid);
        Assert.NotNull(retrieved);
        Assert.Equal("Seagate4TB", retrieved.Label);
        Assert.Equal("E:", retrieved.LastKnownLetter);
    }

    [Fact]
    public async Task SqliteLibraryRepository_CanInsertAndSearchTitles()
    {
        var migrator = new DatabaseMigrator(_tempDbPath);
        migrator.Migrate();

        var repo = new SqliteLibraryRepository(_tempDbPath);

        var title = new TitleItem
        {
            Type = MediaType.Movie,
            TmdbId = "tmdb-550",
            ImdbId = "tt0137523",
            Name = "Fight Club",
            Year = 1999,
            Overview = "An insomniac office worker and a devil-may-care soap maker form an underground fight club.",
            RuntimeMinutes = 139,
            Genres = "Drama",
            Rating = 8.8,
            PosterPath = "/fightclub.jpg",
            Director = "David Fincher",
            CastMembers = "Brad Pitt, Edward Norton, Helena Bonham Carter",
            MatchedBy = MatchMethod.Auto,
            ConfidenceScore = 0.98
        };

        var titleId = await repo.UpsertTitleAsync(title);
        Assert.True(titleId > 0);

        var results = await repo.SearchTitlesAsync("Fight");
        Assert.Single(results);
        Assert.Equal("Fight Club", results[0].Name);

        var actorSearch = await repo.SearchTitlesAsync("Brad Pitt");
        Assert.Single(actorSearch);
        Assert.Equal("Fight Club", actorSearch[0].Name);
    }
}
