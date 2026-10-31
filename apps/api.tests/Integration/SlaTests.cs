using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Integration;

public class SlaTests(WebApplicationFactory<Program> factory) : ApiTests(factory)
{
    [Fact]
    public async Task A_new_ticket_is_on_track_with_its_first_response_deadline_ahead()
    {
        var customer = await RegisterCustomerAsync();
        var ticketId = await CreateTicketAsync(customer.Token, "How-to", priority: "urgent");

        var detail = await JsonAsync(await SendAsync(HttpMethod.Get, $"/tickets/{ticketId}", customer.Token));

        var sla = detail.GetProperty("sla");
        Assert.Equal("on_track", sla.GetProperty("state").GetString());
        Assert.Equal("first_response", sla.GetProperty("nextDue").GetString());
        Assert.True(sla.GetProperty("dueAt").GetDateTime() > DateTime.UtcNow);
    }

    [Fact]
    public async Task A_staff_reply_moves_the_countdown_to_resolution()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var ticketId = await CreateTicketAsync(customer.Token, Unique("Reply"), priority: "urgent");

        await SendAsync(HttpMethod.Post, $"/tickets/{ticketId}/comments", agent, new { body = "Looking into it." });

        var sla = (await JsonAsync(await SendAsync(HttpMethod.Get, $"/tickets/{ticketId}", agent))).GetProperty("sla");
        Assert.Equal("resolution", sla.GetProperty("nextDue").GetString());
        Assert.Equal("on_track", sla.GetProperty("state").GetString());
    }

    [Fact]
    public async Task A_resolved_ticket_has_no_countdown_left()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var ticketId = await CreateTicketAsync(customer.Token, Unique("Resolved"));

        await Patch(agent, ticketId, new { status = "resolved" });

        var sla = (await JsonAsync(await SendAsync(HttpMethod.Get, $"/tickets/{ticketId}", agent))).GetProperty("sla");
        Assert.Equal("on_track", sla.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, sla.GetProperty("nextDue").ValueKind);
        Assert.Equal(JsonValueKind.Null, sla.GetProperty("dueAt").ValueKind);
    }

    [Fact]
    public async Task Fresh_tickets_are_never_at_risk_or_breached()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var category = Unique("Fresh");
        await CreateTicketAsync(customer.Token, category, priority: "urgent");
        await CreateTicketAsync(customer.Token, category, priority: "low");
        var encoded = Uri.EscapeDataString(category);

        var atRisk = await JsonAsync(await SendAsync(HttpMethod.Get, $"/tickets?category={encoded}&sla=at_risk", agent));
        var breached = await JsonAsync(await SendAsync(HttpMethod.Get, $"/tickets?category={encoded}&sla=breached", agent));

        Assert.Equal(0, atRisk.GetProperty("total").GetInt32());
        Assert.Equal(0, breached.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task The_sla_filter_returns_only_tickets_in_that_state()
    {
        var agent = await AgentTokenAsync();

        // Ticket states move with the clock, so check the filter's contract rather than a fixed set of tickets
        foreach (var state in new[] { "at_risk", "breached" })
        {
            var page = await JsonAsync(await SendAsync(HttpMethod.Get, $"/tickets?sla={state}&pageSize=100", agent));
            foreach (var item in page.GetProperty("items").EnumerateArray())
                Assert.Equal(state, item.GetProperty("sla").GetProperty("state").GetString());
        }
    }

    [Fact]
    public async Task An_unknown_sla_filter_is_rejected()
    {
        var agent = await AgentTokenAsync();

        var response = await SendAsync(HttpMethod.Get, "/tickets?sla=overdue", agent);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_breach_report_lists_every_priority_most_urgent_first()
    {
        var agent = await AgentTokenAsync();

        var response = await SendAsync(HttpMethod.Get, "/stats/sla", agent);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = (await JsonAsync(response)).GetProperty("byPriority").EnumerateArray().ToList();
        Assert.Equal(["urgent", "high", "medium", "low"], rows.Select(row => row.GetProperty("priority").GetString()));
        foreach (var row in rows)
        {
            var total = row.GetProperty("total").GetInt32();
            var breached = row.GetProperty("breached").GetInt32();
            Assert.True(breached <= total);
            if (total == 0) Assert.Equal(JsonValueKind.Null, row.GetProperty("breachRate").ValueKind);
            else Assert.Equal(Math.Round(100.0 * breached / total, 1), row.GetProperty("breachRate").GetDouble());
        }
    }

    [Fact]
    public async Task The_breach_report_is_for_staff_only()
    {
        var customer = await RegisterCustomerAsync();

        var response = await SendAsync(HttpMethod.Get, "/stats/sla", customer.Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private Task<HttpResponseMessage> Patch(string token, int id, object body) =>
        SendAsync(HttpMethod.Patch, $"/tickets/{id}", token, body);
}
