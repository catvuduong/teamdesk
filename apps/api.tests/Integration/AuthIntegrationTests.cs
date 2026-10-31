using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Integration;

public class AuthIntegrationTests(WebApplicationFactory<Program> factory) : ApiTests(factory)
{
    [Fact]
    public async Task Register_creates_a_customer_and_returns_a_working_token()
    {
        var email = UniqueEmail();
        var response = await SendAsync(HttpMethod.Post, "/auth/register",
            body: new { name = "Ada Lovelace", email = $"  {email.ToUpperInvariant()} ", password = "password-123" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await JsonAsync(response);
        Assert.Equal("customer", json.GetProperty("user").GetProperty("role").GetString());
        Assert.Equal(email, json.GetProperty("user").GetProperty("email").GetString());

        var me = await SendAsync(HttpMethod.Get, "/me", json.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal("Ada Lovelace", (await JsonAsync(me)).GetProperty("name").GetString());
    }

    [Fact]
    public async Task Registering_an_existing_email_is_a_conflict()
    {
        var email = UniqueEmail();
        var body = new { name = "Twice", email, password = "password-123" };
        (await SendAsync(HttpMethod.Post, "/auth/register", body: body)).EnsureSuccessStatusCode();

        var second = await SendAsync(HttpMethod.Post, "/auth/register", body: body);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Invalid_registration_returns_field_errors()
    {
        var response = await SendAsync(HttpMethod.Post, "/auth/register",
            body: new { name = "", email = "nope", password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await JsonAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Wrong_password_is_unauthorized()
    {
        var response = await SendAsync(HttpMethod.Post, "/auth/login",
            body: new { email = "agent@teamdesk.demo", password = "not-the-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Demo_agent_can_sign_in_and_is_an_agent()
    {
        var token = await AgentTokenAsync();

        var me = await SendAsync(HttpMethod.Get, "/me", token);

        Assert.Equal("agent", (await JsonAsync(me)).GetProperty("role").GetString());
    }

    [Fact]
    public async Task Protected_endpoints_without_a_token_are_unauthorized()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, "/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, "/tickets")).StatusCode);
    }

    [Fact]
    public async Task Garbage_token_is_unauthorized()
    {
        var response = await SendAsync(HttpMethod.Get, "/me", "not.a.jwt");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
