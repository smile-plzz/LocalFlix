using LocalFlix.Core.Interfaces;
using LocalFlix.Core.Models;
using System.Net.Http;
using System.Text.Json;

namespace LocalFlix.Metadata;

public class TmdbMetadataProvider : IMetadataProvider
{
    private readonly HttpClient _httpClient;
    private readonly string? _configuredApiKey;
    private string? _apiKey => _configuredApiKey ?? Environment.GetEnvironmentVariable("TMDB_API_KEY", EnvironmentVariableTarget.User) ?? Environment.GetEnvironmentVariable("TMDB_API_KEY");

    public string ProviderName => "TheMovieDatabase";

    public TmdbMetadataProvider(HttpClient? httpClient = null, string? apiKey = null)
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
            var endpoint = mediaType == MediaType.TvShow || mediaType == MediaType.Episode ? "tv" : "movie";
            var url = $"https://api.themoviedb.org/3/search/{endpoint}?api_key={_apiKey}&query={Uri.EscapeDataString(query)}";
            if (year.HasValue)
            {
                url += endpoint == "movie" ? $"&year={year.Value}" : $"&first_air_date_year={year.Value}";
            }

            var response = await _httpClient.GetStringAsync(url, cancellationToken);
            using var doc = JsonDocument.Parse(response);
            if (!doc.RootElement.TryGetProperty("results", out var resultsElem))
            {
                return Array.Empty<MetadataSearchResult>();
            }

            var list = new List<MetadataSearchResult>();
            foreach (var item in resultsElem.EnumerateArray())
            {
                var id = item.GetProperty("id").GetInt64().ToString();
                var title = endpoint == "movie"
                    ? (item.TryGetProperty("title", out var t) ? t.GetString() : null)
                    : (item.TryGetProperty("name", out var n) ? n.GetString() : null);

                if (string.IsNullOrWhiteSpace(title)) continue;

                int? itemYear = null;
                var dateProp = endpoint == "movie" ? "release_date" : "first_air_date";
                if (item.TryGetProperty(dateProp, out var d) && !string.IsNullOrEmpty(d.GetString()) && d.GetString()!.Length >= 4)
                {
                    if (int.TryParse(d.GetString()![..4], out var y)) itemYear = y;
                }

                var poster = item.TryGetProperty("poster_path", out var p) && p.GetString() != null
                    ? $"https://image.tmdb.org/t/p/w500{p.GetString()}"
                    : null;

                var overview = item.TryGetProperty("overview", out var o) ? o.GetString() : null;

                var confidence = FuzzyMatcher.CalculateMatchConfidence(query, year, title, itemYear);

                list.Add(new MetadataSearchResult
                {
                    Id = id,
                    Provider = ProviderName,
                    Title = title,
                    Year = itemYear,
                    Type = endpoint == "movie" ? MediaType.Movie : MediaType.TvShow,
                    PosterUrl = poster,
                    Overview = overview,
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
            var endpoint = mediaType == MediaType.TvShow || mediaType == MediaType.Episode ? "tv" : "movie";
            var url = $"https://api.themoviedb.org/3/{endpoint}/{providerId}?api_key={_apiKey}&append_to_response=credits";

            var response = await _httpClient.GetStringAsync(url, cancellationToken);
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            var title = endpoint == "movie"
                ? root.GetProperty("title").GetString() ?? string.Empty
                : root.GetProperty("name").GetString() ?? string.Empty;

            var origTitle = endpoint == "movie"
                ? (root.TryGetProperty("original_title", out var ot) ? ot.GetString() : null)
                : (root.TryGetProperty("original_name", out var on) ? on.GetString() : null);

            int? year = null;
            var dateProp = endpoint == "movie" ? "release_date" : "first_air_date";
            if (root.TryGetProperty(dateProp, out var d) && !string.IsNullOrEmpty(d.GetString()) && d.GetString()!.Length >= 4)
            {
                if (int.TryParse(d.GetString()![..4], out var y)) year = y;
            }

            int? runtime = null;
            if (root.TryGetProperty("runtime", out var r) && r.ValueKind == JsonValueKind.Number)
            {
                runtime = r.GetInt32();
            }

            var genres = string.Empty;
            if (root.TryGetProperty("genres", out var g) && g.ValueKind == JsonValueKind.Array)
            {
                genres = string.Join(", ", g.EnumerateArray().Select(x => x.GetProperty("name").GetString()));
            }

            double? rating = null;
            if (root.TryGetProperty("vote_average", out var v) && v.ValueKind == JsonValueKind.Number)
            {
                rating = Math.Round(v.GetDouble(), 1);
            }

            var poster = root.TryGetProperty("poster_path", out var p) && p.GetString() != null
                ? $"https://image.tmdb.org/t/p/w500{p.GetString()}"
                : null;

            var backdrop = root.TryGetProperty("backdrop_path", out var b) && b.GetString() != null
                ? $"https://image.tmdb.org/t/p/original{b.GetString()}"
                : null;

            string? director = null;
            string? cast = null;
            if (root.TryGetProperty("credits", out var credits))
            {
                if (credits.TryGetProperty("crew", out var crew))
                {
                    director = crew.EnumerateArray()
                        .Where(x => x.TryGetProperty("job", out var job) && job.GetString() == "Director")
                        .Select(x => x.GetProperty("name").GetString()).FirstOrDefault();
                }

                if (credits.TryGetProperty("cast", out var castArray))
                {
                    cast = string.Join(", ", castArray.EnumerateArray().Take(5).Select(x => x.GetProperty("name").GetString()));
                }
            }

            string? imdbId = root.TryGetProperty("imdb_id", out var im) ? im.GetString() : null;

            return new MetadataDetails
            {
                TmdbId = providerId,
                ImdbId = imdbId,
                Title = title,
                OriginalTitle = origTitle,
                Year = year,
                Overview = root.TryGetProperty("overview", out var ov) ? ov.GetString() : null,
                RuntimeMinutes = runtime,
                Genres = genres,
                Rating = rating,
                PosterUrl = poster,
                BackdropUrl = backdrop,
                Director = director,
                CastMembers = cast,
                Type = endpoint == "movie" ? MediaType.Movie : MediaType.TvShow
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return null;
        }
    }
}
