using LocalFlix.Core.Interfaces;
using LocalFlix.Core.Models;
using System.IO;
using System.Text.RegularExpressions;

namespace LocalFlix.Scanner;

public class MediaClassifier : IMediaClassifier
{
    private const long DefaultMinSizeBytes = 35 * 1024 * 1024; // 35 MB

    private static readonly Regex CameraFilenameRegex = new(
        @"^(?:IMG_|VID_|PXL_|MOV_|DSC_|DSCN|MVI_|SAM_|GOPR|GP\d{2}|DJI_|\d{8}_\d{6})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] PersonalFolders =
    {
        "dcim", "camera", "whatsapp", "snapchat", "screen recordings",
        "screencasts", "gopro", "iphone", "family", "personal", "phone"
    };

    private static readonly string[] NoiseKeywords =
    {
        "sample", "trailer", "featurette", "extra", "extras",
        "behind the scenes", "deleted scenes", "bloopers", "bonus"
    };

    private static readonly string[] NoiseDirectories =
    {
        "$recycle.bin", "system volume information", ".git", ".idea",
        ".vscode", "node_modules"
    };

    private static readonly string[] DocumentaryKeywords =
    {
        "documentary", "documentaries", "docuseries", "bbc earth", "national geographic"
    };

    public bool IsNoise(string fullPath, long fileSizeBytes)
    {
        fullPath = fullPath.Replace('\\', Path.DirectorySeparatorChar);
        var filename = Path.GetFileNameWithoutExtension(fullPath).ToLowerInvariant();
        var rawFilename = Path.GetFileName(fullPath);
        var dir = Path.GetDirectoryName(fullPath)?.ToLowerInvariant() ?? string.Empty;

        // Never drop personal videos as noise; they must be classified as Personal and routed to review
        if (CameraFilenameRegex.IsMatch(rawFilename))
        {
            return false;
        }
        foreach (var pFolder in PersonalFolders)
        {
            if (dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '\\').Contains(pFolder))
            {
                return false;
            }
        }

        // Check system directory paths
        foreach (var noiseDir in NoiseDirectories)
        {
            if (dir.Contains(noiseDir))
            {
                return true;
            }
        }

        // Check noise filename keywords
        foreach (var keyword in NoiseKeywords)
        {
            if (filename == keyword || filename.Contains($"-{keyword}") || filename.Contains($".{keyword}") || filename.Contains($" {keyword}"))
            {
                return true;
            }
        }

        // Filter tiny files unless named like a TV episode or specific clip
        if (fileSizeBytes > 0 && fileSizeBytes < DefaultMinSizeBytes && !filename.Contains("s01e") && !filename.Contains("1x0"))
        {
            return true;
        }

        return false;
    }

    public bool IsPersonalVideo(
        string fullPath,
        long fileSizeBytes,
        double durationSeconds,
        ParsedMediaInfo parsedInfo,
        out string reason)
    {
        fullPath = fullPath.Replace('\\', Path.DirectorySeparatorChar);
        var filename = Path.GetFileName(fullPath);
        var dir = Path.GetDirectoryName(fullPath)?.ToLowerInvariant() ?? string.Empty;

        // Signal 1: Camera folder check
        foreach (var pFolder in PersonalFolders)
        {
            if (dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '\\').Contains(pFolder))
            {
                reason = $"Located in personal/camera folder: '{pFolder}'";
                return true;
            }
        }

        // Signal 2: Camera naming pattern (IMG_xxxx, VID_xxxx, PXL_xxxx, 20241012_143522)
        if (CameraFilenameRegex.IsMatch(filename))
        {
            reason = "Matches mobile/digital camera filename pattern";
            return true;
        }

        // Signal 3: Very short clip without any title/season/year match
        if (durationSeconds > 0 && durationSeconds < 40 && parsedInfo.Year == null && parsedInfo.SeasonNumber == null)
        {
            reason = "Very short video clip with no title or movie/TV metadata";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    public MediaType Classify(
        string fullPath,
        long fileSizeBytes,
        double durationSeconds,
        ParsedMediaInfo parsedInfo)
    {
        if (IsNoise(fullPath, fileSizeBytes))
        {
            return MediaType.Unknown;
        }

        if (IsPersonalVideo(fullPath, fileSizeBytes, durationSeconds, parsedInfo, out _))
        {
            return MediaType.Personal;
        }

        var lowerPath = fullPath.ToLowerInvariant();
        foreach (var docWord in DocumentaryKeywords)
        {
            if (lowerPath.Contains(docWord))
            {
                return MediaType.Documentary;
            }
        }

        if (parsedInfo.SeasonNumber.HasValue && parsedInfo.EpisodeNumber.HasValue)
        {
            return MediaType.Episode;
        }

        if (!string.IsNullOrWhiteSpace(parsedInfo.CleanTitle) && parsedInfo.Year.HasValue)
        {
            return MediaType.Movie;
        }

        return MediaType.Movie;
    }
}
