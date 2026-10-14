using LocalFlix.Core.Models;
using LocalFlix.Data;
using LocalFlix.Scanner;
using System.IO;
using Xunit;

namespace LocalFlix.Tests;

public class M1_ScannerAndParserTests
{
    private readonly FilenameParser _parser = new();
    private readonly MediaClassifier _classifier = new();

    [Theory]
    // Movies: Standard release formats
    [InlineData("The.Matrix.1999.1080p.BluRay.x264-SPARKS.mkv", "The Matrix", 1999, null, null, "1080p")]
    [InlineData("Inception (2010) [2160p 4K HDR] [5.1 Audio].mp4", "Inception", 2010, null, null, "2160p")]
    [InlineData("Gladiator.2000.EXTENDED.REMASTERED.1080p.mkv", "Gladiator", 2000, null, null, "1080p")]
    [InlineData("Interstellar.2014.IMAX.2160p.UHD.HDR.x265.mkv", "Interstellar", 2014, null, null, "2160p")]
    [InlineData("Pulp Fiction (1994) [1080p] [YTS.MX].mp4", "Pulp Fiction", 1994, null, null, "1080p")]
    [InlineData("Fight.Club.1999.720p.HDTV.x264.mkv", "Fight Club", 1999, null, null, "720p")]
    [InlineData("The.Dark.Knight.2008.1080p.BluRay.DTS.x264.mkv", "The Dark Knight", 2008, null, null, "1080p")]
    [InlineData("Spirited.Away.2001.JAPANESE.1080p.BluRay.x264.mkv", "Spirited Away", 2001, null, null, "1080p")]
    [InlineData("Parasite.2019.KOREAN.1080p.WEBRip.x264.mp4", "Parasite", 2019, null, null, "1080p")]
    [InlineData("Avengers.Endgame.2019.2160p.WEB-DL.DDP5.1.Atmos.hevc.mkv", "Avengers Endgame", 2019, null, null, "2160p")]
    [InlineData("2001.A.Space.Odyssey.1968.1080p.mkv", "2001 A Space Odyssey", 1968, null, null, "1080p")]
    [InlineData("Pather.Panchali.1955.BENGALI.1080p.mkv", "Pather Panchali", 1955, null, null, "1080p")]
    [InlineData("3.Idiots.2009.HINDI.720p.mkv", "3 Idiots", 2009, null, null, "720p")]
    [InlineData("Amelie.2001.FRENCH.1080p.mkv", "Amelie", 2001, null, null, "1080p")]
    // TV Series: Standard SxxExx and 1x01
    [InlineData("Breaking.Bad.S05E16.Felina.720p.HDTV.x264.mkv", "Breaking Bad", null, 5, 16, "720p")]
    [InlineData("Game.of.Thrones.S01E01.Winter.Is.Coming.1080p.BluRay.mkv", "Game of Thrones", null, 1, 1, "1080p")]
    [InlineData("The.Wire.1x01.The.Target.720p.HDTV.mkv", "The Wire", null, 1, 1, "720p")]
    [InlineData("Stranger.Things.S04E09.1080p.NF.WEB-DL.DDP5.1.x264.mkv", "Stranger Things", null, 4, 9, "1080p")]
    [InlineData("Succession.S04E03.Connor's.Wedding.1080p.mkv", "Succession", null, 4, 3, "1080p")]
    [InlineData("Chernobyl.S01E05.1080p.BluRay.x264.mkv", "Chernobyl", null, 1, 5, "1080p")]
    [InlineData("Severance.S01E01.Good.News.About.Hell.1080p.mkv", "Severance", null, 1, 1, "1080p")]
    [InlineData("The.Last.of.Us.S01E03.Long.Long.Time.1080p.mkv", "The Last of Us", null, 1, 3, "1080p")]
    [InlineData("Planet.Earth.II.S01E01.Islands.2160p.mkv", "Planet Earth II", null, 1, 1, "2160p")]
    public void FilenameParser_AccuratelyExtractsTitleYearSeasonAndEpisode(
        string filename,
        string expectedTitle,
        int? expectedYear,
        int? expectedSeason,
        int? expectedEpisode,
        string? expectedResolution)
    {
        var result = _parser.Parse(filename);

        Assert.Equal(expectedTitle, result.CleanTitle);
        if (expectedYear.HasValue)
        {
            Assert.Equal(expectedYear, result.Year);
        }
        if (expectedSeason.HasValue)
        {
            Assert.Equal(expectedSeason, result.SeasonNumber);
        }
        if (expectedEpisode.HasValue)
        {
            Assert.Equal(expectedEpisode, result.EpisodeNumber);
        }
        if (expectedResolution != null)
        {
            Assert.Equal(expectedResolution, result.Resolution);
        }
    }

    [Fact]
    public void FilenameParser_HandlesMultiEpisodeFiles()
    {
        var result = _parser.Parse("Friends.S01E01-E02.Pilot.720p.mkv");

        Assert.Equal("Friends", result.CleanTitle);
        Assert.Equal(1, result.SeasonNumber);
        Assert.Equal(1, result.EpisodeNumber);
        Assert.Equal(2, result.SecondEpisodeNumber);
        Assert.True(result.IsMultiEpisode);
    }

