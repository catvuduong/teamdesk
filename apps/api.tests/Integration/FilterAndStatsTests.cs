using System.Net;
using Api.Seed;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Integration;

public class FilterAndStatsTests(WebApplicationFactory<Program> factory) : ApiTests(factory)
{
    [Fact]
    public async Task Pagination_reports_total_and_pages_through_results()
    {
        var customer = await RegisterCustomerAsync();
        var category = Unique("Paging");
        for (var i = 0; i < 5; i++) await CreateTicketAsync(customer.Token, category);
        var query = $"/tickets?category={Uri.EscapeDataString(category)}&pageSize=2";

        var first = await JsonAsync(await SendAsync(HttpMethod.Get, $"{query}&page=1", customer.Token));
        var last = await JsonAsync(await SendAsync(HttpMethod.Get, $"{query}&page=3", customer.Token));

        Assert.Equal(5, first.GetProperty("total").GetInt32());
        Assert.Equal(2, first.GetProperty("items").GetArrayLength());
        Assert.Equal(1, last.GetProperty("items").GetArrayLength());
        Assert.Equal(3, last.GetProperty("page").GetInt32());
    }

    [Fact]
    public async Task Status_and_priority_filters_narrow_the_list()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var category = Unique("Filters");
        var urgentId = await CreateTicketAsync(customer.Token, category, priority: "urgent");
        await CreateTicketAsync(customer.Token, category, priority: "low");
        await SendAsync(HttpMethod.Patch, $"/tickets/{urgentId}", agent, new { status = "in_progress" });
        var encoded = Uri.EscapeDataString(category);

        var inProgress = await JsonAsync(await SendAsync(HttpMethod.Get, $"/tickets?category={encoded}&status=in_progress", agent));
        var urgent = await JsonAsync(await SendAsync(HttpMethod.Get, $"/tickets?category={encoded}&priority=urgent", agent));

