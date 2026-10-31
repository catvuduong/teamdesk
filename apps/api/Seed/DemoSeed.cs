using Api.Auth;
using Api.Sla;
using Api.Tickets;
using Dapper;
using Npgsql;

namespace Api.Seed;

/// <summary>
/// Loads the demo accounts and 45 tickets spread over the last 30 days. Runs once: when the demo admin already exists
/// it does nothing. An advisory lock keeps concurrent instances from seeding twice.
/// </summary>
public static class DemoSeed
{
    private const string MarkerEmail = "admin@teamdesk.demo";

    public static async Task EnsureAsync(Db db)
    {
        await using var conn = await db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await conn.ExecuteAsync("SELECT pg_advisory_xact_lock(727275)", transaction: tx);
        var alreadySeeded = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM users WHERE email = @email)", new { email = MarkerEmail }, tx);
        if (alreadySeeded)
        {
            await tx.RollbackAsync();
            return;
        }

        var now = DateTime.UtcNow;
        var hash = BCrypt.Net.BCrypt.HashPassword(SeedData.DemoPassword);
        var ids = await InsertUsersAsync(conn, tx, hash);

        var admin = ids[MarkerEmail];
        var agents = SeedData.Users.Where(u => u.Role == Roles.Agent).Select(u => ids[u.Email]).ToArray();
        var customers = SeedData.Users.Where(u => u.Role == Roles.Customer).Select(u => ids[u.Email]).ToArray();
        var names = SeedData.Users.ToDictionary(u => ids[u.Email], u => u.Name);
        var policies = await SlaQueries.PoliciesAsync(conn, tx);

        for (var i = 0; i < SeedData.Tickets.Length; i++)
        {
            var template = SeedData.Tickets[i];
            var policy = policies[template.Priority];
            var plan = new TicketPlan(
                Index: i,
                Template: template,
                Created: now - AgeOf(i, template, policy),
                Now: now,
                Requester: customers[i % customers.Length],
                Assignee: template.Status == TicketRules.Open && i % 2 == 1 ? null : agents[i % agents.Length],
                Admin: admin,
                Names: names,
                Policy: policy);
            await InsertTicketAsync(conn, tx, plan);
        }

