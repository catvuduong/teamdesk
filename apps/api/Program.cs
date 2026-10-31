using Api;
using Api.Auth;
using Api.Comments;
using Api.Seed;
using Api.Stats;
using Api.Tickets;
using Api.Users;

var builder = WebApplication.CreateBuilder(args);

// Render (and most hosts) pass the port to listen on
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port)) builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddSingleton<Db>();
builder.Services.AddTeamDeskAuth();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    var origins = (Environment.GetEnvironmentVariable("CORS_ORIGINS") ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    else policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Text("teamdesk API (ASP.NET Core). Endpoints: GET /health, POST /auth/register, POST /auth/login, GET /me, GET|POST /tickets, GET|PATCH /tickets/{id}, POST /tickets/{id}/comments, GET /users, GET /stats"));
app.MapGet("/health", async (Db db) => Results.Ok(new { status = "ok", db = await db.StatusAsync() }));

app.MapAuthEndpoints();
app.MapTicketEndpoints();
app.MapCommentEndpoints();
app.MapUserEndpoints();
app.MapStatsEndpoints();

// Demo data is optional: a failure is logged, not fatal, so the API still starts while the database is unavailable
var db = app.Services.GetRequiredService<Db>();
if (db.Enabled)
{
    try
    {
        await DemoSeed.EnsureAsync(db);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Demo data was not seeded");
    }
}

app.Run();

public partial class Program;
