using Dapper;
using Npgsql;

namespace Api.Sla;

public static class SlaQueries
{
    /// <summary>The targets for every priority, keyed by priority.</summary>
    public static async Task<IReadOnlyDictionary<string, SlaPolicy>> PoliciesAsync(NpgsqlConnection conn, NpgsqlTransaction? tx = null)
    {
        var rows = await conn.QueryAsync<PolicyRow>(
            "SELECT priority, first_response_hours, resolution_hours FROM sla_policies", transaction: tx);
        return rows.ToDictionary(row => row.Priority, row => new SlaPolicy(row.FirstResponseHours, row.ResolutionHours));
    }

    private sealed class PolicyRow
    {
        public string Priority { get; set; } = "";
        public int FirstResponseHours { get; set; }
        public int ResolutionHours { get; set; }
    }
}
