using Api.Tickets;

namespace Api.Tests.Unit;

public class TicketRulesTests
{
    [Theory]
    [InlineData("open", "in_progress")]
    [InlineData("open", "waiting")]
    [InlineData("open", "resolved")]
    [InlineData("in_progress", "open")]
    [InlineData("in_progress", "resolved")]
    [InlineData("waiting", "in_progress")]
    [InlineData("resolved", "closed")]
    [InlineData("resolved", "open")]
    [InlineData("closed", "open")]
    public void Allowed_transitions_are_accepted(string from, string to)
    {
        Assert.True(TicketRules.CanTransition(from, to));
    }

    [Theory]
    [InlineData("closed", "in_progress")]
    [InlineData("closed", "waiting")]
    [InlineData("closed", "resolved")]
    [InlineData("open", "closed")]
    [InlineData("in_progress", "closed")]
    [InlineData("waiting", "closed")]
    public void Invalid_transitions_are_rejected(string from, string to)
    {
        Assert.False(TicketRules.CanTransition(from, to));
    }

    [Fact]
    public void Setting_the_same_status_is_not_a_transition()
    {
        Assert.False(TicketRules.CanTransition("open", "open"));
    }

    [Fact]
    public void Closed_tickets_can_only_be_reopened_to_open()
    {
        Assert.Equal(["open"], TicketRules.AllowedTargets("closed"));
    }

    [Theory]
    [InlineData("open", true)]
    [InlineData("in_progress", true)]
    [InlineData("waiting", true)]
    [InlineData("resolved", false)]
    [InlineData("closed", false)]
    public void Unresolved_statuses_are_open_work(string status, bool expected)
    {
        Assert.Equal(expected, TicketRules.IsUnresolved(status));
    }

    [Fact]
    public void Resolving_sets_resolved_at_to_now()
    {
        var now = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(now, TicketRules.ResolvedAtAfter("resolved", null, now));
    }

    [Fact]
    public void Closing_keeps_the_original_resolution_time()
    {
        var resolved = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(resolved, TicketRules.ResolvedAtAfter("closed", resolved, now));
    }

    [Theory]
    [InlineData("open")]
    [InlineData("in_progress")]
    [InlineData("waiting")]
    public void Going_back_to_work_clears_resolved_at(string to)
    {
        var now = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        Assert.Null(TicketRules.ResolvedAtAfter(to, now, now));
    }

    [Fact]
    public void New_ticket_validation_reports_each_bad_field()
    {
        var errors = TicketRules.ValidateNew("  ", "", "", "critical");

        Assert.Contains("title", errors.Keys);
        Assert.Contains("description", errors.Keys);
        Assert.Contains("category", errors.Keys);
        Assert.Contains("priority", errors.Keys);
    }

    [Fact]
    public void Valid_new_ticket_has_no_errors()
    {
        Assert.Empty(TicketRules.ValidateNew("Cannot log in", "Reset link fails", "Login & access", "high"));
    }

    [Fact]
    public void Title_length_is_capped()
    {
        Assert.Contains("title", TicketRules.ValidateNew(new string('x', 141), "d", "c", null).Keys);
    }
}
