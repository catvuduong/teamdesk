using Api.Tickets;

namespace Api.Sla;

/// <summary>One priority's row in the SLA report. <see cref="BreachRate"/> is a percentage, null when there are no tickets.</summary>
public sealed record PriorityBreach(string Priority, int Total, int Breached, int AtRisk, double? BreachRate);

/// <summary>Breach rate per priority. Pure: unit-tested without a database.</summary>
public static class SlaReport
{
    /// <summary>Rows for every priority, most urgent first. Tickets with no row are not counted.</summary>
    public static IReadOnlyList<PriorityBreach> ByPriority(IEnumerable<(string Priority, string State)> tickets)
    {
        var rows = tickets.ToList();
        return PriorityRules.All.Reverse().Select(priority =>
        {
            var states = rows.Where(row => row.Priority == priority).Select(row => row.State).ToList();
            var breached = states.Count(state => state == SlaRules.Breached);
            var rate = states.Count == 0 ? (double?)null : Math.Round(100.0 * breached / states.Count, 1);
            return new PriorityBreach(priority, states.Count, breached, states.Count(state => state == SlaRules.AtRisk), rate);
        }).ToList();
    }
}
