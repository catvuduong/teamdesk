using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Integration;

public class VisibilityTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task A_customer_cannot_read_another_customers_ticket()
    {
        var owner = await Actors.RegisterCustomerAsync(factory);
        var stranger = await Actors.RegisterCustomerAsync(factory);
        var ticket = await Tickets.CreateAsync(owner, Tickets.UniqueCategory());

        var response = await stranger.Http.GetAsync($"/tickets/{ticket.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_customer_cannot_comment_on_another_customers_ticket()
    {
        var owner = await Actors.RegisterCustomerAsync(factory);
        var stranger = await Actors.RegisterCustomerAsync(factory);
        var ticket = await Tickets.CreateAsync(owner, Tickets.UniqueCategory());

        var response = await stranger.Http.PostAsJsonAsync($"/tickets/{ticket.Id}/comments", new { body = "hello", @internal = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Customers_never_see_internal_comments_but_agents_do()
    {
        var customer = await Actors.RegisterCustomerAsync(factory);
        var agent = await Actors.DemoAgentAsync(factory);
        var ticket = await Tickets.CreateAsync(customer, Tickets.UniqueCategory());

        await customer.Http.PostAsJsonAsync($"/tickets/{ticket.Id}/comments", new { body = "Public question", @internal = false });
        await agent.Http.PostAsJsonAsync($"/tickets/{ticket.Id}/comments", new { body = "Secret internal note", @internal = true });

        var asCustomer = await Tickets.GetAsync(customer, ticket.Id);
        var asAgent = await Tickets.GetAsync(agent, ticket.Id);

        Assert.Equal(["Public question"], asCustomer.Comments.Select(c => c.Body));
        Assert.Contains(asAgent.Comments, c => c.Body == "Secret internal note" && c.Internal);
    }

    [Fact]
    public async Task Customers_cannot_post_internal_notes()
    {
        var customer = await Actors.RegisterCustomerAsync(factory);
        var ticket = await Tickets.CreateAsync(customer, Tickets.UniqueCategory());

        var response = await customer.Http.PostAsJsonAsync($"/tickets/{ticket.Id}/comments", new { body = "Sneaky", @internal = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Customers_cannot_change_a_ticket()
    {
        var customer = await Actors.RegisterCustomerAsync(factory);
        var ticket = await Tickets.CreateAsync(customer, Tickets.UniqueCategory());

        var response = await Tickets.PatchAsync(customer, ticket.Id, new { status = "resolved" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Customers_cannot_list_users_or_stats()
    {
        var customer = await Actors.RegisterCustomerAsync(factory);

        Assert.Equal(HttpStatusCode.Forbidden, (await customer.Http.GetAsync("/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.Http.GetAsync("/stats")).StatusCode);
    }

    [Fact]
    public async Task Agents_can_list_users()
    {
        var agent = await Actors.DemoAgentAsync(factory);

        var users = await agent.Http.GetFromJsonAsync<List<UserDto>>("/users");

        Assert.Contains(users!, u => u.Email == "agent@teamdesk.demo");
    }

    [Fact]
    public async Task Customers_only_list_their_own_tickets()
    {
        var customer = await Actors.RegisterCustomerAsync(factory);
        var other = await Actors.RegisterCustomerAsync(factory);
        var category = Tickets.UniqueCategory();
        await Tickets.CreateAsync(customer, category);
        await Tickets.CreateAsync(other, category);

        var page = await Tickets.ListAsync(customer, $"category={category}");

        Assert.Equal(1, page!.Total);
        Assert.All(page.Items, t => Assert.Equal(customer.User.Id, t.Requester.Id));
    }

    [Fact]
    public async Task Anonymous_requests_to_tickets_are_unauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
