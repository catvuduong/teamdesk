using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Integration;

public class RoleAndTransitionTests(WebApplicationFactory<Program> factory) : ApiTests(factory)
{
    [Fact]
    public async Task Customer_cannot_read_another_customers_ticket()
    {
        var owner = await RegisterCustomerAsync();
        var stranger = await RegisterCustomerAsync();
        var ticketId = await CreateTicketAsync(owner.Token, "Billing");

        var response = await SendAsync(HttpMethod.Get, $"/tickets/{ticketId}", stranger.Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_comment_on_another_customers_ticket()
    {
        var owner = await RegisterCustomerAsync();
        var stranger = await RegisterCustomerAsync();
        var ticketId = await CreateTicketAsync(owner.Token, "Billing");

        var response = await SendAsync(HttpMethod.Post, $"/tickets/{ticketId}/comments", stranger.Token, new { body = "Hello?" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Customers_list_only_their_own_tickets()
    {
        var owner = await RegisterCustomerAsync();
        var stranger = await RegisterCustomerAsync();
        var category = Unique("Owner-only");
        var ticketId = await CreateTicketAsync(owner.Token, category);

        var ownList = await JsonAsync(await SendAsync(HttpMethod.Get, $"/tickets?category={Uri.EscapeDataString(category)}", owner.Token));
        var strangerList = await JsonAsync(await SendAsync(HttpMethod.Get, $"/tickets?category={Uri.EscapeDataString(category)}", stranger.Token));

        Assert.Equal(1, ownList.GetProperty("total").GetInt32());
        Assert.Equal(ticketId, ownList.GetProperty("items")[0].GetProperty("id").GetInt32());
        Assert.Equal(0, strangerList.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Internal_notes_are_hidden_from_customers_but_visible_to_agents()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var ticketId = await CreateTicketAsync(customer.Token, "Login & access");

        await SendAsync(HttpMethod.Post, $"/tickets/{ticketId}/comments", agent, new { body = "Public reply", @internal = false });
        await SendAsync(HttpMethod.Post, $"/tickets/{ticketId}/comments", agent, new { body = "SECRET internal note", @internal = true });

        var asCustomer = await JsonAsync(await SendAsync(HttpMethod.Get, $"/tickets/{ticketId}", customer.Token));
        var asAgent = await JsonAsync(await SendAsync(HttpMethod.Get, $"/tickets/{ticketId}", agent));

        var customerBodies = asCustomer.GetProperty("comments").EnumerateArray().Select(c => c.GetProperty("body").GetString()).ToList();
        Assert.Equal(["Public reply"], customerBodies);
        Assert.Contains("SECRET internal note", asAgent.GetProperty("comments").EnumerateArray().Select(c => c.GetProperty("body").GetString()));
    }

    [Fact]
    public async Task Customers_cannot_post_internal_notes()
    {
        var customer = await RegisterCustomerAsync();
        var ticketId = await CreateTicketAsync(customer.Token, "Billing");

        var response = await SendAsync(HttpMethod.Post, $"/tickets/{ticketId}/comments", customer.Token, new { body = "Sneaky", @internal = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Customers_cannot_change_tickets_list_users_or_stats()
    {
        var customer = await RegisterCustomerAsync();
        var ticketId = await CreateTicketAsync(customer.Token, "Billing");

        var patch = await SendAsync(HttpMethod.Patch, $"/tickets/{ticketId}", customer.Token, new { status = "resolved" });
        var users = await SendAsync(HttpMethod.Get, "/users", customer.Token);
        var stats = await SendAsync(HttpMethod.Get, "/stats", customer.Token);

        Assert.Equal(HttpStatusCode.Forbidden, patch.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, users.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, stats.StatusCode);
    }

    [Fact]
    public async Task Only_admins_can_change_roles()
    {
        var agent = await AgentTokenAsync();
        var customer = await RegisterCustomerAsync();
        var customerId = (await JsonAsync(await SendAsync(HttpMethod.Get, "/me", customer.Token))).GetProperty("id").GetInt32();

        var asAgent = await SendAsync(HttpMethod.Patch, $"/users/{customerId}", agent, new { role = "agent" });
        var asAdmin = await SendAsync(HttpMethod.Patch, $"/users/{customerId}", await AdminTokenAsync(), new { role = "agent" });

        Assert.Equal(HttpStatusCode.Forbidden, asAgent.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asAdmin.StatusCode);
        Assert.Equal("agent", (await JsonAsync(asAdmin)).GetProperty("role").GetString());
    }

    [Fact]
    public async Task Staff_can_list_users_and_admins_cannot_demote_themselves()
    {
        var agent = await AgentTokenAsync();
        var admin = await AdminTokenAsync();
        var adminId = (await JsonAsync(await SendAsync(HttpMethod.Get, "/me", admin))).GetProperty("id").GetInt32();

        var users = await SendAsync(HttpMethod.Get, "/users?role=agent", agent);
        var demote = await SendAsync(HttpMethod.Patch, $"/users/{adminId}", admin, new { role = "customer" });

        Assert.Equal(HttpStatusCode.OK, users.StatusCode);
        Assert.All((await JsonAsync(users)).EnumerateArray(), u => Assert.Equal("agent", u.GetProperty("role").GetString()));
        Assert.Equal(HttpStatusCode.BadRequest, demote.StatusCode);
    }

    [Fact]
    public async Task Valid_transitions_are_applied_and_reopening_a_closed_ticket_is_allowed()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var ticketId = await CreateTicketAsync(customer.Token, "Bug report");

        await Patch(agent, ticketId, new { status = "resolved" }, HttpStatusCode.OK);
        await Patch(agent, ticketId, new { status = "closed" }, HttpStatusCode.OK);
        var reopened = await Patch(agent, ticketId, new { status = "open" }, HttpStatusCode.OK);

        Assert.Equal("open", reopened.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, reopened.GetProperty("resolvedAt").ValueKind);
    }

    [Fact]
    public async Task Invalid_transitions_are_rejected_with_400()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var ticketId = await CreateTicketAsync(customer.Token, "Bug report");

        // open -> closed skips resolution
        var skip = await SendAsync(HttpMethod.Patch, $"/tickets/{ticketId}", agent, new { status = "closed" });
        Assert.Equal(HttpStatusCode.BadRequest, skip.StatusCode);
        Assert.Contains("Cannot change status", (await JsonAsync(skip)).GetProperty("error").GetString());

        await Patch(agent, ticketId, new { status = "resolved" }, HttpStatusCode.OK);
        await Patch(agent, ticketId, new { status = "closed" }, HttpStatusCode.OK);

        // closed tickets can only be reopened to open
        await Patch(agent, ticketId, new { status = "in_progress" }, HttpStatusCode.BadRequest);
        await Patch(agent, ticketId, new { status = "waiting" }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unknown_status_or_priority_is_a_validation_error()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var ticketId = await CreateTicketAsync(customer.Token, "Bug report");

        var response = await SendAsync(HttpMethod.Patch, $"/tickets/{ticketId}", agent, new { status = "done", priority = "critical" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await JsonAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("status", out _));
        Assert.True(errors.TryGetProperty("priority", out _));
    }

    [Fact]
    public async Task Assignee_must_be_staff()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var ticketId = await CreateTicketAsync(customer.Token, "Bug report");
        var customerId = (await JsonAsync(await SendAsync(HttpMethod.Get, "/me", customer.Token))).GetProperty("id").GetInt32();

        var response = await SendAsync(HttpMethod.Patch, $"/tickets/{ticketId}", agent, new { assigneeId = customerId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Every_change_writes_an_event_in_order()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var agentId = (await JsonAsync(await SendAsync(HttpMethod.Get, "/me", agent))).GetProperty("id").GetInt32();
        var ticketId = await CreateTicketAsync(customer.Token, "Bug report", priority: "low");

        await Patch(agent, ticketId, new { status = "in_progress", priority = "high", assigneeId = agentId }, HttpStatusCode.OK);
        var detail = await Patch(agent, ticketId, new { assigneeId = (int?)null }, HttpStatusCode.OK);

        var types = detail.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("type").GetString()).ToList();
        Assert.Equal(["created", "status_changed", "priority_changed", "assignee_changed", "assignee_changed"], types);

        var details = detail.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("detail").GetString()).ToList();
        Assert.Equal("open → in_progress", details[1]);
        Assert.Equal("low → high", details[2]);
        Assert.Equal("unassigned → Priya Shah", details[3]);
        Assert.Equal("Priya Shah → unassigned", details[4]);
    }

    [Fact]
    public async Task Patching_with_no_changes_writes_no_events()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var ticketId = await CreateTicketAsync(customer.Token, "Bug report");

        var detail = await Patch(agent, ticketId, new { status = "open" }, HttpStatusCode.OK);

        Assert.Single(detail.GetProperty("events").EnumerateArray());
    }

    [Fact]
    public async Task Missing_ticket_is_404_for_staff()
    {
        var agent = await AgentTokenAsync();

        var response = await SendAsync(HttpMethod.Patch, "/tickets/2000000000", agent, new { status = "open" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<JsonElement> Patch(string token, int ticketId, object body, HttpStatusCode expected)
    {
        var response = await SendAsync(HttpMethod.Patch, $"/tickets/{ticketId}", token, body);
        Assert.True(response.StatusCode == expected, $"Expected {expected} but got {response.StatusCode}");
        return await JsonAsync(response);
    }
}
