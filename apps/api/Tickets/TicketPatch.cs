using System.Text.Json;

namespace Api.Tickets;

/// <summary>
/// The parsed PATCH body. <see cref="AssigneeProvided"/> separates "leave the assignee alone" (absent)
/// from "unassign" (null).
/// </summary>
public sealed record TicketPatch(
    string? Status,
    string? Priority,
    bool AssigneeProvided,
    int? AssigneeId,
    Dictionary<string, string> Errors)
{
    public static TicketPatch Parse(JsonElement body)
    {
        var errors = new Dictionary<string, string>();
        if (body.ValueKind != JsonValueKind.Object)
            return new TicketPatch(null, null, false, null, new() { ["body"] = "Expected a JSON object" });

        string? status = null;
        if (body.TryGetProperty("status", out var statusElement))
        {
            var value = statusElement.ValueKind == JsonValueKind.String ? statusElement.GetString() : null;
            if (TicketRules.IsKnownStatus(value)) status = value;
            else errors["status"] = "Unknown status";
        }

        string? priority = null;
        if (body.TryGetProperty("priority", out var priorityElement))
        {
            var value = priorityElement.ValueKind == JsonValueKind.String ? priorityElement.GetString() : null;
            if (PriorityRules.IsValid(value)) priority = value;
            else errors["priority"] = "Unknown priority";
        }

        var assigneeProvided = body.TryGetProperty("assigneeId", out var assigneeElement);
        int? assigneeId = null;
        if (assigneeProvided)
        {
            if (assigneeElement.ValueKind == JsonValueKind.Null) assigneeId = null;
            else if (assigneeElement.ValueKind == JsonValueKind.Number && assigneeElement.TryGetInt32(out var id) && id > 0) assigneeId = id;
            else errors["assigneeId"] = "Assignee must be a user id or null";
        }

        return new TicketPatch(status, priority, assigneeProvided, assigneeId, errors);
    }
}
