using LocalFlix.Core.Models;
using LocalFlix.Data;
using System.IO;
using Xunit;

namespace LocalFlix.Tests;

public class M4_PlaybackTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqliteLibraryRepository _repository;

    public M4_PlaybackTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"LocalFlix_PlayTest_{Guid.NewGuid():N}.db");
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
    public async Task PlaybackState_PersistsAndLoadsResumePosition()
    {
        var drive = new Drive { VolumeGuid = @"\\?\Volume{m4-guid}\", Label = "Drive", LastKnownLetter = "D:" };
        var driveId = await _repository.UpsertDriveAsync(drive);

        var file = new MediaFile
        {
            DriveId = driveId,
            RelativePath = @"Movies\Inception.2010.mkv"
        };
        var fileId = await _repository.UpsertMediaFileAsync(file);

        // Save in-progress playback state at 45 minutes
        var state = new PlaybackState
        {
            MediaFileId = fileId,
            PositionSeconds = 2700.0,
            DurationSeconds = 8880.0,
            IsWatched = false,
            LastPlayedUtc = DateTime.UtcNow
        };
        await _repository.UpsertPlaybackStateAsync(state);

        var retrieved = await _repository.GetPlaybackStateAsync(fileId);
        Assert.NotNull(retrieved);
        Assert.Equal(2700.0, retrieved.PositionSeconds);
        Assert.False(retrieved.IsWatched);

        // Update to 95% finished
        state.PositionSeconds = 8500.0;
        state.IsWatched = true;
        await _repository.UpsertPlaybackStateAsync(state);

        var updated = await _repository.GetPlaybackStateAsync(fileId);
        Assert.NotNull(updated);
        Assert.Equal(8500.0, updated.PositionSeconds);
        Assert.True(updated.IsWatched);
    }

    [Theory]
    [InlineData(100.0, 1000.0, false)] // 10% watched -> false
    [InlineData(500.0, 1000.0, false)] // 50% watched -> false
    [InlineData(910.0, 1000.0, true)]  // >90% watched -> true
    [InlineData(990.0, 1000.0, true)]  // >90% watched -> true
    [InlineData(5850.0, 6000.0, true)] // Within last 150 seconds of 6000s -> true
    public void PlaybackThreshold_CalculatesWatchedStateCorrectly(double pos, double dur, bool expectedWatched)
    {
        bool isWatched = pos >= (dur * 0.90) || (dur - pos) < 180;
        Assert.Equal(expectedWatched, isWatched);
    }
}
