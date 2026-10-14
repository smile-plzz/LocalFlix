using LocalFlix.Core.Interfaces;
using LocalFlix.Core.Models;
using System.Text.Json;

namespace LocalFlix.Metadata;

public class MetadataOrchestrator
{
    private readonly ILibraryRepository _repository;
    private readonly IImageCacheService _imageCache;
    private readonly IMetadataProvider _primaryProvider;
    private readonly IMetadataProvider? _secondaryProvider;

    public MetadataOrchestrator(
        ILibraryRepository repository,
        IImageCacheService imageCache,
        IMetadataProvider primaryProvider,
        IMetadataProvider? secondaryProvider = null)
    {
        _repository = repository;
        _imageCache = imageCache;
        _primaryProvider = primaryProvider;
        _secondaryProvider = secondaryProvider;
    }

    public async Task<TitleItem?> MatchMediaFileAsync(
        MediaFile file,
        ParsedMediaInfo parsedInfo,
        string fullDiskPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var existingTitle = await _repository.GetTitleByMediaFileIdAsync(file.Id);
        if (existingTitle?.MatchedBy == MatchMethod.Manual) return existingTitle;
        if (file.Status == FileStatus.Excluded) return null;
        // Check for .nfo hints first
        var nfoHints = NfoMetadataReader.TryReadHints(fullDiskPath);
        string? targetProviderId = nfoHints?.TmdbId ?? nfoHints?.ImdbId;

        MetadataDetails? details = null;
        double confidence = 1.0;

        if (!string.IsNullOrEmpty(targetProviderId))
        {
            details = await _primaryProvider.GetDetailsAsync(targetProviderId, parsedInfo.InferredType, cancellationToken);
            if (details == null && _secondaryProvider != null)
            {
                details = await _secondaryProvider.GetDetailsAsync(targetProviderId, parsedInfo.InferredType, cancellationToken);
            }
        }

        if (details == null)
        {
            var searchTitle = nfoHints?.Title ?? parsedInfo.CleanTitle;
            var searchYear = nfoHints?.Year ?? parsedInfo.Year;

            var candidates = await _primaryProvider.SearchAsync(searchTitle, searchYear, parsedInfo.InferredType, cancellationToken);
            if ((candidates == null || candidates.Count == 0) && _secondaryProvider != null)
            {
                candidates = await _secondaryProvider.SearchAsync(searchTitle, searchYear, parsedInfo.InferredType, cancellationToken);
            }

            if (candidates == null || candidates.Count == 0)
            {
                // Unmatched: Send to review queue
                await _repository.UpsertReviewItemAsync(new ReviewItem
                {
                    MediaFileId = file.Id,
                    Reason = $"No online metadata matches found for '{parsedInfo.CleanTitle}'",
                    CandidateMatchesJson = "[]"
                });
                return null;
            }

            var topCandidate = candidates.OrderByDescending(c => c.ConfidenceScore).First();
            confidence = topCandidate.ConfidenceScore;

            if (confidence >= 0.85)
            {
                // High confidence: Auto-accept
                var provider = topCandidate.Provider == _secondaryProvider?.ProviderName ? _secondaryProvider! : _primaryProvider;
                details = await provider.GetDetailsAsync(topCandidate.Id, topCandidate.Type, cancellationToken);
            }
            else
            {
                // Low or medium confidence: Route to Needs Review queue
                var candidateSummaries = candidates.Take(5).Select(c => new
                {
                    c.Id,
                    c.Provider,
                    c.Title,
                    c.Year,
                    Type = c.Type.ToString(),
                    c.PosterUrl,
                    c.ConfidenceScore
                });

                await _repository.UpsertReviewItemAsync(new ReviewItem
                {
                    MediaFileId = file.Id,
                    Reason = $"Ambiguous match (Confidence: {confidence:P0}) for '{parsedInfo.CleanTitle}'",
                    CandidateMatchesJson = JsonSerializer.Serialize(candidateSummaries)
                });
                return null;
            }
        }

        if (details == null) return null;

        // Fetch IMDb score from secondary provider if primary is TMDB and IMDb rating missing
        if (!string.IsNullOrEmpty(details.ImdbId) && _secondaryProvider != null)
        {
            var secondaryDetails = await _secondaryProvider.GetDetailsAsync(details.ImdbId, details.Type, cancellationToken);
            if (secondaryDetails?.Rating.HasValue == true)
            {
                details = details with { Rating = secondaryDetails.Rating };
            }
        }

        // Cache poster and backdrop locally for offline browsing
        string? localPosterPath = null;
        if (!string.IsNullOrEmpty(details.PosterUrl))
        {
            localPosterPath = await _imageCache.CacheImageAsync(details.PosterUrl, "posters", cancellationToken);
        }

        string? localBackdropPath = null;
        if (!string.IsNullOrEmpty(details.BackdropUrl))
        {
            localBackdropPath = await _imageCache.CacheImageAsync(details.BackdropUrl, "backdrops", cancellationToken);
        }

        var titleItem = new TitleItem
        {
            Type = details.Type == MediaType.Movie && details.Genres?.Contains("Documentary", StringComparison.OrdinalIgnoreCase) == true ? MediaType.Documentary : details.Type,
            TmdbId = details.TmdbId,
            ImdbId = details.ImdbId,
            Name = details.Title,
            OriginalName = details.OriginalTitle,
            Year = details.Year,
            Overview = details.Overview,
            RuntimeMinutes = details.RuntimeMinutes,
            Genres = details.Genres,
            Rating = details.Rating,
            PosterPath = localPosterPath ?? details.PosterUrl,
            BackdropPath = localBackdropPath ?? details.BackdropUrl,
            Director = details.Director,
            CastMembers = details.CastMembers,
            MatchedBy = MatchMethod.Auto,
            ConfidenceScore = confidence,
            MediaFileId = file.Id
        };

        var titleId = await _repository.UpsertTitleAsync(titleItem);
        titleItem.Id = titleId;

        await LinkEpisodesAsync(titleItem, file.Id, parsedInfo);

        foreach (var review in (await _repository.GetPendingReviewItemsAsync()).Where(r => r.MediaFileId == file.Id))
            await _repository.ResolveReviewItemAsync(review.Id);
        return titleItem;
    }

