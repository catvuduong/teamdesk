using System.Net;
using System.Net.Http.Json;
using Api.Seed;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Api.Tests.Integration;

public class LoginRateLimitTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Repeated_failed_logins_from_one_address_get_429_with_retry_after()
    {
        // A low limit on its own host, so the test does not depend on how many other tests have signed in
        var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimit:Login:PermitLimit", "3"));
        var client = limited.CreateClient();

        var responses = new List<HttpResponseMessage>();
        for (var attempt = 0; attempt < 6; attempt++)
        {
            responses.Add(await client.PostAsJsonAsync("/auth/login",
                new { email = "agent@teamdesk.demo", password = "wrong-password" }));
        }

        Assert.Equal(HttpStatusCode.Unauthorized, responses[0].StatusCode);
        var rejected = responses.FirstOrDefault(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        Assert.NotNull(rejected);
        Assert.True(int.Parse(rejected.Headers.GetValues("Retry-After").Single()) > 0);
    }

    [Fact]
    public async Task A_limited_login_is_refused_even_with_the_right_password_until_the_window_resets()
    {
        var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimit:Login:PermitLimit", "1"));
        var client = limited.CreateClient();
        await client.PostAsJsonAsync("/auth/login", new { email = "agent@teamdesk.demo", password = "wrong-password" });

        var response = await client.PostAsJsonAsync("/auth/login",
            new { email = "agent@teamdesk.demo", password = SeedData.DemoPassword });

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }
}
