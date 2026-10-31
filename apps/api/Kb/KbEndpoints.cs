using Api.Auth;
using Dapper;
using Npgsql;

namespace Api.Kb;

public static class KbEndpoints
{
    /// <summary>Search results are capped so a broad query stays small. Suggestions use <see cref="KbRules.SuggestionLimit"/>.</summary>
    private const int SearchLimit = 50;

    private const string Columns = "id, slug, title, body, category, published, views, created_at, updated_at";

    public static void MapKnowledgeEndpoints(this IEndpointRouteBuilder app)
    {
        // Reading is public: customers and visitors only ever see published articles
        var kb = app.MapGroup("/kb").RequireDatabase();
        kb.MapGet("/", ListArticles);
        kb.MapGet("/suggest", Suggest);
        kb.MapGet("/{slug}", GetArticle);
        kb.MapPost("/articles", CreateArticle).RequireAuthorization(AuthSetup.StaffPolicy);
        kb.MapPatch("/articles/{id:int}", UpdateArticle).RequireAuthorization(AuthSetup.StaffPolicy);
    }

    /// <summary>Published articles, optionally filtered by category and searched by <c>q</c> (most relevant first).</summary>
    private static async Task<IResult> ListArticles(Db db, string? q, string? category)
    {
        var terms = KbRules.Terms(q);
        if (!string.IsNullOrWhiteSpace(q) && terms.Count == 0) return Results.Ok(Array.Empty<ArticleSummary>());

        var categoryFilter = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        await using var conn = await db.OpenAsync();

        if (terms.Count == 0)
        {
            var all = await MatchingAsync(conn, [], categoryFilter, "ORDER BY category, title");
            return Results.Ok(all.Select(article => article.ToSummary()).ToList());
        }

        var matches = await MatchingAsync(conn, terms, categoryFilter, "");
        return Results.Ok(KbRules.Rank(matches, terms, SearchLimit).Select(article => article.ToSummary()).ToList());
    }

    /// <summary>Up to three published articles for a ticket title, for the new-ticket form.</summary>
    private static async Task<IResult> Suggest(Db db, string? q)
    {
        var terms = KbRules.Terms(q);
        if (terms.Count == 0) return Results.Ok(Array.Empty<ArticleSummary>());

        await using var conn = await db.OpenAsync();
        var matches = await MatchingAsync(conn, terms, null, "");
        return Results.Ok(KbRules.Rank(matches, terms, KbRules.SuggestionLimit).Select(article => article.ToSummary()).ToList());
    }

    /// <summary>One published article. Each read counts as a view, in the same statement that returns the article.</summary>
    private static async Task<IResult> GetArticle(string slug, Db db)
    {
        await using var conn = await db.OpenAsync();
        var article = await conn.QuerySingleOrDefaultAsync<ArticleRow>(
            $"UPDATE articles SET views = views + 1 WHERE slug = @slug AND published RETURNING {Columns}",
            new { slug });

        return article is null ? Errors.Problem(StatusCodes.Status404NotFound, "Article not found") : Results.Ok(article.ToDetail());
    }

    private static async Task<IResult> CreateArticle(CreateArticleRequest body, Db db)
    {
        var errors = KbRules.ValidateNew(body.Slug, body.Title, body.Body, body.Category);
        if (errors.Count > 0) return Errors.Validation(errors);

        await using var conn = await db.OpenAsync();
        try
        {
            var article = await conn.QuerySingleAsync<ArticleRow>(
                $"""
                INSERT INTO articles (slug, title, body, category, published, created_at, updated_at)
                VALUES (@slug, @title, @body, @category, @published, @now, @now)
                RETURNING {Columns}
                """,
                new
                {
                    slug = body.Slug!.Trim(),
                    title = body.Title!.Trim(),
                    body = body.Body!.Trim(),
                    category = body.Category!.Trim(),
                    published = body.Published ?? false,
                    now = DateTime.UtcNow,
                });
            return Results.Created($"/kb/{article.Slug}", article.ToDetail());
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Errors.Problem(StatusCodes.Status409Conflict, "An article with this slug already exists");
        }
    }

    /// <summary>Edits any of the article's fields. A field that is not sent keeps its value.</summary>
    private static async Task<IResult> UpdateArticle(int id, UpdateArticleRequest body, Db db)
    {
        var errors = KbRules.ValidateChange(body.Slug, body.Title, body.Body, body.Category);
        if (errors.Count > 0) return Errors.Validation(errors);

        await using var conn = await db.OpenAsync();
        try
        {
            var article = await conn.QuerySingleOrDefaultAsync<ArticleRow>(
                $"""
                UPDATE articles
                SET slug = COALESCE(@slug::text, slug),
                    title = COALESCE(@title::text, title),
                    body = COALESCE(@body::text, body),
                    category = COALESCE(@category::text, category),
                    published = COALESCE(@published::boolean, published),
                    updated_at = @now
                WHERE id = @id
                RETURNING {Columns}
                """,
                new
                {
                    id,
                    slug = body.Slug?.Trim(),
                    title = body.Title?.Trim(),
                    body = body.Body?.Trim(),
                    category = body.Category?.Trim(),
                    published = body.Published,
                    now = DateTime.UtcNow,
                });

            return article is null ? Errors.Problem(StatusCodes.Status404NotFound, "Article not found") : Results.Ok(article.ToDetail());
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Errors.Problem(StatusCodes.Status409Conflict, "An article with this slug already exists");
        }
    }

    /// <summary>
    /// Published articles that contain at least one term in the title or body. Each term is its own parameter, so no
    /// user text reaches the SQL itself. With no terms, every published article in the category comes back.
    /// </summary>
    private static async Task<IReadOnlyList<ArticleRow>> MatchingAsync(
        NpgsqlConnection conn,
        IReadOnlyList<string> terms,
        string? category,
        string orderBy)
    {
        var where = new List<string> { "published" };
        var parameters = new DynamicParameters();

        if (category is not null)
        {
            where.Add("category = @category");
            parameters.Add("category", category);
        }

        if (terms.Count > 0)
        {
            var clauses = terms.Select((term, index) =>
            {
                parameters.Add($"term{index}", $"%{term}%");
                return $"title ILIKE @term{index} OR body ILIKE @term{index}";
            });
            where.Add($"({string.Join(" OR ", clauses)})");
        }

        var rows = await conn.QueryAsync<ArticleRow>(
            $"SELECT {Columns} FROM articles WHERE {string.Join(" AND ", where)} {orderBy}",
            parameters);
        return rows.ToList();
    }
}
