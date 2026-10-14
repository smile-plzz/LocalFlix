using LocalFlix.Core.Interfaces;
using LocalFlix.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace LocalFlix.App.Views;

public partial class SettingsView : UserControl
{
    private readonly IImageCacheService _imageCache;
    private readonly string _appDataFolder;

    public SettingsView(IImageCacheService imageCache)
    {
        InitializeComponent();
        _imageCache = imageCache;
        _appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalFlix");

        Loaded += (s, e) => LoadSettings();
    }

    private void LoadSettings()
    {
        TmdbKeyBox.Text = Environment.GetEnvironmentVariable("TMDB_API_KEY") ?? string.Empty;
        OmdbKeyBox.Text = Environment.GetEnvironmentVariable("OMDB_API_KEY") ?? string.Empty;

        var cacheDir = _imageCache.GetLocalCacheDirectory();
        CachePathText.Text = cacheDir;
        UpdateCacheSizeDisplay(cacheDir);
    }

    private void UpdateCacheSizeDisplay(string cacheDir)
    {
        if (Directory.Exists(cacheDir))
        {
            try
            {
                var files = Directory.GetFiles(cacheDir, "*.*", SearchOption.AllDirectories);
                long totalBytes = files.Sum(f => new FileInfo(f).Length);
                var mb = totalBytes / (1024.0 * 1024.0);
                CacheSizeText.Text = $"Current cache size: {mb:0.0} MB ({files.Length} cached images)";
            }
            catch
            {
                CacheSizeText.Text = "Unable to read cache size.";
            }
        }
        else
        {
            CacheSizeText.Text = "Current cache size: 0 MB";
        }
    }

    private void SaveKeysButton_Click(object sender, RoutedEventArgs e)
    {
        Environment.SetEnvironmentVariable("TMDB_API_KEY", TmdbKeyBox.Text.Trim(), EnvironmentVariableTarget.User);
        Environment.SetEnvironmentVariable("OMDB_API_KEY", OmdbKeyBox.Text.Trim(), EnvironmentVariableTarget.User);
        MessageBox.Show("API keys saved successfully to user environment.", "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ClearCacheButton_Click(object sender, RoutedEventArgs e)
    {
        var cacheDir = _imageCache.GetLocalCacheDirectory();
        if (Directory.Exists(cacheDir))
        {
            try
            {
                Directory.Delete(cacheDir, true);
                Directory.CreateDirectory(cacheDir);
                UpdateCacheSizeDisplay(cacheDir);
                MessageBox.Show("Artwork cache cleared.", "Cache Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to clear cache: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void BackupDbButton_Click(object sender, RoutedEventArgs e)
    {
        var dbPath = DatabaseMigrator.GetDefaultDatabasePath();
        if (!File.Exists(dbPath))
        {
            MessageBox.Show("No active database to backup.", "Backup", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"LocalFlix_Backup_{DateTime.UtcNow:yyyyMMdd_HHmm}.db",
            Filter = "SQLite Database (*.db)|*.db",
            Title = "Save Database Backup"
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                File.Copy(dbPath, saveDialog.FileName, true);
                MessageBox.Show("Database backed up successfully!", "Backup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Backup failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void OpenDataFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(_appDataFolder))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _appDataFolder,
                UseShellExecute = true
            });
        }
    }
}
