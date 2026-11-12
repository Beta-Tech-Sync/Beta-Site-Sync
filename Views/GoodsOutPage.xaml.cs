using SiteSyncTracker.Models;
using SiteSyncTracker.Services;

namespace SiteSyncTracker.Views;

public partial class GoodsOutPage : ContentPage
{
    private readonly InventoryService _inventoryService;

    public GoodsOutPage(InventoryService? inventoryService = null)
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
        try
        {
            LocationPicker.ItemsSource = (await _inventoryService.GetLocationsAsync()).ToList();
            StatusPicker.ItemsSource = (await _inventoryService.GetStatusesAsync()).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load locations/statuses: {ex.Message}");
        }
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

    private async void OnConfirmIssueClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ManifestEntry.Text) || !int.TryParse(QuantityEntry.Text, out var quantity)
            || quantity <= 0 || LocationPicker.SelectedItem is not Location location
            || StatusPicker.SelectedItem is not Status status)
        {
            await DisplayAlertAsync("Validation", "Enter a manifest, positive quantity, destination, and status.", "OK");
            return;
        }

        try
        {
            var item = (await _inventoryService.GetItemsAsync(ManifestEntry.Text.Trim()))
                .FirstOrDefault(inventoryItem => inventoryItem.ManifestNumber.Equals(ManifestEntry.Text.Trim(), StringComparison.OrdinalIgnoreCase));

            if (item is null)
            {
                await DisplayAlertAsync("Not found", "No inventory item matches that manifest.", "OK");
                return;
            }

            if (quantity > item.QuantityReceived)
            {
                await DisplayAlertAsync("Validation", "Quantity out cannot exceed quantity received.", "OK");
                return;
            }

            item.QuantityReceived -= quantity;
            item.CurrentLocationId = location.Id;
            item.CurrentLocation = location;
            item.CurrentStatusId = status.Id;
            item.CurrentStatus = status;
            await _inventoryService.UpdateAsync(item);

            ManifestEntry.Text = QuantityEntry.Text = string.Empty;
            await DisplayAlertAsync("Saved", "The material issue was recorded.", "OK");
            await NavigateToAsync(nameof(InventoryPage));
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Could not record material issue: {ex.Message}", "OK");
        }
    }
}
