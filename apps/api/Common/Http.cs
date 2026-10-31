using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Api;

/// <summary>Uniform JSON error bodies: { "error": "..." } and, for validation, { "error", "errors": { field: message } }.</summary>
public static class Errors
{
    public static IResult Problem(int status, string message) =>
        Results.Json(new { error = message }, statusCode: status);

    public static IResult Validation(IReadOnlyDictionary<string, string> fields) =>
        Results.Json(new { error = "Validation failed", errors = fields }, statusCode: StatusCodes.Status400BadRequest);

    public static IResult DatabaseDisabled() =>
        Problem(StatusCodes.Status503ServiceUnavailable, "DATABASE_URL is not configured");
}

public static class RouteExtensions
{
    /// <summary>Answers 503 instead of crashing when DATABASE_URL is not set (health still works).</summary>
    public static TBuilder RequireDatabase<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        builder.Add(endpoint => endpoint.FilterFactories.Add((_, next) => async context =>
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<Db>();
            return db.Enabled ? await next(context) : Errors.DatabaseDisabled();
        }));
        return builder;
    }
}
