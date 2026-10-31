using Api.Auth;
using Dapper;
using Npgsql;

namespace Api.Tickets;

public sealed record TicketFilter(
    string? Status,
    string? Priority,
    string? Category,
    string? Assignee,
    string? Q,
    bool Mine,
    string Sort,
    int Page,
    int PageSize);

/// <summary>SQL for tickets. Visibility is enforced in the query: customers only ever see their own tickets.</summary>
public static class TicketQueries
{
    private const string Select = """
        SELECT t.id, t.number, t.title, t.description, t.status, t.priority, t.category,
               t.requester_id, r.name AS requester_name, t.assignee_id, a.name AS assignee_name,
               t.created_at, t.updated_at, t.resolved_at
        FROM tickets t
        JOIN users r ON r.id = t.requester_id
        LEFT JOIN users a ON a.id = t.assignee_id
        """;

    public static async Task<TicketPage> ListAsync(NpgsqlConnection conn, CurrentUser me, TicketFilter filter)
    {
        var (page, pageSize) = TicketListRules.Paging(filter.Page, filter.PageSize);
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (!me.IsStaff)
        {
            where.Add("t.requester_id = @viewer");
            parameters.Add("viewer", me.Id);
        }
        else if (filter.Mine)
        {
            where.Add("(t.requester_id = @viewer OR t.assignee_id = @viewer)");
            parameters.Add("viewer", me.Id);
        }

        if (filter.Status is not null)
        {
            where.Add("t.status = @status");
            parameters.Add("status", filter.Status);
        }

        if (filter.Priority is not null)
        {
            where.Add("t.priority = @priority");
            parameters.Add("priority", filter.Priority);
        }

        if (filter.Category is not null)
        {
            where.Add("t.category = @category");
            parameters.Add("category", filter.Category);
        }

        if (filter.Assignee is { } assignee)
        {
            if (assignee == TicketListRules.Unassigned) where.Add("t.assignee_id IS NULL");
            else
            {
                where.Add("t.assignee_id = @assignee");
                parameters.Add("assignee", int.Parse(assignee, System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            where.Add("(t.title ILIKE @q OR t.description ILIKE @q)");
            parameters.Add("q", $"%{EscapeLike(filter.Q.Trim())}%");
        }

        var whereSql = where.Count == 0 ? "" : "WHERE " + string.Join(" AND ", where);
        var total = await conn.ExecuteScalarAsync<int>($"SELECT count(*)::int FROM tickets t {whereSql}", parameters);

        parameters.Add("take", pageSize);
        parameters.Add("skip", (page - 1) * pageSize);
        var rows = await conn.QueryAsync<TicketRow>(
            $"{Select} {whereSql} ORDER BY {TicketListRules.OrderBy(filter.Sort)} LIMIT @take OFFSET @skip",
            parameters);

        return new TicketPage(rows.Select(r => r.ToSummary()).ToList(), total, page, pageSize);
    }

    public static Task<TicketRow?> GetVisibleAsync(NpgsqlConnection conn, CurrentUser me, int id) =>
        conn.QuerySingleOrDefaultAsync<TicketRow>(
            $"{Select} WHERE t.id = @id AND (@staff OR t.requester_id = @viewer)",
            new { id, staff = me.IsStaff, viewer = me.Id });

    public static async Task<TicketDetail?> DetailAsync(NpgsqlConnection conn, CurrentUser me, int id)
    {
        var ticket = await GetVisibleAsync(conn, me, id);
        if (ticket is null) return null;

        // Customers never receive internal notes
        var comments = await conn.QueryAsync<CommentRow>(
            """
            SELECT c.id, c.body, c.internal, c.author_id, u.name AS author_name, u.role AS author_role, c.created_at
            FROM comments c
            JOIN users u ON u.id = c.author_id
            WHERE c.ticket_id = @id AND (@staff OR NOT c.internal)
            ORDER BY c.created_at, c.id
            """,
            new { id, staff = me.IsStaff });

        var events = await conn.QueryAsync<EventRow>(
            """
            SELECT e.id, e.type, e.detail, e.actor_id, u.name AS actor_name, e.created_at
            FROM ticket_events e
            JOIN users u ON u.id = e.actor_id
            WHERE e.ticket_id = @id
            ORDER BY e.created_at, e.id
            """,
            new { id });

        return ticket.ToDetail(
            comments.Select(c => c.ToView()).ToList(),
            events.Select(e => e.ToView()).ToList());
    }

    public static Task AddEventAsync(NpgsqlConnection conn, NpgsqlTransaction? tx, int ticketId, int actorId, string type, string detail, DateTime now) =>
        conn.ExecuteAsync(
            "INSERT INTO ticket_events (ticket_id, actor_id, type, detail, created_at) VALUES (@ticketId, @actorId, @type, @detail, @now)",
            new { ticketId, actorId, type, detail, now },
            tx);

    /// <summary>Escapes LIKE wildcards so a search for "100%" matches literally.</summary>
    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
