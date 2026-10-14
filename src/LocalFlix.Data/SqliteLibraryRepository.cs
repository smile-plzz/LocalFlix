using Dapper;
using LocalFlix.Core.Interfaces;
using LocalFlix.Core.Models;
using Microsoft.Data.Sqlite;

namespace LocalFlix.Data;

public class SqliteLibraryRepository : ILibraryRepository
{
    private readonly string _connectionString;

    public SqliteLibraryRepository(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
    }

    private SqliteConnection CreateConnection() => new(_connectionString);

    // Drives
    public async Task<Drive?> GetDriveByVolumeGuidAsync(string volumeGuid)
    {
        using var conn = CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Drive>(
            "SELECT * FROM Drives WHERE VolumeGuid = @volumeGuid",
            new { volumeGuid });
    }

    public async Task<Drive?> GetDriveByIdAsync(int id)
    {
        using var conn = CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Drive>(
            "SELECT * FROM Drives WHERE Id = @id",
            new { id });
    }

    public async Task<IReadOnlyList<Drive>> GetAllDrivesAsync()
    {
        using var conn = CreateConnection();
        var result = await conn.QueryAsync<Drive>("SELECT * FROM Drives ORDER BY Label, LastKnownLetter");
        return result.ToList();
    }

    public async Task<int> UpsertDriveAsync(Drive drive)
    {
        using var conn = CreateConnection();
        var existing = await conn.QuerySingleOrDefaultAsync<Drive>(
            "SELECT * FROM Drives WHERE VolumeGuid = @VolumeGuid",
            new { drive.VolumeGuid });

        if (existing == null)
        {
            var id = await conn.ExecuteScalarAsync<int>(@"
                INSERT INTO Drives (VolumeGuid, SerialNumber, Label, FileSystem, LastKnownLetter, LastSeenUtc, IsConnected)
                VALUES (@VolumeGuid, @SerialNumber, @Label, @FileSystem, @LastKnownLetter, @LastSeenUtc, @IsConnected);
                SELECT last_insert_rowid();", drive);
            drive.Id = id;
            return id;
        }
        else
        {
            drive.Id = existing.Id;
            await conn.ExecuteAsync(@"
                UPDATE Drives
                SET SerialNumber = @SerialNumber,
                    Label = @Label,
                    FileSystem = @FileSystem,
                    LastKnownLetter = @LastKnownLetter,
                    LastSeenUtc = @LastSeenUtc,
                    IsConnected = @IsConnected
                WHERE Id = @Id;", drive);
            return drive.Id;
        }
    }

    public async Task UpdateDriveStatusAsync(int driveId, bool isConnected, string lastKnownLetter)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(@"
            UPDATE Drives
            SET IsConnected = @isConnected,
                LastKnownLetter = @lastKnownLetter,
                LastSeenUtc = @lastSeenUtc
            WHERE Id = @driveId;",
            new { driveId, isConnected, lastKnownLetter, lastSeenUtc = DateTime.UtcNow.ToString("O") });
    }

    // Media Files
    public async Task<MediaFile?> GetMediaFileByRelativePathAsync(int driveId, string relativePath)
    {
        using var conn = CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<MediaFile>(
            "SELECT * FROM MediaFiles WHERE DriveId = @driveId AND RelativePath = @relativePath",
            new { driveId, relativePath });
    }

    public async Task<MediaFile?> GetMediaFileByIdAsync(int id)
    {
        using var conn = CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<MediaFile>(
            "SELECT * FROM MediaFiles WHERE Id = @id",
            new { id });
    }

    public async Task<IReadOnlyList<MediaFile>> GetFilesByDriveIdAsync(int driveId)
    {
        using var conn = CreateConnection();
        var result = await conn.QueryAsync<MediaFile>(
            "SELECT * FROM MediaFiles WHERE DriveId = @driveId",
            new { driveId });
        return result.ToList();
    }

    public async Task<int> UpsertMediaFileAsync(MediaFile file)
    {
        using var conn = CreateConnection();
        var existing = await conn.QuerySingleOrDefaultAsync<MediaFile>(
            "SELECT * FROM MediaFiles WHERE DriveId = @DriveId AND RelativePath = @RelativePath",
            new { file.DriveId, file.RelativePath });

        if (existing == null)
        {
            var id = await conn.ExecuteScalarAsync<int>(@"
                INSERT INTO MediaFiles (DriveId, RelativePath, FileSizeBytes, DateModifiedUtc, DurationSeconds, Width, Height, VideoCodec, AudioCodec, AudioChannels, SubtitleTracksJson, IdentityHash, Classification, Status, CreatedUtc)
                VALUES (@DriveId, @RelativePath, @FileSizeBytes, @DateModifiedUtc, @DurationSeconds, @Width, @Height, @VideoCodec, @AudioCodec, @AudioChannels, @SubtitleTracksJson, @IdentityHash, @Classification, @Status, @CreatedUtc);
                SELECT last_insert_rowid();", file);
            file.Id = id;
            return id;
        }
        else
        {
            file.Id = existing.Id;
            await conn.ExecuteAsync(@"
                UPDATE MediaFiles
                SET FileSizeBytes = @FileSizeBytes,
                    DateModifiedUtc = @DateModifiedUtc,
                    DurationSeconds = @DurationSeconds,
                    Width = @Width,
                    Height = @Height,
                    VideoCodec = @VideoCodec,
                    AudioCodec = @AudioCodec,
                    AudioChannels = @AudioChannels,
                    SubtitleTracksJson = @SubtitleTracksJson,
                    IdentityHash = @IdentityHash,
                    Classification = @Classification,
                    Status = @Status
                WHERE Id = @Id;", file);
            return file.Id;
        }
    }

    public async Task UpdateFileStatusAsync(int fileId, FileStatus status)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE MediaFiles SET Status = @status WHERE Id = @fileId",
            new { fileId, status });
    }

    public async Task<TitleItem?> GetTitleByMediaFileIdAsync(int mediaFileId)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<TitleItem>(
            "SELECT t.* FROM Titles t WHERE t.MediaFileId = @mediaFileId OR t.Id IN (SELECT s.TitleItemId FROM Series s JOIN Seasons sn ON sn.SeriesId = s.Id JOIN Episodes e ON e.SeasonId = sn.Id WHERE e.MediaFileId = @mediaFileId) ORDER BY t.Id LIMIT 1", new { mediaFileId });
    }

