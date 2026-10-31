using System.Runtime.CompilerServices;

namespace Api.Tests;

/// <summary>
/// Every test class runs its own in-process host, and all of them share one test address, so the default login limit
/// would be used up by the suite's ordinary sign-ins. Raise it for the whole test run; LoginRateLimitTests sets a low limit itself.
/// </summary>
internal static class TestSetup
{
    [ModuleInitializer]
    internal static void RaiseLoginRateLimit() => Environment.SetEnvironmentVariable("RateLimit__Login__PermitLimit", "1000");
}
