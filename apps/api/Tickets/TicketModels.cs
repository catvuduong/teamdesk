using Api.Sla;
using Api.Users;

namespace Api.Tickets;

public sealed record TicketSummary(
    int Id,
    int Number,
    string Title,
    string Status,
    string Priority,
    string Category,
    PersonRef Requester,
    PersonRef? Assignee,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ResolvedAt,
    SlaStatus Sla);

public sealed record TicketPage(IReadOnlyList<TicketSummary> Items, int Total, int Page, int PageSize);

public sealed record CommentView(int Id, string Body, bool Internal, PersonRef Author, string AuthorRole, DateTime CreatedAt);

public sealed record EventView(int Id, string Type, string Detail, PersonRef Actor, DateTime CreatedAt);

public sealed record TicketDetail(
    int Id,
    int Number,
    string Title,
    string Description,
    string Status,
    string Priority,
    string Category,
    PersonRef Requester,
    PersonRef? Assignee,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ResolvedAt,
    IReadOnlyList<string> AllowedStatuses,
    SlaStatus Sla,
    IReadOnlyList<CommentView> Comments,
    IReadOnlyList<EventView> Events);

public sealed record CreateTicketRequest(string? Title, string? Description, string? Category, string? Priority);

/// <summary>Flat row for ticket queries; <see cref="ToSummary"/> and <see cref="ToDetail"/> shape it for clients.</summary>
public sealed class TicketRow
{
    public int Id { get; set; }
    public int Number { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "";
    public string Priority { get; set; } = "";
    public string Category { get; set; } = "";
    public int RequesterId { get; set; }
    public string RequesterName { get; set; } = "";
    public int? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    /// <summary>The first public staff reply at or after creation (see TicketQueries.FirstReplyJoin); null when none.</summary>
    public DateTime? FirstReplyAt { get; set; }

    public PersonRef Requester => new(RequesterId, RequesterName);

    public PersonRef? Assignee => AssigneeId is { } id ? new PersonRef(id, AssigneeName ?? "") : null;

    public TicketSummary ToSummary(SlaStatus sla) => new(
        Id, Number, Title, Status, Priority, Category, Requester, Assignee, CreatedAt, UpdatedAt, ResolvedAt, sla);

    public TicketDetail ToDetail(SlaStatus sla, IReadOnlyList<CommentView> comments, IReadOnlyList<EventView> events) => new(
        Id, Number, Title, Description, Status, Priority, Category, Requester, Assignee,
        CreatedAt, UpdatedAt, ResolvedAt, TicketRules.AllowedTargets(Status), sla, comments, events);
}

public sealed class CommentRow
{
    public int Id { get; set; }
    public string Body { get; set; } = "";
    public bool Internal { get; set; }
    public int AuthorId { get; set; }
    public string AuthorName { get; set; } = "";
    public string AuthorRole { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public CommentView ToView() => new(Id, Body, Internal, new PersonRef(AuthorId, AuthorName), AuthorRole, CreatedAt);
}

public sealed class EventRow
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public string Detail { get; set; } = "";
    public int ActorId { get; set; }
    public string ActorName { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public EventView ToView() => new(Id, Type, Detail, new PersonRef(ActorId, ActorName), CreatedAt);
}