    // Titles
    public async Task<TitleItem?> GetTitleByIdAsync(int id)
    {
        using var conn = CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<TitleItem>(
            "SELECT * FROM Titles WHERE Id = @id",
            new { id });
    }

    public async Task<IReadOnlyList<TitleItem>> GetAllTitlesAsync(MediaType? filterType = null)
    {
        using var conn = CreateConnection();
        string sql = filterType.HasValue
            ? "SELECT * FROM Titles WHERE Type = @filterType ORDER BY Name"
            : "SELECT * FROM Titles ORDER BY Name";
        var result = await conn.QueryAsync<TitleItem>(sql, new { filterType });
        return result.ToList();
    }

    public async Task<IReadOnlyList<TitleItem>> SearchTitlesAsync(string query, MediaType? filterType = null, string? genre = null)
    {
        using var conn = CreateConnection();
        var sql = @"
            SELECT * FROM Titles
            WHERE (Name LIKE @likeQuery OR CastMembers LIKE @likeQuery OR Director LIKE @likeQuery OR Genres LIKE @likeQuery)
              AND (@filterType IS NULL OR Type = @filterType)
              AND (@genre IS NULL OR Genres LIKE @likeGenre)
            ORDER BY Name";

        var result = await conn.QueryAsync<TitleItem>(sql, new
        {
            likeQuery = $"%{query}%",
            filterType,
            genre,
            likeGenre = $"%{genre}%"
        });
        return result.ToList();
    }

    public async Task<IReadOnlyList<TitleItem>> GetRecentlyAddedTitlesAsync(int count = 20)
    {
        using var conn = CreateConnection();
        var result = await conn.QueryAsync<TitleItem>(
            "SELECT * FROM Titles ORDER BY CreatedUtc DESC LIMIT @count",
            new { count });
        return result.ToList();
    }

