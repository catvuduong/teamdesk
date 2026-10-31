using System.Net;
using Api.Stats;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Integration;

public class CsatEndpointTests(WebApplicationFactory<Program> factory) : ApiTests(factory)
{
    [Fact]
    public async Task Staff_see_the_average_distribution_and_weekly_trend()
    {
        var agent = await AgentTokenAsync();

        var stats = await JsonAsync(await SendAsync(HttpMethod.Get, "/stats/csat", agent));

        var distribution = stats.GetProperty("distribution");
        Assert.Equal(5, distribution.GetArrayLength());
        var counted = distribution.EnumerateArray().Sum(row => row.GetProperty("count").GetInt32());
        Assert.Equal(stats.GetProperty("count").GetInt32(), counted);
        Assert.Equal(Csat.TrendWeeks, stats.GetProperty("trend").GetArrayLength());
    }

    [Fact]
    public async Task A_new_rating_is_counted()
    {
        var agent = await AgentTokenAsync();
        var before = (await JsonAsync(await SendAsync(HttpMethod.Get, "/stats/csat", agent))).GetProperty("count").GetInt32();

        var customer = await RegisterCustomerAsync();
        var ticketId = await CreateTicketAsync(customer.Token, "How-to");
        (await SendAsync(HttpMethod.Patch, $"/tickets/{ticketId}", agent, new { status = "resolved" })).EnsureSuccessStatusCode();
        (await SendAsync(HttpMethod.Post, $"/tickets/{ticketId}/rating", customer.Token, new { score = 4 })).EnsureSuccessStatusCode();

        var after = (await JsonAsync(await SendAsync(HttpMethod.Get, "/stats/csat", agent))).GetProperty("count").GetInt32();
        Assert.True(after >= before + 1);
    }

    [Fact]
    public async Task Customers_cannot_read_the_satisfaction_stats()
    {
        var customer = await RegisterCustomerAsync();

        var response = await SendAsync(HttpMethod.Get, "/stats/csat", customer.Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
