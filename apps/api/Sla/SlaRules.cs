namespace Api.Sla;

/// <summary>Response and resolution targets for one priority, in whole hours.</summary>
public sealed record SlaPolicy(int FirstResponseHours, int ResolutionHours);

/// <summary>
/// A ticket's SLA state now. <see cref="NextDue"/> and <see cref="DueAt"/> name the open clock with the earliest
/// deadline, so the UI can count down to it; both are null when every clock has stopped.
/// </summary>
public sealed record SlaStatus(string State, string? NextDue, DateTime? DueAt);

/// <summary>
/// SLA state from a ticket's two clocks, first response and resolution.
/// A stopped clock (the first staff reply or the resolution happened) is judged against its deadline: on time or breached.
/// A running clock is on track, at risk once more than 75% of its time has passed, and breached once the deadline is reached.
/// The ticket shows the worst of its two clocks. Pure: unit-tested without a database.
/// </summary>
public static class SlaRules
{
    public const string OnTrack = "on_track";
    public const string AtRisk = "at_risk";
    public const string Breached = "breached";

    public const string FirstResponse = "first_response";
    public const string Resolution = "resolution";

    /// <summary>Fraction of a clock's time that must pass before it counts as at risk. Exactly 75% is still on track.</summary>
    public const int AtRiskPercent = 75;

    public static bool IsFilterState(string? state) => state is AtRisk or Breached;

    /// <param name="createdAt">When the ticket was created (UTC).</param>
    /// <param name="firstResponseAt">The first public staff reply at or after creation, or null.</param>
    /// <param name="resolvedAt">When the ticket was resolved, or null while it is unresolved.</param>
    /// <param name="policy">The targets for the ticket's priority.</param>
    /// <param name="now">The moment the state is judged at (UTC).</param>
    public static SlaStatus Evaluate(
        DateTime createdAt,
        DateTime? firstResponseAt,
        DateTime? resolvedAt,
        SlaPolicy policy,
        DateTime now)
    {
        if (policy.FirstResponseHours <= 0 || policy.ResolutionHours <= 0)
            throw new ArgumentOutOfRangeException(nameof(policy), policy, "SLA targets must be at least one hour");

        // Resolving a ticket also answers it, so a ticket resolved without a reply stops its response clock too
        var clocks = new[]
        {
            (Kind: FirstResponse, Due: createdAt.AddHours(policy.FirstResponseHours), DoneAt: firstResponseAt ?? resolvedAt),
            (Kind: Resolution, Due: createdAt.AddHours(policy.ResolutionHours), DoneAt: resolvedAt),
        };

        var state = clocks
            .Select(clock => ClockState(createdAt, clock.Due, clock.DoneAt, now))
            .MaxBy(Severity)!;

        var next = clocks.Where(clock => clock.DoneAt is null).OrderBy(clock => clock.Due).ToList();
        return next.Count == 0
            ? new SlaStatus(state, null, null)
            : new SlaStatus(state, next[0].Kind, next[0].Due);
    }

    private static string ClockState(DateTime start, DateTime due, DateTime? doneAt, DateTime now)
    {
        if (doneAt is { } done) return done <= due ? OnTrack : Breached;
        if (now >= due) return Breached;

        // Integer arithmetic keeps "exactly 75%" exact; Int128 avoids overflow on very long targets
        var elapsed = (Int128)(now - start).Ticks;
        var target = (Int128)(due - start).Ticks;
        return elapsed * 100 > target * AtRiskPercent ? AtRisk : OnTrack;
    }

    private static int Severity(string state) => state switch
    {
        OnTrack => 0,
        AtRisk => 1,
        Breached => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown SLA state"),
    };
}
