using System.Globalization;

namespace Api.Stats;

public sealed record ScoreCount(int Score, int Count);

/// <summary>One Monday-to-Sunday week (UTC). <see cref="Average"/> is null when nothing was rated that week.</summary>
public sealed record CsatWeek(string WeekStart, int Count, double? Average);

public sealed record CsatStats(int Count, double? Average, IReadOnlyList<ScoreCount> Distribution, IReadOnlyList<CsatWeek> Trend);

/// <summary>
/// Customer satisfaction numbers: average, distribution and a weekly trend. Pure: unit-tested without a database.
/// </summary>
public static class Csat
{
    public const int TrendWeeks = 8;

    private const int MinScore = 1;
    private const int MaxScore = 5;

    /// <summary>Average score rounded to one decimal, or null when nothing was rated.</summary>
    public static double? Average(IEnumerable<int> scores)
    {
        var list = scores.ToList();
        return list.Count == 0 ? null : Math.Round(list.Average(), 1);
    }

    /// <summary>A count for every score from 1 to 5, including the ones nobody gave.</summary>
    public static IReadOnlyList<ScoreCount> Distribution(IEnumerable<int> scores)
    {
        var counts = scores.GroupBy(score => score).ToDictionary(group => group.Key, group => group.Count());
        return Enumerable.Range(MinScore, MaxScore - MinScore + 1)
            .Select(score => new ScoreCount(score, counts.GetValueOrDefault(score)))
            .ToList();
    }

    /// <summary>The Monday that starts the week containing <paramref name="day"/>.</summary>
    public static DateOnly WeekStart(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    /// <summary>
    /// One entry per week for the last <paramref name="weeks"/> weeks, ending with the week of <paramref name="today"/>.
    /// Weeks with no ratings are kept, with a null average, so the chart shows a gap rather than skipping a week.
    /// </summary>
    public static IReadOnlyList<CsatWeek> Trend(IEnumerable<(DateOnly Day, int Score)> ratings, DateOnly today, int weeks)
    {
        var byWeek = ratings
            .GroupBy(rating => WeekStart(rating.Day))
            .ToDictionary(group => group.Key, group => group.Select(rating => rating.Score).ToList());

        var first = WeekStart(today).AddDays(-7 * (weeks - 1));
        return Enumerable.Range(0, weeks)
            .Select(offset => first.AddDays(7 * offset))
            .Select(start =>
            {
                var scores = byWeek.GetValueOrDefault(start, []);
                return new CsatWeek(start.ToString(DayBuckets.Format, CultureInfo.InvariantCulture), scores.Count, Average(scores));
            })
            .ToList();
    }

    public static CsatStats Summarize(IEnumerable<(DateTime CreatedAt, int Score)> ratings, DateOnly today)
    {
        var rows = ratings.ToList();
        var scores = rows.Select(rating => rating.Score).ToList();
        var trend = Trend(rows.Select(rating => (DateOnly.FromDateTime(rating.CreatedAt), rating.Score)), today, TrendWeeks);

        return new CsatStats(scores.Count, Average(scores), Distribution(scores), trend);
    }
}
