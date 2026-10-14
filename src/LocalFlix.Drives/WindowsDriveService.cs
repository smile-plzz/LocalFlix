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

        // Fallback to normalized drive root if volume GUID unavailable
        return $@"\\?\Volume{{{root.Replace(":", "").Replace("\\", "")}}}\";
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
        // First check current known letter
        if (!string.IsNullOrEmpty(drive.LastKnownLetter))
        {
            var full = Path.Combine(drive.LastKnownLetter, relativePath);
            if (File.Exists(full) || Directory.Exists(full))
            {
                return full;
            }
        }

        // If letter changed, search across all currently connected drives by Volume GUID or Serial
        foreach (var d in DriveInfo.GetDrives())
        {
            if (!d.IsReady) continue;

            var currentRoot = d.RootDirectory.FullName;
            var currentGuid = GetVolumeGuidForPath(currentRoot);
            var currentSerial = GetVolumeSerialForPath(currentRoot);

            if (currentGuid == drive.VolumeGuid || (currentSerial == drive.SerialNumber && !string.IsNullOrEmpty(drive.SerialNumber)))
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

    public void TriggerDriveChanged(Drive drive, bool isArrival)
    {
        DriveChanged?.Invoke(this, new DriveChangedEventArgs(drive, isArrival));
    }
}
