using SiteSyncTracker.Data;

namespace SiteSyncTracker.Services;

public static class ServiceCollectionExtensions
{
    public static MauiAppBuilder AddSiteSyncServices(this MauiAppBuilder builder)
    {
        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "sitesync.db");
        builder.Services.AddPooledDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));
        builder.Services.AddSingleton<InventoryService>();
        builder.Services.AddSingleton<RemoteSyncService>(serviceProvider =>
        {
#if DEBUG && ANDROID && !SITESYNC_PREVIEW
            const string defaultBaseUrl = "http://10.0.2.2:8080/";
#else
            const string defaultBaseUrl = "https://beta-sync-tech.com.au/";
#endif
            var baseUrl = Preferences.Default.Get("api_base_url", defaultBaseUrl).Trim();
            if (!baseUrl.EndsWith('/'))
            {
                baseUrl += "/";
            }

            var client = new HttpClient
            {
                BaseAddress = new Uri(baseUrl, UriKind.Absolute),
                Timeout = TimeSpan.FromSeconds(30)
            };
            return new RemoteSyncService(client);
        });
        return builder;
    }
}
