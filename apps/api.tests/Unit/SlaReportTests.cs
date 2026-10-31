using Api.Sla;

namespace Api.Tests.Unit;

public class SlaReportTests
{
    [Fact]
    public void Breach_rate_is_a_percentage_of_all_tickets_in_the_priority()
    {
        var tickets = new[]
        {
            ("urgent", SlaRules.Breached),
            ("urgent", SlaRules.OnTrack),
            ("urgent", SlaRules.OnTrack),
            ("urgent", SlaRules.AtRisk),
        };

        var urgent = SlaReport.ByPriority(tickets).Single(row => row.Priority == "urgent");

        Assert.Equal(4, urgent.Total);
        Assert.Equal(1, urgent.Breached);
        Assert.Equal(1, urgent.AtRisk);
        Assert.Equal(25.0, urgent.BreachRate);
    }

    [Fact]
    public void Rate_is_rounded_to_one_decimal()
    {
        var tickets = new[] { ("high", SlaRules.Breached), ("high", SlaRules.OnTrack), ("high", SlaRules.OnTrack) };

        var high = SlaReport.ByPriority(tickets).Single(row => row.Priority == "high");

        Assert.Equal(33.3, high.BreachRate);
    }

    [Fact]
    public void A_priority_with_no_tickets_has_no_rate_rather_than_zero()
    {
        var low = SlaReport.ByPriority([]).Single(row => row.Priority == "low");

        Assert.Equal(0, low.Total);
        Assert.Null(low.BreachRate);
    }

    [Fact]
    public void Rows_are_ordered_most_urgent_first_and_cover_every_priority()
    {
        var rows = SlaReport.ByPriority([]);

        Assert.Equal(["urgent", "high", "medium", "low"], rows.Select(row => row.Priority));
    }
}
