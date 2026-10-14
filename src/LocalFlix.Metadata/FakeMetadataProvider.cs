using LocalFlix.Core.Interfaces;
using LocalFlix.Core.Models;

namespace LocalFlix.Metadata;

public class FakeMetadataProvider : IMetadataProvider
{
    public string ProviderName => "FakeMetadataProvider";

    private readonly List<MetadataDetails> _catalog = new()
    {
        new MetadataDetails
        {
            TmdbId = "tmdb-101",
            ImdbId = "tt0111161",
            Title = "The Shawshank Redemption",
            OriginalTitle = "The Shawshank Redemption",
            Year = 1994,
            Overview = "Two imprisoned men bond over a number of years, finding solace and eventual redemption through acts of common decency.",
            RuntimeMinutes = 142,
            Genres = "Drama, Crime",
            Rating = 9.3,
            PosterUrl = "https://image.tmdb.org/t/p/w500/shawshank.jpg",
            BackdropUrl = "https://image.tmdb.org/t/p/original/shawshank_backdrop.jpg",
            Director = "Frank Darabont",
            CastMembers = "Tim Robbins, Morgan Freeman, Bob Gunton",
            Type = MediaType.Movie
        },
        new MetadataDetails
        {
            TmdbId = "tmdb-102",
            ImdbId = "tt0468569",
            Title = "The Dark Knight",
            OriginalTitle = "The Dark Knight",
            Year = 2008,
            Overview = "When the menace known as the Joker wreaks havoc and chaos on the people of Gotham, Batman must accept one of the greatest psychological and physical tests of his ability to fight injustice.",
            RuntimeMinutes = 152,
            Genres = "Action, Crime, Drama",
            Rating = 9.0,
            PosterUrl = "https://image.tmdb.org/t/p/w500/dark_knight.jpg",
            BackdropUrl = "https://image.tmdb.org/t/p/original/dark_knight_backdrop.jpg",
            Director = "Christopher Nolan",
            CastMembers = "Christian Bale, Heath Ledger, Aaron Eckhart",
            Type = MediaType.Movie
        },
        new MetadataDetails
        {
            TmdbId = "tmdb-103",
            ImdbId = "tt1375666",
            Title = "Inception",
            OriginalTitle = "Inception",
            Year = 2010,
            Overview = "A thief who steals corporate secrets through the use of dream-sharing technology is given the inverse task of planting an idea into the mind of a C.E.O.",
            RuntimeMinutes = 148,
            Genres = "Action, Adventure, Sci-Fi",
            Rating = 8.8,
            PosterUrl = "https://image.tmdb.org/t/p/w500/inception.jpg",
            BackdropUrl = "https://image.tmdb.org/t/p/original/inception_backdrop.jpg",
            Director = "Christopher Nolan",
            CastMembers = "Leonardo DiCaprio, Joseph Gordon-Levitt, Elliot Page",
            Type = MediaType.Movie
        },
        new MetadataDetails
        {
            TmdbId = "tmdb-104",
            ImdbId = "tt0903747",
            Title = "Breaking Bad",
            OriginalTitle = "Breaking Bad",
            Year = 2008,
            Overview = "A chemistry teacher diagnosed with inoperable lung cancer turns to manufacturing and selling methamphetamine with a former student in order to secure his family's financial future.",
            RuntimeMinutes = 49,
            Genres = "Crime, Drama, Thriller",
            Rating = 9.5,
            PosterUrl = "https://image.tmdb.org/t/p/w500/breaking_bad.jpg",
            BackdropUrl = "https://image.tmdb.org/t/p/original/breaking_bad_backdrop.jpg",
            Director = "Vince Gilligan",
            CastMembers = "Bryan Cranston, Aaron Paul, Anna Gunn",
            Type = MediaType.TvShow
        },
        new MetadataDetails
        {
            TmdbId = "tmdb-105",
            ImdbId = "tt0944947",
            Title = "Game of Thrones",
            OriginalTitle = "Game of Thrones",
            Year = 2011,
            Overview = "Nine noble families fight for control over the lands of Westeros, while an ancient enemy returns after being dormant for millennia.",
            RuntimeMinutes = 57,
            Genres = "Action, Adventure, Drama",
            Rating = 9.2,
            PosterUrl = "https://image.tmdb.org/t/p/w500/game_of_thrones.jpg",
            BackdropUrl = "https://image.tmdb.org/t/p/original/got_backdrop.jpg",
            Director = "David Benioff, D.B. Weiss",
            CastMembers = "Emilia Clarke, Peter Dinklage, Kit Harington",
            Type = MediaType.TvShow
        },
        new MetadataDetails
        {
            TmdbId = "tmdb-106",
            ImdbId = "tt0795176",
            Title = "Planet Earth",
            OriginalTitle = "Planet Earth",
            Year = 2006,
            Overview = "Dazzling, state-of-the-art high-definition footage of planet Earth and its breathtaking wildlife.",
            RuntimeMinutes = 530,
            Genres = "Documentary",
            Rating = 9.4,
            PosterUrl = "https://image.tmdb.org/t/p/w500/planet_earth.jpg",
            BackdropUrl = "https://image.tmdb.org/t/p/original/planet_earth_backdrop.jpg",
            Director = "Alastair Fothergill",
            CastMembers = "David Attenborough, Sigourney Weaver",
            Type = MediaType.Documentary
        }
    };

    public Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
        string query,
        int? year = null,
        MediaType mediaType = MediaType.Unknown,
        CancellationToken cancellationToken = default)
    {
        var cleanQuery = query.Trim().ToLowerInvariant();
        var results = new List<MetadataSearchResult>();

        foreach (var item in _catalog)
        {
            if (mediaType != MediaType.Unknown && item.Type != mediaType && mediaType != MediaType.Episode)
            {
                continue;
            }

            var itemTitle = item.Title.ToLowerInvariant();
            double confidence = 0.0;

            if (itemTitle == cleanQuery)
            {
                confidence = 0.95;
            }
            else if (itemTitle.Contains(cleanQuery) || cleanQuery.Contains(itemTitle))
            {
                confidence = 0.80;
            }

            if (confidence > 0 && year.HasValue && item.Year.HasValue)
            {
                if (Math.Abs(year.Value - item.Year.Value) == 0)
                {
                    confidence += 0.05;
                }
                else if (Math.Abs(year.Value - item.Year.Value) > 2)
                {
                    confidence -= 0.20;
                }
            }

            if (confidence > 0.40)
            {
                results.Add(new MetadataSearchResult
                {
                    Id = item.TmdbId,
                    Provider = ProviderName,
                    Title = item.Title,
                    Year = item.Year,
                    Type = item.Type,
                    PosterUrl = item.PosterUrl,
                    Overview = item.Overview,
                    ConfidenceScore = Math.Min(1.0, Math.Max(0.0, confidence))
                });
            }
        }

        return Task.FromResult<IReadOnlyList<MetadataSearchResult>>(
            results.OrderByDescending(r => r.ConfidenceScore).ToList());
    }

    public Task<MetadataDetails?> GetDetailsAsync(
        string providerId,
        MediaType mediaType = MediaType.Unknown,
        CancellationToken cancellationToken = default)
    {
        var match = _catalog.FirstOrDefault(c => c.TmdbId == providerId || c.ImdbId == providerId);
        return Task.FromResult(match);
    }
}
