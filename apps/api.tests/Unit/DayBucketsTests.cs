using Api.Stats;

namespace Api.Tests.Unit;

public class DayBucketsTests
{
    [Fact]
    public void Returns_one_row_per_day_ending_on_the_end_day()
    {
        var days = DayBuckets.Build(new DateOnly(2026, 10, 31), 14, new Dictionary<string, int>(), new Dictionary<string, int>());

        Assert.Equal(14, days.Count);
        Assert.Equal("2026-10-18", days[0].Date);
        Assert.Equal("2026-10-31", days[^1].Date);
    }

    [Fact]
    public void Fills_in_counts_and_zeroes_for_quiet_days()
    {
        var created = new Dictionary<string, int> { ["2026-10-30"] = 3 };
        var resolved = new Dictionary<string, int> { ["2026-10-31"] = 1 };

        var days = DayBuckets.Build(new DateOnly(2026, 10, 31), 3, created, resolved);

        Assert.Equal([new DayCount("2026-10-29", 0, 0), new DayCount("2026-10-30", 3, 0), new DayCount("2026-10-31", 0, 1)], days);
    }

    [Fact]
    public void Window_crosses_month_boundaries()
    {
        var days = DayBuckets.Build(new DateOnly(2026, 3, 2), 4, new Dictionary<string, int>(), new Dictionary<string, int>());

        Assert.Equal(["2026-02-27", "2026-02-28", "2026-03-01", "2026-03-02"], days.Select(d => d.Date));
    }
}
