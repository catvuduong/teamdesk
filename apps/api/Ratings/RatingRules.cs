using Api.Tickets;

namespace Api.Ratings;

/// <summary>
/// Customer satisfaction rules: what a rating may contain and when a ticket may be rated.
/// Pure: no I/O, so it is unit-tested directly.
/// </summary>
public static class RatingRules
{
    public const int MaxComment = 1000;

    /// <summary>Only a ticket that is resolved or closed can be rated. Anything still being worked on cannot.</summary>
    public static bool CanRate(string status) => status is TicketRules.Resolved or TicketRules.Closed;

    /// <summary>Field errors for a rating. Empty when valid.</summary>
    public static Dictionary<string, string> Validate(int? score, string? comment)
    {
        var errors = new Dictionary<string, string>();

        if (score is null) errors["score"] = "Score is required";
        else if (score is < 1 or > 5) errors["score"] = "Score must be between 1 and 5";

        if ((comment?.Trim().Length ?? 0) > MaxComment) errors["comment"] = "Comment must be 1000 characters or fewer";

        return errors;
    }
}
