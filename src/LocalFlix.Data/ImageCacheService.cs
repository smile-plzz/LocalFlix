using LocalFlix.Core.Interfaces;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace LocalFlix.Data;

public class ImageCacheService : IImageCacheService
{
    private readonly string _cacheRoot;
    private readonly HttpClient _httpClient;

    public ImageCacheService(string? customCacheRoot = null, HttpClient? httpClient = null)
    {
        _cacheRoot = customCacheRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LocalFlix",
            "cache");
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public string GetLocalCacheDirectory() => _cacheRoot;

    public string? GetCachedImagePath(string imageUrl, string subfolder = "posters")
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;

        var hash = ComputeHash(imageUrl);
        var ext = Path.GetExtension(new Uri(imageUrl).AbsolutePath);
        if (string.IsNullOrEmpty(ext)) ext = ".jpg";

        var filePath = Path.Combine(_cacheRoot, subfolder, $"{hash}{ext}");
        return File.Exists(filePath) ? filePath : null;
    }

    public async Task<string?> CacheImageAsync(string imageUrl, string subfolder = "posters", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;

        try
        {
            var hash = ComputeHash(imageUrl);
            var ext = Path.GetExtension(new Uri(imageUrl).AbsolutePath);
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";

            var folder = Path.Combine(_cacheRoot, subfolder);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var filePath = Path.Combine(folder, $"{hash}{ext}");
            if (File.Exists(filePath))
            {
                return filePath;
            }

            var bytes = await _httpClient.GetByteArrayAsync(imageUrl, cancellationToken);
            await File.WriteAllBytesAsync(filePath, bytes, cancellationToken);
            return filePath;
        }
        catch
        {
            // If offline or download fails, return null so UI can fallback to placeholder
            return null;
        }
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }
}
