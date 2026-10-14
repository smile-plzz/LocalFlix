using LocalFlix.Core.Interfaces;
using LocalFlix.Data;
using LocalFlix.Metadata;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;

namespace LocalFlix.App;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);

        ServiceProvider = services.BuildServiceProvider();

        // Run database migrations on startup
        var migrator = ServiceProvider.GetRequiredService<DatabaseMigrator>();
        migrator.Migrate();

        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        var dbPath = DatabaseMigrator.GetDefaultDatabasePath();
        services.AddSingleton(new DatabaseMigrator(dbPath));
        services.AddSingleton<ILibraryRepository>(_ => new SqliteLibraryRepository(dbPath));
        services.AddSingleton<IImageCacheService, ImageCacheService>();
        services.AddSingleton<IMetadataProvider>(_ => new CachedMetadataProvider(new TmdbMetadataProvider(), Path.Combine(Path.GetDirectoryName(dbPath)!, "metadata")));
        services.AddSingleton<OmdbMetadataProvider>();

        // Scanner & Parser
        services.AddSingleton<IFilenameParser, LocalFlix.Scanner.FilenameParser>();
        services.AddSingleton<IMediaClassifier, LocalFlix.Scanner.MediaClassifier>();
        services.AddSingleton<IFileScanner, LocalFlix.Scanner.FileScanner>();

        // Metadata Orchestrator & Drive Service
        services.AddSingleton(sp => new MetadataOrchestrator(
            sp.GetRequiredService<ILibraryRepository>(), sp.GetRequiredService<IImageCacheService>(),
            sp.GetRequiredService<IMetadataProvider>(), new CachedMetadataProvider(sp.GetRequiredService<OmdbMetadataProvider>(), Path.Combine(Path.GetDirectoryName(dbPath)!, "metadata"))));
        services.AddSingleton<IDriveService, LocalFlix.Drives.WindowsDriveService>();

        services.AddSingleton<MainWindow>();
    }
}
