using System.Data.SQLite;

namespace BadgeFed.Services;

/// <summary>
/// A dedicated SQLite database for federation analytics, separate from the main domain DB
/// to avoid "database is locked" errors from concurrent writes.
/// Database file: {domain}_analytics.db
/// </summary>
public class AnalyticsDbService
{
    private readonly string _connectionString;
    public readonly string DbPath;

    public AnalyticsDbService(string dbPath)
    {
        if (string.IsNullOrEmpty(dbPath))
        {
            dbPath = "default_analytics.db";
        }

        if (Path.IsPathRooted(dbPath))
        {
            this.DbPath = dbPath;
        }
        else
        {
            dbPath = dbPath.Replace(" ", "").Replace(":", "_").Trim().ToLowerInvariant();
            this.DbPath = LocalDbService.GetDbPath(dbPath);
        }

        _connectionString = $"Data Source={DbPath};Version=3;";
        EnsureCreated();
    }

    public SQLiteConnection GetConnection()
    {
        return new SQLiteConnection(_connectionString);
    }

    private void EnsureCreated()
    {
        if (!File.Exists(DbPath))
        {
            using var connection = GetConnection();
            connection.Open();
            CreateSchema(connection);
            connection.Close();
        }
        else
        {
            // Ensure schema exists even if file was created empty
            using var connection = GetConnection();
            connection.Open();
            CreateSchema(connection);
            connection.Close();
        }
    }

    private static void CreateSchema(SQLiteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS FederationEvent (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EventType TEXT NOT NULL,
                ActorUri TEXT,
                ObjectUri TEXT,
                TargetUri TEXT,
                RemoteHost TEXT,
                RequestIp TEXT,
                UserAgent TEXT,
                GroupId TEXT,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
            );

            CREATE INDEX IF NOT EXISTS idx_federation_event_type ON FederationEvent(EventType);
            CREATE INDEX IF NOT EXISTS idx_federation_event_created ON FederationEvent(CreatedAt);
            CREATE INDEX IF NOT EXISTS idx_federation_event_actor ON FederationEvent(ActorUri);
            CREATE INDEX IF NOT EXISTS idx_federation_event_group ON FederationEvent(GroupId);
        ";
        command.ExecuteNonQuery();
    }
}

/// <summary>
/// Scoped analytics DB service that derives the database path from the current HTTP request's domain.
/// </summary>
public class ScopedAnalyticsDbService : AnalyticsDbService
{
    public ScopedAnalyticsDbService(IHttpContextAccessor httpContextAccessor)
        : base(ResolvePath(httpContextAccessor))
    {
    }

    private static string ResolvePath(IHttpContextAccessor httpContextAccessor)
    {
        var envPath = Environment.GetEnvironmentVariable("SQLITE_DB_PATH");
        if (!string.IsNullOrEmpty(envPath))
            return envPath + "_analytics.db";

        var host = httpContextAccessor.HttpContext?.Request?.Host.Host;
        return (host ?? "default") + "_analytics.db";
    }
}
