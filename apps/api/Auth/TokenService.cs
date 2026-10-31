using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Api.Auth;

/// <summary>Issues HS256 JWTs for a user. Tokens last 12 hours.</summary>
public sealed class TokenService
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    private readonly SigningCredentials _credentials;
    private readonly JsonWebTokenHandler _handler = new();

    public TokenService(byte[] key)
    {
        _credentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);
    }

    public string Issue(int userId, string name, string role, DateTime nowUtc)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = AuthSetup.Issuer,
            Audience = AuthSetup.Audience,
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", userId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim("name", name),
                new Claim("role", role),
            ]),
            IssuedAt = nowUtc,
            Expires = nowUtc.Add(Lifetime),
            SigningCredentials = _credentials,
        };
        return _handler.CreateToken(descriptor);
    }
}
