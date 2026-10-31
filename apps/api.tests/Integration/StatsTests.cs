using System.Net.Http.Json;
using Api.Tickets;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Integration;

public class StatsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Stats_cover_every_status_priority_and_the_last_14_days()
    {
        var agent = await Actors.DemoAgentAsync(factory);

        var stats = await agent.Http.GetFromJsonAsync<StatsDto>("/stats");

        Assert.NotNull(stats);
        Assert.Equal(TicketRules.Statuses.Count, stats.ByStatus.Count);
        Assert.Equal(PriorityRules.All.Count, stats.ByPriority.Count);
        Assert.Equal(14, stats.Daily.Count);
        Assert.Equal(DateTime.UtcNow.Date.ToString("yyyy-MM-dd"), stats.Daily[^1].Date);
        Assert.Equal(stats.ByStatus.Where(s => TicketRules.IsUnresolved(s.Key)).Sum(s => s.Value), stats.Open);
    }
}
