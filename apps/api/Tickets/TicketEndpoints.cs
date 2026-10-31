using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Api.Auth;
using Api.Users;
using Dapper;
using Npgsql;

namespace Api.Tickets;

public static class TicketEndpoints
{
    private static readonly string[] CsvHeader =
        ["Number", "Title", "Status", "Priority", "Category", "Requester", "Assignee", "Created at", "Updated at", "Resolved at", "SLA state"];

    public static void MapTicketEndpoints(this IEndpointRouteBuilder app)
    {
        var tickets = app.MapGroup("/tickets").RequireAuthorization().RequireDatabase();
        tickets.MapGet("/", ListTickets);
        tickets.MapPost("/", CreateTicket);
        tickets.MapGet("/export.csv", ExportTicketsCsv).RequireAuthorization(AuthSetup.StaffPolicy);
        tickets.MapGet("/{id:int}", GetTicket);
        tickets.MapPatch("/{id:int}", UpdateTicket).RequireAuthorization(AuthSetup.StaffPolicy);
    }

    private static async Task<IResult> ListTickets(
        ClaimsPrincipal principal,
        Db db,
        string? status,
        string? priority,
        string? category,
        string? assignee,
        string? q,
        string? sla,
        bool mine = false,
        string? sort = null,
        int page = 1,
        int pageSize = TicketListRules.DefaultPageSize)
    {
        var errors = TicketListRules.Validate(status, priority, assignee, sort, sla);
        if (errors.Count > 0) return Errors.Validation(errors);

        var filter = FilterFrom(status, priority, category, assignee, q, sla, mine, sort, page, pageSize);

        await using var conn = await db.OpenAsync();
        return Results.Ok(await TicketQueries.ListAsync(conn, CurrentUser.From(principal), filter, DateTime.UtcNow));
    }

    /// <summary>
    /// Every ticket the filters match, as CSV for agents and admins. The filters are the list's, without paging.
    /// Rows are read from the database and written to the response one at a time, so the export is never held in memory.
    /// </summary>
    private static IResult ExportTicketsCsv(
        ClaimsPrincipal principal,
        Db db,
        string? status,
        string? priority,
        string? category,
        string? assignee,
        string? q,
        string? sla,
        bool mine = false,
        string? sort = null)
    {
        var errors = TicketListRules.Validate(status, priority, assignee, sort, sla);
        if (errors.Count > 0) return Errors.Validation(errors);

        var me = CurrentUser.From(principal);
        var filter = FilterFrom(status, priority, category, assignee, q, sla, mine, sort, 1, TicketListRules.DefaultPageSize);
        var now = DateTime.UtcNow;

        return Results.Stream(async stream =>
        {
            await using var conn = await db.OpenAsync();
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { NewLine = Csv.NewLine };
            await writer.WriteLineAsync(Csv.Line(CsvHeader));
            await foreach (var ticket in TicketQueries.StreamMatchingAsync(conn, me, filter, now))
                await writer.WriteLineAsync(Csv.Line(CsvFields(ticket)));
            await writer.FlushAsync();
        }, "text/csv; charset=utf-8", "tickets.csv");
    }

    private static IEnumerable<string?> CsvFields(TicketSummary ticket) =>
    [
        ticket.Number.ToString(CultureInfo.InvariantCulture),
        ticket.Title,
        ticket.Status,
        ticket.Priority,
        ticket.Category,
        ticket.Requester.Name,
        ticket.Assignee?.Name,
        IsoUtc(ticket.CreatedAt),
        IsoUtc(ticket.UpdatedAt),
        IsoUtc(ticket.ResolvedAt),
        ticket.Sla.State,
    ];

    private static string? IsoUtc(DateTime? value) => value?.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static TicketFilter FilterFrom(
        string? status, string? priority, string? category, string? assignee, string? q, string? sla,
        bool mine, string? sort, int page, int pageSize) =>
        new(status, priority, string.IsNullOrWhiteSpace(category) ? null : category.Trim(), assignee, q, sla,
            mine, sort ?? TicketListRules.Newest, page, pageSize);

