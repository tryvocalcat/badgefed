using System.Data.SQLite;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BadgeFed.Services;

public sealed class StorageHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!File.Exists("init.sql")
            || !File.Exists(Path.Combine("assets", "queuedb", "1.0.0_init.queue.sql"))
            || !Directory.Exists(Path.Combine("assets", "migrations"))
            || !Directory.EnumerateFiles(Path.Combine("assets", "migrations"), "*.sql").Any())
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Database initialization assets are missing."));
        }

        var domains = configuration.GetSection("BadgesDomains").Get<string[]>();
        if (domains is not { Length: > 0 })
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("No badge domains are configured."));
        }

        foreach (var domainRaw in domains)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var domain = domainRaw.Split(':')[0];
            var db = new LocalScopedDb(domain);

            if (new DatabaseMigrationService(db).GetPendingMigrations().Count != 0)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Database migrations are pending."));
            }

            using (var connection = db.GetConnection())
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM Actor WHERE Username = '_relaybot'";
                if (Convert.ToInt64(command.ExecuteScalar()) == 0)
                {
                    return Task.FromResult(HealthCheckResult.Unhealthy("Relay initialization is incomplete."));
                }
            }

            // Use the same initialization and filenames as the worker, including on a fresh instance.
            var queue = new QueueDb(domain + "-queue.db");
            var analytics = new AnalyticsDbService(domain + "_analytics.db");
            CheckDatabase(db.GetConnection(), "SELECT Id FROM BadgeRecord LIMIT 0");
            CheckDatabase(queue.GetConnection(), "SELECT Id FROM Jobs LIMIT 0");
            CheckDatabase(analytics.GetConnection(), "SELECT Id FROM FederationEvent LIMIT 0");

            var directory = Path.GetDirectoryName(Path.GetFullPath(db.DbPath))!;
            using var probe = new FileStream(
                Path.Combine(directory, $".health-{Guid.NewGuid():N}"),
                FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
            probe.WriteByte(0);
            probe.Flush(flushToDisk: true);
        }

        return Task.FromResult(HealthCheckResult.Healthy());
    }

    private static void CheckDatabase(SQLiteConnection connection, string schemaQuery)
    {
        using (connection)
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = schemaQuery;
            command.ExecuteNonQuery();
            command.CommandText = "PRAGMA quick_check(1)";
            if (!string.Equals(command.ExecuteScalar()?.ToString(), "ok", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("SQLite integrity check failed.");
            }

            // Exercise a real database write without committing any schema or data changes.
            using var transaction = connection.BeginTransaction();
            command.Transaction = transaction;
            command.CommandText = "PRAGMA user_version";
            var version = Convert.ToInt32(command.ExecuteScalar());
            command.CommandText = $"PRAGMA user_version = {version}";
            command.ExecuteNonQuery();
            transaction.Rollback();
        }
    }
}
