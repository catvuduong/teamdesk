using System.Text.RegularExpressions;

namespace Api.Kb;

/// <summary>
/// Knowledge base search, suggestions and article validation. Pure: no I/O, so it is unit-tested directly.
/// </summary>
public static partial class KbRules
{
    public const int SuggestionLimit = 3;
    public const int ExcerptLength = 160;

    private const int MinTermLength = 3;
    private const int TitleWeight = 2;
    private const int BodyWeight = 1;

    /// <summary>Words that match almost every article, so they are ignored when searching.</summary>
    private static readonly HashSet<string> Stopwords =
    [
        "after", "all", "and", "any", "are", "but", "can", "cannot", "could", "does", "did", "for", "from", "get",
        "got", "has", "have", "into", "its", "just", "not", "out", "our", "over", "some", "that", "the", "their",
        "this", "too", "very", "was", "were", "when", "where", "will", "with", "would", "you", "your",
    ];

    private static readonly Regex SlugPattern = SlugRegex();

    /// <summary>
    /// The words a search looks for: lowercase letters and digits, at least three long, without stopwords.
    /// Only letters and digits survive, so a term never carries a LIKE wildcard.
    /// </summary>
    public static IReadOnlyList<string> Terms(string? query) =>
        WordRegex().Matches(query ?? "")
            .Select(match => match.Value.ToLowerInvariant())
            .Where(word => word.Length >= MinTermLength && !Stopwords.Contains(word))
            .Distinct()
            .ToList();

    /// <summary>How well an article matches: each term counts once in the title (weighted higher) and once in the body.</summary>
    public static int Score(string title, string body, IReadOnlyList<string> terms)
    {
        var lowerTitle = title.ToLowerInvariant();
        var lowerBody = body.ToLowerInvariant();
        return terms.Sum(term =>
            (lowerTitle.Contains(term, StringComparison.Ordinal) ? TitleWeight : 0) +
            (lowerBody.Contains(term, StringComparison.Ordinal) ? BodyWeight : 0));
    }

    /// <summary>
    /// The best matches, most relevant first, then most viewed, then by title. Articles that match no term are left out.
    /// </summary>
    public static IReadOnlyList<ArticleRow> Rank(IEnumerable<ArticleRow> articles, IReadOnlyList<string> terms, int limit)
    {
        if (terms.Count == 0) return [];

        return articles
            .Select(article => (Article: article, Score: Score(article.Title, article.Body, terms)))
            .Where(match => match.Score > 0)
            .OrderByDescending(match => match.Score)
            .ThenByDescending(match => match.Article.Views)
            .ThenBy(match => match.Article.Title, StringComparer.Ordinal)
            .Take(limit)
            .Select(match => match.Article)
            .ToList();
    }

    /// <summary>
    /// A short plain-text preview: the first line of prose, without heading, code fence or table markers and list or quote
    /// prefixes, cut to <see cref="ExcerptLength"/> characters.
    /// </summary>
    public static string Excerpt(string body)
    {
        var line = body.Split('\n')
            .Select(text => text.Trim())
            .FirstOrDefault(text => text.Length > 0 && !text.StartsWith('#') && !text.StartsWith("```", StringComparison.Ordinal) && !text.StartsWith('|'))
            ?? "";

        var plain = ListPrefix().Replace(line, "").Replace("**", "").Replace("`", "").Trim();
        if (plain.Length <= ExcerptLength) return plain;
        return plain[..(ExcerptLength - 1)].TrimEnd() + "…";
    }

    /// <summary>Field errors for a new article. Every field is required.</summary>
    public static Dictionary<string, string> ValidateNew(string? slug, string? title, string? body, string? category) =>
        Validate(slug, title, body, category, partial: false);

    /// <summary>Field errors for an update. A null field is left as it is and not checked.</summary>
    public static Dictionary<string, string> ValidateChange(string? slug, string? title, string? body, string? category) =>
        Validate(slug, title, body, category, partial: true);

    private static Dictionary<string, string> Validate(string? slug, string? title, string? body, string? category, bool partial)
    {
        var errors = new Dictionary<string, string>();

        if (!partial || slug is not null)
        {
            var trimmed = slug?.Trim() ?? "";
            if (trimmed.Length == 0) errors["slug"] = "Slug is required";
            else if (trimmed.Length < 3 || trimmed.Length > 100 || !SlugPattern.IsMatch(trimmed))
                errors["slug"] = "Slug uses lowercase letters, digits and single hyphens, 3 to 100 characters";
        }

        if (!partial || title is not null)
        {
            var trimmed = title?.Trim() ?? "";
            if (trimmed.Length == 0) errors["title"] = "Title is required";
            else if (trimmed.Length > 140) errors["title"] = "Title must be 140 characters or fewer";
        }

        if (!partial || body is not null)
        {
            var trimmed = body?.Trim() ?? "";
            if (trimmed.Length == 0) errors["body"] = "Body is required";
            else if (trimmed.Length > 20000) errors["body"] = "Body must be 20000 characters or fewer";
        }

        if (!partial || category is not null)
        {
            var trimmed = category?.Trim() ?? "";
            if (trimmed.Length == 0) errors["category"] = "Category is required";
            else if (trimmed.Length > 60) errors["category"] = "Category must be 60 characters or fewer";
        }

        return errors;
    }

    [GeneratedRegex("[\\p{L}\\p{N}]+")]
    private static partial Regex WordRegex();

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();

    [GeneratedRegex(@"^(?:[-*+]\s+|\d+\.\s+|>\s*)")]
    private static partial Regex ListPrefix();
}
