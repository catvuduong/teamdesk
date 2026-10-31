using Api.Auth;
using Api.Ratings;
using Api.Sla;
using Dapper;
using Npgsql;

namespace Api.Tickets;

public sealed record TicketFilter(
    string? Status,
    string? Priority,
    string? Category,
    string? Assignee,
    string? Q,
    string? Sla,
    bool Mine,
    string Sort,
    int Page,
    int PageSize);

/// <summary>SQL for tickets. Visibility is enforced in the query: customers only ever see their own tickets.</summary>
public static class TicketQueries
{
    /// <summary>
    /// The first public staff reply, which stops the first-response clock. A ticket with no such reply gets null.
    /// Customer and internal comments do not count, matching the dashboard's response time.
    /// </summary>
    private const string FirstReplyJoin = """
        LEFT JOIN LATERAL (
            SELECT min(c.created_at) AS first_reply_at
            FROM comments c
            JOIN users cu ON cu.id = c.author_id
            WHERE c.ticket_id = t.id AND NOT c.internal AND cu.role <> 'customer' AND c.created_at >= t.created_at
        ) fr ON true
        """;

    private const string Select = $"""
        SELECT t.id, t.number, t.title, t.description, t.status, t.priority, t.category,
               t.requester_id, r.name AS requester_name, t.assignee_id, a.name AS assignee_name,
               t.created_at, t.updated_at, t.resolved_at, fr.first_reply_at
        FROM tickets t
        JOIN users r ON r.id = t.requester_id
        LEFT JOIN users a ON a.id = t.assignee_id
        {FirstReplyJoin}
        """;

    public static async Task<TicketPage> ListAsync(NpgsqlConnection conn, CurrentUser me, TicketFilter filter, DateTime now)
    {
        var (page, pageSize) = TicketListRules.Paging(filter.Page, filter.PageSize);
        var skip = (page - 1) * pageSize;

        // The SLA state is judged in C#, so an SLA filter has to see every matching ticket before the page is cut
        if (filter.Sla is not null)
        {
            var matching = new List<TicketSummary>();
            await foreach (var ticket in StreamMatchingAsync(conn, me, filter, now)) matching.Add(ticket);
            return new TicketPage(matching.Skip(skip).Take(pageSize).ToList(), matching.Count, page, pageSize);
        }

        var (whereSql, parameters) = Where(me, filter);
        var total = await conn.ExecuteScalarAsync<int>($"SELECT count(*)::int FROM tickets t {whereSql}", parameters);

        parameters.Add("take", pageSize);
        parameters.Add("skip", skip);
        var rows = await conn.QueryAsync<TicketRow>(
            $"{Select} {whereSql} ORDER BY {TicketListRules.OrderBy(filter.Sort)} LIMIT @take OFFSET @skip",
            parameters);

        var policies = await SlaQueries.PoliciesAsync(conn);
        return new TicketPage(rows.Select(r => r.ToSummary(SlaOf(r, policies, now))).ToList(), total, page, pageSize);
    }

    /// <summary>
    /// Every ticket the filter matches, in list order and without paging. The CSV export reads this. Rows are read one at a
    /// time as the caller advances, so the full result is never held in memory.
    /// </summary>
    public static async IAsyncEnumerable<TicketSummary> StreamMatchingAsync(NpgsqlConnection conn, CurrentUser me, TicketFilter filter, DateTime now)
    {
        var (whereSql, parameters) = Where(me, filter);
        var policies = await SlaQueries.PoliciesAsync(conn);

        await using var reader = await conn.ExecuteReaderAsync(
            $"{Select} {whereSql} ORDER BY {TicketListRules.OrderBy(filter.Sort)}",
            parameters);
        var parse = reader.GetRowParser<TicketRow>();
        while (await reader.ReadAsync())
        {
            var row = parse(reader);
            var ticket = row.ToSummary(SlaOf(row, policies, now));
            if (filter.Sla is null || ticket.Sla.State == filter.Sla) yield return ticket;
        }
    }

    /// <summary>The SLA state of every ticket, for the breach-rate report. Visibility does not apply: the report is staff-only.</summary>
    public static async Task<IReadOnlyList<(string Priority, string State)>> AllSlaStatesAsync(NpgsqlConnection conn, DateTime now)
    {
        var rows = await conn.QueryAsync<TicketRow>(Select);
        var policies = await SlaQueries.PoliciesAsync(conn);
        return rows.Select(r => (r.Priority, SlaOf(r, policies, now).State)).ToList();
    }

    public static Task<TicketRow?> GetVisibleAsync(NpgsqlConnection conn, CurrentUser me, int id) =>
        conn.QuerySingleOrDefaultAsync<TicketRow>(
            $"{Select} WHERE t.id = @id AND (@staff OR t.requester_id = @viewer)",
            new { id, staff = me.IsStaff, viewer = me.Id });

    public static async Task<TicketDetail?> DetailAsync(NpgsqlConnection conn, CurrentUser me, int id, DateTime now)
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

        var rating = await conn.QuerySingleOrDefaultAsync<RatingRow>(
            "SELECT ticket_id, score, comment, created_at FROM ratings WHERE ticket_id = @id", new { id });

        var policies = await SlaQueries.PoliciesAsync(conn);
        return ticket.ToDetail(
            SlaOf(ticket, policies, now),
            comments.Select(c => c.ToView()).ToList(),
            events.Select(e => e.ToView()).ToList(),
            rating?.ToView());
    }

    public static Task AddEventAsync(NpgsqlConnection conn, NpgsqlTransaction? tx, int ticketId, int actorId, string type, string detail, DateTime now) =>
        conn.ExecuteAsync(
            "INSERT INTO ticket_events (ticket_id, actor_id, type, detail, created_at) VALUES (@ticketId, @actorId, @type, @detail, @now)",
            new { ticketId, actorId, type, detail, now },
            tx);

    private static (string WhereSql, DynamicParameters Parameters) Where(CurrentUser me, TicketFilter filter)
    {
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
        return (whereSql, parameters);
    }

    private static SlaStatus SlaOf(TicketRow row, IReadOnlyDictionary<string, SlaPolicy> policies, DateTime now) =>
        SlaRules.Evaluate(row.CreatedAt, row.FirstReplyAt, row.ResolvedAt, policies[row.Priority], now);

    /// <summary>Escapes LIKE wildcards so a search for "100%" matches literally.</summary>
    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
