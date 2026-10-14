using LocalFlix.Core.Models;

namespace LocalFlix.Core.Interfaces;

public interface ILibraryRepository
{
    // Drives
    Task<Drive?> GetDriveByVolumeGuidAsync(string volumeGuid);
    Task<Drive?> GetDriveByIdAsync(int id);
    Task<IReadOnlyList<Drive>> GetAllDrivesAsync();
    Task<int> UpsertDriveAsync(Drive drive);
    Task UpdateDriveStatusAsync(int driveId, bool isConnected, string lastKnownLetter);

    // Media Files
    Task<MediaFile?> GetMediaFileByRelativePathAsync(int driveId, string relativePath);
    Task<MediaFile?> GetMediaFileByIdAsync(int id);
    Task<IReadOnlyList<MediaFile>> GetFilesByDriveIdAsync(int driveId);
    Task<int> UpsertMediaFileAsync(MediaFile file);
    Task UpdateFileStatusAsync(int fileId, FileStatus status);

    // Titles
    Task<TitleItem?> GetTitleByIdAsync(int id);
    Task<TitleItem?> GetTitleByMediaFileIdAsync(int mediaFileId);
    Task<IReadOnlyList<TitleItem>> GetAllTitlesAsync(MediaType? filterType = null);
    Task<IReadOnlyList<TitleItem>> SearchTitlesAsync(string query, MediaType? filterType = null, string? genre = null);
    Task<IReadOnlyList<TitleItem>> GetRecentlyAddedTitlesAsync(int count = 20);
    Task<IReadOnlyList<TitleItem>> GetContinueWatchingTitlesAsync(int count = 20);
    Task<int> UpsertTitleAsync(TitleItem title);
    Task DeleteTitleAsync(int id);

    // Series, Seasons, Episodes
    Task<Series?> GetSeriesByTitleIdAsync(int titleId);
    Task<IReadOnlyList<Season>> GetSeasonsBySeriesIdAsync(int seriesId);
    Task<IReadOnlyList<Episode>> GetEpisodesBySeasonIdAsync(int seasonId);
    Task<int> UpsertSeriesAsync(Series series);
    Task<int> UpsertSeasonAsync(Season season);
    Task<int> UpsertEpisodeAsync(Episode episode);

    // Playback States
    Task<PlaybackState?> GetPlaybackStateAsync(int mediaFileId);
    Task UpsertPlaybackStateAsync(PlaybackState state);

    // Review Items
    Task<IReadOnlyList<ReviewItem>> GetPendingReviewItemsAsync();
    Task<int> UpsertReviewItemAsync(ReviewItem item);
    Task ResolveReviewItemAsync(int id);

    // Watchlist & Favorites
    Task<IReadOnlyList<TitleItem>> GetUserListTitlesAsync(UserListType listType);
    Task AddToUserListAsync(int titleId, UserListType listType);
    Task RemoveFromUserListAsync(int titleId, UserListType listType);
    Task<bool> IsInUserListAsync(int titleId, UserListType listType);
}
