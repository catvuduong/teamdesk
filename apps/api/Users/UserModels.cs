namespace Api.Users;

/// <summary>A users row as read by Dapper (includes the password hash; never returned to clients).</summary>
public sealed class UserRow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "";
}

public sealed record PublicUser(int Id, string Name, string Email, string Role);

/// <summary>A short reference to a person, used inside tickets, comments and events.</summary>
public sealed record PersonRef(int Id, string Name);
