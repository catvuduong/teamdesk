using Npgsql;

namespace Api;

/// <summary>
/// PostgreSQL access (Npgsql; use Dapper on the returned connection for queries).
/// Add every table as a CREATE TABLE IF NOT EXISTS statement to <see cref="Schema"/>; it runs once per process,
/// serialized by an advisory lock so concurrent instances cannot race.
/// </summary>
public sealed class Db
{
    private static readonly string[] Schema = [
        """
        CREATE TABLE IF NOT EXISTS users (
            id serial PRIMARY KEY,
            name text NOT NULL,
            email text NOT NULL UNIQUE,
            password_hash text NOT NULL,
            role text NOT NULL CHECK (role IN ('customer', 'agent', 'admin')),
            created_at timestamptz NOT NULL DEFAULT now()
        )
        """,
        "CREATE SEQUENCE IF NOT EXISTS ticket_number_seq START 1001",
        """
        CREATE TABLE IF NOT EXISTS tickets (
            id serial PRIMARY KEY,
            number int NOT NULL UNIQUE,
            title text NOT NULL,
            description text NOT NULL,
            status text NOT NULL CHECK (status IN ('open', 'in_progress', 'waiting', 'resolved', 'closed')),
            priority text NOT NULL CHECK (priority IN ('low', 'medium', 'high', 'urgent')),
            category text NOT NULL,
            requester_id int NOT NULL REFERENCES users (id),
            assignee_id int NULL REFERENCES users (id),
            created_at timestamptz NOT NULL DEFAULT now(),
            updated_at timestamptz NOT NULL DEFAULT now(),
            resolved_at timestamptz NULL
        )
        """,
        "CREATE INDEX IF NOT EXISTS tickets_status_idx ON tickets (status)",
        "CREATE INDEX IF NOT EXISTS tickets_requester_idx ON tickets (requester_id)",
        "CREATE INDEX IF NOT EXISTS tickets_assignee_idx ON tickets (assignee_id)",
        "CREATE INDEX IF NOT EXISTS tickets_created_idx ON tickets (created_at)",
        """
        CREATE TABLE IF NOT EXISTS comments (
            id serial PRIMARY KEY,
            ticket_id int NOT NULL REFERENCES tickets (id) ON DELETE CASCADE,
            author_id int NOT NULL REFERENCES users (id),
            body text NOT NULL,
            internal boolean NOT NULL DEFAULT false,
            created_at timestamptz NOT NULL DEFAULT now()
        )
        """,
        "CREATE INDEX IF NOT EXISTS comments_ticket_idx ON comments (ticket_id)",
        """
        CREATE TABLE IF NOT EXISTS ticket_events (
            id serial PRIMARY KEY,
            ticket_id int NOT NULL REFERENCES tickets (id) ON DELETE CASCADE,
            actor_id int NOT NULL REFERENCES users (id),
            type text NOT NULL,
            detail text NOT NULL,
            created_at timestamptz NOT NULL DEFAULT now()
        )
        """,
        "CREATE INDEX IF NOT EXISTS ticket_events_ticket_idx ON ticket_events (ticket_id)",
    ];

    private readonly NpgsqlDataSource? _source;
    private Task? _ready;

    static Db()
    {
        // Dapper maps snake_case columns (created_at) onto PascalCase members (CreatedAt)
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public Db()
    {
        var url = Environment.GetEnvironmentVariable("DATABASE_URL");
        _source = string.IsNullOrWhiteSpace(url) ? null : NpgsqlDataSource.Create(ToConnectionString(url));
    }

    public bool Enabled => _source is not null;

    public async Task<NpgsqlConnection> OpenAsync()
    {
        if (_source is null) throw new InvalidOperationException("DATABASE_URL is not set");
        await EnsureSchemaAsync();
        return await _source.OpenConnectionAsync();
    }

    public async Task<string> StatusAsync()
    {
        if (_source is null) return "disabled";
        try
        {
            await using var conn = await OpenAsync();
            await using var cmd = new NpgsqlCommand("SELECT 1", conn);
            await cmd.ExecuteScalarAsync();
            return "up";
        }
        catch
        {
            return "down";
        }
    }

    private Task EnsureSchemaAsync() => _ready ??= RunSchemaAsync();

    private async Task RunSchemaAsync()
    {
        try
        {
            await using var conn = await _source!.OpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();
            await new NpgsqlCommand("SELECT pg_advisory_xact_lock(727274)", conn, tx).ExecuteNonQueryAsync();
            foreach (var statement in Schema)
                await new NpgsqlCommand(statement, conn, tx).ExecuteNonQueryAsync();
            await tx.CommitAsync();
        }
        catch
        {
            _ready = null; // retry on the next call
            throw;
        }
    }

    /// <summary>Converts postgres://user:pass@host:5432/db?sslmode=require into an Npgsql connection string.</summary>
    public static string ToConnectionString(string url)
    {
        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var local = uri.Host is "localhost" or "127.0.0.1";
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port < 0 ? 5432 : uri.Port,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            Database = uri.AbsolutePath.TrimStart('/'),
            SslMode = query["sslmode"] is "disable" || (local && query["sslmode"] is null) ? SslMode.Disable : SslMode.Require,
        };
        return builder.ConnectionString;
    }
}
