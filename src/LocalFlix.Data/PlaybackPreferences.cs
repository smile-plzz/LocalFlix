using System.Text.Json;

namespace LocalFlix.Data;

public class PlaybackPreferences
{
    public string SubtitleLanguage { get; set; } = "English";
    public bool HardwareAcceleration { get; set; } = true;
    public bool AutoPlayNext { get; set; } = true;
    private static string SettingsPath => Path.Combine(Path.GetDirectoryName(DatabaseMigrator.GetDefaultDatabasePath())!, "preferences.json");
    public static PlaybackPreferences Load()
    {
        try { return JsonSerializer.Deserialize<PlaybackPreferences>(File.ReadAllText(SettingsPath)) ?? new(); }
        catch (IOException) { return new(); }
        catch (JsonException) { return new(); }
    }
    public void Save()
    {
        File.WriteAllText(SettingsPath + ".tmp", JsonSerializer.Serialize(this));
        File.Move(SettingsPath + ".tmp", SettingsPath, true);
    }
}
