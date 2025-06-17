using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BadgeFed.Services;

public class Migration
{
    public string Version { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsApplied { get; set; }
    public DateTime? AppliedAt { get; set; }
    public string? Checksum { get; set; }
}

public class DatabaseMigrationService
{
    private readonly LocalDbService _dbService;
    private readonly string _migrationsPath;

    public DatabaseMigrationService(LocalDbService dbService)
    {
        _dbService = dbService;
        _migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "assets", "migrations");
    }

    /// <summary>
    /// Gets all available migrations from the filesystem
    /// </summary>
    public List<Migration> GetAvailableMigrations()
    {
        var migrations = new List<Migration>();
        
        if (!Directory.Exists(_migrationsPath))
        {
            Directory.CreateDirectory(_migrationsPath);
            return migrations;
        }

        var files = Directory.GetFiles(_migrationsPath, "*.sql")
            .OrderBy(f => f)
            .ToArray();

        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var parts = fileName.Split('_', 2);
            
            if (parts.Length >= 2)
            {
                var version = parts[0];
                var name = parts[1].Replace('_', ' ');
                var content = File.ReadAllText(file);
                var checksum = GenerateChecksum(content);

                migrations.Add(new Migration
                {
                    Version = version,
                    Name = name,
                    FilePath = file,
                    Content = content,
                    Checksum = checksum
                });
            }
        }

