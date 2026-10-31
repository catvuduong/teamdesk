namespace Api.Tickets;

/// <summary>Ticket priorities and their urgency order. Pure: unit-tested without a database.</summary>
public static class PriorityRules
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";
    public const string Urgent = "urgent";

    /// <summary>Lowest to highest urgency.</summary>
    public static readonly IReadOnlyList<string> All = [Low, Medium, High, Urgent];

    public static bool IsValid(string? priority) => priority is not null && All.Contains(priority);

    /// <summary>0 for low up to 3 for urgent. Higher means more urgent.</summary>
    public static int Rank(string priority) => priority switch
    {
        Low => 0,
        Medium => 1,
        High => 2,
        Urgent => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unknown priority"),
    };

    /// <summary>A SQL CASE expression that gives the same order as <see cref="Rank"/>, built from the same values.</summary>
    public static string SqlRank(string column) =>
        $"(CASE {column} " + string.Join(' ', All.Select(p => $"WHEN '{p}' THEN {Rank(p)}")) + " END)";
}