    [Fact]
    public void FilenameParser_HandlesAnimeAbsoluteNumbering()
    {
        var result = _parser.Parse("[SubsPlease] Frieren - 12 (1080p) [ABCD1234].mkv");

        Assert.Equal("Frieren", result.CleanTitle);
        Assert.Equal(1, result.SeasonNumber);
        Assert.Equal(12, result.EpisodeNumber);
        Assert.Equal(MediaType.Episode, result.InferredType);
    }

    [Fact]
    public void FilenameParser_FallsBackToParentDirectoryWhenFilenameIsGeneric()
    {
        var result = _parser.Parse("CD1.avi", "The.Godfather.1972.1080p.BluRay");

        Assert.Equal("The Godfather", result.CleanTitle);
        Assert.Equal(1972, result.Year);
    }

    [Theory]
    [InlineData(@"E:\Photos\DCIM\Camera\VID_20231012_143522.mp4", true)]
    [InlineData(@"E:\Photos\Camera\IMG_9941.MOV", true)]
    [InlineData(@"E:\Backups\WhatsApp\WhatsApp Video 2024-01-05.mp4", true)]
    [InlineData(@"E:\GoPro\GOPR1234.MP4", true)]
    [InlineData(@"E:\Movies\The Matrix (1999)\The.Matrix.1999.1080p.mkv", false)]
    [InlineData(@"E:\TV Shows\Breaking Bad\Season 01\Breaking.Bad.S01E01.mkv", false)]
    public void MediaClassifier_CorrectlyIdentifiesPersonalVideos(string path, bool expectedPersonal)
    {
        var parsed = _parser.Parse(Path.GetFileName(path));
        var isPersonal = _classifier.IsPersonalVideo(path, 100 * 1024 * 1024, 60, parsed, out var reason);

        Assert.Equal(expectedPersonal, isPersonal);
        if (expectedPersonal)
        {
            Assert.False(string.IsNullOrEmpty(reason));
        }
    }

    [Theory]
    [InlineData(@"E:\Movies\The Matrix\sample-matrix.mkv", 10 * 1024 * 1024, true)]
    [InlineData(@"E:\Movies\Inception\Inception.trailer.mp4", 25 * 1024 * 1024, true)]
    [InlineData(@"E:\$RECYCLE.BIN\S-1-5-21\movie.mp4", 500 * 1024 * 1024, true)]
    [InlineData(@"E:\Movies\Inception (2010)\Inception.2010.1080p.mkv", 1500L * 1024 * 1024, false)]
    public void MediaClassifier_CorrectlyFiltersNoiseAndSamples(string path, long size, bool expectedNoise)
    {
        var isNoise = _classifier.IsNoise(path, size);
        Assert.Equal(expectedNoise, isNoise);
    }

    [Fact]
    public async Task FileScanner_ScansDirectoryIncrementallyAndExcludesPersonalVideos()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"LocalFlix_ScanTest_{Guid.NewGuid():N}");
        var tempDb = Path.Combine(Path.GetTempPath(), $"LocalFlix_Db_{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create dummy test tree
            var movieDir = Path.Combine(tempDir, "Movies");
            var personalDir = Path.Combine(tempDir, "DCIM");
            Directory.CreateDirectory(movieDir);
            Directory.CreateDirectory(personalDir);

            var movieFile = Path.Combine(movieDir, "Gladiator.2000.1080p.mkv");
            var personalFile = Path.Combine(personalDir, "VID_20241014_112233.mp4");
            var noiseFile = Path.Combine(movieDir, "sample-gladiator.mkv");

            // Write 40MB dummy movie file so it passes size threshold
            var dummyData = new byte[1024 * 1024]; // 1MB chunk
            using (var fs = File.Create(movieFile))
            {
                for (int i = 0; i < 40; i++) fs.Write(dummyData);
            }

            File.WriteAllBytes(personalFile, new byte[1024 * 1024]);
            File.WriteAllBytes(noiseFile, new byte[1024 * 1024]);

            var migrator = new DatabaseMigrator(tempDb);
            migrator.Migrate();
            var repo = new SqliteLibraryRepository(tempDb);

            var drive = new Drive
            {
                VolumeGuid = @"\\?\Volume{test-guid}\",
                Label = "TestDrive",
                LastKnownLetter = "T:"
            };
            var driveId = await repo.UpsertDriveAsync(drive);

            var scanner = new FileScanner(repo, _parser, _classifier);
            await scanner.ScanDirectoryAsync(tempDir, driveId);

            var files = await repo.GetFilesByDriveIdAsync(driveId);

            // Expecting 2 files in database: Gladiator (Active) and VID_... (Excluded)
            Assert.Equal(2, files.Count);

            var activeMovie = files.FirstOrDefault(f => f.RelativePath.Contains("Gladiator"));
            Assert.NotNull(activeMovie);
            Assert.Equal(FileStatus.Active, activeMovie.Status);

            var excludedPersonal = files.FirstOrDefault(f => f.RelativePath.Contains("VID_"));
            Assert.NotNull(excludedPersonal);
            Assert.Equal(FileStatus.Excluded, excludedPersonal.Status);

            // Review queue should contain the excluded personal video
            var reviewItems = await repo.GetPendingReviewItemsAsync();
            Assert.Single(reviewItems);
            Assert.Contains("personal", reviewItems[0].Reason.ToLowerInvariant());
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                if (File.Exists(tempDb)) File.Delete(tempDb);
            }
            catch
            {
            }
        }
    }
}
