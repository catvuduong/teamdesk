using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Seed;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Integration;

public sealed record UserDto(int Id, string Name, string Email, string Role);

public sealed record PersonDto(int Id, string Name);

public sealed record AuthDto(string Token, UserDto User);

public sealed record TicketItemDto(
    int Id,
    int Number,
    string Title,
    string Status,
    string Priority,
    string Category,
    PersonDto Requester,
    PersonDto? Assignee,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record TicketPageDto(IReadOnlyList<TicketItemDto> Items, int Total, int Page, int PageSize);

public sealed record CommentDto(int Id, string Body, bool Internal, PersonDto Author, string AuthorRole, DateTime CreatedAt);

public sealed record EventDto(int Id, string Type, string Detail, PersonDto Actor, DateTime CreatedAt);

public sealed record TicketDetailDto(
    int Id,
    int Number,
    string Title,
    string Description,
    string Status,
    string Priority,
    string Category,
    PersonDto Requester,
    PersonDto? Assignee,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ResolvedAt,
    IReadOnlyList<string> AllowedStatuses,
    IReadOnlyList<CommentDto> Comments,
    IReadOnlyList<EventDto> Events);

public sealed record DayDto(string Date, int Created, int Resolved);

public sealed record StatsDto(
    int Open,
    int Resolved,
    int ResolvedToday,
    Dictionary<string, int> ByStatus,
    Dictionary<string, int> ByPriority,
    IReadOnlyList<DayDto> Daily,
    double? AvgFirstResponseHours,
    double? AvgResolutionHours,
    IReadOnlyList<PersonDto> TopAgents);

/// <summary>An HTTP client signed in as one user, so each test reads as the role it plays.</summary>
public sealed record Actor(HttpClient Http, UserDto User);

public static class Actors
{
    public static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    public static async Task<Actor> RegisterCustomerAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/auth/register",
            new { name = "Test Customer", email = UniqueEmail(), password = "test-password-123" });
        response.EnsureSuccessStatusCode();
        return Authenticate(factory, (await response.Content.ReadFromJsonAsync<AuthDto>())!);
    }

    public static async Task<Actor> SignInAsync(WebApplicationFactory<Program> factory, string email)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/login", new { email, password = SeedData.DemoPassword });
        response.EnsureSuccessStatusCode();
        return Authenticate(factory, (await response.Content.ReadFromJsonAsync<AuthDto>())!);
    }

    public static Task<Actor> DemoAgentAsync(WebApplicationFactory<Program> factory) =>
        SignInAsync(factory, "agent@teamdesk.demo");

    public static Task<Actor> DemoAdminAsync(WebApplicationFactory<Program> factory) =>
        SignInAsync(factory, "admin@teamdesk.demo");

    private static Actor Authenticate(WebApplicationFactory<Program> factory, AuthDto auth)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return new Actor(client, auth.User);
    }
}

public static class Tickets
{
    public static string UniqueCategory() => $"test-{Guid.NewGuid():N}";

    public static async Task<TicketDetailDto> CreateAsync(
        Actor actor,
        string category,
        string title = "Cannot sign in",
        string description = "Details of the problem for the test.",
        string? priority = null)
    {
        var response = await actor.Http.PostAsJsonAsync(
            "/tickets",
            new { title, description, category, priority });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TicketDetailDto>())!;
    }

    public static Task<HttpResponseMessage> PatchAsync(Actor actor, int id, object body) =>
        actor.Http.PatchAsJsonAsync($"/tickets/{id}", body);

    public static Task<TicketPageDto?> ListAsync(Actor actor, string query) =>
        actor.Http.GetFromJsonAsync<TicketPageDto>($"/tickets?{query}");

    public static async Task<TicketDetailDto> GetAsync(Actor actor, int id)
    {
        var detail = await actor.Http.GetFromJsonAsync<TicketDetailDto>($"/tickets/{id}");
        return detail!;
    }
}
