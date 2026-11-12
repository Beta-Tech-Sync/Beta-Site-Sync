using SiteSyncTracker.Views;

namespace SiteSyncTracker;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(MainPage), typeof(MainPage));
        Routing.RegisterRoute(nameof(GoodsInPage), typeof(GoodsInPage));
        Routing.RegisterRoute(nameof(GoodsOutPage), typeof(GoodsOutPage));
        Routing.RegisterRoute(nameof(InventoryPage), typeof(InventoryPage));
    }
}
