using Api.Stats;

namespace Api.Tests.Unit;

public class TimeStatsTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Average_is_in_hours_rounded_to_one_decimal()
    {
        var spans = new[]
        {
            (Start, Start.AddHours(2)),
            (Start, Start.AddHours(3)),
            (Start, Start.AddMinutes(30)),
        };

        Assert.Equal(1.8, TimeStats.AverageHours(spans));
    }

    [Fact]
    public void No_spans_gives_null_not_zero()
    {
        Assert.Null(TimeStats.AverageHours([]));
    }

    [Fact]
    public void Reversed_spans_count_as_zero_hours()
    {
        var spans = new[] { (Start, Start.AddHours(-4)), (Start, Start.AddHours(4)) };

        Assert.Equal(2.0, TimeStats.AverageHours(spans));
    }

    [Fact]
    public void First_response_is_the_earliest_reply_after_creation()
    {
        var replies = new[]
        {
            Start.AddHours(-1),
            Start.AddHours(5),
            Start.AddHours(2),
        };

        Assert.Equal(Start.AddHours(2), TimeStats.FirstResponseAt(Start, replies));
    }

    [Fact]
    public void Ticket_without_a_reply_has_no_first_response()
    {
        Assert.Null(TimeStats.FirstResponseAt(Start, []));
    }
}
