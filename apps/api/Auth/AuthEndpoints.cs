using System.Security.Claims;
using Api.Users;
using Dapper;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;

namespace Api.Auth;

public sealed record RegisterRequest(string? Name, string? Email, string? Password);
public sealed record LoginRequest(string? Email, string? Password);
public sealed record AuthResponse(string Token, PublicUser User);

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth").RequireDatabase();
        auth.MapPost("/register", Register);
        auth.MapPost("/login", Login).RequireRateLimiting(LoginRateLimit.Policy);

        app.MapGet("/me", Me).RequireAuthorization().RequireDatabase();
    }

    private static async Task<IResult> Register(RegisterRequest body, Db db, TokenService tokens)
    {
        var errors = AccountRules.ValidateRegistration(body.Name, body.Email, body.Password);
        if (errors.Count > 0) return Errors.Validation(errors);

        var name = body.Name!.Trim();
        var email = AccountRules.NormalizeEmail(body.Email);
        var hash = BCrypt.Net.BCrypt.HashPassword(body.Password!);

        await using var conn = await db.OpenAsync();
        int id;
        try
        {
            id = await conn.ExecuteScalarAsync<int>(
                "INSERT INTO users (name, email, password_hash, role) VALUES (@name, @email, @hash, @role) RETURNING id",
                new { name, email, hash, role = Roles.Customer });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Errors.Problem(StatusCodes.Status409Conflict, "An account with this email already exists");
        }

        var user = new PublicUser(id, name, email, Roles.Customer);
        return Results.Created($"/me", new AuthResponse(tokens.Issue(id, name, Roles.Customer, DateTime.UtcNow), user));
    }

    private static async Task<IResult> Login(LoginRequest body, Db db, TokenService tokens)
    {
        var email = AccountRules.NormalizeEmail(body.Email);
        if (email.Length == 0 || string.IsNullOrEmpty(body.Password))
            return Errors.Problem(StatusCodes.Status401Unauthorized, "Invalid email or password");

        await using var conn = await db.OpenAsync();
        var row = await conn.QuerySingleOrDefaultAsync<UserRow>(
            "SELECT id, name, email, password_hash, role FROM users WHERE email = @email", new { email });

        if (row is null || !BCrypt.Net.BCrypt.Verify(body.Password, row.PasswordHash))
            return Errors.Problem(StatusCodes.Status401Unauthorized, "Invalid email or password");

        var user = new PublicUser(row.Id, row.Name, row.Email, row.Role);
        return Results.Ok(new AuthResponse(tokens.Issue(row.Id, row.Name, row.Role, DateTime.UtcNow), user));
    }

    private static async Task<IResult> Me(ClaimsPrincipal principal, Db db)
    {
        var me = CurrentUser.From(principal);
        await using var conn = await db.OpenAsync();
        var user = await conn.QuerySingleOrDefaultAsync<PublicUser>(
            "SELECT id, name, email, role FROM users WHERE id = @id", new { id = me.Id });

        // The account was deleted after the token was issued
        return user is null
            ? Errors.Problem(StatusCodes.Status401Unauthorized, "Unauthorized")
            : Results.Ok(user);
    }
}
