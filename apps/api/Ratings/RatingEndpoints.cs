using System.Security.Claims;
using Api.Auth;
using Api.Tickets;
using Dapper;
using Npgsql;

namespace Api.Ratings;

public sealed record RatingRequest(int? Score, string? Comment);

public sealed record RatingView(int TicketId, int Score, string? Comment, DateTime CreatedAt);

public static class RatingEndpoints
{
    public static void MapRatingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/tickets/{id:int}/rating", AddRating)
            .RequireAuthorization()
            .RequireDatabase();
    }

    /// <summary>
    /// The requester rates a resolved or closed ticket once. The ticket row is locked for the whole check and insert, so a
    /// double click or two tabs cannot both store a rating; the primary key is the last line of defence.
    /// </summary>
    private static async Task<IResult> AddRating(int id, RatingRequest body, ClaimsPrincipal principal, Db db)
    {
        var errors = RatingRules.Validate(body.Score, body.Comment);
        if (errors.Count > 0) return Errors.Validation(errors);

        var me = CurrentUser.From(principal);
        var comment = string.IsNullOrWhiteSpace(body.Comment) ? null : body.Comment.Trim();

        await using var conn = await db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var ticket = await conn.QuerySingleOrDefaultAsync<RatedTicket>(
            "SELECT id, status, requester_id FROM tickets WHERE id = @id FOR UPDATE", new { id }, tx);
        if (ticket is null) return Errors.Problem(StatusCodes.Status404NotFound, "Ticket not found");
        if (ticket.RequesterId != me.Id) return Errors.Problem(StatusCodes.Status403Forbidden, "Only the requester can rate this ticket");
        if (!RatingRules.CanRate(ticket.Status))
            return Errors.Problem(StatusCodes.Status409Conflict, "Only resolved or closed tickets can be rated");

        var alreadyRated = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM ratings WHERE ticket_id = @id)", new { id }, tx);
        if (alreadyRated) return Errors.Problem(StatusCodes.Status409Conflict, "This ticket has already been rated");

        var now = DateTime.UtcNow;
        try
        {
            await conn.ExecuteAsync(
                "INSERT INTO ratings (ticket_id, score, comment, created_at) VALUES (@id, @score, @comment, @now)",
                new { id, score = body.Score!.Value, comment, now },
                tx);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Errors.Problem(StatusCodes.Status409Conflict, "This ticket has already been rated");
        }

        await tx.CommitAsync();
        return Results.Created($"/tickets/{id}", new RatingView(id, body.Score!.Value, comment, now));
    }

    private sealed class RatedTicket
    {
        public int Id { get; set; }
        public string Status { get; set; } = "";
        public int RequesterId { get; set; }
    }
}

/// <summary>A stored rating as read from the ratings table; <see cref="ToView"/> shapes it for clients.</summary>
public sealed class RatingRow
{
    public int TicketId { get; set; }
    public int Score { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }

    public RatingView ToView() => new(TicketId, Score, Comment, CreatedAt);
}
