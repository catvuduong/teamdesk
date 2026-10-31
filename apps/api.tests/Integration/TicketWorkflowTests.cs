using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Integration;

public class TicketWorkflowTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task An_agent_moves_a_ticket_through_the_lifecycle()
    {
        var customer = await Actors.RegisterCustomerAsync(factory);
        var agent = await Actors.DemoAgentAsync(factory);
        var ticket = await Tickets.CreateAsync(customer, Tickets.UniqueCategory());

        var inProgress = await PatchStatusAsync(agent, ticket.Id, "in_progress");
        Assert.Equal("in_progress", inProgress.Status);

        var resolved = await PatchStatusAsync(agent, ticket.Id, "resolved");
        Assert.Equal("resolved", resolved.Status);
        Assert.NotNull(resolved.ResolvedAt);

        var closed = await PatchStatusAsync(agent, ticket.Id, "closed");
        Assert.Equal("closed", closed.Status);
        Assert.Equal(resolved.ResolvedAt, closed.ResolvedAt);

        var reopened = await PatchStatusAsync(agent, ticket.Id, "open");
        Assert.Equal("open", reopened.Status);
        Assert.Null(reopened.ResolvedAt);
    }

    [Fact]
    public async Task Invalid_transitions_are_rejected_with_400()
    {
        var customer = await Actors.RegisterCustomerAsync(factory);
        var agent = await Actors.DemoAgentAsync(factory);
        var ticket = await Tickets.CreateAsync(customer, Tickets.UniqueCategory());

        // Open cannot jump straight to closed
        var skip = await Tickets.PatchAsync(agent, ticket.Id, new { status = "closed" });
        Assert.Equal(HttpStatusCode.BadRequest, skip.StatusCode);

        // A closed ticket can only be reopened to open
        await PatchStatusAsync(agent, ticket.Id, "resolved");
        await PatchStatusAsync(agent, ticket.Id, "closed");
        var closedToWork = await Tickets.PatchAsync(agent, ticket.Id, new { status = "in_progress" });
        Assert.Equal(HttpStatusCode.BadRequest, closedToWork.StatusCode);
    }

    [Fact]
    public async Task Unknown_status_and_priority_values_are_rejected_with_400()
    {
        var customer = await Actors.RegisterCustomerAsync(factory);
        var agent = await Actors.DemoAgentAsync(factory);
        var ticket = await Tickets.CreateAsync(customer, Tickets.UniqueCategory());

        Assert.Equal(HttpStatusCode.BadRequest, (await Tickets.PatchAsync(agent, ticket.Id, new { status = "archived" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Tickets.PatchAsync(agent, ticket.Id, new { priority = "critical" })).StatusCode);
    }

    [Fact]
    public async Task Every_change_is_written_to_the_event_trail()
    {
        var customer = await Actors.RegisterCustomerAsync(factory);
        var agent = await Actors.DemoAgentAsync(factory);
        var ticket = await Tickets.CreateAsync(customer, Tickets.UniqueCategory(), priority: "medium");

        var response = await Tickets.PatchAsync(agent, ticket.Id, new
        {
            status = "in_progress",
            priority = "high",
            assigneeId = agent.User.Id,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await Tickets.GetAsync(agent, ticket.Id);
        var types = detail.Events.Select(e => e.Type).ToList();
        Assert.Equal(["created", "status_changed", "priority_changed", "assignee_changed"], types);
        Assert.Equal("open → in_progress", detail.Events[1].Detail);
        Assert.Equal("medium → high", detail.Events[2].Detail);
        Assert.Equal(agent.User.Id, detail.Events[1].Actor.Id);
        Assert.Equal("in_progress", detail.Status);
        Assert.Equal(agent.User.Id, detail.Assignee!.Id);
    }

    [Fact]
    public async Task Assignees_must_be_staff()
    {
        var customer = await Actors.RegisterCustomerAsync(factory);
        var agent = await Actors.DemoAgentAsync(factory);
        var ticket = await Tickets.CreateAsync(customer, Tickets.UniqueCategory());

        var response = await Tickets.PatchAsync(agent, ticket.Id, new { assigneeId = customer.User.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tickets_are_not_found()
    {
        var agent = await Actors.DemoAgentAsync(factory);

        Assert.Equal(HttpStatusCode.NotFound, (await agent.Http.GetAsync("/tickets/2147000000")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Tickets.PatchAsync(agent, 2147000000, new { status = "in_progress" })).StatusCode);
    }

    [Fact]
    public async Task Invalid_new_tickets_are_rejected_with_field_errors()
    {
        var customer = await Actors.RegisterCustomerAsync(factory);

        var response = await customer.Http.PostAsJsonAsync("/tickets", new { title = "", description = "", category = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Contains("title", body!["errors"].ToString());
        Assert.Contains("category", body["errors"].ToString());
    }

    private static async Task<TicketDetailDto> PatchStatusAsync(Actor agent, int id, string status)
    {
        var response = await Tickets.PatchAsync(agent, id, new { status });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TicketDetailDto>())!;
    }
}
