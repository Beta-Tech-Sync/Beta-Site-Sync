using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;

namespace SiteSyncTracker.Web.Tests;

public sealed class ApiTests : IClassFixture<TestApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiTests(TestApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Health_is_public()
    {
        using var response = await _client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Inventory_requires_authentication()
    {
        using var response = await _client.GetAsync("/api/inventory");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Inventory_write_requires_authentication()
    {
        using var response = await _client.PostAsJsonAsync("/api/inventory", new
        {
            manifestNumber = "TEST-1",
            description = "Test item",
            quantityExpected = 1,
            quantityReceived = 0,
            currentStatusId = 1,
            currentLocationId = 1
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    [Fact]
    public async Task Registered_user_can_sign_in_and_read_but_cannot_write_inventory()
    {
        var email = $"release-{Guid.NewGuid():N}@example.com";
        const string password = "Release-Test-Password-123!";
        using var registration = await _client.PostAsJsonAsync("/api/auth/register", new { email, password });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        // RemoteSyncService uses the built-in Identity bearer-token endpoint.
        using var login = await _client.PostAsJsonAsync("/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = await login.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = token.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        using var read = new HttpRequestMessage(HttpMethod.Get, "/api/inventory");
        read.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var inventory = await _client.SendAsync(read);
        Assert.Equal(HttpStatusCode.OK, inventory.StatusCode);

        using var write = new HttpRequestMessage(HttpMethod.Post, "/api/inventory");
        write.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        write.Content = JsonContent.Create(new { manifestNumber = "TEST", description = "Test item" });
        using var denied = await _client.SendAsync(write);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
}
