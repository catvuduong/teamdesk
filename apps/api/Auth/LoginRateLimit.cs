using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Auth;

/// <summary>
/// A per-IP limit on sign-in attempts, using the built-in ASP.NET Core rate limiter. Every attempt counts, successful or
/// not, so password guessing is slowed down. Over the limit the API answers 429 with a Retry-After header.
/// The limit is read from RateLimit:Login:PermitLimit (attempts per minute).
/// </summary>
public static class LoginRateLimit
{
    public const string Policy = "login";
    public const int DefaultPermitLimit = 10;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddLoginRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue("RateLimit:Login:PermitLimit", DefaultPermitLimit);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Policy, context => RateLimitPartition.GetFixedWindowLimiter(
                ClientAddress(context),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = Window, QueueLimit = 0 }));

            options.OnRejected = async (context, token) =>
            {
                var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? Math.Ceiling(retryAfter.TotalSeconds)
                    : Window.TotalSeconds;
                context.HttpContext.Response.Headers["Retry-After"] = seconds.ToString(CultureInfo.InvariantCulture);
                await Errors.Problem(StatusCodes.Status429TooManyRequests, "Too many sign-in attempts. Wait a minute and try again.")
                    .ExecuteAsync(context.HttpContext);
            };
        });
        return services;
    }

    /// <summary>The caller's IP. Behind a proxy this is the forwarded client address (see ForwardedHeaders in Program.cs).</summary>
    private static string ClientAddress(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
