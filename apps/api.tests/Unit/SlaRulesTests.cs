using Api.Sla;

namespace Api.Tests.Unit;

public class SlaRulesTests
{
    private static readonly DateTime Created = new(2026, 10, 31, 9, 0, 0, DateTimeKind.Utc);

    // Two-hour response and eight-hour resolution targets keep the arithmetic readable
    private static readonly SlaPolicy Policy = new(FirstResponseHours: 2, ResolutionHours: 8);

    private static SlaStatus Judge(DateTime now, DateTime? firstReply = null, DateTime? resolved = null) =>
        SlaRules.Evaluate(Created, firstReply, resolved, Policy, now);

    [Fact]
    public void Exactly_75_percent_of_the_response_time_is_still_on_track()
    {
        // 1.5 of 2 hours is 75%: the threshold is "over 75%"
        var status = Judge(Created.AddHours(1.5));

        Assert.Equal(SlaRules.OnTrack, status.State);
    }

    [Fact]
    public void Just_past_75_percent_is_at_risk()
    {
        var status = Judge(Created.AddHours(1.5).AddTicks(1));

        Assert.Equal(SlaRules.AtRisk, status.State);
        Assert.Equal(SlaRules.FirstResponse, status.NextDue);
        Assert.Equal(Created.AddHours(2), status.DueAt);
    }

    [Fact]
    public void Exactly_at_the_deadline_is_breached()
    {
        var status = Judge(Created.AddHours(2));

        Assert.Equal(SlaRules.Breached, status.State);
    }

    [Fact]
    public void One_tick_before_the_deadline_is_still_at_risk_not_breached()
    {
        var status = Judge(Created.AddHours(2).AddTicks(-1));

        Assert.Equal(SlaRules.AtRisk, status.State);
    }

    [Fact]
    public void A_reply_exactly_at_the_deadline_is_on_time()
    {
        var status = Judge(Created.AddHours(3), firstReply: Created.AddHours(2));

        Assert.Equal(SlaRules.OnTrack, status.State);
        Assert.Equal(SlaRules.Resolution, status.NextDue);
    }

    [Fact]
    public void A_reply_one_tick_after_the_deadline_is_a_breach_even_though_the_reply_is_in_the_past()
    {
        var status = Judge(Created.AddHours(3), firstReply: Created.AddHours(2).AddTicks(1));

        Assert.Equal(SlaRules.Breached, status.State);
    }

    [Fact]
    public void A_reply_stops_the_response_clock_so_an_old_ticket_is_not_breached_on_response()
    {
        var status = Judge(Created.AddHours(5), firstReply: Created.AddHours(1));

        Assert.Equal(SlaRules.OnTrack, status.State);
        Assert.Equal(SlaRules.Resolution, status.NextDue);
        Assert.Equal(Created.AddHours(8), status.DueAt);
    }

    [Fact]
    public void Resolution_clock_reaches_at_risk_independently_of_the_response_clock()
    {
        // Replied in time, so only the resolution clock runs: 7 of 8 hours is over 75%
        var status = Judge(Created.AddHours(7), firstReply: Created.AddMinutes(30));

        Assert.Equal(SlaRules.AtRisk, status.State);
    }

    [Fact]
    public void Ticket_takes_the_worse_of_its_two_clocks()
    {
        // Response clock is breached (no reply after 2h) while resolution is only at 25%
        var status = Judge(Created.AddHours(2));

        Assert.Equal(SlaRules.Breached, status.State);
    }

    [Fact]
    public void Resolved_within_the_target_is_on_track_and_has_nothing_left_to_count_down()
    {
        var status = Judge(Created.AddDays(30), firstReply: Created.AddHours(1), resolved: Created.AddHours(4));

        Assert.Equal(SlaRules.OnTrack, status.State);
        Assert.Null(status.NextDue);
        Assert.Null(status.DueAt);
    }

    [Fact]
    public void Resolved_exactly_at_the_resolution_deadline_is_on_track()
    {
        var status = Judge(Created.AddDays(30), firstReply: Created.AddHours(1), resolved: Created.AddHours(8));

        Assert.Equal(SlaRules.OnTrack, status.State);
    }

    [Fact]
    public void Resolved_after_the_resolution_deadline_stays_breached_however_long_ago()
    {
        var status = Judge(Created.AddDays(30), firstReply: Created.AddHours(1), resolved: Created.AddHours(8).AddMinutes(1));

        Assert.Equal(SlaRules.Breached, status.State);
    }

    [Fact]
    public void Resolved_without_any_reply_stops_the_response_clock_at_resolution()
    {
        // Resolved after the 2h response target with no reply: the response was late
        var late = Judge(Created.AddDays(1), resolved: Created.AddHours(3));
        var onTime = Judge(Created.AddDays(1), resolved: Created.AddHours(1));

        Assert.Equal(SlaRules.Breached, late.State);
        Assert.Equal(SlaRules.OnTrack, onTime.State);
    }

    [Fact]
    public void Resolved_tickets_are_never_at_risk()
    {
        var status = Judge(Created.AddHours(7), firstReply: Created.AddHours(1), resolved: Created.AddHours(7));

        Assert.NotEqual(SlaRules.AtRisk, status.State);
    }

    [Fact]
    public void Open_ticket_with_no_time_used_is_on_track_with_both_deadlines_pending()
    {
        var status = Judge(Created);

        Assert.Equal(SlaRules.OnTrack, status.State);
        Assert.Equal(SlaRules.FirstResponse, status.NextDue);
        Assert.Equal(Created.AddHours(2), status.DueAt);
    }

    [Fact]
    public void Zero_hour_targets_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SlaRules.Evaluate(Created, null, null, new SlaPolicy(0, 8), Created));
    }

    [Fact]
    public void Filter_states_are_only_at_risk_and_breached()
    {
        Assert.True(SlaRules.IsFilterState(SlaRules.AtRisk));
        Assert.True(SlaRules.IsFilterState(SlaRules.Breached));
        Assert.False(SlaRules.IsFilterState(SlaRules.OnTrack));
        Assert.False(SlaRules.IsFilterState(null));
    }
}
