using LocalFlix.Core.Interfaces;
using LocalFlix.Core.Models;
using System.IO;
using System.Text.RegularExpressions;

namespace LocalFlix.Scanner;

public partial class FilenameParser : IFilenameParser
{
    private static readonly string[] VideoExtensions =
    {
        ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".m4v",
        ".ts", ".m2ts", ".webm", ".mpg", ".mpeg"
    };

    private static readonly Regex SeasonEpisodeRegex = new(
        @"(?:s|season\s*)(\d{1,2})[\s._-]*(?:e|ep|episode\s*)(\d{1,3})(?:[\s._-]*(?:e|ep|episode|-)\s*(\d{1,3}))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AltSeasonEpisodeRegex = new(
        @"(?:^|[\s._\-\(\[])(\d{1,2})x(\d{1,3})(?:[\s._\-\(\[]|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex YearRegex = new(
        @"(?:[\s._\(\[\-])((?:19|20)\d{2})(?:[\s._\)\]\-]|$)",
        RegexOptions.Compiled);

    private static readonly Regex ResolutionRegex = new(
        @"(2160p|4k|1080p|1080i|720p|576p|480p)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CodecRegex = new(
        @"(x264|x265|h264|hevc|av1|xvid|divx|10bit)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SourceRegex = new(
        @"(bluray|blu-ray|bdrip|brrip|web-dl|webdl|webrip|web|hdtv|dvdrip|remux)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AudioRegex = new(
        @"(dts-hd|dts|truehd|atmos|ac3|ddp5\.1|dd5\.1|aac|mp3|flac|5\.1|7\.1)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AnimeEpisodeRegex = new(
        @"^\[([^\]]+)\]\s*(.+?)\s*-\s*(\d{1,4})(?:\s*v\d)?(?:\s*\(|\s*\[|\s*$)",
        RegexOptions.Compiled);

    private static readonly Regex CleanBracketsRegex = new(
        @"\[[^\]]*\]|\([^\)]*\)",
        RegexOptions.Compiled);

