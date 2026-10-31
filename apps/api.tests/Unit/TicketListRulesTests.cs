using Api.Tickets;

namespace Api.Tests.Unit;

public class TicketListRulesTests
{
    [Theory]
    [InlineData(0, 20, 1, 20)]
    [InlineData(-3, 5, 1, 5)]
    [InlineData(2, 500, 2, 100)]
    [InlineData(1, 0, 1, 20)]
    public void Paging_is_clamped_to_sane_values(int page, int pageSize, int expectedPage, int expectedSize)
    {
        Assert.Equal((expectedPage, expectedSize), TicketListRules.Paging(page, pageSize));
    }

    [Fact]
    public void Priority_sort_orders_by_urgency_then_newest()
    {
        var sql = TicketListRules.OrderBy(TicketListRules.ByPriority);
        Assert.StartsWith("(CASE t.priority", sql);
        Assert.EndsWith("t.created_at DESC, t.id DESC", sql);
    }

    [Fact]
    public void Validation_rejects_unknown_filters()
    {
        var errors = TicketListRules.Validate("archived", "critical", "nobody", "random");

        Assert.Equal(4, errors.Count);
    }

    [Theory]
    [InlineData("unassigned", true)]
    [InlineData("7", true)]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("abc", false)]
    public void Assignee_filter_accepts_unassigned_or_a_positive_id(string assignee, bool valid)
    {
        Assert.Equal(valid, TicketListRules.IsValidAssignee(assignee));
    }
}
