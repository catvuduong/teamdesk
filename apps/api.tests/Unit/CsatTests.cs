using Api.Stats;

namespace Api.Tests.Unit;

public class CsatTests
{
    private static readonly DateOnly Wednesday = new(2026, 11, 4);

    [Fact]
    public void Average_is_rounded_to_one_decimal()
    {
        Assert.Equal(4.3, Csat.Average([5, 4, 4]));
    }

    [Fact]
    public void No_ratings_gives_no_average_rather_than_zero()
    {
        Assert.Null(Csat.Average([]));
    }

    [Fact]
    public void Distribution_lists_every_score_even_when_nobody_gave_it()
    {
        var distribution = Csat.Distribution([5, 5, 1]);

        Assert.Equal([1, 2, 3, 4, 5], distribution.Select(row => row.Score));
        Assert.Equal([1, 0, 0, 0, 2], distribution.Select(row => row.Count));
    }

    [Fact]
    public void Weeks_start_on_monday_so_a_sunday_belongs_to_the_week_before()
    {
        Assert.Equal(new DateOnly(2026, 10, 26), Csat.WeekStart(new DateOnly(2026, 11, 1)));
        Assert.Equal(new DateOnly(2026, 11, 2), Csat.WeekStart(new DateOnly(2026, 11, 2)));
    }

    [Fact]
    public void Trend_has_one_entry_per_week_ending_with_the_current_week()
    {
        var trend = Csat.Trend([], Wednesday, Csat.TrendWeeks);

        Assert.Equal(Csat.TrendWeeks, trend.Count);
        Assert.Equal("2026-09-14", trend[0].WeekStart);
        Assert.Equal("2026-11-02", trend[^1].WeekStart);
    }

    [Fact]
    public void Weeks_without_ratings_are_kept_with_no_average()
    {
        var trend = Csat.Trend([(new DateOnly(2026, 11, 3), 4)], Wednesday, Csat.TrendWeeks);

        var current = trend[^1];
        Assert.Equal(1, current.Count);
        Assert.Equal(4.0, current.Average);
        Assert.All(trend.SkipLast(1), week =>
        {
            Assert.Equal(0, week.Count);
            Assert.Null(week.Average);
        });
    }

    [Fact]
    public void Weekly_average_covers_the_ratings_made_in_that_week()
    {
        var ratings = new[] { (new DateOnly(2026, 11, 2), 5), (new DateOnly(2026, 11, 4), 4) };

        var current = Csat.Trend(ratings, Wednesday, Csat.TrendWeeks)[^1];

        Assert.Equal(2, current.Count);
        Assert.Equal(4.5, current.Average);
    }

    [Fact]
    public void Ratings_older_than_the_trend_window_are_left_out_of_it()
    {
        var trend = Csat.Trend([(new DateOnly(2026, 8, 1), 1)], Wednesday, Csat.TrendWeeks);

        Assert.All(trend, week => Assert.Equal(0, week.Count));
    }

    [Fact]
    public void Summary_combines_count_average_distribution_and_trend()
    {
        var ratings = new[]
        {
            (new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc), 5),
            (new DateTime(2026, 11, 3, 18, 30, 0, DateTimeKind.Utc), 4),
            (new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc), 2),
        };

        var stats = Csat.Summarize(ratings, Wednesday);

        Assert.Equal(3, stats.Count);
        Assert.Equal(3.7, stats.Average);
        Assert.Equal([0, 1, 0, 1, 1], stats.Distribution.Select(row => row.Count));
        Assert.Equal(Csat.TrendWeeks, stats.Trend.Count);
        Assert.Equal(3, stats.Trend.Sum(week => week.Count));
    }

    [Fact]
    public void Summary_without_ratings_has_no_average_and_empty_weeks()
    {
        var stats = Csat.Summarize([], Wednesday);

        Assert.Equal(0, stats.Count);
        Assert.Null(stats.Average);
        Assert.All(stats.Distribution, row => Assert.Equal(0, row.Count));
        Assert.All(stats.Trend, week => Assert.Null(week.Average));
    }
}
