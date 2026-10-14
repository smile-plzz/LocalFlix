namespace LocalFlix.Core.Models;

public enum MediaType
{
    Unknown = 0,
    Movie = 1,
    TvShow = 2,
    Episode = 3,
    Documentary = 4,
    Other = 5,
    Personal = 6
}

public enum FileStatus
{
    Active = 0,
    NeedsReview = 1,
    Excluded = 2,
    MissingDrive = 3
}

public enum MatchMethod
{
    Auto = 0,
    Manual = 1
}

public enum UserListType
{
    Watchlist = 0,
    Favorite = 1
}