    private async Task LinkEpisodesAsync(TitleItem titleItem, int fileId, ParsedMediaInfo parsedInfo)
    {
        if (titleItem.Type == MediaType.TvShow || parsedInfo.SeasonNumber.HasValue)
        {
            var series = new Series
            {
                TitleItemId = titleItem.Id,
                Name = titleItem.Name,
                Overview = titleItem.Overview,
                PosterPath = titleItem.PosterPath,
                BackdropPath = titleItem.BackdropPath
            };
            var seriesId = await _repository.UpsertSeriesAsync(series);

            var seasonNumber = parsedInfo.SeasonNumber ?? 1;
            var season = new Season
            {
                SeriesId = seriesId,
                SeasonNumber = seasonNumber,
                Name = $"Season {seasonNumber}"
            };
            var seasonId = await _repository.UpsertSeasonAsync(season);

            var episode = new Episode
            {
                SeasonId = seasonId,
                MediaFileId = fileId,
                EpisodeNumber = parsedInfo.EpisodeNumber ?? 1,
                Name = $"Episode {parsedInfo.EpisodeNumber ?? 1}"
            };
            await _repository.UpsertEpisodeAsync(episode);
            if (parsedInfo.SecondEpisodeNumber.HasValue && parsedInfo.SecondEpisodeNumber > episode.EpisodeNumber)
                for (var number = episode.EpisodeNumber + 1; number <= Math.Min(parsedInfo.SecondEpisodeNumber.Value, episode.EpisodeNumber + 20); number++)
                    await _repository.UpsertEpisodeAsync(new Episode { SeasonId = seasonId, MediaFileId = fileId, EpisodeNumber = number, Name = $"Episode {number}" });
        }

    }

    public async Task<TitleItem> ApplyManualMatchAsync(
        int mediaFileId,
        string providerId,
        IMetadataProvider provider,
        MediaType mediaType = MediaType.Movie,
        int? reviewItemId = null,
        CancellationToken cancellationToken = default)
    {
        if (providerId.StartsWith("tt", StringComparison.OrdinalIgnoreCase) && _secondaryProvider != null)
            provider = _secondaryProvider;
        var details = await provider.GetDetailsAsync(providerId, mediaType, cancellationToken)
            ?? throw new InvalidOperationException($"Could not find metadata for provider ID '{providerId}'");

        string? localPosterPath = null;
        if (!string.IsNullOrEmpty(details.PosterUrl))
        {
            localPosterPath = await _imageCache.CacheImageAsync(details.PosterUrl, "posters", cancellationToken);
        }

        string? localBackdropPath = null;
        if (!string.IsNullOrEmpty(details.BackdropUrl))
        {
            localBackdropPath = await _imageCache.CacheImageAsync(details.BackdropUrl, "backdrops", cancellationToken);
        }

        var titleItem = new TitleItem
        {
            Type = details.Type == MediaType.Movie && details.Genres?.Contains("Documentary", StringComparison.OrdinalIgnoreCase) == true ? MediaType.Documentary : details.Type,
            TmdbId = details.TmdbId,
            ImdbId = details.ImdbId,
            Name = details.Title,
            OriginalName = details.OriginalTitle,
            Year = details.Year,
            Overview = details.Overview,
            RuntimeMinutes = details.RuntimeMinutes,
            Genres = details.Genres,
            Rating = details.Rating,
            PosterPath = localPosterPath ?? details.PosterUrl,
            BackdropPath = localBackdropPath ?? details.BackdropUrl,
            Director = details.Director,
            CastMembers = details.CastMembers,
            MatchedBy = MatchMethod.Manual,
            ConfidenceScore = 1.0,
            MediaFileId = mediaFileId
        };

        var titleId = await _repository.UpsertTitleAsync(titleItem);
        titleItem.Id = titleId;

        var file = await _repository.GetMediaFileByIdAsync(mediaFileId);
        if (file != null && titleItem.Type == MediaType.TvShow)
        {
            // Season and episode numbers are read from the existing local filename, never sent as paths.
            var match = System.Text.RegularExpressions.Regex.Match(file.RelativePath, @"[Ss](\d+)[Ee](\d+)");
            await LinkEpisodesAsync(titleItem, mediaFileId, new ParsedMediaInfo
            {
                SeasonNumber = match.Success ? int.Parse(match.Groups[1].Value) : 1,
                EpisodeNumber = match.Success ? int.Parse(match.Groups[2].Value) : 1
            });
        }
        await _repository.UpdateFileStatusAsync(mediaFileId, FileStatus.Active);
        if (reviewItemId.HasValue)
        {
            await _repository.ResolveReviewItemAsync(reviewItemId.Value);
        }

        foreach (var review in (await _repository.GetPendingReviewItemsAsync()).Where(r => r.MediaFileId == titleItem.MediaFileId))
            await _repository.ResolveReviewItemAsync(review.Id);
        return titleItem;
    }
}
