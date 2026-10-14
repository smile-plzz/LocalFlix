using LocalFlix.Core.Models;

namespace LocalFlix.Core.Interfaces;

public record MetadataSearchResult
{
    public string Id { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public int? Year { get; init; }
    public MediaType Type { get; init; }
    public string? PosterUrl { get; init; }
    public string? Overview { get; init; }
    public double ConfidenceScore { get; init; }
}

public record MetadataDetails
{
    public string TmdbId { get; init; } = string.Empty;
    public string? ImdbId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? OriginalTitle { get; init; }
    public int? Year { get; init; }
    public string? Overview { get; init; }
    public int? RuntimeMinutes { get; init; }
    public string? Genres { get; init; }
    public double? Rating { get; init; }
    public string? PosterUrl { get; init; }
    public string? BackdropUrl { get; init; }
    public string? Director { get; init; }
    public string? CastMembers { get; init; }
    public MediaType Type { get; init; }
}

public interface IMetadataProvider
{
    string ProviderName { get; }
    Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(string query, int? year = null, MediaType mediaType = MediaType.Unknown, CancellationToken cancellationToken = default);
    Task<MetadataDetails?> GetDetailsAsync(string providerId, MediaType mediaType = MediaType.Unknown, CancellationToken cancellationToken = default);
}
