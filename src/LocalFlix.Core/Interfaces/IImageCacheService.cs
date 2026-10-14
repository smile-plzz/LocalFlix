namespace LocalFlix.Core.Interfaces;

public interface IImageCacheService
{
    string GetLocalCacheDirectory();
    Task<string?> CacheImageAsync(string imageUrl, string subfolder = "posters", CancellationToken cancellationToken = default);
    string? GetCachedImagePath(string imageUrl, string subfolder = "posters");
}
