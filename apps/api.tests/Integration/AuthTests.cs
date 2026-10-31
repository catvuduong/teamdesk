using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Auth;
using Api.Seed;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Integration;

public class AuthTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Register_creates_a_customer_and_returns_a_token()
    {
        var email = Actors.UniqueEmail();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/register", new { name = "Ada Test", email, password = "long-enough-pass" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthDto>();
        Assert.False(string.IsNullOrEmpty(auth!.Token));
        Assert.Equal(Roles.Customer, auth.User.Role);
        Assert.Equal(email, auth.User.Email);
    }

    [Fact]
    public async Task Register_rejects_a_duplicate_email_with_409()
    {
        var email = Actors.UniqueEmail();
        using var client = factory.CreateClient();
        var body = new { name = "Ada", email, password = "long-enough-pass" };
        (await client.PostAsJsonAsync("/auth/register", body)).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/auth/register", body);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_reports_invalid_fields_with_400()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/register", new { name = "", email = "nope", password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ValidationBody>();
        Assert.Contains("name", body!.Errors.Keys);
        Assert.Contains("email", body.Errors.Keys);
        Assert.Contains("password", body.Errors.Keys);
    }

    [Fact]
    public async Task Login_with_the_wrong_password_is_unauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new { email = "agent@teamdesk.demo", password = "not-the-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_as_the_demo_agent_returns_an_agent_token()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new { email = "agent@teamdesk.demo", password = SeedData.DemoPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthDto>();
        Assert.Equal(Roles.Agent, auth!.User.Role);
    }

    [Fact]
    public async Task Me_returns_the_signed_in_user()
    {
        var customer = await Actors.RegisterCustomerAsync(factory);

        var me = await customer.Http.GetFromJsonAsync<UserDto>("/me");

        Assert.Equal(customer.User, me);
    }

    [Fact]
    public async Task Me_without_a_token_is_unauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_garbage_token_is_unauthorized()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.jwt");

        var response = await client.GetAsync("/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record ValidationBody(string Error, Dictionary<string, string> Errors);
}
