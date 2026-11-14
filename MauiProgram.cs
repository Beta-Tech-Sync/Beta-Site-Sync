using Microsoft.Extensions.Logging;
#if ANDROID
using ZXing.Net.Maui.Controls;
#endif
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

#if ANDROID
        builder.UseBarcodeReader();
#endif
#if WINDOWS
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", Path.Combine(FileSystem.AppDataDirectory, "WebView2"));
#endif
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