    private static async Task<IResult> CreateTicket(CreateTicketRequest body, ClaimsPrincipal principal, Db db)
    {
        var errors = TicketRules.ValidateNew(body.Title, body.Description, body.Category, body.Priority);
        if (errors.Count > 0) return Errors.Validation(errors);

        var me = CurrentUser.From(principal);
        var now = DateTime.UtcNow;
        var priority = body.Priority ?? PriorityRules.Medium;

        await using var conn = await db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var id = await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO tickets (number, title, description, status, priority, category, requester_id, created_at, updated_at)
            VALUES (nextval('ticket_number_seq'), @title, @description, 'open', @priority, @category, @requester, @now, @now)
            RETURNING id
            """,
            new
            {
                title = body.Title!.Trim(),
                description = body.Description!.Trim(),
                priority,
                category = body.Category!.Trim(),
                requester = me.Id,
                now,
            },
            tx);

        await TicketQueries.AddEventAsync(conn, tx, id, me.Id, "created", "Ticket created", now);
        await tx.CommitAsync();

        var detail = await TicketQueries.DetailAsync(conn, me, id, now);
        return Results.Created($"/tickets/{id}", detail);
    }

    private static async Task<IResult> GetTicket(int id, ClaimsPrincipal principal, Db db)
    {
        await using var conn = await db.OpenAsync();
        var detail = await TicketQueries.DetailAsync(conn, CurrentUser.From(principal), id, DateTime.UtcNow);
        return detail is null ? Errors.Problem(StatusCodes.Status404NotFound, "Ticket not found") : Results.Ok(detail);
    }

    /// <summary>
    /// Status, priority and assignee changes. Each real change writes a ticket_event in the same transaction
    /// as the update, so the audit trail can never disagree with the ticket.
    /// </summary>
    private static async Task<IResult> UpdateTicket(int id, JsonElement body, ClaimsPrincipal principal, Db db)
    {
        var patch = TicketPatch.Parse(body);
        if (patch.Errors.Count > 0) return Errors.Validation(patch.Errors);

        var me = CurrentUser.From(principal);
        await using var conn = await db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var current = await conn.QuerySingleOrDefaultAsync<TicketState>(
            "SELECT id, status, priority, assignee_id, resolved_at FROM tickets WHERE id = @id FOR UPDATE",
            new { id }, tx);
        if (current is null) return Errors.Problem(StatusCodes.Status404NotFound, "Ticket not found");

        if (patch.AssigneeProvided && patch.AssigneeId is { } assigneeId)
        {
            var assignee = await conn.QuerySingleOrDefaultAsync<UserRow>(
                "SELECT id, name, email, password_hash, role FROM users WHERE id = @assigneeId", new { assigneeId }, tx);
            if (assignee is null || !Roles.IsStaff(assignee.Role))
                return Errors.Validation(new Dictionary<string, string> { ["assigneeId"] = "Assignee must be an agent or admin" });
        }

        var now = DateTime.UtcNow;
        var events = new List<(string Type, string Detail)>();
        var status = current.Status;
        var priority = current.Priority;
        var assigneeIdNow = current.AssigneeId;
        var resolvedAt = current.ResolvedAt;

        if (patch.Status is { } newStatus && newStatus != current.Status)
        {
            if (!TicketRules.CanTransition(current.Status, newStatus))
                return Errors.Problem(StatusCodes.Status400BadRequest, $"Cannot change status from {current.Status} to {newStatus}");

            events.Add(("status_changed", $"{current.Status} → {newStatus}"));
            status = newStatus;
            resolvedAt = TicketRules.ResolvedAtAfter(newStatus, current.ResolvedAt, now);
        }

        if (patch.Priority is { } newPriority && newPriority != current.Priority)
        {
            events.Add(("priority_changed", $"{current.Priority} → {newPriority}"));
            priority = newPriority;
        }

        if (patch.AssigneeProvided && patch.AssigneeId != current.AssigneeId)
        {
            var from = await NameOfAsync(conn, tx, current.AssigneeId) ?? "unassigned";
            var to = await NameOfAsync(conn, tx, patch.AssigneeId) ?? "unassigned";
            events.Add(("assignee_changed", $"{from} → {to}"));
            assigneeIdNow = patch.AssigneeId;
        }

        if (events.Count > 0)
        {
            await conn.ExecuteAsync(
                """
                UPDATE tickets
                SET status = @status, priority = @priority, assignee_id = @assigneeIdNow, resolved_at = @resolvedAt, updated_at = @now
                WHERE id = @id
                """,
                new { id, status, priority, assigneeIdNow, resolvedAt, now },
                tx);

            foreach (var (type, text) in events)
                await TicketQueries.AddEventAsync(conn, tx, id, me.Id, type, text, now);
        }

        await tx.CommitAsync();

        var detail = await TicketQueries.DetailAsync(conn, me, id, DateTime.UtcNow);
        return Results.Ok(detail);
    }

    private static Task<string?> NameOfAsync(NpgsqlConnection conn, NpgsqlTransaction tx, int? userId) =>
        userId is null
            ? Task.FromResult<string?>(null)
            : conn.ExecuteScalarAsync<string?>("SELECT name FROM users WHERE id = @userId", new { userId }, tx);

    private sealed class TicketState
    {
        public int Id { get; set; }
        public string Status { get; set; } = "";
        public string Priority { get; set; } = "";
        public int? AssigneeId { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }
}
