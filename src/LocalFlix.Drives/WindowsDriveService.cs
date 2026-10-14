using LocalFlix.Core.Interfaces;
using LocalFlix.Core.Models;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace LocalFlix.Drives;

public class WindowsDriveService : IDriveService
{
    public event EventHandler<DriveChangedEventArgs>? DriveChanged;

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetVolumeInformation(
        string rootPathName,
        StringBuilder volumeNameBuffer,
        int volumeNameSize,
        out uint volumeSerialNumber,
        out uint maximumComponentLength,
        out uint fileSystemFlags,
        StringBuilder fileSystemNameBuffer,
        int fileSystemNameSize);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetVolumeNameForVolumeMountPoint(
        string volumeName,
        StringBuilder volumeGuid,
        int volumeGuidBufLen);

    public IReadOnlyList<Drive> GetConnectedDrives()
    {
        var list = new List<Drive>();
        foreach (var d in DriveInfo.GetDrives())
        {
            if (!d.IsReady) continue;

            try
            {
                var letter = d.RootDirectory.FullName;
                var guid = GetVolumeGuidForPath(letter);
                var serial = GetVolumeSerialForPath(letter);
                var label = string.IsNullOrEmpty(d.VolumeLabel) ? $"Drive ({letter.TrimEnd('\\')})" : d.VolumeLabel;

                list.Add(new Drive
                {
                    VolumeGuid = guid,
                    SerialNumber = serial,
                    Label = label,
                    FileSystem = d.DriveFormat,
                    LastKnownLetter = letter,
                    LastSeenUtc = DateTime.UtcNow,
                    IsConnected = true
                });
            }
            catch
            {
                // Ignore transient drive errors
            }
        }
        return list;
    }

    public string GetVolumeGuidForPath(string path)
    {
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root)) return string.Empty;
        if (!root.EndsWith('\\')) root += "\\";

        var sb = new StringBuilder(100);
        if (GetVolumeNameForVolumeMountPoint(root, sb, sb.Capacity))
        {
            return sb.ToString();
        }

        throw new IOException("Cannot determine a stable volume identity. Reconnect the drive and try again.");
    }

    public string GetVolumeSerialForPath(string path)
    {
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root)) return string.Empty;
        if (!root.EndsWith('\\')) root += "\\";

        var volName = new StringBuilder(260);
        var fsName = new StringBuilder(260);
        if (GetVolumeInformation(root, volName, volName.Capacity, out uint serial, out _, out _, fsName, fsName.Capacity))
        {
            return serial.ToString("X8");
        }

        return "SN-UNKNOWN";
    }

    public string GetVolumeLabelForPath(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            if (!string.IsNullOrEmpty(root))
            {
                var d = new DriveInfo(root);
                if (d.IsReady && !string.IsNullOrEmpty(d.VolumeLabel))
                {
                    return d.VolumeLabel;
                }
            }
        }
        catch
        {
        }
        return "Local Drive";
    }

    public string? ResolveFullPath(Drive drive, string relativePath)
    {
        if (Path.IsPathRooted(relativePath) || relativePath.Split('\\', '/').Contains("..")) return null;
        // If letter changed, search across all currently connected drives by Volume GUID or Serial
        foreach (var d in DriveInfo.GetDrives())
        {
            if (!d.IsReady) continue;

            var currentRoot = d.RootDirectory.FullName;
            var currentGuid = GetVolumeGuidForPath(currentRoot);
            var currentSerial = GetVolumeSerialForPath(currentRoot);

            if (SameVolume(drive, currentGuid, currentSerial))
            {
                drive.LastKnownLetter = currentRoot;
                drive.IsConnected = true;
                var resolved = Path.Combine(currentRoot, relativePath);
                return resolved;
            }
        }

        drive.IsConnected = false;
        return null;
    }

    public string? ResolveFullPath(MediaFile mediaFile)
    {
        // Resolved at repository / playback level using the drive
        return mediaFile.FullPath;
    }

    public const int WM_DEVICECHANGE = 0x0219;
    public const int DBT_DEVICEARRIVAL = 0x8000;
    public const int DBT_DEVICEREMOVECOMPLETE = 0x8004;

    public IntPtr HandleWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_DEVICECHANGE)
        {
            var wp = wParam.ToInt32();
            if (wp == DBT_DEVICEARRIVAL || wp == DBT_DEVICEREMOVECOMPLETE)
            {
                var isArrival = wp == DBT_DEVICEARRIVAL;
                DriveChanged?.Invoke(this, new DriveChangedEventArgs(new Drive(), isArrival));
            }
        }
        return IntPtr.Zero;
    }

    public async Task SynchronizeDrivesAsync(ILibraryRepository repository)
    {
        var dbDrives = await repository.GetAllDrivesAsync();
        var connected = GetConnectedDrives();

        foreach (var dbDrive in dbDrives)
        {
            var match = connected.FirstOrDefault(c =>
                SameVolume(dbDrive, c.VolumeGuid, c.SerialNumber));

            if (match != null)
            {
                if (!dbDrive.IsConnected || dbDrive.LastKnownLetter != match.LastKnownLetter)
                {
                    await repository.UpdateDriveStatusAsync(dbDrive.Id, true, match.LastKnownLetter);

                }
            }
            else
            {
                if (dbDrive.IsConnected)
                {
                    await repository.UpdateDriveStatusAsync(dbDrive.Id, false, dbDrive.LastKnownLetter);

                }
            }
        }
    }

    public static bool SameVolume(Drive stored, string guid, string serial)
    {
        if (!string.IsNullOrWhiteSpace(stored.VolumeGuid) && !string.IsNullOrWhiteSpace(guid))
            return string.Equals(stored.VolumeGuid, guid, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrEmpty(stored.SerialNumber) || stored.SerialNumber == "SN-UNKNOWN"
                    || string.Equals(stored.SerialNumber, serial, StringComparison.OrdinalIgnoreCase));
        return !string.IsNullOrEmpty(serial) && serial != "SN-UNKNOWN"
            && string.Equals(serial, stored.SerialNumber, StringComparison.OrdinalIgnoreCase);
    }

    public void TriggerDriveChanged(Drive drive, bool isArrival)
    {
        DriveChanged?.Invoke(this, new DriveChangedEventArgs(drive, isArrival));
    }
}