    public async Task<IReadOnlyList<TitleItem>> GetContinueWatchingTitlesAsync(int count = 20)
    {
        using var conn = CreateConnection();
        var sql = @"
            SELECT t.* FROM Titles t
            JOIN PlaybackStates ps ON ps.MediaFileId = t.MediaFileId
                OR ps.MediaFileId IN (SELECT e.MediaFileId FROM Series s JOIN Seasons sn ON sn.SeriesId = s.Id JOIN Episodes e ON e.SeasonId = sn.Id WHERE s.TitleItemId = t.Id)
            WHERE ps.IsWatched = 0 AND ps.PositionSeconds > 30
            GROUP BY t.Id
            ORDER BY MAX(ps.LastPlayedUtc) DESC
            LIMIT @count";
        var result = await conn.QueryAsync<TitleItem>(sql, new { count });
        return result.ToList();
    }

    public async Task<int> UpsertTitleAsync(TitleItem title)
    {
        using var conn = CreateConnection();
        if (title.Id == 0)
        {
            var existing = await conn.QueryFirstOrDefaultAsync<TitleItem>(@"
                SELECT * FROM Titles WHERE
                (@MediaFileId IS NOT NULL AND MediaFileId = @MediaFileId)
                OR (@Type = 2 AND Type = 2 AND
                    ((@TmdbId IS NOT NULL AND @TmdbId <> '' AND TmdbId = @TmdbId)
                     OR (@ImdbId IS NOT NULL AND @ImdbId <> '' AND ImdbId = @ImdbId)))
                ORDER BY Id LIMIT 1", title);
            if (existing != null)
            {
                title.Id = existing.Id;
                title.CreatedUtc = existing.CreatedUtc;
                if (title.Type == MediaType.TvShow) title.MediaFileId = existing.MediaFileId;
                if (existing.MatchedBy == MatchMethod.Manual && title.MatchedBy != MatchMethod.Manual)
                    return existing.Id;
            }
        }
        if (title.Id == 0)
        {
            var id = await conn.ExecuteScalarAsync<int>(@"
                INSERT INTO Titles (Type, TmdbId, ImdbId, Name, OriginalName, Year, Overview, RuntimeMinutes, Genres, Rating, PosterPath, BackdropPath, Director, CastMembers, MatchedBy, ConfidenceScore, CreatedUtc, MediaFileId)
                VALUES (@Type, @TmdbId, @ImdbId, @Name, @OriginalName, @Year, @Overview, @RuntimeMinutes, @Genres, @Rating, @PosterPath, @BackdropPath, @Director, @CastMembers, @MatchedBy, @ConfidenceScore, @CreatedUtc, @MediaFileId);
                SELECT last_insert_rowid();", title);
            title.Id = id;
            return id;
        }
        else
        {
            await conn.ExecuteAsync(@"
                UPDATE Titles
                SET Type = @Type,
                    TmdbId = @TmdbId,
                    ImdbId = @ImdbId,
                    Name = @Name,
                    OriginalName = @OriginalName,
                    Year = @Year,
                    Overview = @Overview,
                    RuntimeMinutes = @RuntimeMinutes,
                    Genres = @Genres,
                    Rating = @Rating,
                    PosterPath = @PosterPath,
                    BackdropPath = @BackdropPath,
                    Director = @Director,
                    CastMembers = @CastMembers,
                    MatchedBy = @MatchedBy,
                    ConfidenceScore = @ConfidenceScore,
                    MediaFileId = @MediaFileId
                WHERE Id = @Id;", title);
            return title.Id;
        }
    }

    public async Task DeleteTitleAsync(int id)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("DELETE FROM Titles WHERE Id = @id", new { id });
    }

    // Series, Seasons, Episodes
    public async Task<Series?> GetSeriesByTitleIdAsync(int titleId)
    {
        using var conn = CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Series>(
            "SELECT * FROM Series WHERE TitleItemId = @titleId",
            new { titleId });
    }

    public async Task<IReadOnlyList<Season>> GetSeasonsBySeriesIdAsync(int seriesId)
    {
        using var conn = CreateConnection();
        var result = await conn.QueryAsync<Season>(
            "SELECT s.Id, s.SeriesId, s.SeasonNumber, s.Name, s.PosterPath, (SELECT COUNT(*) FROM Episodes e WHERE e.SeasonId = s.Id) AS EpisodeCount FROM Seasons s WHERE SeriesId = @seriesId ORDER BY SeasonNumber",
            new { seriesId });
        return result.ToList();
    }

    public async Task<IReadOnlyList<Episode>> GetEpisodesBySeasonIdAsync(int seasonId)
    {
        using var conn = CreateConnection();
        var result = await conn.QueryAsync<Episode>(
            "SELECT * FROM Episodes WHERE SeasonId = @seasonId ORDER BY EpisodeNumber",
            new { seasonId });
        return result.ToList();
    }

    public async Task<int> UpsertSeriesAsync(Series series)
    {
        using var conn = CreateConnection();
        var existing = await conn.QuerySingleOrDefaultAsync<Series>(
            "SELECT * FROM Series WHERE TitleItemId = @TitleItemId",
            new { series.TitleItemId });

        if (existing == null)
        {
            var id = await conn.ExecuteScalarAsync<int>(@"
                INSERT INTO Series (TitleItemId, Name, Overview, TotalSeasons, PosterPath, BackdropPath)
                VALUES (@TitleItemId, @Name, @Overview, @TotalSeasons, @PosterPath, @BackdropPath);
                SELECT last_insert_rowid();", series);
            series.Id = id;
            return id;
        }
        else
        {
            series.Id = existing.Id;
            await conn.ExecuteAsync(@"
                UPDATE Series
                SET Name = @Name,
                    Overview = @Overview,
                    TotalSeasons = @TotalSeasons,
                    PosterPath = @PosterPath,
                    BackdropPath = @BackdropPath
                WHERE Id = @Id;", series);
            return series.Id;
        }
    }

    public async Task<int> UpsertSeasonAsync(Season season)
    {
        using var conn = CreateConnection();
        var existing = await conn.QuerySingleOrDefaultAsync<Season>(
            "SELECT * FROM Seasons WHERE SeriesId = @SeriesId AND SeasonNumber = @SeasonNumber",
            new { season.SeriesId, season.SeasonNumber });

        if (existing == null)
        {
            var id = await conn.ExecuteScalarAsync<int>(@"
                INSERT INTO Seasons (SeriesId, SeasonNumber, Name, PosterPath, EpisodeCount)
                VALUES (@SeriesId, @SeasonNumber, @Name, @PosterPath, @EpisodeCount);
                SELECT last_insert_rowid();", season);
            season.Id = id;
            return id;
        }
        else
        {
            season.Id = existing.Id;
            await conn.ExecuteAsync(@"
                UPDATE Seasons
                SET Name = @Name,
                    PosterPath = @PosterPath,
                    EpisodeCount = @EpisodeCount
                WHERE Id = @Id;", season);
            return season.Id;
        }
    }

    public async Task<int> UpsertEpisodeAsync(Episode episode)
    {
        using var conn = CreateConnection();
        var existing = await conn.QuerySingleOrDefaultAsync<Episode>(
            "SELECT * FROM Episodes WHERE SeasonId = @SeasonId AND EpisodeNumber = @EpisodeNumber",
            new { episode.SeasonId, episode.EpisodeNumber });

        if (existing == null)
        {
            var id = await conn.ExecuteScalarAsync<int>(@"
                INSERT INTO Episodes (SeasonId, MediaFileId, EpisodeNumber, Name, Overview, AirDate, StillPath)
                VALUES (@SeasonId, @MediaFileId, @EpisodeNumber, @Name, @Overview, @AirDate, @StillPath);
                SELECT last_insert_rowid();", episode);
            episode.Id = id;
            return id;
        }
        else
        {
            episode.Id = existing.Id;
            await conn.ExecuteAsync(@"
                UPDATE Episodes
                SET MediaFileId = @MediaFileId,
                    Name = @Name,
                    Overview = @Overview,
                    AirDate = @AirDate,
                    StillPath = @StillPath
                WHERE Id = @Id;", episode);
            return episode.Id;
        }
    }

    // Playback States
    public async Task<PlaybackState?> GetPlaybackStateAsync(int mediaFileId)
    {
        using var conn = CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<PlaybackState>(
            "SELECT * FROM PlaybackStates WHERE MediaFileId = @mediaFileId",
            new { mediaFileId });
    }

    public async Task UpsertPlaybackStateAsync(PlaybackState state)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(@"
            INSERT INTO PlaybackStates (MediaFileId, PositionSeconds, DurationSeconds, IsWatched, LastPlayedUtc)
            VALUES (@MediaFileId, @PositionSeconds, @DurationSeconds, @IsWatched, @LastPlayedUtc)
            ON CONFLICT(MediaFileId) DO UPDATE SET
                PositionSeconds = @PositionSeconds,
                DurationSeconds = @DurationSeconds,
                IsWatched = @IsWatched,
                LastPlayedUtc = @LastPlayedUtc;", state);
    }

    // Review Items
    public async Task<IReadOnlyList<ReviewItem>> GetPendingReviewItemsAsync()
    {
        using var conn = CreateConnection();
        var result = await conn.QueryAsync<ReviewItem>(
            "SELECT * FROM ReviewItems WHERE IsResolved = 0 ORDER BY CreatedUtc DESC");
        return result.ToList();
    }

    public async Task<int> UpsertReviewItemAsync(ReviewItem item)
    {
        using var conn = CreateConnection();
        var existingId = await conn.QueryFirstOrDefaultAsync<int?>(
            "SELECT Id FROM ReviewItems WHERE MediaFileId = @MediaFileId AND IsResolved = 0 ORDER BY Id LIMIT 1", item);
        if (existingId.HasValue)
        {
            item.Id = existingId.Value;
            await conn.ExecuteAsync("UPDATE ReviewItems SET Reason = @Reason, CandidateMatchesJson = @CandidateMatchesJson WHERE Id = @Id", item);
            return item.Id;
        }
        var id = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO ReviewItems (MediaFileId, Reason, CandidateMatchesJson, CreatedUtc, IsResolved)
            VALUES (@MediaFileId, @Reason, @CandidateMatchesJson, @CreatedUtc, @IsResolved);
            SELECT last_insert_rowid();", item);
        item.Id = id;
        return id;
    }

    public async Task ResolveReviewItemAsync(int id)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE ReviewItems SET IsResolved = 1 WHERE Id = @id",
            new { id });
    }

    // Watchlist & Favorites
    public async Task<IReadOnlyList<TitleItem>> GetUserListTitlesAsync(UserListType listType)
    {
        using var conn = CreateConnection();
        var sql = @"
            SELECT t.* FROM Titles t
            JOIN UserLists ul ON ul.TitleItemId = t.Id
            WHERE ul.ListType = @listType
            ORDER BY ul.AddedUtc DESC";
        var result = await conn.QueryAsync<TitleItem>(sql, new { listType });
        return result.ToList();
    }

    public async Task AddToUserListAsync(int titleId, UserListType listType)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(@"
            INSERT INTO UserLists (TitleItemId, ListType, AddedUtc)
            VALUES (@titleId, @listType, @addedUtc)
            ON CONFLICT(TitleItemId, ListType) DO NOTHING;",
            new { titleId, listType, addedUtc = DateTime.UtcNow.ToString("O") });
    }

    public async Task RemoveFromUserListAsync(int titleId, UserListType listType)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "DELETE FROM UserLists WHERE TitleItemId = @titleId AND ListType = @listType",
            new { titleId, listType });
    }

    public async Task<bool> IsInUserListAsync(int titleId, UserListType listType)
    {
        using var conn = CreateConnection();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM UserLists WHERE TitleItemId = @titleId AND ListType = @listType",
            new { titleId, listType });
        return count > 0;
    }
}
