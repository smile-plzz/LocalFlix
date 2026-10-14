namespace LocalFlix.Core.Models;

public class Drive
{
    public int Id { get; set; }
    public string VolumeGuid { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string FileSystem { get; set; } = string.Empty;
    public string LastKnownLetter { get; set; } = string.Empty;
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
    public bool IsConnected { get; set; } = true;
}

public class ScanRoot
{
    public int Id { get; set; }
    public int DriveId { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public DateTime? LastScannedUtc { get; set; }
    public int TotalFiles { get; set; }
    public bool IsSmartScan { get; set; }
}
