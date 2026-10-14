namespace LocalFlix.Core.Models;

public class PlaybackState
{
    public int Id { get; set; }
    public int MediaFileId { get; set; }
    public double PositionSeconds { get; set; }
    public double DurationSeconds { get; set; }
    public bool IsWatched { get; set; }
    public DateTime LastPlayedUtc { get; set; } = DateTime.UtcNow;
}

public class UserList
{
    public int Id { get; set; }
    public int TitleItemId { get; set; }
    public UserListType ListType { get; set; } = UserListType.Watchlist;
    public DateTime AddedUtc { get; set; } = DateTime.UtcNow;
}

public class ReviewItem
{
    public int Id { get; set; }
    public int MediaFileId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string CandidateMatchesJson { get; set; } = "[]";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public bool IsResolved { get; set; }
}
