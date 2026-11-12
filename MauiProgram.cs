using Microsoft.Extensions.Logging;
using SiteSyncTracker.Services;
using SiteSyncTracker.Views;

namespace SiteSyncTracker;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .AddSiteSyncServices()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register pages for DI navigation
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<GoodsInPage>();
        builder.Services.AddTransient<GoodsOutPage>();
        builder.Services.AddTransient<InventoryPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
