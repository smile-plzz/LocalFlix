using Microsoft.Data.Sqlite;
using System.IO;

namespace LocalFlix.Data;

public class DatabaseMigrator
{
    private readonly string _dbPath;

    public DatabaseMigrator(string dbPath)
    {
        _dbPath = dbPath;
    }

    public static string GetDefaultDatabasePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(localAppData, "LocalFlix");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return Path.Combine(dir, "localflix.db");
    }

    public void Migrate()
    {
        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Backup existing database before migration if it exists
        if (File.Exists(_dbPath))
        {
            var backupPath = $"{_dbPath}.bak";
            try
            {
                File.Copy(_dbPath, backupPath, true);
            }
            catch
            {
                // Non-fatal if backup fails due to lock, but logged if logger attached
            }
        }

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        // Create migrations table
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS __Migrations (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Version INTEGER NOT NULL UNIQUE,
                    Description TEXT NOT NULL,
                    AppliedUtc TEXT NOT NULL
                );";
            cmd.ExecuteNonQuery();
        }

        // Execute migrations
        ApplyMigration(connection, 1, "Initial Schema", GetMigrationV1Script());
    }

    private void ApplyMigration(SqliteConnection connection, int version, string description, string sql)
    {
        using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM __Migrations WHERE Version = $version";
            checkCmd.Parameters.AddWithValue("$version", version);
            var count = Convert.ToInt64(checkCmd.ExecuteScalar());
            if (count > 0)
            {
                return;
            }
        }

        using var transaction = connection.BeginTransaction();
        using (var sqlCmd = connection.CreateCommand())
        {
            sqlCmd.Transaction = transaction;
            sqlCmd.CommandText = sql;
            sqlCmd.ExecuteNonQuery();
        }

        using (var recordCmd = connection.CreateCommand())
        {
            recordCmd.Transaction = transaction;
            recordCmd.CommandText = @"
                INSERT INTO __Migrations (Version, Description, AppliedUtc)
                VALUES ($version, $desc, $utc);";
            recordCmd.Parameters.AddWithValue("$version", version);
            recordCmd.Parameters.AddWithValue("$desc", description);
            recordCmd.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O"));
            recordCmd.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private string GetMigrationV1Script()
    {
        return @"
            CREATE TABLE IF NOT EXISTS Drives (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                VolumeGuid TEXT NOT NULL UNIQUE,
                SerialNumber TEXT NOT NULL DEFAULT '',
                Label TEXT NOT NULL DEFAULT '',
                FileSystem TEXT NOT NULL DEFAULT '',
                LastKnownLetter TEXT NOT NULL DEFAULT '',
                LastSeenUtc TEXT NOT NULL,
                IsConnected INTEGER NOT NULL DEFAULT 1
            );

            CREATE TABLE IF NOT EXISTS ScanRoots (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DriveId INTEGER NOT NULL,
                RelativePath TEXT NOT NULL,
                LastScannedUtc TEXT,
                TotalFiles INTEGER NOT NULL DEFAULT 0,
                IsSmartScan INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (DriveId) REFERENCES Drives(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS MediaFiles (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DriveId INTEGER NOT NULL,
                RelativePath TEXT NOT NULL,
                FileSizeBytes INTEGER NOT NULL DEFAULT 0,
                DateModifiedUtc TEXT NOT NULL,
                DurationSeconds REAL NOT NULL DEFAULT 0,
                Width INTEGER NOT NULL DEFAULT 0,
                Height INTEGER NOT NULL DEFAULT 0,
                VideoCodec TEXT NOT NULL DEFAULT '',
                AudioCodec TEXT NOT NULL DEFAULT '',
                AudioChannels TEXT NOT NULL DEFAULT '',
                SubtitleTracksJson TEXT NOT NULL DEFAULT '',
                IdentityHash TEXT NOT NULL DEFAULT '',
                Classification INTEGER NOT NULL DEFAULT 0,
                Status INTEGER NOT NULL DEFAULT 0,
                CreatedUtc TEXT NOT NULL,
                UNIQUE (DriveId, RelativePath),
                FOREIGN KEY (DriveId) REFERENCES Drives(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Titles (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Type INTEGER NOT NULL DEFAULT 1,
                TmdbId TEXT,
                ImdbId TEXT,
                Name TEXT NOT NULL,
                OriginalName TEXT,
                Year INTEGER,
                Overview TEXT,
                RuntimeMinutes INTEGER,
                Genres TEXT,
                Rating REAL,
                PosterPath TEXT,
                BackdropPath TEXT,
                Director TEXT,
                CastMembers TEXT,
                MatchedBy INTEGER NOT NULL DEFAULT 0,
                ConfidenceScore REAL NOT NULL DEFAULT 1.0,
                CreatedUtc TEXT NOT NULL,
                MediaFileId INTEGER,
                FOREIGN KEY (MediaFileId) REFERENCES MediaFiles(Id) ON DELETE SET NULL
            );

            CREATE TABLE IF NOT EXISTS Series (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TitleItemId INTEGER NOT NULL UNIQUE,
                Name TEXT NOT NULL,
                Overview TEXT,
                TotalSeasons INTEGER NOT NULL DEFAULT 1,
                PosterPath TEXT,
                BackdropPath TEXT,
                FOREIGN KEY (TitleItemId) REFERENCES Titles(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Seasons (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SeriesId INTEGER NOT NULL,
                SeasonNumber INTEGER NOT NULL,
                Name TEXT NOT NULL DEFAULT '',
                PosterPath TEXT,
                EpisodeCount INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (SeriesId) REFERENCES Series(Id) ON DELETE CASCADE,
                UNIQUE (SeriesId, SeasonNumber)
            );

            CREATE TABLE IF NOT EXISTS Episodes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SeasonId INTEGER NOT NULL,
                MediaFileId INTEGER,
                EpisodeNumber INTEGER NOT NULL,
                Name TEXT NOT NULL DEFAULT '',
                Overview TEXT,
                AirDate TEXT,
                StillPath TEXT,
                FOREIGN KEY (SeasonId) REFERENCES Seasons(Id) ON DELETE CASCADE,
                FOREIGN KEY (MediaFileId) REFERENCES MediaFiles(Id) ON DELETE SET NULL,
                UNIQUE (SeasonId, EpisodeNumber)
            );

            CREATE TABLE IF NOT EXISTS PlaybackStates (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MediaFileId INTEGER NOT NULL UNIQUE,
                PositionSeconds REAL NOT NULL DEFAULT 0,
                DurationSeconds REAL NOT NULL DEFAULT 0,
                IsWatched INTEGER NOT NULL DEFAULT 0,
                LastPlayedUtc TEXT NOT NULL,
                FOREIGN KEY (MediaFileId) REFERENCES MediaFiles(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS UserLists (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TitleItemId INTEGER NOT NULL,
                ListType INTEGER NOT NULL DEFAULT 0,
                AddedUtc TEXT NOT NULL,
                UNIQUE (TitleItemId, ListType),
                FOREIGN KEY (TitleItemId) REFERENCES Titles(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS ReviewItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MediaFileId INTEGER NOT NULL,
                Reason TEXT NOT NULL,
                CandidateMatchesJson TEXT NOT NULL DEFAULT '[]',
                CreatedUtc TEXT NOT NULL,
                IsResolved INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (MediaFileId) REFERENCES MediaFiles(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS IX_MediaFiles_DriveId ON MediaFiles(DriveId);
            CREATE INDEX IF NOT EXISTS IX_Titles_Type ON Titles(Type);
            CREATE INDEX IF NOT EXISTS IX_Titles_Name ON Titles(Name);
            CREATE INDEX IF NOT EXISTS IX_Episodes_SeasonId ON Episodes(SeasonId);
            CREATE INDEX IF NOT EXISTS IX_UserLists_Type ON UserLists(ListType);
        ";
    }
}
