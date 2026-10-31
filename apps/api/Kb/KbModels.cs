namespace Api.Kb;

/// <summary>An article in a list or search result. <see cref="Excerpt"/> is plain text, not markdown.</summary>
public sealed record ArticleSummary(int Id, string Slug, string Title, string Category, string Excerpt, int Views, DateTime UpdatedAt);

public sealed record ArticleDetail(
    int Id,
    string Slug,
    string Title,
    string Body,
    string Category,
    bool Published,
    int Views,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateArticleRequest(string? Slug, string? Title, string? Body, string? Category, bool? Published);

/// <summary>A PATCH: every field is optional, and a field that is not sent is left as it is.</summary>
public sealed record UpdateArticleRequest(string? Slug, string? Title, string? Body, string? Category, bool? Published);

/// <summary>Flat row for article queries; <see cref="ToSummary"/> and <see cref="ToDetail"/> shape it for clients.</summary>
public sealed class ArticleRow
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Category { get; set; } = "";
    public bool Published { get; set; }
    public int Views { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ArticleSummary ToSummary() => new(Id, Slug, Title, Category, KbRules.Excerpt(Body), Views, UpdatedAt);

    public ArticleDetail ToDetail() => new(Id, Slug, Title, Body, Category, Published, Views, CreatedAt, UpdatedAt);
}
