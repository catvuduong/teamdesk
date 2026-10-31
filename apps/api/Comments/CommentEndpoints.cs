using System.Security.Claims;
using Api.Auth;
using Api.Tickets;
using Api.Users;
using Dapper;

namespace Api.Comments;

public sealed record CommentRequest(string? Body, bool Internal);

public static class CommentEndpoints
{
    public static void MapCommentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/tickets/{id:int}/comments", AddComment)
            .RequireAuthorization()
            .RequireDatabase();
    }

    private static async Task<IResult> AddComment(int id, CommentRequest body, ClaimsPrincipal principal, Db db)
    {
        var me = CurrentUser.From(principal);

        var text = body.Body?.Trim() ?? "";
        var errors = new Dictionary<string, string>();
        if (text.Length == 0) errors["body"] = "Comment cannot be empty";
        else if (text.Length > 5000) errors["body"] = "Comment must be 5000 characters or fewer";
        if (body.Internal && !me.IsStaff) errors["internal"] = "Only agents can add internal notes";
        if (errors.Count > 0) return Errors.Validation(errors);

        await using var conn = await db.OpenAsync();
        var ticket = await TicketQueries.GetVisibleAsync(conn, me, id);
        if (ticket is null) return Errors.Problem(StatusCodes.Status404NotFound, "Ticket not found");

        var now = DateTime.UtcNow;
        await using var tx = await conn.BeginTransactionAsync();
        var commentId = await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO comments (ticket_id, author_id, body, internal, created_at)
            VALUES (@id, @author, @text, @internal, @now)
            RETURNING id
            """,
            new { id, author = me.Id, text, @internal = body.Internal, now },
            tx);
        await conn.ExecuteAsync("UPDATE tickets SET updated_at = @now WHERE id = @id", new { id, now }, tx);
        await tx.CommitAsync();

        var view = new CommentView(commentId, text, body.Internal, new(me.Id, me.Name), me.Role, now);
        return Results.Created($"/tickets/{id}", view);
    }
}