        await tx.CommitAsync();
    }

    private static async Task<Dictionary<string, int>> InsertUsersAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string hash)
    {
        var ids = new Dictionary<string, int>();
        foreach (var user in SeedData.Users)
        {
            ids[user.Email] = await conn.ExecuteScalarAsync<int>(
                "INSERT INTO users (name, email, password_hash, role) VALUES (@name, @email, @hash, @role) RETURNING id",
                new { user.Name, user.Email, hash, user.Role },
                tx);
        }
        return ids;
    }

    private sealed record TicketPlan(
        int Index,
        SeedTicket Template,
        DateTime Created,
        DateTime Now,
        int Requester,
        int? Assignee,
        int Admin,
        IReadOnlyDictionary<int, string> Names,
        SlaPolicy Policy);

    /// <summary>
    /// Share of the target that has elapsed for unresolved tickets, by position in <see cref="SeedData.Tickets"/>.
    /// Most stay well inside their target. A few are at risk (over 75% used) or breached, so the SLA views have examples.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, double> ElapsedShare = new Dictionary<int, double>
    {
        [14] = 0.85, [31] = 0.8, [43] = 0.9, // at risk
        [5] = 1.5, [39] = 1.5, [34] = 1.25, [37] = 1.3, // breached
    };

    /// <summary>
    /// How long ago a ticket was opened. Resolved tickets keep a spread of history. Unresolved tickets sit at a share of the
    /// target they are judged against: the first response while open, the resolution once work has started.
    /// </summary>
    private static TimeSpan AgeOf(int index, SeedTicket template, SlaPolicy policy)
    {
        if (template.Status is TicketRules.Resolved or TicketRules.Closed) return TimeSpan.FromHours(16 * index + 3);

        var targetHours = template.Status == TicketRules.Open ? policy.FirstResponseHours : policy.ResolutionHours;
        var share = ElapsedShare.GetValueOrDefault(index, 0.3 + 0.05 * (index % 5));
        return TimeSpan.FromHours(targetHours * share);
    }

    /// <summary>
    /// Writes one ticket with a believable history: assignment, status path, public replies and some internal notes.
    /// Every timestamp is clamped to now, so nothing is stamped in the future.
    /// </summary>
    private static async Task InsertTicketAsync(NpgsqlConnection conn, NpgsqlTransaction tx, TicketPlan plan)
    {
        var index = plan.Index;
        var template = plan.Template;
        var now = plan.Now;
        var created = plan.Created;
        var actor = plan.Assignee ?? plan.Admin;

        DateTime At(TimeSpan offset) => Min(created + offset, now);

        // Resolved tickets finish inside their resolution target, so breaches in the seed come from the unresolved set
        var resolvedAt = template.Status is TicketRules.Resolved or TicketRules.Closed
            ? At(TimeSpan.FromHours(Math.Min(6 + (index * 7) % 40, 0.6 * plan.Policy.ResolutionHours)))
            : (DateTime?)null;

        var ticketId = await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO tickets (number, title, description, status, priority, category, requester_id, assignee_id, created_at, updated_at, resolved_at)
            VALUES (nextval('ticket_number_seq'), @title, @description, 'open', @priority, @category, @requester, @assignee, @created, @created, NULL)
            RETURNING id
            """,
            new
            {
                template.Title,
                template.Description,
                template.Priority,
                template.Category,
                requester = plan.Requester,
                assignee = plan.Assignee,
                created,
            },
            tx);

        var updated = created;
        await AddEventAsync(conn, tx, ticketId, plan.Requester, "created", "Ticket created", created);

        if (plan.Assignee is { } agent)
        {
            var at = At(TimeSpan.FromMinutes(30));
            await AddEventAsync(conn, tx, ticketId, plan.Admin, "assignee_changed", $"unassigned → {plan.Names[agent]}", at);
            updated = Max(updated, at);
        }

        // Walk the status path that leads to the template's final status
        var path = new List<(string To, DateTime At)>();
        if (template.Status != TicketRules.Open) path.Add((TicketRules.InProgress, At(TimeSpan.FromHours(2))));
        if (template.Status == TicketRules.Waiting) path.Add((TicketRules.Waiting, At(TimeSpan.FromHours(6))));
        if (resolvedAt is { } done && template.Status is TicketRules.Resolved or TicketRules.Closed)
            path.Add((TicketRules.Resolved, done));
        if (resolvedAt is { } closedAt && template.Status == TicketRules.Closed)
            path.Add((TicketRules.Closed, Min(closedAt.AddDays(1), now)));

        var from = TicketRules.Open;
        foreach (var (to, at) in path)
        {
            await AddEventAsync(conn, tx, ticketId, actor, "status_changed", $"{from} → {to}", at);
            updated = Max(updated, at);
            from = to;
        }

        if (template.Status == TicketRules.Open && plan.Assignee is not null && index % 5 == 0)
        {
            updated = Max(updated, await AddCommentAsync(conn, tx, ticketId, actor, SeedData.InternalNotes[index % SeedData.InternalNotes.Length], true, At(TimeSpan.FromHours(1))));
        }

        if (template.Status != TicketRules.Open)
        {
            updated = Max(updated, await AddCommentAsync(conn, tx, ticketId, actor, SeedData.AgentReplies[index % SeedData.AgentReplies.Length], false, At(TimeSpan.FromHours(plan.Policy.FirstResponseHours / 2.0))));

            if (template.Status is (TicketRules.InProgress or TicketRules.Waiting or TicketRules.Resolved or TicketRules.Closed) && index % 3 == 0)
            {
                updated = Max(updated, await AddCommentAsync(conn, tx, ticketId, actor, SeedData.InternalNotes[index % SeedData.InternalNotes.Length], true, At(TimeSpan.FromHours(2))));
            }

            if (index % 4 == 1)
            {
                updated = Max(updated, await AddCommentAsync(conn, tx, ticketId, plan.Requester, SeedData.CustomerFollowUps[index % SeedData.CustomerFollowUps.Length], false, At(TimeSpan.FromHours(5))));
            }

            if (resolvedAt is { } finished)
            {
                updated = Max(updated, await AddCommentAsync(conn, tx, ticketId, actor, SeedData.ResolutionMessages[index % SeedData.ResolutionMessages.Length], false, finished.AddMinutes(-10)));
            }
        }

        await conn.ExecuteAsync(
            "UPDATE tickets SET status = @status, updated_at = @updated, resolved_at = @resolvedAt WHERE id = @id",
            new { id = ticketId, status = template.Status, updated, resolvedAt },
            tx);
    }

    private static Task AddEventAsync(NpgsqlConnection conn, NpgsqlTransaction tx, int ticketId, int actorId, string type, string detail, DateTime at) =>
        conn.ExecuteAsync(
            "INSERT INTO ticket_events (ticket_id, actor_id, type, detail, created_at) VALUES (@ticketId, @actorId, @type, @detail, @at)",
            new { ticketId, actorId, type, detail, at },
            tx);

    /// <summary>Inserts a comment and returns its timestamp, so the caller can keep updated_at current.</summary>
    private static async Task<DateTime> AddCommentAsync(NpgsqlConnection conn, NpgsqlTransaction tx, int ticketId, int authorId, string body, bool isInternal, DateTime at)
    {
        await conn.ExecuteAsync(
            "INSERT INTO comments (ticket_id, author_id, body, internal, created_at) VALUES (@ticketId, @authorId, @body, @isInternal, @at)",
            new { ticketId, authorId, body, isInternal, at },
            tx);
        return at;
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
}
