using LocalFlix.Core.Models;
using LocalFlix.Data;
using LocalFlix.Drives;
using System.IO;
using Xunit;

namespace LocalFlix.Tests;

public class M5_DriveAwarenessTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqliteLibraryRepository _repository;
    private readonly WindowsDriveService _driveService;

    public M5_DriveAwarenessTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"LocalFlix_DriveTest_{Guid.NewGuid():N}.db");
        var migrator = new DatabaseMigrator(_tempDbPath);
        migrator.Migrate();

        _repository = new SqliteLibraryRepository(_tempDbPath);
        _driveService = new WindowsDriveService();
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
            var bak = $"{_tempDbPath}.bak";
            if (File.Exists(bak)) File.Delete(bak);
        }
        catch
        {
        }
    }

    [Fact]
    public void DriveService_EnumeratesConnectedDrivesWithVolumeGuid()
    {
        var drives = _driveService.GetConnectedDrives();
        Assert.NotEmpty(drives);

        var systemDrive = drives.FirstOrDefault(d => d.LastKnownLetter.StartsWith("C:", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(systemDrive);
        Assert.False(string.IsNullOrEmpty(systemDrive.VolumeGuid));
        Assert.True(systemDrive.IsConnected);
    }

    [Fact]
    public async Task DriveService_UpdatesStatusAndRelinksDriveWhenLetterChanges()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), $"DriveRelink_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempFolder);
        var testFile = Path.Combine(tempFolder, "movie.mkv");
        File.WriteAllBytes(testFile, new byte[100]);

        try
        {
            var root = Path.GetPathRoot(tempFolder) ?? "C:\\";
            var guid = _driveService.GetVolumeGuidForPath(root);
            var serial = _driveService.GetVolumeSerialForPath(root);

            var drive = new Drive
            {
                VolumeGuid = guid,
                SerialNumber = serial,
                Label = "TestDrive",
                LastKnownLetter = "Z:\\", // Stored with old/outdated drive letter Z:
                IsConnected = false
            };
            var driveId = await _repository.UpsertDriveAsync(drive);

            var relativePath = Path.GetRelativePath(root, testFile);

            // Relink path: DriveService should discover that this Volume GUID is actually at 'root' (e.g. C:)
            var resolved = _driveService.ResolveFullPath(drive, relativePath);

            Assert.NotNull(resolved);
            Assert.True(File.Exists(resolved));
            Assert.True(drive.IsConnected);
            Assert.Equal(root, drive.LastKnownLetter);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task DriveService_SynchronizeDrivesReflectsArrivalAndRemoval()
    {
        // Insert a fictional disconnected drive into DB
        var fictionalDrive = new Drive
        {
            VolumeGuid = @"\\?\Volume{fictional-disconnected-999}\",
            SerialNumber = "SN-FICTIONAL",
            Label = "OfflineDrive",
            LastKnownLetter = "X:\\",
            IsConnected = true // marked connected initially
        };
        var driveId = await _repository.UpsertDriveAsync(fictionalDrive);

        // Run sync
        await _driveService.SynchronizeDrivesAsync(_repository);

        // The fictional drive should now be marked disconnected in database
        var updated = await _repository.GetDriveByIdAsync(driveId);
        Assert.NotNull(updated);
        Assert.False(updated.IsConnected);
    }
}
