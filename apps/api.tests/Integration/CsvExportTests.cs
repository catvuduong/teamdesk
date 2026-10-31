using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Integration;

public class CsvExportTests(WebApplicationFactory<Program> factory) : ApiTests(factory)
{
    [Fact]
    public async Task Customers_cannot_export()
    {
        var customer = await RegisterCustomerAsync();

        var response = await SendAsync(HttpMethod.Get, "/tickets/export.csv", customer.Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Export_is_a_csv_attachment_with_a_header_row()
    {
        var agent = await AgentTokenAsync();

        var response = await SendAsync(HttpMethod.Get, "/tickets/export.csv", agent);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("tickets.csv", response.Content.Headers.ContentDisposition?.FileName);
        Assert.StartsWith("Number,Title,Status,Priority,Category,Requester,Assignee,Created at,Updated at,Resolved at,SLA state", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Commas_quotes_and_line_breaks_in_titles_are_escaped()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var category = Unique("Escaping");
        const string title = "Printer, \"office\" jams\nsecond line";
        await CreateTicketAsync(customer.Token, category, title: title);

        var csv = await ExportAsync(agent, $"category={Uri.EscapeDataString(category)}");

        Assert.Contains("\"Printer, \"\"office\"\" jams\nsecond line\"", csv);
    }

    [Fact]
    public async Task Export_applies_the_same_filters_as_the_list()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var category = Unique("ExportFilter");
        var urgentTitle = Unique("Urgent export row");
        var lowTitle = Unique("Low export row");
        await CreateTicketAsync(customer.Token, category, title: urgentTitle, priority: "urgent");
        await CreateTicketAsync(customer.Token, category, title: lowTitle, priority: "low");

        var csv = await ExportAsync(agent, $"category={Uri.EscapeDataString(category)}&priority=urgent");

        Assert.Contains(urgentTitle, csv);
        Assert.DoesNotContain(lowTitle, csv);
    }

    [Fact]
    public async Task Export_with_an_sla_filter_leaves_out_tickets_in_other_states()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var category = Unique("ExportSla");
        var freshTitle = Unique("Fresh export row");
        await CreateTicketAsync(customer.Token, category, title: freshTitle, priority: "urgent");

        var csv = await ExportAsync(agent, $"category={Uri.EscapeDataString(category)}&sla=breached");

        Assert.DoesNotContain(freshTitle, csv);
    }

    [Fact]
    public async Task Export_rejects_an_unknown_filter()
    {
        var agent = await AgentTokenAsync();

        var response = await SendAsync(HttpMethod.Get, "/tickets/export.csv?status=archived", agent);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<string> ExportAsync(string token, string query)
    {
        var response = await SendAsync(HttpMethod.Get, $"/tickets/export.csv?{query}", token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
