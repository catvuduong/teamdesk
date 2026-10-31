using Api.Auth;

namespace Api.Tickets;

/// <summary>
/// Ticket lifecycle rules: which status changes are allowed, when resolved_at is set, and who may see a ticket.
/// Pure: no I/O, so it is unit-tested directly.
/// </summary>
public static class TicketRules
{
    public const string Open = "open";
    public const string InProgress = "in_progress";
    public const string Waiting = "waiting";
    public const string Resolved = "resolved";
    public const string Closed = "closed";

    public static readonly IReadOnlyList<string> Statuses = [Open, InProgress, Waiting, Resolved, Closed];

    private static readonly IReadOnlyDictionary<string, string[]> Transitions = new Dictionary<string, string[]>
    {
        [Open] = [InProgress, Waiting, Resolved],
        [InProgress] = [Open, Waiting, Resolved],
        [Waiting] = [Open, InProgress, Resolved],
        [Resolved] = [Closed, Open, InProgress],
        // A closed ticket can only be reopened, and only to open
        [Closed] = [Open],
    };

    public static bool IsKnownStatus(string? status) => status is not null && Statuses.Contains(status);

    public static IReadOnlyList<string> AllowedTargets(string status) =>
        Transitions.TryGetValue(status, out var targets) ? targets : [];

    /// <summary>A change to a different status that the lifecycle allows. Setting the same status is not a transition.</summary>
    public static bool CanTransition(string from, string to) => from != to && AllowedTargets(from).Contains(to);

    /// <summary>Unresolved tickets still need work: open, in progress or waiting.</summary>
    public static bool IsUnresolved(string status) => status is Open or InProgress or Waiting;

    /// <summary>
    /// resolved_at after moving to <paramref name="to"/>: set on resolve, kept when closed,
    /// cleared when a ticket goes back to work.
    /// </summary>
    public static DateTime? ResolvedAtAfter(string to, DateTime? current, DateTime now) => to switch
    {
        Resolved => now,
        Closed => current ?? now,
        _ => null,
    };

    public static bool CanView(CurrentUser me, int requesterId) => me.IsStaff || me.Id == requesterId;

    /// <summary>Validates a new ticket from the create form. Returns field errors (empty when valid).</summary>
    public static Dictionary<string, string> ValidateNew(string? title, string? description, string? category, string? priority)
    {
        var errors = new Dictionary<string, string>();

        var trimmedTitle = title?.Trim() ?? "";
        if (trimmedTitle.Length == 0) errors["title"] = "Title is required";
        else if (trimmedTitle.Length < 3) errors["title"] = "Title must be at least 3 characters";
        else if (trimmedTitle.Length > 140) errors["title"] = "Title must be 140 characters or fewer";

        var trimmedDescription = description?.Trim() ?? "";
        if (trimmedDescription.Length == 0) errors["description"] = "Describe the problem so we can help";
        else if (trimmedDescription.Length > 5000) errors["description"] = "Description must be 5000 characters or fewer";

        var trimmedCategory = category?.Trim() ?? "";
        if (trimmedCategory.Length == 0) errors["category"] = "Choose a category";
        else if (trimmedCategory.Length > 60) errors["category"] = "Category must be 60 characters or fewer";

        if (priority is not null && !PriorityRules.IsValid(priority)) errors["priority"] = "Unknown priority";

        return errors;
    }
}
