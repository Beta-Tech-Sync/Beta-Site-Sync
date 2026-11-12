using SiteSyncTracker.Models;
using SiteSyncTracker.Services;

namespace SiteSyncTracker.Views;

public partial class GoodsInPage : ContentPage
{
    private readonly InventoryService _inventoryService;

    public GoodsInPage(InventoryService? inventoryService = null)
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
            var locations = (await _inventoryService.GetLocationsAsync()).ToList();
            var statuses = (await _inventoryService.GetStatusesAsync()).ToList();
            LocationPicker.ItemsSource = locations;
            StatusPicker.ItemsSource = statuses;
            if (statuses.Count > 1)
            {
                StatusPicker.SelectedIndex = 1;
            }
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

    private async void OnConfirmReceiptClicked(object? sender, EventArgs e)
    {
        if (!int.TryParse(ExpectedEntry.Text, out var expected) || !int.TryParse(ReceivedEntry.Text, out var received)
            || expected < 0 || received < 0 || string.IsNullOrWhiteSpace(ManifestEntry.Text)
            || string.IsNullOrWhiteSpace(DescriptionEntry.Text) || LocationPicker.SelectedItem is not Location location
            || StatusPicker.SelectedItem is not Status status)
        {
            await DisplayAlertAsync("Validation", "Enter valid manifest, description, quantities, location, and status values.", "OK");
            return;
        }

        try
        {
            await _inventoryService.AddAsync(new InventoryItem
            {
                ManifestNumber = ManifestEntry.Text.Trim(),
                Description = DescriptionEntry.Text.Trim(),
                QuantityExpected = expected,
                QuantityReceived = received,
                CurrentLocationId = location.Id,
                CurrentLocation = location,
                CurrentStatusId = status.Id,
                CurrentStatus = status,
                NotifyTo = NotifyEntry.Text?.Trim()
            });

            ManifestEntry.Text = DescriptionEntry.Text = ExpectedEntry.Text = ReceivedEntry.Text = NotifyEntry.Text = string.Empty;
            await DisplayAlertAsync("Saved", "The delivery was added to inventory.", "OK");
            await NavigateToAsync(nameof(InventoryPage));
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Could not save inventory item: {ex.Message}", "OK");
        }
    }
}