        return migrations;
    }

    /// <summary>
    /// Gets applied migrations from the database
    /// </summary>
    public List<Migration> GetAppliedMigrations()
    {
        var migrations = new List<Migration>();
        
        using var connection = _dbService.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Version, Name, AppliedAt, Checksum FROM DbMigrations ORDER BY Version";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            migrations.Add(new Migration
            {
                Version = reader.GetString(reader.GetOrdinal("Version")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                IsApplied = true,
                AppliedAt = reader.GetDateTime(reader.GetOrdinal("AppliedAt")),
                Checksum = reader["Checksum"] as string
            });
        }

        return migrations;
    }

    /// <summary>
    /// Gets all migrations with their status (applied/pending)
    /// </summary>
    public List<Migration> GetMigrationsWithStatus()
    {
        var availableMigrations = GetAvailableMigrations();
        var appliedMigrations = GetAppliedMigrations();
        
        var appliedVersions = appliedMigrations.ToDictionary(m => m.Version, m => m);

        foreach (var migration in availableMigrations)
        {
            if (appliedVersions.TryGetValue(migration.Version, out var applied))
            {
                migration.IsApplied = true;
                migration.AppliedAt = applied.AppliedAt;
                
                // Check if checksum matches (file hasn't changed after migration)
                if (applied.Checksum != migration.Checksum && !string.IsNullOrEmpty(applied.Checksum))
                {
                    migration.Name += " (⚠️ FILE MODIFIED)";
                }
            }
        }

        return availableMigrations.OrderBy(m => CompareVersions(m.Version, "0.0.0")).ToList();
    }

    /// <summary>
    /// Gets pending migrations that need to be applied
    /// </summary>
    public List<Migration> GetPendingMigrations()
    {
        return GetMigrationsWithStatus()
            .Where(m => !m.IsApplied)
            .OrderBy(m => CompareVersions(m.Version, "0.0.0"))
            .ToList();
    }

    /// <summary>
    /// Gets the current database version (highest applied migration)
    /// </summary>
    public string GetCurrentDatabaseVersion()
    {
        var appliedMigrations = GetAppliedMigrations();
        if (!appliedMigrations.Any())
            return "1.0.0"; // Initial version

        return appliedMigrations
            .OrderByDescending(m => CompareVersions(m.Version, "0.0.0"))
            .First().Version;
    }

    /// <summary>
    /// Applies all pending migrations
    /// </summary>
    public async Task<(bool Success, string Message, List<string> AppliedMigrations)> ApplyPendingMigrations()
    {
        var pendingMigrations = GetPendingMigrations();
        var appliedMigrations = new List<string>();
        
        if (!pendingMigrations.Any())
        {
            return (true, "No pending migrations to apply.", appliedMigrations);
        }

        try
        {
            foreach (var migration in pendingMigrations)
            {
                await ApplyMigration(migration);
                appliedMigrations.Add($"{migration.Version}: {migration.Name}");
            }

            return (true, $"Successfully applied {appliedMigrations.Count} migration(s).", appliedMigrations);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to apply migrations: {ex.Message}", appliedMigrations);
        }
    }

    /// <summary>
    /// Applies a specific migration
    /// </summary>
    public async Task<bool> ApplyMigration(Migration migration)
    {
        try
        {
            using var connection = _dbService.GetConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            // Split the migration content by semicolon and execute each statement
            var statements = SplitSqlStatements(migration.Content);

            foreach (var statement in statements)
            {
                if (!string.IsNullOrWhiteSpace(statement))
                {
                    var command = connection.CreateCommand();
                    command.CommandText = statement;
                    command.Transaction = transaction;
                    command.ExecuteNonQuery();
                }
            }

            // Record the migration as applied
            var recordCommand = connection.CreateCommand();
            recordCommand.Transaction = transaction;
            recordCommand.CommandText = @"
                INSERT INTO DbMigrations (Version, Name, AppliedAt, Checksum)
                VALUES (@Version, @Name, CURRENT_TIMESTAMP, @Checksum)";
            recordCommand.Parameters.AddWithValue("@Version", migration.Version);
            recordCommand.Parameters.AddWithValue("@Name", migration.Name);
            recordCommand.Parameters.AddWithValue("@Checksum", migration.Checksum);
            recordCommand.ExecuteNonQuery();

            transaction.Commit();
            
            Console.WriteLine($"Successfully applied migration: {migration.Version} - {migration.Name}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to apply migration {migration.Version}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Re-applies a specific migration (for testing/fixing)
    /// </summary>
    public async Task<bool> ReapplyMigration(string version)
    {
        var migrations = GetMigrationsWithStatus();
        var migration = migrations.FirstOrDefault(m => m.Version == version);
        
        if (migration == null)
        {
            throw new ArgumentException($"Migration {version} not found.");
        }

        try
        {
            using var connection = _dbService.GetConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            // Remove existing migration record
            var deleteCommand = connection.CreateCommand();
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM DbMigrations WHERE Version = @Version";
            deleteCommand.Parameters.AddWithValue("@Version", version);
            deleteCommand.ExecuteNonQuery();

            transaction.Commit();

            // Apply the migration again
            return await ApplyMigration(migration);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to reapply migration {version}: {ex.Message}");
            throw;
        }
    }

    private string GenerateChecksum(string content)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hash);
    }

    private List<string> SplitSqlStatements(string sql)
    {
        var statements = new List<string>();
        var currentStatement = new StringBuilder();
        var inComment = false;
        var inString = false;
        char stringDelimiter = '\0';

        for (int i = 0; i < sql.Length; i++)
        {
            var currentChar = sql[i];
            var nextChar = i + 1 < sql.Length ? sql[i + 1] : '\0';

            if (!inString && !inComment)
            {
                // Check for comment start
                if (currentChar == '-' && nextChar == '-')
                {
                    inComment = true;
                    i++; // Skip next character
                    continue;
                }

                // Check for string start
                if (currentChar == '\'' || currentChar == '"')
                {
                    inString = true;
                    stringDelimiter = currentChar;
                    currentStatement.Append(currentChar);
                    continue;
                }

                // Check for statement separator
                if (currentChar == ';')
                {
                    var statement = currentStatement.ToString().Trim();
                    if (!string.IsNullOrEmpty(statement))
                    {
                        statements.Add(statement);
                    }
                    currentStatement.Clear();
                    continue;
                }
            }
            else if (inComment)
            {
                // End comment on newline
                if (currentChar == '\n' || currentChar == '\r')
                {
                    inComment = false;
                }
                continue;
            }
            else if (inString)
            {
                currentStatement.Append(currentChar);
                
                // Check for string end (handle escaped quotes)
                if (currentChar == stringDelimiter)
                {
                    if (nextChar == stringDelimiter)
                    {
                        // Escaped quote, add both characters
                        currentStatement.Append(nextChar);
                        i++; // Skip next character
                    }
                    else
                    {
                        // End of string
                        inString = false;
                        stringDelimiter = '\0';
                    }
                }
                continue;
            }

            currentStatement.Append(currentChar);
        }

        // Add any remaining statement
        var finalStatement = currentStatement.ToString().Trim();
        if (!string.IsNullOrEmpty(finalStatement))
        {
            statements.Add(finalStatement);
        }

        return statements;
    }

    private int CompareVersions(string version1, string version2)
    {
        var v1Parts = version1.Split('.').Select(int.Parse).ToArray();
        var v2Parts = version2.Split('.').Select(int.Parse).ToArray();

        var maxLength = Math.Max(v1Parts.Length, v2Parts.Length);

        for (int i = 0; i < maxLength; i++)
        {
            var v1Part = i < v1Parts.Length ? v1Parts[i] : 0;
            var v2Part = i < v2Parts.Length ? v2Parts[i] : 0;

            var comparison = v1Part.CompareTo(v2Part);
            if (comparison != 0)
                return comparison;
        }

        return 0;
    }
}
