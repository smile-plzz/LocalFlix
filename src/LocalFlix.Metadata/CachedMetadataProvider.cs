using LocalFlix.Core.Interfaces;
using LocalFlix.Core.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LocalFlix.Metadata;

// Only provider queries and responses are cached. No API keys or media paths are stored.
public sealed class CachedMetadataProvider(IMetadataProvider inner, string cacheDirectory) : IMetadataProvider
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public string ProviderName => inner.ProviderName;

    public async Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(string query, int? year = null,
        MediaType mediaType = MediaType.Unknown, CancellationToken cancellationToken = default)
    {
        var key = $"search:{ProviderName}:{mediaType}:{year}:{query}";
        return await CachedAsync(key, async () => (await inner.SearchAsync(query, year, mediaType, cancellationToken)).ToList(),
            value => value.Count > 0, cancellationToken) ?? new List<MetadataSearchResult>();
    }

    public Task<MetadataDetails?> GetDetailsAsync(string providerId, MediaType mediaType = MediaType.Unknown,
        CancellationToken cancellationToken = default) => CachedAsync($"details:{ProviderName}:{mediaType}:{providerId}",
            () => inner.GetDetailsAsync(providerId, mediaType, cancellationToken), value => true, cancellationToken);

    private async Task<T?> CachedAsync<T>(string key, Func<Task<T?>> fetch, Func<T, bool> usable, CancellationToken token) where T : class
    {
        var filename = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".json";
        var path = Path.Combine(cacheDirectory, filename);
        await _gate.WaitAsync(token);
        try
        {
            if (File.Exists(path))
            {
                try
                {
                    var cached = JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path, token));
                    if (cached != null) return cached;
                }
                catch (JsonException) { }
                catch (IOException) { }
            }
            T? result = null;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                await Task.Delay(attempt == 0 ? 250 : 1000, token);
                result = await fetch();
                if (result != null && usable(result)) break;
            }
            if (result != null && usable(result))
            {
                try
                {
                    Directory.CreateDirectory(cacheDirectory);
                    await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(result), token);
                    File.Move(path + ".tmp", path, true);
                }
                catch (IOException) { /* Metadata remains usable if the disk cache is unavailable. */ }
                catch (UnauthorizedAccessException) { }
            }
            return result;
        }
        finally { _gate.Release(); }
    }
}
