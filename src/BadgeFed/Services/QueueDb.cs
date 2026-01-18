using Microsoft.Extensions.Logging;
using System;
using System.Data.SQLite;
using System.Text.Json;
using BadgeFed.Models;

namespace BadgeFed.Services;

public class QueueDb
{
    private readonly string connectionString;

    private readonly ILogger<QueueDb>? _logger;

    public readonly string DbPath;

    public static string GetDbPath(string dbFileName)
    {
        var dbDataDir = Environment.GetEnvironmentVariable("DB_DATA");
        
        if (!string.IsNullOrEmpty(dbDataDir))
        {
            // Ensure the directory exists
            Directory.CreateDirectory(dbDataDir);
            return Path.Combine(dbDataDir, dbFileName);
        }
        
        // Fall back to current directory
        return dbFileName;
    }

    private bool IsDbHealthy()
    {
        // check if table Jobs exists
        using var connection = GetConnection();
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Jobs';";
        var result = command.ExecuteScalar();
        if (result == null)
        {
            Log(LogLevel.Error, "Jobs table does not exist in Queue DB at {DatabasePath}", DbPath);
        }
        else
        {
            Log(LogLevel.Information, "Queue DB at {DatabasePath} is healthy", DbPath);
        }

        connection.Close();

        return result != null;
    }

    private void CreateDb()
    {
        // create if not exists
        if (!File.Exists(DbPath) || !IsDbHealthy())
        {
            Log(LogLevel.Information, "Creating Queue Database at {DatabasePath}...", DbPath);
            using var connection = GetConnection();
            connection.Open();

            var command = connection.CreateCommand();
            //read from init.sql
            var sql = File.ReadAllText("assets/queuedb/1.0.0_init.queue.sql");
            command.CommandText = sql;
            command.ExecuteNonQuery();
            connection.Close();

            Log(LogLevel.Information, "Database created at {DatabasePath}", DbPath);
        } else
        {
            Log(LogLevel.Information, "Database already exists at {DatabasePath}", DbPath);
        }
    }

    public QueueDb(string dbPath, ILogger<QueueDb>? logger = null)
    {
        _logger = logger;
        
        if (string.IsNullOrEmpty(dbPath))
        {
            Log(LogLevel.Warning, "DB PATH CANNOT BE EMPTY");
            dbPath = "default-queue.db";
        }

        if (Path.IsPathRooted(dbPath))
        {
            // It's already a full path, use it as-is
            this.DbPath = dbPath;
        }
        else
        {
            // It's just a filename, apply transformations and use GetDbPath
            dbPath = dbPath.Replace(" ", "").Replace(":", "_").Trim().ToLowerInvariant();
            this.DbPath = GetDbPath(dbPath);
        }
        
        this.connectionString = $"Data Source={DbPath};Version=3;";

        CreateDb();
    }

    public QueueDb(IHttpContextAccessor httpContextAccessor)
        : this(
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SQLITE_DB_PATH"))
                ? Environment.GetEnvironmentVariable("SQLITE_DB_PATH") + "-queue.db"
                : httpContextAccessor.HttpContext?.Request?.Host.Host + "-queue.db"
          )
    {
        
    }

    public SQLiteConnection GetConnection()
    {
        return new SQLiteConnection(connectionString);
    }

    private void Log(LogLevel level, string message, params object[] args)
    {
        _logger?.Log(level, message, args);
    }
}
