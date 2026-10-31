using System.Security.Claims;
using Api.Auth;
using Dapper;

namespace Api.Users;

public sealed record UpdateRoleRequest(string? Role);

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/users").RequireAuthorization().RequireDatabase();
        users.MapGet("/", ListUsers).RequireAuthorization(AuthSetup.StaffPolicy);
        users.MapPatch("/{id:int}", UpdateRole).RequireAuthorization(AuthSetup.AdminPolicy);
    }

    private static async Task<IResult> ListUsers(Db db, string? role)
    {
        if (role is not null && !Roles.All.Contains(role))
            return Errors.Validation(new Dictionary<string, string> { ["role"] = "Unknown role" });

        await using var conn = await db.OpenAsync();
        var users = await conn.QueryAsync<PublicUser>(
            "SELECT id, name, email, role FROM users WHERE (CAST(@role AS text) IS NULL OR role = @role) ORDER BY name",
            new { role });
        return Results.Ok(users);
    }

    private static async Task<IResult> UpdateRole(int id, UpdateRoleRequest body, ClaimsPrincipal principal, Db db)
    {
        if (body.Role is null || !Roles.All.Contains(body.Role))
            return Errors.Validation(new Dictionary<string, string> { ["role"] = "Role must be customer, agent or admin" });

        var me = CurrentUser.From(principal);
        if (me.Id == id && body.Role != Roles.Admin)
            return Errors.Problem(StatusCodes.Status400BadRequest, "You cannot remove your own admin role");

        await using var conn = await db.OpenAsync();
        var user = await conn.QuerySingleOrDefaultAsync<PublicUser>(
            "UPDATE users SET role = @role WHERE id = @id RETURNING id, name, email, role",
            new { id, role = body.Role });

        return user is null ? Errors.Problem(StatusCodes.Status404NotFound, "User not found") : Results.Ok(user);
    }
}
