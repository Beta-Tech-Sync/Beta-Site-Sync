using SiteSyncTracker.Services;
using SiteSyncTracker.Views;

namespace SiteSyncTracker;

public partial class MainPage : ContentPage
{
    private readonly InventoryService _inventoryService;

    public MainPage(InventoryService? inventoryService = null)
    {
        InitializeComponent();
        _inventoryService = inventoryService
            ?? IPlatformApplication.Current?.Services?.GetService<InventoryService>()
            ?? Application.Current?.Handler?.MauiContext?.Services?.GetService<InventoryService>()
            ?? new InventoryService(null!);
    }

    private async Task NavigateToAsync(string route)
    {
        try
        {
            await Shell.Current.GoToAsync(route);
        }
        catch
        {
            await Shell.Current.GoToAsync($"//{route}");
        }
    }

    private async void OnGoodsInClicked(object? sender, EventArgs e)
    {
        await NavigateToAsync(nameof(GoodsInPage));
    }

    private async void OnGoodsOutClicked(object? sender, EventArgs e)
    {
        await NavigateToAsync(nameof(GoodsOutPage));
    }

    private async void OnInventoryClicked(object? sender, EventArgs e)
    {
        await NavigateToAsync(nameof(InventoryPage));
    }

    private async void OnDashboardNavClicked(object? sender, EventArgs e)
    {
        await NavigateToAsync(nameof(MainPage));
    }

    private async void OnStatusCardTapped(object? sender, TappedEventArgs e)
    {
        await NavigateToAsync(nameof(InventoryPage));
    }
}