        Assert.Equal(1, inProgress.GetProperty("total").GetInt32());
        Assert.Equal(urgentId, inProgress.GetProperty("items")[0].GetProperty("id").GetInt32());
        Assert.Equal(1, urgent.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Search_matches_title_and_description()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var category = Unique("Search");
        var needle = Unique("zebra");
        var titled = await CreateTicketAsync(customer.Token, category, title: $"Printer {needle} jams");
        await CreateTicketAsync(customer.Token, category, title: "Unrelated");

        var result = await JsonAsync(await SendAsync(HttpMethod.Get, $"/tickets?category={Uri.EscapeDataString(category)}&q={Uri.EscapeDataString(needle)}", agent));

        Assert.Equal(1, result.GetProperty("total").GetInt32());
        Assert.Equal(titled, result.GetProperty("items")[0].GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Search_treats_percent_and_underscore_literally()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var category = Unique("Wildcards");
        await CreateTicketAsync(customer.Token, category, title: "Discount 100% off");
        await CreateTicketAsync(customer.Token, category, title: "Discount 1000 off");

        var result = await JsonAsync(await SendAsync(HttpMethod.Get, $"/tickets?category={Uri.EscapeDataString(category)}&q=100%25", agent));

        Assert.Equal(1, result.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Priority_sort_puts_urgent_first()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var category = Unique("Sorting");
        await CreateTicketAsync(customer.Token, category, priority: "low");
        await CreateTicketAsync(customer.Token, category, priority: "urgent");
        await CreateTicketAsync(customer.Token, category, priority: "high");

        var result = await JsonAsync(await SendAsync(HttpMethod.Get, $"/tickets?category={Uri.EscapeDataString(category)}&sort=priority", agent));

        var priorities = result.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("priority").GetString()).ToList();
        Assert.Equal(new[] { "urgent", "high", "low" }, priorities);
    }

    [Fact]
    public async Task Assignee_filter_finds_unassigned_and_mine()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var agentId = (await JsonAsync(await SendAsync(HttpMethod.Get, "/me", agent))).GetProperty("id").GetInt32();
        var category = Unique("Assignee");
        var mine = await CreateTicketAsync(customer.Token, category);
        await CreateTicketAsync(customer.Token, category);
        await SendAsync(HttpMethod.Patch, $"/tickets/{mine}", agent, new { assigneeId = agentId });
        var encoded = Uri.EscapeDataString(category);

        var unassigned = await JsonAsync(await SendAsync(HttpMethod.Get, $"/tickets?category={encoded}&assignee=unassigned", agent));
        var assignedToMe = await JsonAsync(await SendAsync(HttpMethod.Get, $"/tickets?category={encoded}&assignee={agentId}", agent));

        Assert.Equal(1, unassigned.GetProperty("total").GetInt32());
        Assert.Equal(1, assignedToMe.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Invalid_filters_are_400()
    {
        var agent = await AgentTokenAsync();

        var response = await SendAsync(HttpMethod.Get, "/tickets?status=archived", agent);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Comments_show_in_the_thread_with_the_author()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var ticketId = await CreateTicketAsync(customer.Token, "How-to");

        var posted = await SendAsync(HttpMethod.Post, $"/tickets/{ticketId}/comments", agent, new { body = "Try clearing the cache." });
        Assert.Equal(HttpStatusCode.Created, posted.StatusCode);

        var detail = await JsonAsync(await SendAsync(HttpMethod.Get, $"/tickets/{ticketId}", customer.Token));
        var comment = detail.GetProperty("comments")[0];
        Assert.Equal("Try clearing the cache.", comment.GetProperty("body").GetString());
        Assert.Equal("agent", comment.GetProperty("authorRole").GetString());
    }

    [Fact]
    public async Task Empty_comments_are_rejected()
    {
        var customer = await RegisterCustomerAsync();
        var ticketId = await CreateTicketAsync(customer.Token, "How-to");

        var response = await SendAsync(HttpMethod.Post, $"/tickets/{ticketId}/comments", customer.Token, new { body = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ticket_validates_and_returns_the_detail()
    {
        var customer = await RegisterCustomerAsync();

        var invalid = await SendAsync(HttpMethod.Post, "/tickets", customer.Token, new { title = "", description = "", category = "" });
        var created = await SendAsync(HttpMethod.Post, "/tickets", customer.Token, new { title = "Cannot export", description = "CSV is empty", category = "Bug report", priority = "high" });

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var detail = await JsonAsync(created);
        Assert.Equal("open", detail.GetProperty("status").GetString());
        Assert.Equal("created", detail.GetProperty("events")[0].GetProperty("type").GetString());
    }

    [Fact]
    public async Task Stats_report_dashboard_numbers_for_staff()
    {
        var agent = await AgentTokenAsync();

        var response = await SendAsync(HttpMethod.Get, "/stats", agent);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stats = await JsonAsync(response);
        Assert.Equal(14, stats.GetProperty("daily").GetArrayLength());
        var byStatus = stats.GetProperty("byStatus");
        foreach (var status in new[] { "open", "in_progress", "waiting", "resolved", "closed" })
            Assert.True(byStatus.TryGetProperty(status, out _));
        Assert.True(stats.GetProperty("open").GetInt32() >= 0);
        Assert.True(stats.GetProperty("topAgents").GetArrayLength() <= 5);
    }

    [Fact]
    public async Task Seed_is_loaded_with_the_demo_accounts()
    {
        var admin = await AdminTokenAsync();

        var users = await JsonAsync(await SendAsync(HttpMethod.Get, "/users?role=agent", admin));
        var customerLogin = await SendAsync(HttpMethod.Post, "/auth/login",
            body: new { email = "customer@teamdesk.demo", password = SeedData.DemoPassword });

        Assert.True(users.GetArrayLength() >= 3, "the seed creates three demo agents; other tests may add more");
        Assert.Equal(HttpStatusCode.OK, customerLogin.StatusCode);
    }
}
