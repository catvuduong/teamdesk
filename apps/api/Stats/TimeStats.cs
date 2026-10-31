namespace Api.Stats;

/// <summary>Average durations in hours, rounded to one decimal. Pure: unit-tested without a database.</summary>
public static class TimeStats
{
    /// <summary>Average of the spans in hours, or null when there are none. Reversed spans count as zero.</summary>
    public static double? AverageHours(IEnumerable<(DateTime From, DateTime To)> spans)
    {
        var hours = spans.Select(span => Math.Max(0, (span.To - span.From).TotalHours)).ToList();
        return hours.Count == 0 ? null : Math.Round(hours.Average(), 1);
    }

    /// <summary>The first reply at or after the ticket was created, or null when no reply exists.</summary>
    public static DateTime? FirstResponseAt(DateTime createdAt, IEnumerable<DateTime> replies) =>
        replies.Where(reply => reply >= createdAt).Cast<DateTime?>().Min();
}
