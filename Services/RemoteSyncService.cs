using SiteSyncTracker.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SiteSyncTracker.Services;

public sealed class RemoteSyncService
{
    private const string AccessTokenKey = "sitesync_access_token";
    private readonly HttpClient _httpClient;

    public RemoteSyncService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("login", new { email, password }, cancellationToken);
        if (!response.IsSuccessStatusCode) return false;
        var result = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(result?.AccessToken)) return false;
        await SecureStorage.Default.SetAsync(AccessTokenKey, result.AccessToken);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
        return true;
    }

    public async Task<IReadOnlyList<InventoryItem>> GetInventoryAsync(CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        return await _httpClient.GetFromJsonAsync<List<InventoryItem>>("api/inventory", cancellationToken) ?? [];
    }

    public async Task ApplyTokenAsync()
    {
        var token = await SecureStorage.Default.GetAsync(AccessTokenKey);
        _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    public void SignOut()
    {
        SecureStorage.Default.Remove(AccessTokenKey);
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    private sealed record TokenResponse(string? TokenType, string? AccessToken, int ExpiresIn, string? RefreshToken);
}
