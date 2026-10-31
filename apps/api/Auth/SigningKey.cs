using System.Security.Cryptography;
using System.Text;

namespace Api.Auth;

/// <summary>
/// The JWT signing key is SHA-256(JWT_SECRET), falling back to SHA-256(DATABASE_URL) when the secret is not set,
/// so a deploy that only provides DATABASE_URL still gets a stable key.
/// </summary>
public static class SigningKey
{
    public static byte[] Derive(string? jwtSecret, string? databaseUrl)
    {
        var source = string.IsNullOrWhiteSpace(jwtSecret) ? databaseUrl ?? "" : jwtSecret;
        return SHA256.HashData(Encoding.UTF8.GetBytes(source));
    }
}