    public ParsedMediaInfo Parse(string filename, string? parentDirectoryName = null)
    {
        var rawName = Path.GetFileNameWithoutExtension(filename);
        var extension = Path.GetExtension(filename).ToLowerInvariant();

        if (!VideoExtensions.Contains(extension))
        {
            return new ParsedMediaInfo
            {
                RawTitle = rawName,
                CleanTitle = rawName,
                InferredType = MediaType.Unknown
            };
        }

        // Check for anime-style pattern first: [Group] Title - 01 [Tags]
        var animeMatch = AnimeEpisodeRegex.Match(rawName);
        if (animeMatch.Success)
        {
            var animeTitle = NormalizeTitle(animeMatch.Groups[2].Value);
            var epNum = int.Parse(animeMatch.Groups[3].Value);
            return new ParsedMediaInfo
            {
                RawTitle = rawName,
                CleanTitle = animeTitle,
                SeasonNumber = 1,
                EpisodeNumber = epNum,
                InferredType = MediaType.Episode
            };
        }

        int? seasonNumber = null;
        int? episodeNumber = null;
        int? secondEpisodeNumber = null;
        bool isMultiEpisode = false;

        // Try standard S01E01 pattern
        var seMatch = SeasonEpisodeRegex.Match(rawName);
        if (seMatch.Success)
        {
            seasonNumber = int.Parse(seMatch.Groups[1].Value);
            episodeNumber = int.Parse(seMatch.Groups[2].Value);
            if (seMatch.Groups[3].Success && int.TryParse(seMatch.Groups[3].Value, out var ep2))
            {
                secondEpisodeNumber = ep2;
                isMultiEpisode = true;
            }
        }
        else
        {
            // Try 1x01 pattern
            var altMatch = AltSeasonEpisodeRegex.Match(rawName);
            if (altMatch.Success)
            {
                seasonNumber = int.Parse(altMatch.Groups[1].Value);
                episodeNumber = int.Parse(altMatch.Groups[2].Value);
            }
        }

        // Detect resolution, source, codec
        var resMatch = ResolutionRegex.Match(rawName);
        string? resolution = resMatch.Success ? resMatch.Groups[1].Value.ToLowerInvariant() : null;

        var sourceMatch = SourceRegex.Match(rawName);
        string? source = sourceMatch.Success ? sourceMatch.Groups[1].Value : null;

        var codecMatch = CodecRegex.Match(rawName);
        string? codec = codecMatch.Success ? codecMatch.Groups[1].Value.ToLowerInvariant() : null;

        var audioMatch = AudioRegex.Match(rawName);
        string? audio = audioMatch.Success ? audioMatch.Groups[1].Value : null;

        // Year extraction
        int? year = null;
        var yearMatches = YearRegex.Matches(rawName);
        if (yearMatches.Count > 0)
        {
            // Pick the year that isn't part of season/episode or resolution
            foreach (Match ym in yearMatches)
            {
                if (int.TryParse(ym.Groups[1].Value, out var y) && y >= 1900 && y <= DateTime.UtcNow.Year + 2)
                {
                    year = y;
                    break;
                }
            }
        }

        // Title cleaning
        string cleanTitle;
        if (seasonNumber.HasValue && seMatch.Success)
        {
            // Everything before SxxExx is the series title
            cleanTitle = rawName.Substring(0, seMatch.Index);
        }
        else if (seasonNumber.HasValue)
        {
            var altMatch = AltSeasonEpisodeRegex.Match(rawName);
            cleanTitle = altMatch.Success ? rawName.Substring(0, altMatch.Index) : rawName;
        }
        else if (year.HasValue)
        {
            // Everything before the year is the title
            var ym = YearRegex.Match(rawName);
            cleanTitle = rawName.Substring(0, ym.Index);
        }
        else
        {
            var tag = Regex.Match(rawName, @"[ ._\[]+(?:2160p|1080[pi]|720p|480p|WEB-DL|WEBRip|BluRay|BDRip|HDTV|x264|x265|HEVC)(?:[ ._\]\-]|$)", RegexOptions.IgnoreCase);
            cleanTitle = tag.Success ? rawName[..tag.Index] : rawName;
        }

        cleanTitle = NormalizeTitle(cleanTitle);

        // Fallback to parent directory if filename is too generic (e.g., "CD1", "movie", "01")
        if (IsGenericName(cleanTitle) && !string.IsNullOrWhiteSpace(parentDirectoryName))
        {
            var parentParsed = Parse(parentDirectoryName + extension);
            if (!string.IsNullOrWhiteSpace(parentParsed.CleanTitle) && !IsGenericName(parentParsed.CleanTitle))
            {
                cleanTitle = parentParsed.CleanTitle;
                if (!year.HasValue && parentParsed.Year.HasValue)
                {
                    year = parentParsed.Year;
                }
            }
        }

        var inferredType = seasonNumber.HasValue ? MediaType.Episode : MediaType.Movie;

        return new ParsedMediaInfo
        {
            RawTitle = rawName,
            CleanTitle = cleanTitle,
            Year = year,
            SeasonNumber = seasonNumber,
            EpisodeNumber = episodeNumber,
            SecondEpisodeNumber = secondEpisodeNumber,
            IsMultiEpisode = isMultiEpisode,
            Resolution = resolution,
            Source = source,
            VideoCodec = codec,
            AudioCodec = audio,
            InferredType = inferredType
        };
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;

        // Remove release tags inside brackets: [1080p], [YTS], [BluRay]
        title = CleanBracketsRegex.Replace(title, " ");

        // Replace dots, underscores, pluses with spaces
        title = Regex.Replace(title, @"[\._\+]+", " ");

        // Remove release group suffixes at end like "-SPARKS", "-YIFY"
        title = Regex.Replace(title, @"\s*-(?:SPARKS|YIFY|RARBG|YTS|ETRG|EVO)$", "");

        // Remove trailing hyphens or noise
        title = Regex.Replace(title, @"[\s\-]+$", "");

        // Collapse multiple spaces
        title = Regex.Replace(title, @"\s{2,}", " ");

        return title.Trim();
    }

    private static bool IsGenericName(string name)
    {
        var lower = name.Trim().ToLowerInvariant();
        return lower is "cd1" or "cd2" or "part1" or "part2" or "movie" or "video" or "sample"
            or "01" or "02" or "03" or "04" or "05" or "06" or "07" or "08" or "09" or "10"
            || int.TryParse(lower, out _);
    }
}
