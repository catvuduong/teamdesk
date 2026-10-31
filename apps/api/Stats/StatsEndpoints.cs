using Api.Auth;
using Api.Sla;
using Api.Tickets;
using Dapper;
using Npgsql;

namespace Api.Stats;

public sealed record SlaStatsResponse(IReadOnlyList<PriorityBreach> ByPriority);

public sealed record StatsResponse(
    int Open,
    int Resolved,
    int ResolvedToday,
    IReadOnlyDictionary<string, int> ByStatus,
    IReadOnlyDictionary<string, int> ByPriority,
    IReadOnlyList<DayCount> Daily,
    double? AvgFirstResponseHours,
    double? AvgResolutionHours,
    IReadOnlyList<TopAgent> TopAgents);

public sealed record TopAgent(int Id, string Name, int Resolved);

public static class StatsEndpoints
{
    public const int WindowDays = 14;

    public static void MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/stats", GetStats)
            .RequireAuthorization(AuthSetup.StaffPolicy)
            .RequireDatabase();
        app.MapGet("/stats/sla", GetSlaStats)
            .RequireAuthorization(AuthSetup.StaffPolicy)
            .RequireDatabase();
    }

    /// <summary>Breach rate per priority: the share of tickets whose SLA is breached now or was breached when they closed.</summary>
    private static async Task<IResult> GetSlaStats(Db db)
    {
        await using var conn = await db.OpenAsync();
        var states = await TicketQueries.AllSlaStatesAsync(conn, DateTime.UtcNow);
        return Results.Ok(new SlaStatsResponse(SlaReport.ByPriority(states)));
    }

    /// <summary>
    /// Dashboard numbers, all in UTC. Open = unresolved (open, in progress, waiting);
    /// Resolved = resolved or closed.
    /// </summary>
    private static async Task<IResult> GetStats(Db db)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var windowStart = today.AddDays(-(WindowDays - 1)).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var todayStart = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        await using var conn = await db.OpenAsync();

        var byStatus = await CountByAsync(conn, "status");
        var byPriority = await CountByAsync(conn, "priority");

        var created = await DayCountsAsync(conn, "created_at", windowStart);
        var resolved = await DayCountsAsync(conn, "resolved_at", windowStart);

        var resolvedToday = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM tickets WHERE resolved_at >= @todayStart",
            new { todayStart });

        // First staff reply (public comments only) and resolution time per ticket
        var spans = (await conn.QueryAsync<TicketSpanRow>(
            """
            SELECT t.created_at, t.resolved_at,
                   (SELECT array_agg(c.created_at)
                    FROM comments c
                    JOIN users u ON u.id = c.author_id
                    WHERE c.ticket_id = t.id AND NOT c.internal AND u.role <> 'customer') AS staff_reply_times
            FROM tickets t
            """)).ToList();

        var responseSpans = spans
            .Select(s => (From: s.CreatedAt, Reply: TimeStats.FirstResponseAt(s.CreatedAt, s.StaffReplyTimes ?? [])))
            .Where(s => s.Reply is not null)
            .Select(s => (s.From, s.Reply!.Value))
            .ToList();
        var resolutionSpans = spans
            .Where(s => s.ResolvedAt is not null)
            .Select(s => (From: s.CreatedAt, To: s.ResolvedAt!.Value))
            .ToList();

        var topAgents = await conn.QueryAsync<TopAgent>(
            """
            SELECT u.id, u.name, count(*)::int AS resolved
            FROM tickets t
            JOIN users u ON u.id = t.assignee_id
            WHERE t.status IN ('resolved', 'closed')
            GROUP BY u.id, u.name
            ORDER BY resolved DESC, u.name
            LIMIT 5
            """);

        var open = TicketRules.Statuses.Where(TicketRules.IsUnresolved).Sum(s => byStatus.GetValueOrDefault(s));
        var resolvedTotal = byStatus.GetValueOrDefault(TicketRules.Resolved) + byStatus.GetValueOrDefault(TicketRules.Closed);

        return Results.Ok(new StatsResponse(
            open,
            resolvedTotal,
            resolvedToday,
            byStatus,
            byPriority,
            DayBuckets.Build(today, WindowDays, created, resolved),
            TimeStats.AverageHours(responseSpans),
            TimeStats.AverageHours(resolutionSpans),
            topAgents.ToList()));
    }

    private static async Task<IReadOnlyDictionary<string, int>> CountByAsync(NpgsqlConnection conn, string column)
    {
        // Column names come from this file only, never from a request
        var counts = await conn.QueryAsync<KeyCountRow>(
            $"SELECT {column} AS key, count(*)::int AS count FROM tickets GROUP BY {column}");
        var lookup = counts.ToDictionary(c => c.Key, c => c.Count);
        var keys = column == "status" ? TicketRules.Statuses : PriorityRules.All;
        return keys.ToDictionary(k => k, k => lookup.GetValueOrDefault(k));
    }

    private static async Task<IReadOnlyDictionary<string, int>> DayCountsAsync(NpgsqlConnection conn, string column, DateTime since)
    {
        var rows = await conn.QueryAsync<DayRow>(
            $"""
            SELECT to_char({column} AT TIME ZONE 'UTC', 'YYYY-MM-DD') AS day, count(*)::int AS count
            FROM tickets
            WHERE {column} >= @since
            GROUP BY 1
            """,
            new { since });
        return rows.ToDictionary(r => r.Day, r => r.Count);
    }

    private sealed class KeyCountRow
    {
        public string Key { get; set; } = "";
        public int Count { get; set; }
    }

    private sealed class DayRow
    {
        public string Day { get; set; } = "";
        public int Count { get; set; }
    }

    private sealed class TicketSpanRow
    {
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime[]? StaffReplyTimes { get; set; }
    }
}
