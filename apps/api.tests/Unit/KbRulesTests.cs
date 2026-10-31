using Api.Kb;

namespace Api.Tests.Unit;

public class KbRulesTests
{
    private static ArticleRow Article(string title, string body, int views = 0) =>
        new() { Title = title, Body = body, Views = views };

    [Fact]
    public void Terms_are_lowercase_words_without_stopwords_or_short_words()
    {
        Assert.Equal(["log", "password", "reset"], KbRules.Terms("Cannot log in after Password reset"));
    }

    [Fact]
    public void Terms_keep_only_letters_and_digits_so_no_like_wildcard_survives()
    {
        Assert.Equal(["100", "percent"], KbRules.Terms("100% percent_"));
    }

    [Fact]
    public void Repeated_terms_are_counted_once()
    {
        Assert.Equal(["reset"], KbRules.Terms("reset RESET reset"));
    }

    [Fact]
    public void A_title_match_outweighs_a_body_match()
    {
        var terms = KbRules.Terms("password");

        Assert.Equal(2, KbRules.Score("Reset your password", "Use the link.", terms));
        Assert.Equal(1, KbRules.Score("Other", "Enter your password", terms));
        Assert.Equal(0, KbRules.Score("Other", "Nothing relevant", terms));
    }

    [Fact]
    public void Rank_puts_the_best_match_first_and_leaves_out_articles_that_match_nothing()
    {
        var articles = new[]
        {
            Article("Account locked", "Enter your password to unlock."),
            Article("Reset your password", "Use the link."),
            Article("Invoices", "Download a PDF."),
        };

        var ranked = KbRules.Rank(articles, KbRules.Terms("password reset"), 10);

        Assert.Equal(["Reset your password", "Account locked"], ranked.Select(article => article.Title));
    }

    [Fact]
    public void Rank_breaks_ties_by_views_then_title()
    {
        var articles = new[]
        {
            Article("Beta password", "x", views: 1),
            Article("Alpha password", "x", views: 1),
            Article("Gamma password", "x", views: 9),
        };

        var ranked = KbRules.Rank(articles, KbRules.Terms("password"), 10);

        Assert.Equal(["Gamma password", "Alpha password", "Beta password"], ranked.Select(article => article.Title));
    }

    [Fact]
    public void Rank_returns_no_more_than_the_limit()
    {
        var articles = Enumerable.Range(1, 5).Select(n => Article($"Password {n}", "x")).ToList();

        Assert.Equal(3, KbRules.Rank(articles, KbRules.Terms("password"), KbRules.SuggestionLimit).Count);
    }

    [Fact]
    public void Rank_with_no_usable_terms_matches_nothing()
    {
        var articles = new[] { Article("Password", "x") };

        Assert.Empty(KbRules.Rank(articles, KbRules.Terms("the"), 10));
    }

    [Fact]
    public void Excerpt_is_the_first_line_of_prose_without_markdown_markers()
    {
        var body = "# Title\n\n- **Urgent:** first response in 1 hour";

        Assert.Equal("Urgent: first response in 1 hour", KbRules.Excerpt(body));
    }

    [Fact]
    public void Long_excerpts_are_cut_to_the_limit_with_an_ellipsis()
    {
        var excerpt = KbRules.Excerpt(string.Concat(Enumerable.Repeat("word ", 100)));

        Assert.True(excerpt.Length <= KbRules.ExcerptLength);
        Assert.EndsWith("…", excerpt);
    }

    [Fact]
    public void A_complete_new_article_has_no_errors()
    {
        var errors = KbRules.ValidateNew("reset-your-password", "Reset your password", "Body text.", "Login & access");

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("Bad Slug!")]
    [InlineData("ab")]
    [InlineData("double--hyphen")]
    [InlineData("-leading")]
    public void Slugs_are_lowercase_words_joined_by_single_hyphens(string slug)
    {
        Assert.Contains("slug", KbRules.ValidateNew(slug, "Title", "Body", "How-to").Keys);
    }

    [Fact]
    public void A_new_article_needs_every_field()
    {
        var errors = KbRules.ValidateNew(null, "", "  ", null);

        Assert.Equal(["slug", "title", "body", "category"], errors.Keys);
    }

    [Fact]
    public void An_update_only_checks_the_fields_it_sends()
    {
        Assert.Empty(KbRules.ValidateChange(null, null, null, null));
        Assert.Contains("title", KbRules.ValidateChange(null, "", null, null).Keys);
    }

    [Fact]
    public void Titles_longer_than_140_characters_are_rejected()
    {
        var errors = KbRules.ValidateNew("valid-slug", new string('a', 141), "Body", "How-to");

        Assert.Contains("title", errors.Keys);
    }
}
