using System.Data.SQLite;
using Microsoft.Extensions.Logging;

namespace FediProfile.Services;

/// <summary>
/// LocalDbService provides SQLite database access for FediProfile.
/// It handles database initialization and multi-tenant support via domain-specific database files.
/// </summary>
public class LocalDbService
{
    private readonly string _connectionString;
    private readonly ILogger<LocalDbService>? _logger;

    public readonly string DbPath;

    /// <summary>
    /// Constructs a database file path using the DB_DATA environment variable if set,
    /// otherwise uses the current directory.
    /// </summary>
    /// <param name="dbFileName">The database filename (without directory path)</param>
    /// <returns>Full path to the database file</returns>
    public static string GetDbPath(string dbFileName)
    {
        var dbDataDir = Environment.GetEnvironmentVariable("DB_DATA");
        
        if (!string.IsNullOrEmpty(dbDataDir))
        {
            // Ensure the directory exists
            Directory.CreateDirectory(dbDataDir);
            return Path.Combine(dbDataDir, dbFileName);
        }
        
        // Fall back to App_Data directory
        return Path.Combine("App_Data", dbFileName);
    }

    public LocalDbService(string dbPath, ILogger<LocalDbService>? logger = null, bool autoCreate = true)
    {
        _logger = logger;
        
        if (string.IsNullOrEmpty(dbPath))
        {
            Log(LogLevel.Warning, "DB PATH CANNOT BE EMPTY");
            dbPath = "default.db";
        }

        // Check if dbPath is already a full/resolved path or just a bare filename
        if (Path.IsPathRooted(dbPath))
        {
            // It's already a full absolute path, use it as-is
            this.DbPath = dbPath;
        }
        else if (!string.IsNullOrEmpty(Path.GetDirectoryName(dbPath)))
        {
            // It already contains a directory component (e.g., "App_Data/localhost.db"),
            // meaning it was already resolved by GetDbPath — use as-is to avoid double-nesting
            this.DbPath = dbPath;
        }
        else
        {
            // It's just a bare filename, apply sanitization and resolve via GetDbPath
            dbPath = dbPath.Replace(" ", "").Replace(":", "_").Trim().ToLowerInvariant();
            this.DbPath = GetDbPath(dbPath);
        }
        
        this._connectionString = $"Data Source={DbPath};Version=3;";

        // NOTE: CreateDb() is NOT called here to avoid the virtual-call-in-constructor problem.
        // Derived classes (DomainScopedDb, UserScopedDb) must call EnsureCreated() at the end
        // of their own constructors, after their fields are initialized.
    }
    
    /// <summary>
    /// Checks if the database file exists on disk.
    /// </summary>
    public bool DatabaseExists()
    {
        return File.Exists(DbPath);
    }

    private void Log(LogLevel level, string message, params object[] args)
    {
        var structuredArgs = new List<object> { DbPath };
        structuredArgs.AddRange(args);
        
        var messageWithDbPath = "[DbPath: {DbPath}] " + message;
        
        if (_logger != null)
        {
            _logger.Log(level, messageWithDbPath, structuredArgs.ToArray());
        }
        else
        {
            // Fallback to Console when logger is not available
            Console.WriteLine($"[{level}] {messageWithDbPath}");
        }
    }

    public SQLiteConnection GetConnection()
    {
        Console.WriteLine(_connectionString);
        return new SQLiteConnection(_connectionString);
    }

    private static readonly HashSet<string> _migratedDatabases = new();

