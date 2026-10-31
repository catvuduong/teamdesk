using Api.Tickets;

namespace Api.Tests.Unit;

public class PriorityRulesTests
{
    [Fact]
    public void Ranks_increase_from_low_to_urgent()
    {
        var ranks = PriorityRules.All.Select(PriorityRules.Rank).ToArray();
        Assert.Equal(new[] { 0, 1, 2, 3 }, ranks);
    }

    [Fact]
    public void Sorting_by_rank_descending_puts_urgent_first()
    {
        var shuffled = new[] { "medium", "urgent", "low", "high" };

        var sorted = shuffled.OrderByDescending(PriorityRules.Rank).ToArray();

        Assert.Equal(new[] { "urgent", "high", "medium", "low" }, sorted);
    }

    [Fact]
    public void Unknown_priority_is_rejected()
    {
        Assert.False(PriorityRules.IsValid("critical"));
        Assert.False(PriorityRules.IsValid(null));
        Assert.Throws<ArgumentOutOfRangeException>(() => PriorityRules.Rank("critical"));
    }

    [Fact]
    public void SQL_rank_expression_uses_the_same_order_as_Rank()
    {
        var sql = PriorityRules.SqlRank("t.priority");

        foreach (var priority in PriorityRules.All)
            Assert.Contains($"WHEN '{priority}' THEN {PriorityRules.Rank(priority)}", sql);
    }
}
