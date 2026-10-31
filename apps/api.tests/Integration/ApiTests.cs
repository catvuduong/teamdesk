using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Seed;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Integration;

/// <summary>
/// Shared helpers for tests that call the API through the in-memory test server against the real Postgres database.
/// Each test works on its own users and tickets (unique emails and categories), so tests can run in parallel.
/// </summary>
public abstract class ApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    protected HttpClient Client { get; } = factory.CreateClient();

    protected static string UniqueEmail() => $"{Guid.NewGuid():N}@test.local";

    protected static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    protected async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? token = null, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await Client.SendAsync(request);
    }

    protected static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    protected async Task<string> LoginAsync(string email, string password = SeedData.DemoPassword)
    {
        var response = await SendAsync(HttpMethod.Post, "/auth/login", body: new { email, password });
        response.EnsureSuccessStatusCode();
        return (await JsonAsync(response)).GetProperty("token").GetString()!;
    }

    protected Task<string> AgentTokenAsync() => LoginAsync("agent@teamdesk.demo");

    protected Task<string> AdminTokenAsync() => LoginAsync("admin@teamdesk.demo");

    protected async Task<(string Token, int Id)> RegisterCustomerAsync()
    {
        var response = await SendAsync(HttpMethod.Post, "/auth/register",
            body: new { name = "Test Customer", email = UniqueEmail(), password = "password-123" });
        response.EnsureSuccessStatusCode();
        var json = await JsonAsync(response);
        return (json.GetProperty("token").GetString()!, json.GetProperty("user").GetProperty("id").GetInt32());
    }

    protected async Task<int> CreateTicketAsync(string token, string category, string? title = null, string priority = "medium")
    {
        var response = await SendAsync(HttpMethod.Post, "/tickets", token, new
        {
            title = title ?? Unique("Test ticket"),
            description = "Steps to reproduce the problem are below.",
            category,
            priority,
        });
        response.EnsureSuccessStatusCode();
        return (await JsonAsync(response)).GetProperty("id").GetInt32();
    }
}
