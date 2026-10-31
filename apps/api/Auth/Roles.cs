namespace Api.Auth;

public static class Roles
{
    public const string Customer = "customer";
    public const string Agent = "agent";
    public const string Admin = "admin";

    public static readonly IReadOnlyList<string> All = [Customer, Agent, Admin];

    /// <summary>Agents and admins work the queue; customers only see their own tickets.</summary>
    public static bool IsStaff(string role) => role is Agent or Admin;
}
