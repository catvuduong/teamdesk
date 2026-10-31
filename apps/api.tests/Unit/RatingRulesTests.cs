using Api.Ratings;
using Api.Tickets;

namespace Api.Tests.Unit;

public class RatingRulesTests
{
    [Theory]
    [InlineData(TicketRules.Resolved)]
    [InlineData(TicketRules.Closed)]
    public void Resolved_and_closed_tickets_can_be_rated(string status)
    {
        Assert.True(RatingRules.CanRate(status));
    }

    [Theory]
    [InlineData(TicketRules.Open)]
    [InlineData(TicketRules.InProgress)]
    [InlineData(TicketRules.Waiting)]
    public void Tickets_still_being_worked_on_cannot_be_rated(string status)
    {
        Assert.False(RatingRules.CanRate(status));
    }

    [Fact]
    public void A_score_is_required()
    {
        Assert.Contains("score", RatingRules.Validate(null, null).Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Scores_outside_one_to_five_are_rejected(int score)
    {
        Assert.Contains("score", RatingRules.Validate(score, null).Keys);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Scores_at_either_end_of_the_scale_are_accepted(int score)
    {
        Assert.Empty(RatingRules.Validate(score, null));
    }

    [Fact]
    public void A_comment_may_be_up_to_1000_characters()
    {
        Assert.Empty(RatingRules.Validate(4, new string('a', 1000)));
        Assert.Contains("comment", RatingRules.Validate(4, new string('a', 1001)).Keys);
    }
}
