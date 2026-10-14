namespace LocalFlix.Core.Models;

public class TitleItem
{
    public int Id { get; set; }
    public MediaType Type { get; set; } = MediaType.Movie;
    public string? TmdbId { get; set; }
    public string? ImdbId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? OriginalName { get; set; }
    public int? Year { get; set; }
    public string? Overview { get; set; }
    public int? RuntimeMinutes { get; set; }
    public string? Genres { get; set; }
    public double? Rating { get; set; }
    public string? PosterPath { get; set; }
    public string? BackdropPath { get; set; }
    public string? Director { get; set; }
    public string? CastMembers { get; set; }
    public MatchMethod MatchedBy { get; set; } = MatchMethod.Auto;
    public double ConfidenceScore { get; set; } = 1.0;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    // Optional direct link to MediaFile for movies
    public int? MediaFileId { get; set; }
}

public class Series
{
    public int Id { get; set; }
    public int TitleItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Overview { get; set; }
    public int TotalSeasons { get; set; }
    public string? PosterPath { get; set; }
    public string? BackdropPath { get; set; }
}

public class Season
{
    public int Id { get; set; }
    public int SeriesId { get; set; }
    public int SeasonNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PosterPath { get; set; }
    public int EpisodeCount { get; set; }
}

public class Episode
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public int? MediaFileId { get; set; }
    public int EpisodeNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Overview { get; set; }
    public string? AirDate { get; set; }
    public string? StillPath { get; set; }
}
