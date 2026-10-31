using System.Globalization;

namespace Api.Stats;

public sealed record DayCount(string Date, int Created, int Resolved);

/// <summary>Builds one row per UTC day for the trailing window, including days with no activity. Pure.</summary>
public static class DayBuckets
{
    public const string Format = "yyyy-MM-dd";

    /// <param name="endDay">The last day of the window (today, UTC).</param>
    /// <param name="days">Window length, e.g. 14.</param>
    /// <param name="created">Counts keyed by yyyy-MM-dd.</param>
    /// <param name="resolved">Counts keyed by yyyy-MM-dd.</param>
    public static IReadOnlyList<DayCount> Build(
        DateOnly endDay,
        int days,
        IReadOnlyDictionary<string, int> created,
        IReadOnlyDictionary<string, int> resolved) =>
        Enumerable.Range(0, days)
            .Select(offset => endDay.AddDays(offset - (days - 1)).ToString(Format, CultureInfo.InvariantCulture))
            .Select(day => new DayCount(day, created.GetValueOrDefault(day), resolved.GetValueOrDefault(day)))
            .ToList();
}
