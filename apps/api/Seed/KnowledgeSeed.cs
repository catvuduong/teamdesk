using Dapper;

namespace Api.Seed;

/// <summary>
/// Loads the help-centre articles and a rating for most resolved tickets. Each row is inserted only when it is missing
/// (keyed by slug, or by ticket for ratings), so the seed is safe to run on every start and repairs a partial load. An
/// advisory lock keeps concurrent instances from interleaving. Runs after <see cref="DemoSeed"/>, so the tickets exist.
/// </summary>
public static class KnowledgeSeed
{
    public static async Task EnsureAsync(Db db)
    {
        await using var conn = await db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await conn.ExecuteAsync("SELECT pg_advisory_xact_lock(727276)", transaction: tx);

        var now = DateTime.UtcNow;
        foreach (var article in KnowledgeSeedData.Articles)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO articles (slug, title, body, category, published, views, created_at, updated_at)
                VALUES (@slug, @title, @body, @category, @published, @views, @now, @now)
                ON CONFLICT (slug) DO NOTHING
                """,
                new
                {
                    article.Slug,
                    article.Title,
                    article.Body,
                    article.Category,
                    article.Published,
                    // Spread the counts so the most-read articles are not all tied
                    views = 12 * (article.Title.Length % 7) + 3,
                    now,
                },
                tx);
        }

        var resolved = (await conn.QueryAsync<ResolvedTicketRow>(
            """
            SELECT number, resolved_at
            FROM tickets
            WHERE status IN ('resolved', 'closed') AND resolved_at IS NOT NULL
            ORDER BY number
            """,
            transaction: tx)).ToList();

        foreach (var ticket in resolved.Where(t => KnowledgeSeedData.IsRated(t.Number)))
        {
            // The customer rates a couple of hours after the fix, and never in the future
            var ratedAt = Min(ticket.ResolvedAt.AddHours(2), now);
            await conn.ExecuteAsync(
                """
                INSERT INTO ratings (ticket_id, score, comment, created_at)
                SELECT id, CAST(@score AS int), CAST(@comment AS text), @ratedAt FROM tickets WHERE number = @number
                ON CONFLICT (ticket_id) DO NOTHING
                """,
                new { number = ticket.Number, score = KnowledgeSeedData.ScoreFor(ticket.Number), comment = KnowledgeSeedData.CommentFor(ticket.Number), ratedAt },
                tx);
        }

        await tx.CommitAsync();
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private sealed class ResolvedTicketRow
    {
        public int Number { get; set; }
        public DateTime ResolvedAt { get; set; }
    }
}
