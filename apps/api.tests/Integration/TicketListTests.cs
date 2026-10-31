using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Integration;

public class TicketListTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Filters_narrow_the_list_by_category_and_priority()
    {
        var customer = await Actors.RegisterCustomerAsync(factory);
        var agent = await Actors.DemoAgentAsync(factory);
        var category = Tickets.UniqueCategory();
        await Tickets.CreateAsync(customer, category, priority: "urgent");
        await Tickets.CreateAsync(customer, category, priority: "low");
        await Tickets.CreateAsync(customer, category, priority: "low");

        var page = await Tickets.ListAsync(agent, $"category={category}&priority=low");

        Assert.Equal(2, page!.Total);
        Assert.All(page.Items, t => Assert.Equal("low", t.Priority));
    }

    [Fact]
    public async Task Filters_narrow_the_list_by_status()
    {
        var customer = await Actors.RegisterCustomerAsync(factory);
        var agent = await Actors.DemoAgentAsync(factory);
        var category = Tickets.UniqueCategory();
        var ticket = await Tickets.CreateAsync(customer, category);
        await Tickets.PatchAsync(agent, ticket.Id, new { status = "waiting" });
        await Tickets.CreateAsync(customer, category);

        var page = await Tickets.ListAsync(agent, $"category={category}&status=waiting");

        Assert.Equal(1, page!.Total);
        Assert.Equal(ticket.Id, page.Items[0].Id);
    }

    [Fact]
    public async Task Search_matches_title_and_description()
    {
        var customer = await Actors.RegisterCustomerAsync(factory);
        var agent = await Actors.DemoAgentAsync(factory);
        var category = Tickets.UniqueCategory();
        var token = $"zebra{Guid.NewGuid():N}";
        await Tickets.CreateAsync(customer, category, title: $"Printer {token}");
        await Tickets.CreateAsync(customer, category, description: $"The invoice mentions {token} somewhere");
        await Tickets.CreateAsync(customer, category, title: "Unrelated");

        var page = await Tickets.ListAsync(agent, $"category={category}&q={token}");

        Assert.Equal(2, page!.Total);
    }

    [Fact]
    public async Task Pagination_returns_each_page_with_the_full_total()
    {
        var customer = await Actors.RegisterCustomerAsync(factory);
        var agent = await Actors.DemoAgentAsync(factory);
        var category = Tickets.UniqueCategory();
        for (var i = 0; i < 5; i++) await Tickets.CreateAsync(customer, category, title: $"Ticket {i}");

        var first = await Tickets.ListAsync(agent, $"category={category}&pageSize=2&page=1");
        var last = await Tickets.ListAsync(agent, $"category={category}&pageSize=2&page=3");

        Assert.Equal(5, first!.Total);
        Assert.Equal(2, first.Items.Count);
        Assert.Single(last!.Items);
        Assert.Empty(first.Items.Select(t => t.Id).Intersect(last.Items.Select(t => t.Id)));
    }

    [Fact]
    public async Task Priority_sort_puts_urgent_tickets_first()
    {
        var customer = await Actors.RegisterCustomerAsync(factory);
        var agent = await Actors.DemoAgentAsync(factory);
        var category = Tickets.UniqueCategory();
        await Tickets.CreateAsync(customer, category, priority: "low");
        await Tickets.CreateAsync(customer, category, priority: "urgent");
        await Tickets.CreateAsync(customer, category, priority: "high");

        var page = await Tickets.ListAsync(agent, $"category={category}&sort=priority");

        Assert.Equal(["urgent", "high", "low"], page!.Items.Select(t => t.Priority));
    }

    [Fact]
    public async Task Unknown_filters_are_rejected_with_400()
    {
        var agent = await Actors.DemoAgentAsync(factory);

        var response = await agent.Http.GetAsync("/tickets?status=archived&priority=critical");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Demo_data_is_seeded_with_about_45_tickets()
    {
        var agent = await Actors.DemoAgentAsync(factory);

        var page = await Tickets.ListAsync(agent, "pageSize=1");

        Assert.True(page!.Total >= 45, $"expected at least 45 seeded tickets, found {page.Total}");
    }
}
