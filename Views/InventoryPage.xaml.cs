using SiteSyncTracker.Services;

namespace SiteSyncTracker.Views;

public partial class InventoryPage : ContentPage
{
    private readonly InventoryService _inventoryService;

    public InventoryPage(InventoryService? inventoryService = null)
    {
        InitializeComponent();
        _inventoryService = inventoryService
            ?? IPlatformApplication.Current?.Services?.GetService<InventoryService>()
            ?? Application.Current?.Handler?.MauiContext?.Services?.GetService<InventoryService>()
            ?? new InventoryService(null!);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadItemsAsync();
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

    private async void OnBackClicked(object? sender, EventArgs e) => await NavigateToAsync(nameof(MainPage));
    private async void OnDashboardNavClicked(object? sender, EventArgs e) => await NavigateToAsync(nameof(MainPage));
    private async void OnGoodsInNavClicked(object? sender, EventArgs e) => await NavigateToAsync(nameof(GoodsInPage));
    private async void OnGoodsOutNavClicked(object? sender, EventArgs e) => await NavigateToAsync(nameof(GoodsOutPage));
    private async void OnInventoryNavClicked(object? sender, EventArgs e) => await NavigateToAsync(nameof(InventoryPage));
    private async void OnReceiveNewDeliveryClicked(object? sender, EventArgs e) => await NavigateToAsync(nameof(GoodsInPage));

    private async void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        await LoadItemsAsync(e.NewTextValue);
    }

    private async Task LoadItemsAsync(string? search = null)
    {
        LoadingIndicator.IsVisible = true;
        try
        {
            InventoryList.ItemsSource = await _inventoryService.GetItemsAsync(search);
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("Inventory", exception.Message, "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
        }
    }
}
