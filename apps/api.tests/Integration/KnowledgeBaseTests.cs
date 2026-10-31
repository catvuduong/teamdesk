using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Integration;

public class KnowledgeBaseTests(WebApplicationFactory<Program> factory) : ApiTests(factory)
{
    /// <summary>A word no other article contains, so searches in these tests only find what the test wrote.</summary>
    private static string Term() => $"kb{Guid.NewGuid():N}";

    private static string Slug() => $"kb-{Guid.NewGuid():N}";

    [Fact]
    public async Task Drafts_are_not_listed_until_they_are_published()
    {
        var agent = await AgentTokenAsync();
        var term = Term();
        var id = await CreateArticleAsync(agent, Slug(), $"Guide {term}", published: false);

        var hidden = await JsonAsync(await SendAsync(HttpMethod.Get, $"/kb?q={term}"));
        Assert.Equal(0, hidden.GetArrayLength());

        (await SendAsync(HttpMethod.Patch, $"/kb/articles/{id}", agent, new { published = true })).EnsureSuccessStatusCode();

        var listed = await JsonAsync(await SendAsync(HttpMethod.Get, $"/kb?q={term}"));
        Assert.Equal(1, listed.GetArrayLength());
        Assert.Equal($"Guide {term}", listed[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task The_seeded_help_centre_answers_a_password_question()
    {
        // The new-ticket form's suggestions and the e2e test rely on this seeded article
        var results = await JsonAsync(await SendAsync(HttpMethod.Get, "/kb?q=password"));

        Assert.Contains(results.EnumerateArray(), article => article.GetProperty("slug").GetString() == "reset-your-password");
    }

    [Fact]
    public async Task Search_matches_words_in_the_title_and_the_body()
    {
        var agent = await AgentTokenAsync();
        var term = Term();
        var slug = Slug();
        await CreateArticleAsync(agent, slug, "Plain title", published: true, body: $"The body mentions {term} once.");

        var results = await JsonAsync(await SendAsync(HttpMethod.Get, $"/kb?q={term}"));

        Assert.Equal(slug, results[0].GetProperty("slug").GetString());
        Assert.Contains(term, results[0].GetProperty("excerpt").GetString());
    }

    [Fact]
    public async Task The_category_filter_keeps_only_that_category()
    {
        var agent = await AgentTokenAsync();
        var term = Term();
        var category = $"Category {Guid.NewGuid():N}";
        await CreateArticleAsync(agent, Slug(), $"Inside {term}", published: true, category: category);
        await CreateArticleAsync(agent, Slug(), $"Outside {term}", published: true, category: "Billing");

        var results = await JsonAsync(await SendAsync(HttpMethod.Get, $"/kb?q={term}&category={Uri.EscapeDataString(category)}"));

        Assert.Equal(1, results.GetArrayLength());
        Assert.Equal($"Inside {term}", results[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task Reading_an_article_by_slug_counts_a_view_each_time()
    {
        var agent = await AgentTokenAsync();
        var slug = Slug();
        await CreateArticleAsync(agent, slug, "Counted article", published: true);

        var first = await JsonAsync(await SendAsync(HttpMethod.Get, $"/kb/{slug}"));
        var second = await JsonAsync(await SendAsync(HttpMethod.Get, $"/kb/{slug}"));

        Assert.Equal(1, first.GetProperty("views").GetInt32());
        Assert.Equal(2, second.GetProperty("views").GetInt32());
    }

    [Fact]
    public async Task An_unpublished_article_cannot_be_read_by_slug()
    {
        var agent = await AgentTokenAsync();
        var slug = Slug();
        await CreateArticleAsync(agent, slug, "Unreleased", published: false);

        var response = await SendAsync(HttpMethod.Get, $"/kb/{slug}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Suggestions_return_at_most_three_published_articles_for_the_title()
    {
        var agent = await AgentTokenAsync();
        var term = Term();
        for (var i = 1; i <= 4; i++) await CreateArticleAsync(agent, Slug(), $"Fix {term} {i}", published: true);
        var draft = Slug();
        await CreateArticleAsync(agent, draft, $"Draft {term}", published: false);

        var suggestions = await JsonAsync(await SendAsync(HttpMethod.Get, $"/kb/suggest?q=Problem%20{term}"));

        Assert.Equal(3, suggestions.GetArrayLength());
        foreach (var item in suggestions.EnumerateArray())
        {
            Assert.Contains(term, item.GetProperty("title").GetString());
            Assert.NotEqual(draft, item.GetProperty("slug").GetString());
        }
    }

    [Fact]
    public async Task A_title_with_no_matching_words_gets_no_suggestions()
    {
        var suggestions = await JsonAsync(await SendAsync(HttpMethod.Get, "/kb/suggest?q=the%20and"));

        Assert.Equal(0, suggestions.GetArrayLength());
    }

    [Fact]
    public async Task Customers_cannot_create_or_edit_articles()
    {
        var customer = await RegisterCustomerAsync();
        var agent = await AgentTokenAsync();
        var id = await CreateArticleAsync(agent, Slug(), "Staff only", published: true);

        var create = await SendAsync(HttpMethod.Post, "/kb/articles", customer.Token, NewArticle(Slug(), "Nope"));
        var edit = await SendAsync(HttpMethod.Patch, $"/kb/articles/{id}", customer.Token, new { published = false });

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
    }

    [Fact]
    public async Task Anonymous_callers_cannot_create_articles()
    {
        var response = await SendAsync(HttpMethod.Post, "/kb/articles", null, NewArticle(Slug(), "Nope"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_slug_can_only_belong_to_one_article()
    {
        var agent = await AgentTokenAsync();
        var slug = Slug();
        await CreateArticleAsync(agent, slug, "First", published: true);

        var response = await SendAsync(HttpMethod.Post, "/kb/articles", agent, NewArticle(slug, "Second"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_articles_are_rejected_with_field_errors()
    {
        var agent = await AgentTokenAsync();

        var response = await SendAsync(HttpMethod.Post, "/kb/articles", agent, new { slug = "Not A Slug", title = "", body = "Body", category = "How-to" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await JsonAsync(response);
        Assert.True(json.GetProperty("errors").TryGetProperty("slug", out _));
        Assert.True(json.GetProperty("errors").TryGetProperty("title", out _));
    }

    [Fact]
    public async Task Editing_an_unknown_article_is_not_found()
    {
        var agent = await AgentTokenAsync();

        var response = await SendAsync(HttpMethod.Patch, "/kb/articles/999999999", agent, new { published = true });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static object NewArticle(string slug, string title, bool published = false) =>
        new { slug, title, body = "Steps to try.", category = "How-to", published };

    private async Task<int> CreateArticleAsync(string agentToken, string slug, string title, bool published, string body = "Steps to try.", string category = "How-to")
    {
        var response = await SendAsync(HttpMethod.Post, "/kb/articles", agentToken, new { slug, title, body, category, published });
        response.EnsureSuccessStatusCode();
        return (await JsonAsync(response)).GetProperty("id").GetInt32();
    }
}
