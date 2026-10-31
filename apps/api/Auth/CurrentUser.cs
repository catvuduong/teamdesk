using System.Security.Claims;
using System.Globalization;

namespace Api.Auth;

/// <summary>The caller, read from the JWT claims set by <see cref="TokenService"/>.</summary>
public sealed record CurrentUser(int Id, string Name, string Role)
{
    public bool IsStaff => Roles.IsStaff(Role);

    public static CurrentUser From(ClaimsPrincipal principal)
    {
        var id = int.Parse(principal.FindFirst("sub")?.Value ?? "0", CultureInfo.InvariantCulture);
        return new CurrentUser(id, principal.FindFirst("name")?.Value ?? "", principal.FindFirst("role")?.Value ?? "");
    }
}