    /// <summary>
    /// Ensures the database file and tables are created.
    /// Must be called by derived classes at the end of their constructor,
    /// after all fields are initialized.
    /// </summary>
    protected void EnsureCreated()
    {
        // Ensure the directory exists
        var directory = Path.GetDirectoryName(DbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Create if not exists
        if (!File.Exists(DbPath))
        {
            using var connection = GetConnection();
            connection.Open();

            Console.WriteLine("Initializing new database at: " + DbPath);

            InitializeTables(connection);
            connection.Close();

            Log(LogLevel.Information, $"Database created at {DbPath}");
        }

        // Apply any pending migrations (per-database tracking)
        lock (_migratedDatabases)
        {
            if (!_migratedDatabases.Contains(DbPath))
            {
                _migratedDatabases.Add(DbPath);
                ApplyPendingMigrations();
            }
        }
    }

    /// <summary>
    /// Virtual method for initializing database tables.
    /// MUST be overridden in derived classes to provide appropriate schema.
    /// 
    /// DomainScopedDb: Creates domain-level tables (Users, Settings)
    /// UserScopedDb: Creates user-level tables (Links, ActorKeys, BadgeIssuers, ReceivedBadges, Followers, InboxMessages)
    /// </summary>
    protected virtual void InitializeTables(SQLiteConnection connection)
    {
        throw new NotImplementedException("InitializeTables must be implemented by derived classes (DomainScopedDb, UserScopedDb)");
    }

    protected virtual void ApplyPendingMigrations()
    {
        // Override in derived classes to apply schema-specific migrations
    }

    protected List<string> GetTableColumns(SQLiteConnection connection, string tableName)
    {
        var columns = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    public async Task ExecuteNonQueryAsync(string sql)
    {
        using var connection = GetConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> CanOpenAsync()
    {
        try
        {
            using var connection = GetConnection();
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Settings management methods
    public async Task<dynamic?> GetSettingsAsync()
    {
        using var connection = GetConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Settings WHERE Id = 1";

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var settings = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                settings[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            return settings;
        }

        return null;
    }

    public async Task<string> GetActorUsernameAsync()
    {
        var settings = await GetSettingsAsync();
        if (settings != null)
        {
            var settingsDict = (Dictionary<string, object>)settings;
            if (settingsDict.TryGetValue("ActorUsername", out var username))
            {
                return username?.ToString() ?? "profile";
            }
        }
        return "profile";
    }

    public async Task<string> GetActorBioAsync()
    {
        var settings = await GetSettingsAsync();
        if (settings != null)
        {
            var settingsDict = (Dictionary<string, object>)settings;
            if (settingsDict.TryGetValue("ActorBio", out var bio))
            {
                return bio?.ToString() ?? "A FediProfile instance";
            }
        }
        return "A FediProfile instance";
    }

    public async Task<string> GetActorAvatarUrlAsync()
    {
        var settings = await GetSettingsAsync();
        if (settings != null)
        {
            var settingsDict = (Dictionary<string, object>)settings;
            if (settingsDict.TryGetValue("ActorAvatarUrl", out var url))
            {
                return url?.ToString() ?? "/assets/avatar.png";
            }
        }
        return "/assets/avatar.png";
    }

    public async Task<string> GetUiThemeAsync()
    {
        var settings = await GetSettingsAsync();
        if (settings != null)
        {
            var settingsDict = (Dictionary<string, object>)settings;
            if (settingsDict.TryGetValue("UiTheme", out var theme))
            {
                return theme?.ToString() ?? "theme-classic.css";
            }
        }
        return "theme-classic.css";
    }

    // ===== STATIC UTILITIES =====

    /// <summary>
    /// Checks if a user slug is reserved and cannot be used.
    /// Reserved slugs prevent routing conflicts with system endpoints.
    /// </summary>
    public static bool IsReservedUserSlug(string slug)
    {
        // Normalize the slug for comparison
        var normalized = slug?.Trim().ToLowerInvariant() ?? string.Empty;

        // List of reserved userSlugs
        var reserved = new[] 
        { 
            "register", 
            "admin", 
            "login", 
            "logout", 
            "denied",
            "well-known",
            "api",
            "assets",
            "static",
            "inbox",
            "outbox",
            "followers",
            "following",
            "likes",
            "shares",
            "notifications",
            "featured",
            "search",
            "profile",
            "about",
            "settings",
            "help"
        };

        return reserved.Contains(normalized);
    }
}

