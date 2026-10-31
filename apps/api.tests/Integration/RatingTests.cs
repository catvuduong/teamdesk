using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Integration;

public class RatingTests(WebApplicationFactory<Program> factory) : ApiTests(factory)
{
    [Fact]
    public async Task The_requester_can_rate_a_resolved_ticket()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var ticketId = await CreateTicketAsync(customer.Token, "How-to");
        await ResolveAsync(agent, ticketId);

        var response = await SendAsync(HttpMethod.Post, $"/tickets/{ticketId}/rating", customer.Token, new { score = 5, comment = " Great, thanks " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var detail = await JsonAsync(await SendAsync(HttpMethod.Get, $"/tickets/{ticketId}", customer.Token));
        Assert.Equal(5, detail.GetProperty("rating").GetProperty("score").GetInt32());
        Assert.Equal("Great, thanks", detail.GetProperty("rating").GetProperty("comment").GetString());
    }

    [Fact]
    public async Task A_closed_ticket_can_be_rated()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var ticketId = await CreateTicketAsync(customer.Token, "How-to");
        await ResolveAsync(agent, ticketId);
        await SendAsync(HttpMethod.Patch, $"/tickets/{ticketId}", agent, new { status = "closed" });

        var response = await SendAsync(HttpMethod.Post, $"/tickets/{ticketId}/rating", customer.Token, new { score = 3 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task An_agent_who_did_not_request_the_ticket_cannot_rate_it()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var ticketId = await CreateTicketAsync(customer.Token, "How-to");
        await ResolveAsync(agent, ticketId);

        var response = await SendAsync(HttpMethod.Post, $"/tickets/{ticketId}/rating", agent, new { score = 5 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Another_customer_cannot_rate_the_ticket()
    {
        var owner = await RegisterCustomerAsync();
        var stranger = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var ticketId = await CreateTicketAsync(owner.Token, "How-to");
        await ResolveAsync(agent, ticketId);

        var response = await SendAsync(HttpMethod.Post, $"/tickets/{ticketId}/rating", stranger.Token, new { score = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_ticket_that_is_still_being_worked_on_cannot_be_rated()
    {
        var customer = await RegisterCustomerAsync();
        var ticketId = await CreateTicketAsync(customer.Token, "How-to");

        var response = await SendAsync(HttpMethod.Post, $"/tickets/{ticketId}/rating", customer.Token, new { score = 4 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_ticket_can_only_be_rated_once()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var ticketId = await CreateTicketAsync(customer.Token, "How-to");
        await ResolveAsync(agent, ticketId);
        (await SendAsync(HttpMethod.Post, $"/tickets/{ticketId}/rating", customer.Token, new { score = 2 })).EnsureSuccessStatusCode();

        var second = await SendAsync(HttpMethod.Post, $"/tickets/{ticketId}/rating", customer.Token, new { score = 5 });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var detail = await JsonAsync(await SendAsync(HttpMethod.Get, $"/tickets/{ticketId}", customer.Token));
        Assert.Equal(2, detail.GetProperty("rating").GetProperty("score").GetInt32());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Scores_outside_one_to_five_are_rejected(int score)
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var ticketId = await CreateTicketAsync(customer.Token, "How-to");
        await ResolveAsync(agent, ticketId);

        var response = await SendAsync(HttpMethod.Post, $"/tickets/{ticketId}/rating", customer.Token, new { score });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rating_a_ticket_that_does_not_exist_is_not_found()
    {
        var customer = await RegisterCustomerAsync();

        var response = await SendAsync(HttpMethod.Post, "/tickets/999999999/rating", customer.Token, new { score = 5 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Rating_requires_a_signed_in_user()
    {
        var customer = await RegisterCustomerAsync();
        var ticketId = await CreateTicketAsync(customer.Token, "How-to");

        var response = await SendAsync(HttpMethod.Post, $"/tickets/{ticketId}/rating", null, new { score = 5 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task ResolveAsync(string agentToken, int ticketId)
    {
        var response = await SendAsync(HttpMethod.Patch, $"/tickets/{ticketId}", agentToken, new { status = "resolved" });
        response.EnsureSuccessStatusCode();
    }
}
