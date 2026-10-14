using System.IO;
using System.Text.RegularExpressions;

namespace LocalFlix.Metadata;

public record NfoHints
{
    public string? ImdbId { get; init; }
    public string? TmdbId { get; init; }
    public string? Title { get; init; }
    public int? Year { get; init; }
}

public static class NfoMetadataReader
{
    private static readonly Regex ImdbRegex = new(@"tt\d{7,8}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TmdbRegex = new(@"tmdb(?:id)?[:\s=]+(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TitleXmlRegex = new(@"<title>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex YearXmlRegex = new(@"<year>(\d{4})</year>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static NfoHints? TryReadHints(string videoFilePath)
    {
        var dir = Path.GetDirectoryName(videoFilePath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;

        var baseName = Path.GetFileNameWithoutExtension(videoFilePath);
        var possibleNfoFiles = new[]
        {
            Path.Combine(dir, $"{baseName}.nfo"),
            Path.Combine(dir, "movie.nfo"),
            Path.Combine(dir, "tvshow.nfo")
        };

        foreach (var nfoPath in possibleNfoFiles)
        {
            if (File.Exists(nfoPath))
            {
                try
                {
                    var content = File.ReadAllText(nfoPath);
                    string? imdbId = null;
                    string? tmdbId = null;
                    string? title = null;
                    int? year = null;

                    var imdbMatch = ImdbRegex.Match(content);
                    if (imdbMatch.Success) imdbId = imdbMatch.Value.ToLowerInvariant();

                    var tmdbMatch = TmdbRegex.Match(content);
                    if (tmdbMatch.Success) tmdbId = tmdbMatch.Groups[1].Value;

                    var titleMatch = TitleXmlRegex.Match(content);
                    if (titleMatch.Success) title = titleMatch.Groups[1].Value.Trim();

                    var yearMatch = YearXmlRegex.Match(content);
                    if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out var y)) year = y;

                    if (!string.IsNullOrEmpty(imdbId) || !string.IsNullOrEmpty(tmdbId) || !string.IsNullOrEmpty(title))
                    {
                        return new NfoHints
                        {
                            ImdbId = imdbId,
                            TmdbId = tmdbId,
                            Title = title,
                            Year = year
                        };
                    }
                }
                catch
                {
                    // Ignore read failure and continue
                }
            }
        }

        return null;
    }
}
