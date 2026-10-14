using LocalFlix.Core.Interfaces;
using LocalFlix.Core.Models;
using System.Net.Http;
using System.Text.Json;

namespace LocalFlix.Metadata;

public class OmdbMetadataProvider : IMetadataProvider
{
    private readonly HttpClient _httpClient;
    private readonly string? _configuredApiKey;
    private string? _apiKey => _configuredApiKey ?? Environment.GetEnvironmentVariable("OMDB_API_KEY", EnvironmentVariableTarget.User) ?? Environment.GetEnvironmentVariable("OMDB_API_KEY");

    public string ProviderName => "OpenMovieDatabase";

    public OmdbMetadataProvider(HttpClient? httpClient = null, string? apiKey = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _configuredApiKey = apiKey;
    }

    public async Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
        string query,
        int? year = null,
        MediaType mediaType = MediaType.Unknown,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<MetadataSearchResult>();
        }

        try
        {
            var typeParam = mediaType == MediaType.TvShow || mediaType == MediaType.Episode ? "&type=series" : "&type=movie";
            var url = $"https://www.omdbapi.com/?apikey={_apiKey}&s={Uri.EscapeDataString(query)}{typeParam}";
            if (year.HasValue)
            {
                url += $"&y={year.Value}";
            }

            var response = await _httpClient.GetStringAsync(url, cancellationToken);
            using var doc = JsonDocument.Parse(response);
            if (!doc.RootElement.TryGetProperty("Search", out var searchElem))
            {
                return Array.Empty<MetadataSearchResult>();
            }

            var list = new List<MetadataSearchResult>();
            foreach (var item in searchElem.EnumerateArray())
            {
                var imdbId = item.GetProperty("imdbID").GetString() ?? string.Empty;
                var title = item.GetProperty("Title").GetString() ?? string.Empty;
                var yearStr = item.GetProperty("Year").GetString();
                int? itemYear = null;
                if (!string.IsNullOrEmpty(yearStr) && yearStr.Length >= 4 && int.TryParse(yearStr[..4], out var y))
                {
                    itemYear = y;
                }

                var poster = item.TryGetProperty("Poster", out var p) && p.GetString() != "N/A"
                    ? p.GetString()
                    : null;

                var confidence = FuzzyMatcher.CalculateMatchConfidence(query, year, title, itemYear);

                list.Add(new MetadataSearchResult
                {
                    Id = imdbId,
                    Provider = ProviderName,
                    Title = title,
                    Year = itemYear,
                    Type = item.TryGetProperty("Type", out var ty) && ty.GetString() == "series" ? MediaType.TvShow : MediaType.Movie,
                    PosterUrl = poster,
                    ConfidenceScore = confidence
                });
            }

            return list.OrderByDescending(r => r.ConfidenceScore).ToList();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return Array.Empty<MetadataSearchResult>();
        }
    }

    public async Task<MetadataDetails?> GetDetailsAsync(
        string providerId,
        MediaType mediaType = MediaType.Unknown,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(providerId))
        {
            return null;
        }

        try
        {
            var url = $"https://www.omdbapi.com/?apikey={_apiKey}&i={Uri.EscapeDataString(providerId)}&plot=full";
            var response = await _httpClient.GetStringAsync(url, cancellationToken);
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (root.TryGetProperty("Response", out var resp) && resp.GetString() == "False")
            {
                return null;
            }

            var title = root.GetProperty("Title").GetString() ?? string.Empty;
            int? year = null;
            var yearStr = root.TryGetProperty("Year", out var ys) ? ys.GetString() : null;
            if (!string.IsNullOrEmpty(yearStr) && yearStr.Length >= 4 && int.TryParse(yearStr[..4], out var y))
            {
                year = y;
            }

            int? runtime = null;
            var runtimeStr = root.TryGetProperty("Runtime", out var rt) ? rt.GetString() : null;
            if (!string.IsNullOrEmpty(runtimeStr))
            {
                var numStr = new string(runtimeStr.TakeWhile(char.IsDigit).ToArray());
                if (int.TryParse(numStr, out var r)) runtime = r;
            }

            double? rating = null;
            var ratingStr = root.TryGetProperty("imdbRating", out var ir) ? ir.GetString() : null;
            if (!string.IsNullOrEmpty(ratingStr) && double.TryParse(ratingStr, System.Globalization.CultureInfo.InvariantCulture, out var rVal))
            {
                rating = rVal;
            }

            return new MetadataDetails
            {
                ImdbId = providerId,
                Title = title,
                Year = year,
                Overview = root.TryGetProperty("Plot", out var pl) && pl.GetString() != "N/A" ? pl.GetString() : null,
                RuntimeMinutes = runtime,
                Genres = root.TryGetProperty("Genre", out var g) && g.GetString() != "N/A" ? g.GetString() : null,
                Rating = rating,
                PosterUrl = root.TryGetProperty("Poster", out var po) && po.GetString() != "N/A" ? po.GetString() : null,
                Director = root.TryGetProperty("Director", out var di) && di.GetString() != "N/A" ? di.GetString() : null,
                CastMembers = root.TryGetProperty("Actors", out var ac) && ac.GetString() != "N/A" ? ac.GetString() : null,
                Type = root.TryGetProperty("Type", out var ty) && ty.GetString() == "series" ? MediaType.TvShow : MediaType.Movie
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return null;
        }
    }
}
