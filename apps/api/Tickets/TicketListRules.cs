namespace Api.Tickets;

/// <summary>Validation, sort orders and paging for the ticket list. Pure: unit-tested without a database.</summary>
public static class TicketListRules
{
    public const string Newest = "newest";
    public const string Updated = "updated";
    public const string ByPriority = "priority";
    public const string Unassigned = "unassigned";

    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static readonly IReadOnlyList<string> Sorts = [Newest, Updated, ByPriority];

    /// <summary>ORDER BY clause for a sort option. Priority puts urgent first, then the newest ticket.</summary>
    public static string OrderBy(string sort) => sort switch
    {
        Updated => "t.updated_at DESC, t.id DESC",
        ByPriority => $"{PriorityRules.SqlRank("t.priority")} DESC, t.created_at DESC, t.id DESC",
        _ => "t.created_at DESC, t.id DESC",
    };

    public static (int Page, int PageSize) Paging(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize <= 0 ? DefaultPageSize : pageSize, 1, MaxPageSize));

    /// <summary>
    /// Validates the query string. <paramref name="assignee"/> is "unassigned" or a user id.
    /// Returns field errors (empty when valid).
    /// </summary>
    public static Dictionary<string, string> Validate(string? status, string? priority, string? assignee, string? sort)
    {
        var errors = new Dictionary<string, string>();
        if (status is not null && !TicketRules.IsKnownStatus(status)) errors["status"] = "Unknown status";
        if (priority is not null && !PriorityRules.IsValid(priority)) errors["priority"] = "Unknown priority";
        if (sort is not null && !Sorts.Contains(sort)) errors["sort"] = "Sort must be newest, updated or priority";
        if (assignee is not null && !IsValidAssignee(assignee)) errors["assignee"] = "Assignee must be a user id or 'unassigned'";
        return errors;
    }

    public static bool IsValidAssignee(string assignee) =>
        assignee == Unassigned || (int.TryParse(assignee, out var id) && id > 0);
}
