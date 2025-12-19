using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using System;
using System.Data.SQLite;
using System.Text.Json;
using BadgeFed.Models;

namespace BadgeFed.Services;

public class LocalDbService
{
    private readonly string connectionString;
    private readonly ILogger<LocalDbService>? _logger;

    public readonly string DbPath;

    public LocalDbService(string dbPath, ILogger<LocalDbService>? logger = null)
    {
        _logger = logger;
        
        if (string.IsNullOrEmpty(dbPath))
        {
            Log(LogLevel.Warning, "DB PATH CANNOT BE EMPTY");
            dbPath = "default.db";
        }
      //  Log(LogLevel.Information, "Initializing LocalDbService with path: {dbPath}", dbPath);

        dbPath = dbPath.Replace(" ", "").Replace(":", "_").Trim().ToLowerInvariant();

        this.DbPath = dbPath;
        this.connectionString = $"Data Source={DbPath};Version=3;";

        CreateDb();
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
            var formattedMessage = messageWithDbPath;
            for (int i = 0; i < structuredArgs.Count; i++)
            {
                formattedMessage = formattedMessage.Replace($"{{{GetParameterName(i)}}}", structuredArgs[i]?.ToString() ?? "null");
            }
            Console.WriteLine($"[{level}] {formattedMessage}");
        }
    }

    private string GetParameterName(int index)
    {
        return index switch
        {
            0 => "DbPath",
            _ => $"Param{index}"
        };
    }

    public SQLiteConnection GetConnection()
    {
        return new SQLiteConnection(connectionString);
    }

    private static bool hasAppliedMigrationsTried = false;

    private void CreateDb()
    {
        // create if not exists
        if (!File.Exists(DbPath))
        {
            using var connection = GetConnection();
            connection.Open();

            var command = connection.CreateCommand();
            //read from init.sql
            var sql = File.ReadAllText("init.sql");
            command.CommandText = sql;
            command.ExecuteNonQuery();
            connection.Close();

            Log(LogLevel.Information, "Database created at {DatabasePath}", DbPath);
        }

        // Apply any pending migrations
        if (!hasAppliedMigrationsTried)
        {
            hasAppliedMigrationsTried = true;
            ApplyPendingMigrations();
        }
    }

    private void ApplyPendingMigrations()
    {
        try
        {
            var migrationService = new DatabaseMigrationService(this);
            var pendingMigrations = migrationService.GetPendingMigrations();

            if (pendingMigrations.Any())
            {
                Log(LogLevel.Information, $"Found {pendingMigrations.Count} pending migrations. Applying...");

                foreach (var migration in pendingMigrations)
                {
                    Log(LogLevel.Information, $"Applying migration: {migration.Version} - {migration.Name}");
                    var task = migrationService.ApplyMigration(migration);
                    task.Wait(); // Wait for async operation to complete
                    Log(LogLevel.Information, $"Successfully applied migration: {migration.Version}");
                }

                Log(LogLevel.Information, "All pending migrations applied successfully.");
            }
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Error applying migrations: {ex.Message}");
            // Don't throw here to avoid breaking database initialization
        }
    }

    public void UpsertFollowedIssuer(FollowedIssuer issuer)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        if (issuer.Id == 0)
        {
            command.CommandText = @"
                INSERT INTO FollowedIssuer (Name, Url, Inbox, Outbox, ActorId, AvatarUri, CreatedAt, UpdatedAt)
                VALUES (@Name, @Url, @Inbox, @Outbox, @ActorId, @AvatarUri, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                ON CONFLICT(Url, ActorId) DO UPDATE SET
                    Name = @Name,
                    Inbox = @Inbox,
                    Outbox = @Outbox,
                    AvatarUri = @AvatarUri,
                    UpdatedAt = CURRENT_TIMESTAMP;
                SELECT last_insert_rowid();
            ";
        }
        else
        {
            command.CommandText = @"
                UPDATE FollowedIssuer SET 
                    Name = @Name, 
                    Url = @Url, 
                    Inbox = @Inbox, 
                    Outbox = @Outbox, 
                    ActorId = @ActorId,
                    AvatarUri = @AvatarUri,
                    UpdatedAt = CURRENT_TIMESTAMP
                WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", issuer.Id);
        }

        command.Parameters.AddWithValue("@Name", issuer.Name);
        command.Parameters.AddWithValue("@Url", issuer.Url);
        command.Parameters.AddWithValue("@Inbox", issuer.Inbox);
        command.Parameters.AddWithValue("@Outbox", issuer.Outbox);
        command.Parameters.AddWithValue("@ActorId", issuer.ActorId);
        command.Parameters.AddWithValue("@AvatarUri", issuer.AvatarUri ?? (object)DBNull.Value);

        if (issuer.Id == 0)
        {
            issuer.Id = Convert.ToInt64(command.ExecuteScalar());
        }
        else
        {
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public List<FollowedIssuer> GetAllFollowedIssuers(bool onlyWithBadges = false)
    {
        var issuers = new List<FollowedIssuer>();

        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        if (onlyWithBadges)
        {
            command.CommandText = @"SELECT fi.*, COUNT(br.id) AS TotalIssued
                                 FROM FollowedIssuer AS fi 
                                 LEFT JOIN BadgeRecord br ON (br.IssuedBy = fi.Url) 
                                 GROUP BY fi.Id";
        }
        else
        {
            command.CommandText = "SELECT * FROM FollowedIssuer";
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            issuers.Add(new FollowedIssuer
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Url = reader.GetString(reader.GetOrdinal("Url")),
                Inbox = reader.GetString(reader.GetOrdinal("Inbox")),
                Outbox = reader.GetString(reader.GetOrdinal("Outbox")),
                ActorId = reader.GetInt64(reader.GetOrdinal("ActorId")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
                AvatarUri = reader["AvatarUri"] == DBNull.Value ? null : reader["AvatarUri"].ToString(),
                TotalIssued = reader["TotalIssued"] == DBNull.Value ? null : (int?)reader.GetInt32(reader.GetOrdinal("TotalIssued"))
            });
        }

        return issuers;
    }


    public FollowedIssuer? GetFollowedIssuerByUrl(string url)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM FollowedIssuer WHERE Url = @Url";
        command.Parameters.AddWithValue("@Url", url);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new FollowedIssuer
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Url = reader.GetString(reader.GetOrdinal("Url")),
                Inbox = reader.GetString(reader.GetOrdinal("Inbox")),
                Outbox = reader.GetString(reader.GetOrdinal("Outbox")),
                ActorId = reader.GetInt64(reader.GetOrdinal("ActorId")),
                AvatarUri = reader["AvatarUri"] == DBNull.Value ? null : reader["AvatarUri"].ToString(),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
            };
        }

        return null;
    }

    public List<FollowedIssuer> GetFollowedIssuers(long actorId)
    {
        var issuers = new List<FollowedIssuer>();

        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM FollowedIssuer WHERE ActorId = @ActorId";
        command.Parameters.AddWithValue("@ActorId", actorId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            issuers.Add(new FollowedIssuer
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Url = reader.GetString(reader.GetOrdinal("Url")),
                Inbox = reader.GetString(reader.GetOrdinal("Inbox")),
                Outbox = reader.GetString(reader.GetOrdinal("Outbox")),
                ActorId = reader.GetInt64(reader.GetOrdinal("ActorId")),
                AvatarUri = reader["AvatarUri"] == DBNull.Value ? null : reader["AvatarUri"].ToString(),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
            });
        }

        return issuers;
    }

    public FollowedIssuer? GetFollowedIssuerById(long id)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM FollowedIssuer WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new FollowedIssuer
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Url = reader.GetString(reader.GetOrdinal("Url")),
                Inbox = reader.GetString(reader.GetOrdinal("Inbox")),
                Outbox = reader.GetString(reader.GetOrdinal("Outbox")),
                ActorId = reader.GetInt64(reader.GetOrdinal("ActorId")),
                AvatarUri = reader["AvatarUri"] == DBNull.Value ? null : reader["AvatarUri"].ToString(),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
            };
        }

        return null;
    }

    public void DeleteFollowedIssuer(long id)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM FollowedIssuer WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        command.ExecuteNonQuery();
    }
    public void UpsertBadgeComment(long badgeRecordId, string noteId, string authorId)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO BadgeRecordComment (BadgeRecordId, NoteId, AuthorId)
            VALUES (@BadgeRecordId, @NoteId, @AuthorId)
            ON CONFLICT(BadgeRecordId, NoteId) DO UPDATE SET
                AuthorId = excluded.AuthorId,
                CreatedAt = CURRENT_TIMESTAMP;
        ";

        command.Parameters.AddWithValue("@BadgeRecordId", badgeRecordId);
        command.Parameters.AddWithValue("@NoteId", noteId);
        command.Parameters.AddWithValue("@AuthorId", authorId);

        command.ExecuteNonQuery();
        transaction.Commit();
        
        InsertRecentActivityLog("Badge comment added", $"Badge record {noteId}");
    }

    public List<string> GetBadgeComments(long? badgeRecordId = null)
    {
        var result = new List<string>();
        
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        
        if (!badgeRecordId.HasValue)
        {
            command.CommandText = "SELECT NoteId FROM BadgeRecordComment";
        }
        else
        {
            command.CommandText = "SELECT NoteId FROM BadgeRecordComment WHERE BadgeRecordId = @BadgeRecordId";
            command.Parameters.AddWithValue("@BadgeRecordId", badgeRecordId.Value);
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add((
                reader.GetString(0)
            ));
        }

        return result;
    }



    private string? SerializeSocialUri(SocialUri? socialUri)
    {
        if (socialUri == null)
            return null;

        try
        {
            return JsonSerializer.Serialize(socialUri);
        }
        catch (Exception ex)
        {
            Log(LogLevel.Warning, "Failed to serialize social URI: {Exception}", ex.Message);
            return null;
        }
    }

    private SocialUri? DeserializeSocialUri(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<SocialUri>(json);
        }
        catch (Exception ex)
        {
            Log(LogLevel.Warning, "Failed to deserialize social URI from JSON: {Json}, Exception: {Exception}", json, ex.Message);
            return null;
        }
    }

    private List<SocialUri> GetSocialsFromDbFields(string? social1, string? social2, string? social3)
    {
        var socials = new List<SocialUri>();

        var social1Obj = DeserializeSocialUri(social1);
        if (social1Obj != null) socials.Add(social1Obj);

        var social2Obj = DeserializeSocialUri(social2);
        if (social2Obj != null) socials.Add(social2Obj);

        var social3Obj = DeserializeSocialUri(social3);
        if (social3Obj != null) socials.Add(social3Obj);

        return socials;
    }

    public void UpsertActor(Actor actor)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();

        // Serialize social URIs for database storage
        string? socialUri1 = null, socialUri2 = null, socialUri3 = null;

        if (actor.Socials != null && actor.Socials.Count > 0)
        {
            socialUri1 = SerializeSocialUri(actor.Socials.ElementAtOrDefault(0));
            socialUri2 = SerializeSocialUri(actor.Socials.ElementAtOrDefault(1));
            socialUri3 = SerializeSocialUri(actor.Socials.ElementAtOrDefault(2));
            Console.WriteLine($"Serialized Social URIs: {socialUri1}, {socialUri2}, {socialUri3}");
        }

        if (actor.Id == 0)
        {
            command.CommandText = @"
                INSERT INTO Actor (Name, Summary, AvatarPath, InformationUri, Uri, Domain, PublicKeyPem, PrivateKeyPem, Username, LinkedInOrganizationId, IsMain, OwnerId, Theme, SocialUri1, SocialUri2, SocialUri3, ShowFollowers)
                VALUES (@Name, @Summary, @AvatarPath, @InformationUri, @Uri, @Domain, @PublicKeyPem, @PrivateKeyPem, @Username, @LinkedInOrganizationId, @IsMain, @OwnerId, @Theme, @SocialUri1, @SocialUri2, @SocialUri3, @ShowFollowers);
                SELECT last_insert_rowid();
            ";
        }
        else
        {
            if (actor.IsMain)
            {
                var singleMainCommand = connection.CreateCommand();
                singleMainCommand.CommandText = "UPDATE Actor SET IsMain = FALSE";
                singleMainCommand.ExecuteNonQuery();
            }

            command.CommandText = @"
                UPDATE Actor SET 
                    Name = @Name, 
                    Summary = @Summary, 
                    AvatarPath = @AvatarPath, 
                    InformationUri = @InformationUri, 
                    Uri = @Uri,
                    Domain = @Domain, 
                    PublicKeyPem = @PublicKeyPem, 
                    PrivateKeyPem = @PrivateKeyPem, 
                    Username = @Username,
                    LinkedInOrganizationId = @LinkedInOrganizationId,
                    IsMain = @IsMain,
                    OwnerId = @OwnerId,
                    Theme = @Theme,
                    SocialUri1 = @SocialUri1,
                    SocialUri2 = @SocialUri2,
                    SocialUri3 = @SocialUri3,
                    ShowFollowers = @ShowFollowers
                WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", actor.Id);
        }

        command.Parameters.AddWithValue("@Name", actor.FullName);
        command.Parameters.AddWithValue("@Summary", actor.Summary ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@AvatarPath", actor.AvatarPath ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@InformationUri", actor.InformationUri ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Uri", actor.Uri ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Domain", actor.Domain ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@PublicKeyPem", actor.PublicKeyPem ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@PrivateKeyPem", actor.PrivateKeyPem ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Username", actor.Username ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@LinkedInOrganizationId", actor.LinkedInOrganizationId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IsMain", actor.IsMain);
        command.Parameters.AddWithValue("@OwnerId", actor.OwnerId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Theme", actor.Theme ?? "default");
        command.Parameters.AddWithValue("@SocialUri1", socialUri1 ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@SocialUri2", socialUri2 ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@SocialUri3", socialUri3 ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ShowFollowers", actor.ShowFollowers);

        if (actor.Id == 0)
        {
            actor.Id = Convert.ToInt32(command.ExecuteScalar());
        }
        else
        {
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void DeleteActor(long id)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Actor WHERE Id = @Id AND IsMain = 0";
        command.Parameters.AddWithValue("@Id", id);

        command.ExecuteNonQuery();
    }

    public List<Actor> GetActors(string? filter = null)
    {
        var actors = new List<Actor>();

        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Actor";

        if (!string.IsNullOrEmpty(filter))
        {
            command.CommandText += $" WHERE {filter}";
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var actor = new Actor
            {
                Id = int.Parse(reader["Id"].ToString()!),
                FullName = reader["Name"].ToString()!,
                Summary = reader["Summary"] == DBNull.Value ? string.Empty : reader["Summary"].ToString()!,
                AvatarPath = reader["AvatarPath"] == DBNull.Value ? null : reader["AvatarPath"].ToString(),
                InformationUri = reader["InformationUri"] == DBNull.Value ? null : reader["InformationUri"].ToString()!,
                Domain = reader["Domain"] == DBNull.Value ? null : reader["Domain"].ToString(),
                PublicKeyPem = reader["PublicKeyPem"] == DBNull.Value ? null : reader["PublicKeyPem"].ToString(),
                PrivateKeyPem = reader["PrivateKeyPem"] == DBNull.Value ? null : reader["PrivateKeyPem"].ToString(),
                Username = reader["Username"] == DBNull.Value ? null : reader["Username"].ToString(),
                LinkedInOrganizationId = reader["LinkedInOrganizationId"] == DBNull.Value ? null : reader["LinkedInOrganizationId"].ToString(),
                IsMain = reader.GetBoolean(reader.GetOrdinal("IsMain")),
                Theme = reader["Theme"] == DBNull.Value ? "default" : reader["Theme"].ToString(),
                ShowFollowers = reader["ShowFollowers"] == DBNull.Value ? true : reader.GetBoolean(reader.GetOrdinal("ShowFollowers"))
            };

           if (reader.GetOrdinal("SocialUri1") != -1) {
                // Deserialize social URIs
                var social1 = reader["SocialUri1"] == DBNull.Value ? null : reader["SocialUri1"].ToString();
                var social2 = reader["SocialUri2"] == DBNull.Value ? null : reader["SocialUri2"].ToString();
                var social3 = reader["SocialUri3"] == DBNull.Value ? null : reader["SocialUri3"].ToString();
                actor.Socials = GetSocialsFromDbFields(social1, social2, social3);
            }
            
            actors.Add(actor);
        }

        return actors;
    }

    public void UpsertUser(User user)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Users (id, email, givenName, surname, provider, role, isActive, groupId, createdAt)
            VALUES (@Id, @Email, @GivenName, @Surname, @Provider, @Role, @IsActive, @GroupId, COALESCE(@CreatedAt, CURRENT_TIMESTAMP))
            ON CONFLICT(id) DO UPDATE SET
                email = excluded.email,
                givenName = excluded.givenName,
                surname = excluded.surname,
                provider = excluded.provider,
                role = excluded.role,
                isActive = excluded.isActive,
                groupId = excluded.groupId;
        ";

        command.Parameters.AddWithValue("@Id", user.Id);
        command.Parameters.AddWithValue("@Email", user.Email);
        command.Parameters.AddWithValue("@GivenName", user.GivenName);
        command.Parameters.AddWithValue("@Surname", user.Surname);
        command.Parameters.AddWithValue("@Provider", user.Provider);
        command.Parameters.AddWithValue("@Role", user.Role ?? "user");
        command.Parameters.AddWithValue("@IsActive", user.IsActive);
        command.Parameters.AddWithValue("@GroupId", user.GroupId ?? "system");
        command.Parameters.AddWithValue("@CreatedAt", user.CreatedAt == default ? (object)DBNull.Value : user.CreatedAt);

        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public User? GetUserById(string id)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Users WHERE id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        Console.WriteLine($"Executing SQL: {command.CommandText} with Id={id}");

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new User
            {
                Id = reader["id"].ToString()!,
                Email = reader["email"].ToString()!,
                GivenName = reader["givenName"].ToString()!,
                Surname = reader["surname"].ToString()!,
                CreatedAt = reader["createdAt"] == DBNull.Value ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("createdAt")),
                Provider = reader["provider"].ToString()!,
                Role = reader["role"].ToString()!,
                IsActive = reader["isActive"] == DBNull.Value ? true : reader.GetBoolean(reader.GetOrdinal("isActive")),
                GroupId = reader["groupId"]?.ToString() ?? "system"
            };
        }

        return null;
    }

    public User? ValidateApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Users WHERE ApiKey = @ApiKey AND IsActive = 1";
        command.Parameters.AddWithValue("@ApiKey", apiKey);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new User
            {
                Id = reader["id"].ToString()!,
                Email = reader["email"].ToString()!,
                GivenName = reader["givenName"].ToString()!,
                Surname = reader["surname"].ToString()!,
                CreatedAt = reader["createdAt"] == DBNull.Value ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("createdAt")),
                Provider = reader["provider"].ToString()!,
                Role = reader["role"].ToString()!,
                IsActive = reader["isActive"] == DBNull.Value ? true : reader.GetBoolean(reader.GetOrdinal("isActive")),
                GroupId = reader["groupId"]?.ToString() ?? "system"
            };
        }

        return null;
    }

    public Actor? GetActorById(long id)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Actor WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            var actor = new Actor
            {
                Id = int.Parse(reader["Id"].ToString()!),
                FullName = reader["Name"].ToString()!,
                Summary = reader["Summary"] == DBNull.Value ? string.Empty : reader["Summary"].ToString()!,
                AvatarPath = reader["AvatarPath"] == DBNull.Value ? null : reader["AvatarPath"].ToString(),
                InformationUri = reader["InformationUri"] == DBNull.Value ? null : reader["InformationUri"].ToString()!,
                Domain = reader["Domain"] == DBNull.Value ? null : reader["Domain"].ToString(),
                PublicKeyPem = reader["PublicKeyPem"] == DBNull.Value ? null : reader["PublicKeyPem"].ToString(),
                PrivateKeyPem = reader["PrivateKeyPem"] == DBNull.Value ? null : reader["PrivateKeyPem"].ToString(),
                Username = reader["Username"] == DBNull.Value ? null : reader["Username"].ToString(),
                LinkedInOrganizationId = reader["LinkedInOrganizationId"] == DBNull.Value ? null : reader["LinkedInOrganizationId"].ToString(),
                IsMain = reader.GetBoolean(reader.GetOrdinal("IsMain")),
                Theme = reader["Theme"] == DBNull.Value ? "default" : reader["Theme"].ToString(),
                ShowFollowers = reader["ShowFollowers"] == DBNull.Value ? true : reader.GetBoolean(reader.GetOrdinal("ShowFollowers"))
            };

            if (reader.GetOrdinal("SocialUri1") != -1) {
                // Deserialize social URIs
                var social1 = reader["SocialUri1"] == DBNull.Value ? null : reader["SocialUri1"].ToString();
                var social2 = reader["SocialUri2"] == DBNull.Value ? null : reader["SocialUri2"].ToString();
                var social3 = reader["SocialUri3"] == DBNull.Value ? null : reader["SocialUri3"].ToString();
                actor.Socials = GetSocialsFromDbFields(social1, social2, social3);
            }
            
            return actor;
        }

        return null;
    }

    public Actor? GetActorByUri(string uri)
    {
        return GetActorByFilter($"Uri = '{uri}'");
    }

    public Actor? GetActorByFilter(string filter)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM Actor WHERE {filter}";

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            var actor = new Actor
            {
                Id = int.Parse(reader["Id"].ToString()!),
                FullName = reader["Name"].ToString()!,
                Summary = reader["Summary"] == DBNull.Value ? string.Empty : reader["Summary"].ToString()!,
                AvatarPath = reader["AvatarPath"] == DBNull.Value ? null : reader["AvatarPath"].ToString(),
                InformationUri = reader["InformationUri"] == DBNull.Value ? null : reader["InformationUri"].ToString()!,
                Domain = reader["Domain"] == DBNull.Value ? null : reader["Domain"].ToString(),
                PublicKeyPem = reader["PublicKeyPem"] == DBNull.Value ? null : reader["PublicKeyPem"].ToString(),
                PrivateKeyPem = reader["PrivateKeyPem"] == DBNull.Value ? null : reader["PrivateKeyPem"].ToString(),
                Username = reader["Username"] == DBNull.Value ? null : reader["Username"].ToString(),
                LinkedInOrganizationId = reader["LinkedInOrganizationId"] == DBNull.Value ? null : reader["LinkedInOrganizationId"].ToString(),
                IsMain = reader.GetBoolean(reader.GetOrdinal("IsMain")),
                Theme = reader["Theme"] == DBNull.Value ? "default" : reader["Theme"].ToString(),
                ShowFollowers = reader["ShowFollowers"] == DBNull.Value ? true : reader.GetBoolean(reader.GetOrdinal("ShowFollowers"))
            };

            if (reader.GetOrdinal("SocialUri1") != -1)
            {
                // Deserialize social URIs
                var social1 = reader["SocialUri1"] == DBNull.Value ? null : reader["SocialUri1"].ToString();
                var social2 = reader["SocialUri2"] == DBNull.Value ? null : reader["SocialUri2"].ToString();
                var social3 = reader["SocialUri3"] == DBNull.Value ? null : reader["SocialUri3"].ToString();
                actor.Socials = GetSocialsFromDbFields(social1, social2, social3);
            }
            
            return actor;
        }

        return null;
    }

    public void UpsertFollower(Follower follower)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Follower (FollowerUri, Domain, ActorId, AvatarUri, DisplayName)
            VALUES (@FollowerUri, @Domain, @ActorId, @AvatarUri, @DisplayName)
            ON CONFLICT(FollowerUri, ActorId) DO UPDATE SET
            AvatarUri = excluded.AvatarUri,
            DisplayName = excluded.DisplayName;
        ";

        command.Parameters.AddWithValue("@FollowerUri", follower.FollowerUri);
        command.Parameters.AddWithValue("@Domain", follower.Domain);
        command.Parameters.AddWithValue("@ActorId", follower.Parent.Id);
        command.Parameters.AddWithValue("@AvatarUri", follower.AvatarUri ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@DisplayName", follower.DisplayName ?? (object)DBNull.Value);

        command.ExecuteNonQuery();
        transaction.Commit();

        InsertRecentActivityLog("New follower", $"Follower {follower.FollowerUri}");
    }

    public List<Follower> GetFollowersToProcess()
    {
        var followers = new List<Follower>();

        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Follower WHERE DisplayName IS NULL AND AvatarUri IS NULL LIMIT 5";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            followers.Add(new Follower
            {
                FollowerUri = reader.GetString(0),
                Domain = reader.GetString(1),
                CreatedAt = reader.GetDateTime(2),
                Parent = new Actor() { Id = reader.GetInt64(3) },
                AvatarUri = reader["AvatarUri"] == DBNull.Value ? null : reader["AvatarUri"].ToString(),
                DisplayName = reader["DisplayName"] == DBNull.Value ? null : reader["DisplayName"].ToString()
            });
        }

        return followers;
    }

    public List<Follower> GetFollowersByActorId(long actorId)
    {
        var followers = new List<Follower>();

        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Follower WHERE ActorId = @ActorId";
        command.Parameters.AddWithValue("@ActorId", actorId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            followers.Add(new Follower
            {
                FollowerUri = reader.GetString(0),
                Domain = reader.GetString(1),
                CreatedAt = reader.GetDateTime(2),
                Parent = new Actor() { Id = reader.GetInt64(3) },
                AvatarUri = reader["AvatarUri"] == DBNull.Value ? null : reader["AvatarUri"].ToString(),
                DisplayName = reader["DisplayName"] == DBNull.Value ? null : reader["DisplayName"].ToString()
            });
        }

        return followers;
    }

    public void DeleteFollower(string followerUri)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Follower WHERE FollowerUri = @FollowerUri";
        command.Parameters.AddWithValue("@FollowerUri", followerUri);

        command.ExecuteNonQuery();
    }

    public void UpsertBadgeDefinition(Badge badge)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        if (badge.Id == 0)
        {
            command.CommandText = @"
                INSERT INTO Badge (Title, Description, IssuedBy, Image, ImageAltText, EarningCriteria, CreatedAt, UpdatedAt, BadgeType, Hashtags, InfoUri, IsCertificate, OwnerId)
                VALUES (@Title, @Description, @IssuedBy, @Image, @ImageAltText, @EarningCriteria, datetime('now'), datetime('now'), @BadgeType, @Hashtags, @InfoUri, @IsCertificate, @OwnerId);
                SELECT last_insert_rowid();
            ";
        }
        else
        {
            command.CommandText = @"
                UPDATE Badge SET 
                    Title = @Title, 
                    Description = @Description, 
                    IssuedBy = @IssuedBy, 
                    Image = @Image,
                    ImageAltText = @ImageAltText,
                    EarningCriteria = @EarningCriteria, 
                    UpdatedAt = datetime('now'),
                    BadgeType = @BadgeType,
                    Hashtags = @Hashtags,
                    InfoUri = @InfoUri,
                    IsCertificate = @IsCertificate,
                    OwnerId = @OwnerId
                WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", badge.Id);
        }

        Log(LogLevel.Debug, "Saving badge: {BadgeImage}", badge.Image);

        command.Parameters.AddWithValue("@Title", badge.Title);
        command.Parameters.AddWithValue("@Description", badge.Description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IssuedBy", badge.IssuedBy);
        command.Parameters.AddWithValue("@Image", badge.Image ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ImageAltText", badge.ImageAltText ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@EarningCriteria", badge.EarningCriteria ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@BadgeType", badge.BadgeType);
        command.Parameters.AddWithValue("@Hashtags", badge.Hashtags ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@InfoUri", badge.InfoUri ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IsCertificate", badge.IsCertificate);
        command.Parameters.AddWithValue("@OwnerId", badge.OwnerId ?? (object)DBNull.Value);

        if (badge.Id == 0)
        {
            badge.Id = Convert.ToInt32(command.ExecuteScalar());
        }
        else
        {
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public List<Badge> GetAllBadgeDefinitions(bool includeActors = false, string? filter = null)
    {
        var badges = new List<Badge>();

        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        

        if (includeActors)
        {
            command.CommandText = @"SELECT b.*, 
                            a.Id as ActorId,
                            a.Name as ActorName,
                            a.Summary as ActorSummary,
                            a.AvatarPath as ActorAvatarPath,
                            a.InformationUri as ActorInformationUri,
                            a.Domain as ActorDomain,
                            a.Username AS ActorUsername
                             FROM Badge AS b INNER JOIN Actor AS a ON b.IssuedBy = a.Id";
        }
        else
        {
            command.CommandText = "SELECT * FROM Badge";
        }

        if (!string.IsNullOrEmpty(filter))
        {
            command.CommandText += $" WHERE {filter}";
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            try 
            {
                Actor? actor = null;

                if (includeActors)
                {
                    actor = new Actor
                    {
                        Id = reader.GetInt64(reader.GetOrdinal("ActorId")),
                        FullName = reader["ActorName"].ToString()!,
                        Summary = reader["ActorSummary"] == DBNull.Value ? string.Empty : reader["ActorSummary"].ToString()!,
                        AvatarPath = reader["ActorAvatarPath"] == DBNull.Value ? null : reader["ActorAvatarPath"].ToString(),
                        InformationUri = reader["ActorInformationUri"] == DBNull.Value ? null : reader["ActorInformationUri"].ToString()!,
                        Domain = reader["ActorDomain"] == DBNull.Value ? null : reader["ActorDomain"].ToString(),
                        Username = reader["ActorUsername"] == DBNull.Value ? null : reader["ActorUsername"].ToString()
                    };
                }

                badges.Add(new Badge
                {
                    Id = reader.GetInt64(reader.GetOrdinal("Id")),
                    Title = reader.GetString(reader.GetOrdinal("Title")),
                    Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString(),
                    IssuedBy = reader.GetInt32(reader.GetOrdinal("IssuedBy")),
                    Image = reader["Image"] == DBNull.Value ? null : reader["Image"].ToString(),
                    ImageAltText = reader["ImageAltText"] == DBNull.Value ? null : reader["ImageAltText"].ToString(),
                    EarningCriteria = reader["EarningCriteria"] == DBNull.Value ? null : reader["EarningCriteria"].ToString(),
                    BadgeType = reader["BadgeType"].ToString(),
                    Hashtags = reader["Hashtags"] == DBNull.Value ? null : reader["Hashtags"].ToString(),
                    InfoUri = reader["InfoUri"] == DBNull.Value ? null : reader["InfoUri"].ToString(),
                    IsCertificate = reader["IsCertificate"] != DBNull.Value && Convert.ToBoolean(reader["IsCertificate"]),
                    Issuer = actor
                });
            } 
            catch (Exception ex)
            {
                Log(LogLevel.Error, "Error reading badge: {ErrorMessage}", ex.Message);
                Log(LogLevel.Debug, "Badge error details: {Exception}", ex);
                Log(LogLevel.Debug, "Badge reader data: {ReaderData}", JsonSerializer.Serialize(reader));
            }
            
        }

        return badges;
    }

    /*
public class ActorStats
{
    public long ActorId { get; set; }

    public int BadgeCount { get; set; } = 0;

    public int FollowerCount { get; set; } = 0;

    public int IssuedCount { get; set; } = 0;

    public int MembersCount { get; set; } = 0;
}*/

    public void ExecuteSql(string sql)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = sql;

        command.ExecuteNonQuery();
    }

    public List<dynamic> ExecuteSqlQuery(string sql)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = sql;

        using var reader = command.ExecuteReader();
        var result = new List<dynamic>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.GetValue(i);
            }

            result.Add(row);
        }
        return result;
    }

    public void InsertRecentActivityLog(string title, string? description = null)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO RecentActivityLog (Title, Description)
            VALUES (@Title, @Description);
        ";

        command.Parameters.AddWithValue("@Title", title);
        command.Parameters.AddWithValue("@Description", description ?? (object)DBNull.Value);

        command.ExecuteNonQuery();
    }

    public List<RecentActivityLog> GetRecentActivityLogs(int limit = 10)
    {
        var logs = new List<RecentActivityLog>();

        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, Title, Description, CreatedAt
            FROM RecentActivityLog
            ORDER BY CreatedAt DESC
            LIMIT @Limit;
        ";

        command.Parameters.AddWithValue("@Limit", limit);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            logs.Add(new RecentActivityLog
            {
                Title = reader.GetString(reader.GetOrdinal("Title")),
                Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString(),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
            });
        }

        return logs;
    }

    public void ClearRecentActivityLogs()
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM RecentActivityLog;";

        command.ExecuteNonQuery();
    }

    public InstanceStats GetInstanceStats()
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(br.AcceptedOn) AS acceptedCount,
                    COUNT(*) AS issuedCount
            FROM BadgeRecord AS br;
            SELECT COUNT(*) AS followerCount FROM Follower;
            ";

        using var reader = command.ExecuteReader();
        var stats = new InstanceStats();
        if (reader.Read())
        {
            stats.AcceptedCount = reader.GetInt32(0);
            stats.IssuedCount = reader.GetInt32(1);
        }

        if (reader.NextResult() && reader.Read())
        {
            stats.FollowerCount = reader.GetInt32(0);
        }

        return stats;
    }

    public BadgeStats GetBadgeStats(long badgeId)
    {
        var stats = new BadgeStats()
        {
            BadgeId = badgeId
        };

        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                COUNT(*) AS IssuedCount,
                COUNT(AcceptedOn) AS AcceptedCount,
                COUNT(RevokedAt) AS RevokedCount
            FROM BadgeRecord 
            WHERE BadgeId = @BadgeId;
        ";
        command.Parameters.AddWithValue("@BadgeId", badgeId);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            stats.IssuedCount = reader.GetInt32(0);
            stats.AcceptedCount = reader.GetInt32(1);
            stats.RevokedCount = reader.GetInt32(2);
        }

        return stats;
    }

    public ActorStats GetIssuerStats(Actor actor)
    {
        var stats = new ActorStats()
        {
            ActorId = actor.Id
        };

        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*) AS followerCount FROM Follower WHERE ActorId = @ActorId;
            SELECT COUNT(*) AS issuedCount, 
            COUNT(
                DISTINCT
                    CASE 
                        WHEN IssuedToSubjectUri IS NOT NULL AND IssuedToSubjectUri != '' THEN IssuedToSubjectUri
                        WHEN IssuedToEmail IS NOT NULL AND IssuedToEmail != '' THEN IssuedToEmail
                        ELSE IssuedToName
                    END
                ) AS memberCount
                FROM BadgeRecord WHERE IssuedBy = @ActorUrl;
            SELECT COUNT(*) AS badgeCount FROM Badge WHERE IssuedBy = @ActorId;
        ";
        command.Parameters.AddWithValue("@ActorId", actor.Id);
        command.Parameters.AddWithValue("@ActorUrl", actor.Uri);

        using var reader = command.ExecuteReader();

        if (reader.Read())
        {
            stats.FollowerCount = reader.GetInt32(0);
        }

        if (reader.NextResult() && reader.Read())
        {
            stats.IssuedCount = reader.GetInt32(0);
            stats.MembersCount = reader.GetInt32(1);
        }

        if (reader.NextResult() && reader.Read())
        {
            stats.BadgeCount = reader.GetInt32(0);
        }

        return stats;
    }

    public List<Badge> GetBadgesByIssuerId(long actorId)
    {
        var badges = new List<Badge>();

        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Badge WHERE IssuedBy = @ActorId";
        command.Parameters.AddWithValue("@ActorId", actorId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            badges.Add(new Badge
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString(),
                IssuedBy = reader.GetInt32(reader.GetOrdinal("IssuedBy")),
                Image = reader["Image"] == DBNull.Value ? null : reader["Image"].ToString(),
                ImageAltText = reader["ImageAltText"] == DBNull.Value ? null : reader["ImageAltText"].ToString(),
                EarningCriteria = reader["EarningCriteria"] == DBNull.Value ? null : reader["EarningCriteria"].ToString(),
                BadgeType = reader["BadgeType"].ToString(),
                Hashtags = reader["Hashtags"] == DBNull.Value ? null : reader["Hashtags"].ToString(),
                InfoUri = reader["InfoUri"] == DBNull.Value ? null : reader["InfoUri"].ToString(),
                IsCertificate = reader["IsCertificate"] != DBNull.Value && Convert.ToBoolean(reader["IsCertificate"])
            });
        }

        return badges;
    }

    public Recipient GetRecipientByIssuedTo(BadgeRecord record)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();

        string filter = "FALSE";

        var recipient = new Recipient();

        if (!string.IsNullOrEmpty(record.IssuedToEmail))
        {
            filter = "Email = @IssuedTo";
            command.Parameters.AddWithValue("@IssuedTo", record.IssuedToEmail);
        }
        else if (!string.IsNullOrEmpty(record.IssuedToSubjectUri))
        {
            filter = "ProfileUri = @IssuedTo";
            command.Parameters.AddWithValue("@IssuedTo", record.IssuedToSubjectUri);
        }

        command.CommandText = $"SELECT * FROM Recipient WHERE {filter}";

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            recipient.Id = reader.GetInt64(0);
            recipient.Name = reader["Name"] == DBNull.Value ? null : reader["Name"].ToString();
            recipient.Email = reader["Email"] == DBNull.Value ? null : reader["Email"].ToString();
            recipient.ProfileUri = reader["ProfileUri"] == DBNull.Value ? null : reader["ProfileUri"].ToString();
        }

        if (string.IsNullOrEmpty(recipient.Name) && !string.IsNullOrEmpty(record.IssuedToName))
        {
            recipient.Name = record.IssuedToName;
        }

        if (string.IsNullOrEmpty(recipient.Email) && !string.IsNullOrEmpty(record.IssuedToEmail))
        {
            recipient.Email = record.IssuedToEmail;
        }

        if (string.IsNullOrEmpty(recipient.ProfileUri) && !string.IsNullOrEmpty(record.IssuedToSubjectUri))
        {
            recipient.ProfileUri = record.IssuedToSubjectUri;
        }

        return recipient;
    }

    public Recipient UpsertRecipient(Recipient recipient)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Recipient (Name, Email, ProfileUri, IsActivityPubActor, CreatedAt, UpdatedAt)
            VALUES (@Name, @Email, @ProfileUri, @IsActivityPubActor, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            ON CONFLICT(ProfileUri) DO UPDATE SET
                Name = excluded.Name,
                Email = excluded.Email,
                IsActivityPubActor = excluded.IsActivityPubActor,
                UpdatedAt = CURRENT_TIMESTAMP;
            SELECT last_insert_rowid();
        ";

        command.Parameters.AddWithValue("@Name", recipient.Name);
        command.Parameters.AddWithValue("@Email", recipient.Email);
        command.Parameters.AddWithValue("@ProfileUri", recipient.ProfileUri);
        command.Parameters.AddWithValue("@IsActivityPubActor", recipient.IsActivityPubActor);

        recipient.Id = Convert.ToInt64(command.ExecuteScalar());

        transaction.Commit();

        return recipient;
    }

    public Badge? GetBadgeDefinitionById(long id)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Badge WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new Badge
            {
                Id = reader.GetInt64(0),
                Title = reader.GetString(1),
                Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString(),
                IssuedBy = reader.GetInt32(3),
                Image = reader["Image"] == DBNull.Value ? null : reader["Image"].ToString(),
                ImageAltText = reader["ImageAltText"] == DBNull.Value ? null : reader["ImageAltText"].ToString(),
                EarningCriteria = reader["EarningCriteria"] == DBNull.Value ? null : reader["EarningCriteria"].ToString(),
                BadgeType = reader["BadgeType"].ToString(),
                Hashtags = reader["Hashtags"] == DBNull.Value ? null : reader["Hashtags"].ToString(),
                InfoUri = reader["InfoUri"] == DBNull.Value ? null : reader["InfoUri"].ToString(),
                IsCertificate = reader["IsCertificate"] != DBNull.Value && Convert.ToBoolean(reader["IsCertificate"])
            };
        }

        return null;
    }

    public long PeekNotifyGrantId()
    {
       // the oldest badgerecords that acceptKey is not null and not empty and acceptedOn is null
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id FROM BadgeRecord WHERE AcceptKey IS NOT NULL AND 
            AcceptKey != '' AND AcceptedOn IS NULL 
            AND NotifiedOfGrant = FALSE ORDER BY IssuedOn ASC LIMIT 1";

        using var reader = command.ExecuteReader();

        if (reader.Read())
        {
            return reader.GetInt64(0);
        }

        return 0;
    }

    public void NotifyGrant(long id)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE BadgeRecord SET 
                NotifiedOfGrant = TRUE
            WHERE Id = @Id;
        ";

        command.Parameters.AddWithValue("@Id", id);

        command.ExecuteNonQuery();
        transaction.Commit();

        InsertRecentActivityLog("Badge notification sent", $"Badge record {id}");
    }

    public long PeekProcessGrantId()
    {
       // the oldest badgerecords that acceptKey is not null and not empty and acceptedOn is null
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id FROM BadgeRecord WHERE
                (AcceptKey = '' OR AcceptKey IS NULL) 
            AND AcceptedOn IS NOT NULL AND IsExternal = FALSE 
            AND (FingerPrint = '' OR FingerPrint IS NULL) ORDER BY IssuedOn ASC LIMIT 1";

        using var reader = command.ExecuteReader();

        if (reader.Read())
        {
            return reader.GetInt64(0);
        }

        return 0;
    }

    public void DeleteBadgeDefinition(long id)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Badge WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        command.ExecuteNonQuery();
    }

    public void DeleteBadgeRecord(long id)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM BadgeRecord WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        command.ExecuteNonQuery();
        transaction.Commit();

        InsertRecentActivityLog("Badge record deleted", $"Badge record {id} has been deleted");
    }

    public void AcceptBadgeRecord(BadgeRecord badgeRecord)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE BadgeRecord SET 
                AcceptedOn = @AcceptedOn,
                AcceptKey = null,
                IssuedToSubjectUri = @IssuedToSubjectUri,
                IssuedToName = @IssuedToName
            WHERE Id = @Id;
        ";

        command.Parameters.AddWithValue("@Id", badgeRecord.Id);
        command.Parameters.AddWithValue("@AcceptedOn", DateTime.UtcNow);
        command.Parameters.AddWithValue("@IssuedToSubjectUri", badgeRecord.IssuedToSubjectUri);
        command.Parameters.AddWithValue("@IssuedToName", badgeRecord.IssuedToName);

        command.ExecuteNonQuery();
        transaction.Commit();

        InsertRecentActivityLog("Badge accepted", $"Badge record {badgeRecord.Id}");
    }


    public void UpdateBadgeSignature(BadgeRecord badgeRecord)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE BadgeRecord SET 
                FingerPrint = @FingerPrint,
                NoteId = @NoteId
            WHERE Id = @Id;
        ";

        command.Parameters.AddWithValue("@Id", badgeRecord.Id);
        command.Parameters.AddWithValue("@FingerPrint", badgeRecord.FingerPrint);
        command.Parameters.AddWithValue("@NoteId", badgeRecord.NoteId);

        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public void MarkBadgeRecordAsBoosted(long badgeRecordId)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE BadgeRecord SET 
                BoostedOn = @BoostedOn
            WHERE Id = @Id;
        ";

        command.Parameters.AddWithValue("@Id", badgeRecordId);
        command.Parameters.AddWithValue("@BoostedOn", DateTime.UtcNow);

        command.ExecuteNonQuery();
        transaction.Commit();

        InsertRecentActivityLog("Badge boosted", $"Badge record {badgeRecordId} boosted to followers");
    }

    public void CreateBadgeRecord(BadgeRecord record)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO BadgeRecord (
                Title, IssuedBy, Description, Image, ImageAltText, EarningCriteria, 
                IssuedOn, IssuedToSubjectUri, IssuedToName, IssuedToEmail,
                AcceptKey, BadgeId, Hashtags, IsExternal, Visibility, FingerPrint, NoteId, AcceptedOn
            )
            VALUES (
                @Title, @IssuedBy, @Description, @Image, @ImageAltText, @EarningCriteria,
                @IssuedOn, @IssuedToSubjectUri, @IssuedToName, @IssuedToEmail,
                @AcceptKey, @BadgeId, @Hashtags, @IsExternal, @Visibility, @FingerPrint, @NoteId, @AcceptedOn
            );
            SELECT last_insert_rowid();
        ";

        command.Parameters.AddWithValue("@Title", record.Title);
        command.Parameters.AddWithValue("@IssuedBy", record.IssuedBy);
        command.Parameters.AddWithValue("@Description", record.Description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Image", record.Image ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ImageAltText", record.ImageAltText ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@EarningCriteria", record.EarningCriteria ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IssuedOn", record.IssuedOn);
        command.Parameters.AddWithValue("@IssuedToSubjectUri", record.IssuedToSubjectUri);
        command.Parameters.AddWithValue("@IssuedToName", record.IssuedToName);
        command.Parameters.AddWithValue("@IssuedToEmail", record.IssuedToEmail);
        command.Parameters.AddWithValue("@AcceptKey", record.AcceptKey);
        command.Parameters.AddWithValue("@BadgeId", record.Badge!.Id!);
        command.Parameters.AddWithValue("@Hashtags", record.Hashtags ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IsExternal", record.IsExternal);
        command.Parameters.AddWithValue("@Visibility", record.Visibility);
        command.Parameters.AddWithValue("@FingerPrint", record.FingerPrint ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@NoteId", record.NoteId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@AcceptedOn", record.AcceptedOn ?? (object)DBNull.Value);

        record.Id = Convert.ToInt64(command.ExecuteScalar());

        transaction.Commit();

        if (record.IsExternal)
        {
            InsertRecentActivityLog($"External badge received", $"Issued by {record.IssuedBy}");
        }
        else
        {
            InsertRecentActivityLog($"Badge granted", $"Issued to {record.IssuedToName}");
        }
    }

    public Badge? GetBadgeById(long id)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Badge WHERE Id = @Id";

        command.Parameters.AddWithValue("@Id", id);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            return new Badge
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString(),
                IssuedBy = reader.GetInt32(reader.GetOrdinal("IssuedBy")),
                Image = reader["Image"] == DBNull.Value ? null : reader["Image"].ToString(),
                ImageAltText = reader["ImageAltText"] == DBNull.Value ? null : reader["ImageAltText"].ToString(),
                EarningCriteria = reader["EarningCriteria"] == DBNull.Value ? null : reader["EarningCriteria"].ToString(),
                BadgeType = reader["BadgeType"].ToString(),
                Hashtags = reader["Hashtags"] == DBNull.Value ? null : reader["Hashtags"].ToString(),
                InfoUri = reader["InfoUri"] == DBNull.Value ? null : reader["InfoUri"].ToString(),
                IsCertificate = reader["IsCertificate"] != DBNull.Value && Convert.ToBoolean(reader["IsCertificate"])
            };
        }

        return null;
    }

    public BadgeRecord? GetBadgeToAccept(long id, string? key = null)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();

        var sql = "SELECT * FROM BadgeRecord WHERE Id = @Id";
        
        command.Parameters.AddWithValue("@Id", id);

        if (!string.IsNullOrEmpty(key))
        {
            sql += " AND AcceptKey = @AcceptKey";
            command.Parameters.AddWithValue("@AcceptKey", key);
        }
        command.CommandText = sql;
       
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            return new BadgeRecord
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                IssuedBy = reader.GetString(reader.GetOrdinal("IssuedBy")),
                Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString(),
                Image = reader["Image"] == DBNull.Value ? null : reader["Image"].ToString(),
                ImageAltText = reader["ImageAltText"] == DBNull.Value ? null : reader["ImageAltText"].ToString(),
                EarningCriteria = reader["EarningCriteria"] == DBNull.Value ? null : reader["EarningCriteria"].ToString(),
                IssuedOn = reader.GetDateTime(reader.GetOrdinal("IssuedOn")),
                IssuedToName = reader.GetString(reader.GetOrdinal("IssuedToName")),
                IssuedToSubjectUri = reader.GetString(reader.GetOrdinal("IssuedToSubjectUri")),
                IssuedToEmail = reader["IssuedToEmail"] == DBNull.Value ? null : reader["IssuedToEmail"].ToString(),
                AcceptedOn = reader["AcceptedOn"] == DBNull.Value ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("AcceptedOn")),
                BoostedOn = reader["BoostedOn"] == DBNull.Value ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("BoostedOn")),
                FingerPrint = reader["FingerPrint"] == DBNull.Value ? null : reader["FingerPrint"].ToString(),
                AcceptKey = reader["AcceptKey"] == DBNull.Value ? null : reader["AcceptKey"].ToString(),
                Badge = new Badge { Id = reader.GetInt64(reader.GetOrdinal("BadgeId")) },
                Hashtags = reader["Hashtags"] == DBNull.Value ? null : reader["Hashtags"].ToString(),
                NoteId = reader["NoteId"] == DBNull.Value ? null : reader["NoteId"].ToString(),
                IsExternal = reader["IsExternal"] == DBNull.Value ? false : reader.GetBoolean(reader.GetOrdinal("IsExternal")),
            };
        }

        return null;
    }

    public void RevokeGrantById(long id)
    {
        using var connection = GetConnection();
        connection.Open();
        var command = connection.CreateCommand();

        command.CommandText = "UPDATE BadgeRecord SET RevokedAt = @RevokedAt WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@RevokedAt", DateTime.UtcNow);

        command.ExecuteNonQuery();  
    }

    public BadgeRecord? GetGrantByNoteId(string noteId)
    {
        Log(LogLevel.Debug, "GetGrantByNoteId: {NoteId}", noteId);
        BadgeRecord? badgeRecord = null;

        using var connection = GetConnection();
        connection.Open();
        var command = connection.CreateCommand();

        command.CommandText = "SELECT * FROM BadgeRecord WHERE RevokedAt IS NULL AND (NoteId = @NoteId OR NoteId LIKE 'https://%/' || @NoteId)";
        command.Parameters.AddWithValue("@NoteId", noteId);
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            badgeRecord = new BadgeRecord
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                NoteId = reader["NoteId"] == DBNull.Value ? null : reader["NoteId"].ToString(),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                IssuedBy = reader.GetString(reader.GetOrdinal("IssuedBy")),
                Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString(),
                Image = reader["Image"] == DBNull.Value ? null : reader["Image"].ToString(),
                ImageAltText = reader["ImageAltText"] == DBNull.Value ? null : reader["ImageAltText"].ToString(),
                EarningCriteria = reader["EarningCriteria"] == DBNull.Value ? null : reader["EarningCriteria"].ToString(),
                IssuedOn = reader.GetDateTime(reader.GetOrdinal("IssuedOn")),
                IssuedToName = reader.GetString(reader.GetOrdinal("IssuedToName")),
                IssuedToSubjectUri = reader.GetString(reader.GetOrdinal("IssuedToSubjectUri")),
                IssuedToEmail = reader["IssuedToEmail"] == DBNull.Value ? null : reader["IssuedToEmail"].ToString(),
                AcceptedOn = reader["AcceptedOn"] == DBNull.Value ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("AcceptedOn")),
                BoostedOn = reader["BoostedOn"] == DBNull.Value ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("BoostedOn")),
                FingerPrint = reader["FingerPrint"] == DBNull.Value ? null : reader["FingerPrint"].ToString(),
                AcceptKey = reader["AcceptKey"] == DBNull.Value ? null : reader["AcceptKey"].ToString(),
                Badge = new Badge { Id = reader.GetInt64(reader.GetOrdinal("BadgeId")) },
                Hashtags = reader["Hashtags"] == DBNull.Value ? null : reader["Hashtags"].ToString(),
                IsExternal = reader["IsExternal"] == DBNull.Value ? false : reader.GetBoolean(reader.GetOrdinal("IsExternal")),
            };

            break;
        }

        return badgeRecord;
    }

    public List<BadgeRecord> GetBadgeRecordsToProcess(long? id = null)
    {
        // retrieve badges without fingerprint, no acceptkey, but acceptedOn
        var records = new List<BadgeRecord>();

        var whereClause = new List<string>();

        using var connection = GetConnection();

        connection.Open();

        var command = connection.CreateCommand();
       
        if (id.HasValue)
        {
            whereClause.Add("Id = @Id");
            command.Parameters.AddWithValue("@Id", id.Value);
        }

        command.CommandText = @"SELECT * FROM BadgeRecord 
                 WHERE (FingerPrint IS NULL OR FingerPrint = '')
                 AND (AcceptKey IS NULL OR AcceptKey = '')
                 AND IsExternal = FALSE 
                 AND AcceptedOn IS NOT NULL " +
                 (whereClause.Count > 0 ? " AND " + string.Join(" AND ", whereClause) : "");

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            records.Add(new BadgeRecord
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                NoteId = reader["NoteId"] == DBNull.Value ? null : reader["NoteId"].ToString(),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                IssuedBy = reader.GetString(reader.GetOrdinal("IssuedBy")),
                Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString(),
                Image = reader["Image"] == DBNull.Value ? null : reader["Image"].ToString(),
                ImageAltText = reader["ImageAltText"] == DBNull.Value ? null : reader["ImageAltText"].ToString(),
                EarningCriteria = reader["EarningCriteria"] == DBNull.Value ? null : reader["EarningCriteria"].ToString(),
                IssuedOn = reader.GetDateTime(reader.GetOrdinal("IssuedOn")),
                IssuedToEmail = reader.GetString(reader.GetOrdinal("IssuedToEmail")),
                IssuedToName = reader.GetString(reader.GetOrdinal("IssuedToName")),
                IssuedToSubjectUri = reader.GetString(reader.GetOrdinal("IssuedToSubjectUri")),
                AcceptedOn = reader["AcceptedOn"] == DBNull.Value ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("AcceptedOn")),
                BoostedOn = reader["BoostedOn"] == DBNull.Value ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("BoostedOn")),
                FingerPrint = reader["FingerPrint"] == DBNull.Value ? null : reader["FingerPrint"].ToString(),
                AcceptKey = reader["AcceptKey"] == DBNull.Value ? null : reader["AcceptKey"].ToString(),
                Badge = new Badge { Id = reader.GetInt64(reader.GetOrdinal("BadgeId")) },
                Hashtags = reader["Hashtags"] == DBNull.Value ? null : reader["Hashtags"].ToString(),
                IsExternal = reader["IsExternal"] == DBNull.Value ? false : reader.GetBoolean(reader.GetOrdinal("IsExternal")),
            });
        }

        return records;
    }

    public List<BadgeRecord> GetBadgeRecords(long? id = null)
    {
        var records = new List<BadgeRecord>();
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        var whereClause = new List<string>();

        if (id.HasValue)
        {
            whereClause.Add("Id = @Id");
            command.Parameters.AddWithValue("@Id", id.Value);
        }

        command.CommandText = "SELECT * FROM BadgeRecord" + 
            (whereClause.Count > 0 ? " WHERE " + string.Join(" AND ", whereClause) : "");

        if (id.HasValue)
        {
            Log(LogLevel.Debug, "{CommandText} - Id = {Id}", command.CommandText, id);
        }

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            records.Add(new BadgeRecord
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                NoteId = reader["NoteId"] == DBNull.Value ? null : reader["NoteId"].ToString(),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                IssuedBy = reader.GetString(reader.GetOrdinal("IssuedBy")),
                Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString(),
                Image = reader["Image"] == DBNull.Value ? null : reader["Image"].ToString(),
                ImageAltText = reader["ImageAltText"] == DBNull.Value ? null : reader["ImageAltText"].ToString(),
                EarningCriteria = reader["EarningCriteria"] == DBNull.Value ? null : reader["EarningCriteria"].ToString(),
                IssuedOn = reader.GetDateTime(reader.GetOrdinal("IssuedOn")),
                IssuedToEmail = reader.GetString(reader.GetOrdinal("IssuedToEmail")),
                IssuedToName = reader.GetString(reader.GetOrdinal("IssuedToName")),
                IssuedToSubjectUri = reader.GetString(reader.GetOrdinal("IssuedToSubjectUri")),
                AcceptedOn = reader["AcceptedOn"] == DBNull.Value ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("AcceptedOn")),
                BoostedOn = reader["BoostedOn"] == DBNull.Value ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("BoostedOn")),
                FingerPrint = reader["FingerPrint"] == DBNull.Value ? null : reader["FingerPrint"].ToString(),
                AcceptKey = reader["AcceptKey"] == DBNull.Value ? null : reader["AcceptKey"].ToString(),
                Badge = new Badge { Id = reader.GetInt64(reader.GetOrdinal("BadgeId")) },
                Hashtags = reader["Hashtags"] == DBNull.Value ? null : reader["Hashtags"].ToString(),
                IsExternal = reader["IsExternal"] == DBNull.Value ? false : reader.GetBoolean(reader.GetOrdinal("IsExternal")),
            });
        }

        return records;
    }
    
    public class Filter
    {
        public string? Where { get; set; }

        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }

    public List<BadgeRecord> GetBadgeRecords(string filter, bool includeBadge = false)
    {
        return GetBadgeRecords(new Filter { Where = filter }, includeBadge);
    }

    public List<BadgeRecord> GetBadgeRecords(Filter filter, bool includeBadge = false)
    {
        var records = new List<BadgeRecord>();
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();

        if (includeBadge)
        {
            command.CommandText = @"
                SELECT br.*, 
                       b.Id AS Badge_Id,
                       b.Title AS Badge_Title,
                       b.Description AS Badge_Description,
                       b.IssuedBy AS Badge_IssuedBy,
                       b.Image AS Badge_Image,
                       b.ImageAltText AS Badge_ImageAltText,
                       b.EarningCriteria AS Badge_EarningCriteria,
                       b.BadgeType AS Badge_BadgeType,
                       b.Hashtags AS Badge_Hashtags,
                       b.InfoUri AS Badge_InfoUri,
                       b.IsCertificate AS Badge_IsCertificate
                FROM BadgeRecord br
                LEFT JOIN Badge b ON br.BadgeId = b.Id";
        }
        else
        {
            command.CommandText = "SELECT * FROM BadgeRecord AS br";
        }

        if (!string.IsNullOrWhiteSpace(filter.Where))
        {
            command.CommandText += " WHERE " + filter.Where;
            foreach (var param in filter.Parameters)
            {
                command.Parameters.AddWithValue(param.Key, param.Value);
            }
        }

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            Badge badge;
            if (includeBadge && reader["Badge_Id"] != DBNull.Value)
            {
                badge = new Badge
                {
                    Id = reader.GetInt64(reader.GetOrdinal("Badge_Id")),
                    Title = reader["Badge_Title"] == DBNull.Value ? null : reader["Badge_Title"].ToString(),
                    Description = reader["Badge_Description"] == DBNull.Value ? null : reader["Badge_Description"].ToString(),
                    IssuedBy = reader["Badge_IssuedBy"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Badge_IssuedBy"]),
                    Image = reader["Badge_Image"] == DBNull.Value ? null : reader["Badge_Image"].ToString(),
                    ImageAltText = reader["Badge_ImageAltText"] == DBNull.Value ? null : reader["Badge_ImageAltText"].ToString(),
                    EarningCriteria = reader["Badge_EarningCriteria"] == DBNull.Value ? null : reader["Badge_EarningCriteria"].ToString(),
                    BadgeType = reader["Badge_BadgeType"] == DBNull.Value ? null : reader["Badge_BadgeType"].ToString(),
                    Hashtags = reader["Badge_Hashtags"] == DBNull.Value ? null : reader["Badge_Hashtags"].ToString(),
                    InfoUri = reader["Badge_InfoUri"] == DBNull.Value ? null : reader["Badge_InfoUri"].ToString(),
                    IsCertificate = reader["Badge_IsCertificate"] != DBNull.Value && Convert.ToBoolean(reader["Badge_IsCertificate"])
                };
            }
            else
            {
                badge = new Badge { Id = reader.GetInt64(reader.GetOrdinal("BadgeId")) };
            }

            records.Add(new BadgeRecord
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                NoteId = reader["NoteId"] == DBNull.Value ? null : reader["NoteId"].ToString(),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                IssuedBy = reader.GetString(reader.GetOrdinal("IssuedBy")),
                Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString(),
                Image = reader["Image"] == DBNull.Value ? null : reader["Image"].ToString(),
                ImageAltText = reader["ImageAltText"] == DBNull.Value ? null : reader["ImageAltText"].ToString(),
                EarningCriteria = reader["EarningCriteria"] == DBNull.Value ? null : reader["EarningCriteria"].ToString(),
                IssuedOn = reader.GetDateTime(reader.GetOrdinal("IssuedOn")),
                IssuedToEmail = reader.GetString(reader.GetOrdinal("IssuedToEmail")),
                IssuedToName = reader.GetString(reader.GetOrdinal("IssuedToName")),
                IssuedToSubjectUri = reader.GetString(reader.GetOrdinal("IssuedToSubjectUri")),
                AcceptedOn = reader["AcceptedOn"] == DBNull.Value ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("AcceptedOn")),
                BoostedOn = reader["BoostedOn"] == DBNull.Value ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("BoostedOn")),
                FingerPrint = reader["FingerPrint"] == DBNull.Value ? null : reader["FingerPrint"].ToString(),
                AcceptKey = reader["AcceptKey"] == DBNull.Value ? null : reader["AcceptKey"].ToString(),
                Badge = badge,
                Hashtags = reader["Hashtags"] == DBNull.Value ? null : reader["Hashtags"].ToString(),
                IsExternal = reader["IsExternal"] == DBNull.Value ? false : reader.GetBoolean(reader.GetOrdinal("IsExternal")),
            });
        }

        return records;
    }

    public InstanceDescription GetInstanceDescription()
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM InstanceDescription LIMIT 1";

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new InstanceDescription
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Name = reader["Name"] as string ?? "",
                Description = reader["Description"] as string ?? "",
                Purpose = reader["Purpose"] as string ?? "",
                ContactInfo = reader["ContactInfo"] as string ?? "",
                CustomLandingPageHtml = reader["CustomLandingPageHtml"] as string ?? "",
                IsEnabled = reader.GetBoolean(reader.GetOrdinal("IsEnabled")),
                LandingPageType = reader["LandingPageType"] as string ?? "default",
                StaticPageFilename = reader["StaticPageFilename"] as string ?? ""
            };
        }

        return new InstanceDescription();
    }

    public void SaveInstanceDescription(InstanceDescription description)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.Transaction = transaction;

        // Sanitize input - remove HTML tags
        description.Name = System.Text.RegularExpressions.Regex.Replace(description.Name ?? "", "<.*?>", "").Trim();
        description.Description = System.Text.RegularExpressions.Regex.Replace(description.Description ?? "", "<.*?>", "").Trim();
        description.Purpose = System.Text.RegularExpressions.Regex.Replace(description.Purpose ?? "", "<.*?>", "").Trim();
        description.ContactInfo = System.Text.RegularExpressions.Regex.Replace(description.ContactInfo ?? "", "<.*?>", "").Trim();

        if (description.Id == 0)
        {
            command.CommandText = @"
                INSERT INTO InstanceDescription (Name, Description, Purpose, ContactInfo, CustomLandingPageHtml, IsEnabled, LandingPageType, StaticPageFilename)
                VALUES (@Name, @Description, @Purpose, @ContactInfo, @CustomLandingPageHtml, @IsEnabled, @LandingPageType, @StaticPageFilename);
                SELECT last_insert_rowid();
            ";
        }
        else
        {
            command.CommandText = @"
                UPDATE InstanceDescription SET 
                    Name = @Name, 
                    Description = @Description, 
                    Purpose = @Purpose, 
                    ContactInfo = @ContactInfo,
                    CustomLandingPageHtml = @CustomLandingPageHtml,
                    IsEnabled = @IsEnabled,
                    LandingPageType = @LandingPageType,
                    StaticPageFilename = @StaticPageFilename
                WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", description.Id);
        }

        command.Parameters.AddWithValue("@Name", description.Name);
        command.Parameters.AddWithValue("@Description", description.Description);
        command.Parameters.AddWithValue("@Purpose", description.Purpose);
        command.Parameters.AddWithValue("@ContactInfo", description.ContactInfo);
        command.Parameters.AddWithValue("@CustomLandingPageHtml", description.CustomLandingPageHtml);
        command.Parameters.AddWithValue("@IsEnabled", description.IsEnabled);
        command.Parameters.AddWithValue("@LandingPageType", description.LandingPageType);
        command.Parameters.AddWithValue("@StaticPageFilename", description.StaticPageFilename);

        if (description.Id == 0)
        {
            description.Id = Convert.ToInt32(command.ExecuteScalar());
        }
        else
        {
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void InitializeInstanceDescriptionTable()
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS InstanceDescription (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL DEFAULT '',
                Description TEXT NOT NULL DEFAULT '',
                Purpose TEXT NOT NULL DEFAULT '',
                ContactInfo TEXT NOT NULL DEFAULT '',
                IsEnabled BOOLEAN NOT NULL DEFAULT 0
            );
        ";
        command.ExecuteNonQuery();
    }

    // Invitation management methods
    public void UpsertInvitation(Invitation invitation)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Invitations (id, email, invitedBy, createdAt, expiresAt, acceptedBy, acceptedAt, isActive, role, notes, groupId)
            VALUES (@Id, @Email, @InvitedBy, @CreatedAt, @ExpiresAt, @AcceptedBy, @AcceptedAt, @IsActive, @Role, @Notes, @GroupId)
            ON CONFLICT(id) DO UPDATE SET
                email = excluded.email,
                invitedBy = excluded.invitedBy,
                expiresAt = excluded.expiresAt,
                acceptedBy = excluded.acceptedBy,
                acceptedAt = excluded.acceptedAt,
                isActive = excluded.isActive,
                role = excluded.role,
                notes = excluded.notes,
                groupId = excluded.groupId;
        ";

        command.Parameters.AddWithValue("@Id", invitation.Id);
        command.Parameters.AddWithValue("@Email", invitation.Email);
        command.Parameters.AddWithValue("@InvitedBy", invitation.InvitedBy);
        command.Parameters.AddWithValue("@CreatedAt", invitation.CreatedAt);
        command.Parameters.AddWithValue("@ExpiresAt", invitation.ExpiresAt);
        command.Parameters.AddWithValue("@AcceptedBy", invitation.AcceptedBy ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@AcceptedAt", invitation.AcceptedAt ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IsActive", invitation.IsActive);
        command.Parameters.AddWithValue("@Role", invitation.Role);
        command.Parameters.AddWithValue("@Notes", invitation.Notes ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@GroupId", invitation.GroupId);

        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public Invitation? GetInvitationById(string id)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Invitations WHERE id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new Invitation
            {
                Id = reader["id"].ToString()!,
                Email = reader["email"].ToString()!,
                InvitedBy = reader["invitedBy"].ToString()!,
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("createdAt")),
                ExpiresAt = reader.GetDateTime(reader.GetOrdinal("expiresAt")),
                AcceptedBy = reader["acceptedBy"] == DBNull.Value ? null : reader["acceptedBy"].ToString(),
                AcceptedAt = reader["acceptedAt"] == DBNull.Value ? null : reader.GetDateTime(reader.GetOrdinal("acceptedAt")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("isActive")),
                Role = reader["role"].ToString()!,
                Notes = reader["notes"] == DBNull.Value ? null : reader["notes"].ToString(),
                GroupId = reader["groupId"] == DBNull.Value ? "system" : reader["groupId"].ToString()!
            };
        }

        return null;
    }

    public List<Invitation> GetInvitations(string? filter = null)
    {
        var invitations = new List<Invitation>();

        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        var query = "SELECT * FROM Invitations";
        
        if (!string.IsNullOrEmpty(filter))
        {
            query += $" WHERE {filter}";
        }
        
        query += " ORDER BY createdAt DESC";
        
        command.CommandText = query;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            invitations.Add(new Invitation
            {
                Id = reader["id"].ToString()!,
                Email = reader["email"].ToString()!,
                InvitedBy = reader["invitedBy"].ToString()!,
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("createdAt")),
                ExpiresAt = reader.GetDateTime(reader.GetOrdinal("expiresAt")),
                AcceptedBy = reader["acceptedBy"] == DBNull.Value ? null : reader["acceptedBy"].ToString(),
                AcceptedAt = reader["acceptedAt"] == DBNull.Value ? null : reader.GetDateTime(reader.GetOrdinal("acceptedAt")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("isActive")),
                Role = reader["role"].ToString()!,
                Notes = reader["notes"] == DBNull.Value ? null : reader["notes"].ToString(),
                GroupId = reader["groupId"] == DBNull.Value ? "system" : reader["groupId"].ToString()!
            });
        }

        return invitations;
    }

    public void AcceptInvitation(string invitationId, string userId)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Invitations 
            SET acceptedBy = @UserId, acceptedAt = @AcceptedAt 
            WHERE id = @InvitationId AND acceptedBy IS NULL";

        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@AcceptedAt", DateTime.UtcNow);
        command.Parameters.AddWithValue("@InvitationId", invitationId);

        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public void DeactivateInvitation(string invitationId)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = "UPDATE Invitations SET isActive = 0 WHERE id = @InvitationId";
        command.Parameters.AddWithValue("@InvitationId", invitationId);

        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public bool IsInvitationValid(string invitationId)
    {
        var invitation = GetInvitationById(invitationId);
        return invitation?.IsValid ?? false;
    }

    // DiscoveredServer methods
    public void UpsertDiscoveredServer(DiscoveredServer server)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        if (server.Id == 0)
        {
            command.CommandText = @"
                INSERT INTO DiscoveredServers (name, url, description, categories, admin, actor, isFollowed, followedAt, createdAt, updatedAt)
                VALUES (@Name, @Url, @Description, @Categories, @Admin, @Actor, @IsFollowed, @FollowedAt, @CreatedAt, @UpdatedAt)
                ON CONFLICT(url) DO UPDATE SET
                    name = @Name,
                    description = @Description,
                    categories = @Categories,
                    admin = @Admin,
                    actor = @Actor,
                    isFollowed = @IsFollowed,
                    followedAt = @FollowedAt,
                    updatedAt = @UpdatedAt;
                SELECT last_insert_rowid();
            ";
        }
        else
        {
            command.CommandText = @"
                UPDATE DiscoveredServers SET 
                    name = @Name, 
                    url = @Url, 
                    description = @Description, 
                    categories = @Categories,
                    admin = @Admin,
                    actor = @Actor,
                    isFollowed = @IsFollowed,
                    followedAt = @FollowedAt,
                    updatedAt = @UpdatedAt
                WHERE id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", server.Id);
        }

        command.Parameters.AddWithValue("@Name", server.Name);
        command.Parameters.AddWithValue("@Url", server.Url);
        command.Parameters.AddWithValue("@Description", server.Description);
        command.Parameters.AddWithValue("@Categories", server.Categories);
        command.Parameters.AddWithValue("@Admin", server.Admin);
        command.Parameters.AddWithValue("@Actor", server.Actor);
        command.Parameters.AddWithValue("@IsFollowed", server.IsFollowed);
        command.Parameters.AddWithValue("@FollowedAt", (object?)server.FollowedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("@CreatedAt", server.CreatedAt);
        command.Parameters.AddWithValue("@UpdatedAt", server.UpdatedAt);

        if (server.Id == 0)
        {
            var result = command.ExecuteScalar();
            if (result != null && long.TryParse(result.ToString(), out long newId))
            {
                server.Id = (int)newId;
            }
        }
        else
        {
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public Actor? GetMainActor()
    {
        return GetActorByFilter("IsMain = 1");
    }
    
    public List<DiscoveredServer> GetAllDiscoveredServers()
    {
        var servers = new List<DiscoveredServer>();

        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM DiscoveredServers ORDER BY name";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            servers.Add(new DiscoveredServer
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                Name = reader.GetString(reader.GetOrdinal("name")),
                Url = reader.GetString(reader.GetOrdinal("url")),
                Description = reader.GetString(reader.GetOrdinal("description")),
                Categories = reader.GetString(reader.GetOrdinal("categories")),
                Admin = reader.GetString(reader.GetOrdinal("admin")),
                Actor = reader.GetString(reader.GetOrdinal("actor")),
                IsFollowed = reader.GetBoolean(reader.GetOrdinal("isFollowed")),
                FollowedAt = reader.IsDBNull(reader.GetOrdinal("followedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("followedAt")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("createdAt")),
                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updatedAt"))
            });
        }

        return servers;
    }

    public DiscoveredServer? GetDiscoveredServerByUrl(string url)
    {
        using var connection = GetConnection();
        connection.Open();
        
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM DiscoveredServers WHERE url = @Url";
        command.Parameters.AddWithValue("@Url", url);
        
        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new DiscoveredServer
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                Name = reader.GetString(reader.GetOrdinal("name")),
                Url = reader.GetString(reader.GetOrdinal("url")),
                Description = reader.GetString(reader.GetOrdinal("description")),
                Categories = reader.GetString(reader.GetOrdinal("categories")),
                Admin = reader.GetString(reader.GetOrdinal("admin")),
                Actor = reader.GetString(reader.GetOrdinal("actor")),
                IsFollowed = reader.GetBoolean(reader.GetOrdinal("isFollowed")),
                FollowedAt = reader.IsDBNull(reader.GetOrdinal("followedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("followedAt")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("createdAt")),
                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updatedAt"))
            };
        }
        
        return null;
    }

    public DiscoveredServer? GetDiscoveredServerById(int id)
    {
        using var connection = GetConnection();
        connection.Open();
        
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM DiscoveredServers WHERE id = @Id";
        command.Parameters.AddWithValue("@Id", id);
        
        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new DiscoveredServer
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                Name = reader.GetString(reader.GetOrdinal("name")),
                Url = reader.GetString(reader.GetOrdinal("url")),
                Description = reader.GetString(reader.GetOrdinal("description")),
                Categories = reader.GetString(reader.GetOrdinal("categories")),
                Admin = reader.GetString(reader.GetOrdinal("admin")),
                Actor = reader.GetString(reader.GetOrdinal("actor")),
                IsFollowed = reader.GetBoolean(reader.GetOrdinal("isFollowed")),
                FollowedAt = reader.IsDBNull(reader.GetOrdinal("followedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("followedAt")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("createdAt")),
                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updatedAt"))
            };
        }
        
        return null;
    }

    // Static Pages management methods
    public void UpsertStaticPage(StaticPage page)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        if (page.Id == 0)
        {
            command.CommandText = @"
                INSERT INTO StaticPages (Filename, Title, Description, FileSize, CreatedAt, UpdatedAt)
                VALUES (@Filename, @Title, @Description, @FileSize, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                ON CONFLICT(Filename) DO UPDATE SET
                    Title = @Title,
                    Description = @Description,
                    FileSize = @FileSize,
                    UpdatedAt = CURRENT_TIMESTAMP;
                SELECT last_insert_rowid();
            ";
        }
        else
        {
            command.CommandText = @"
                UPDATE StaticPages SET 
                    Title = @Title, 
                    Description = @Description, 
                    FileSize = @FileSize,
                    UpdatedAt = CURRENT_TIMESTAMP
                WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", page.Id);
        }

        command.Parameters.AddWithValue("@Filename", page.Filename);
        command.Parameters.AddWithValue("@Title", page.Title);
        command.Parameters.AddWithValue("@Description", page.Description);
        command.Parameters.AddWithValue("@FileSize", page.FileSize);

        if (page.Id == 0)
        {
            var result = command.ExecuteScalar();
            if (result != null && long.TryParse(result.ToString(), out long newId))
            {
                page.Id = (int)newId;
            }
        }
        else
        {
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public List<StaticPage> GetAllStaticPages()
    {
        var pages = new List<StaticPage>();

        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM StaticPages ORDER BY CreatedAt DESC";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            pages.Add(new StaticPage
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Filename = reader.GetString(reader.GetOrdinal("Filename")),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                Description = reader.GetString(reader.GetOrdinal("Description")),
                FileSize = reader.GetInt64(reader.GetOrdinal("FileSize")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
            });
        }

        return pages;
    }

    public StaticPage? GetStaticPageByFilename(string filename)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM StaticPages WHERE Filename = @Filename";
        command.Parameters.AddWithValue("@Filename", filename);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new StaticPage
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Filename = reader.GetString(reader.GetOrdinal("Filename")),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                Description = reader.GetString(reader.GetOrdinal("Description")),
                FileSize = reader.GetInt64(reader.GetOrdinal("FileSize")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
            };
        }

        return null;
    }

    public void DeleteStaticPage(int id)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM StaticPages WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        command.ExecuteNonQuery();
    }

    // Registration methods
    public async Task<int> CreateRegistrationAsync(Registration registration)
    {
        using var connection = GetConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Registrations (FormData, Email, Name, IpAddress, UserAgent, SubmittedAt, IsReviewed, IsApproved)
            VALUES (@FormData, @Email, @Name, @IpAddress, @UserAgent, @SubmittedAt, @IsReviewed, @IsApproved);
            SELECT last_insert_rowid();";

        command.Parameters.AddWithValue("@FormData", registration.FormData);
        command.Parameters.AddWithValue("@Email", registration.Email ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Name", registration.Name ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IpAddress", registration.IpAddress);
        command.Parameters.AddWithValue("@UserAgent", registration.UserAgent);
        command.Parameters.AddWithValue("@SubmittedAt", registration.SubmittedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        command.Parameters.AddWithValue("@IsReviewed", registration.IsReviewed);
        command.Parameters.AddWithValue("@IsApproved", registration.IsApproved);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<List<Registration>> GetRegistrationsAsync(bool? reviewed = null, bool? approved = null)
    {
        var registrations = new List<Registration>();

        using var connection = GetConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        var sql = "SELECT * FROM Registrations";
        var conditions = new List<string>();

        if (reviewed.HasValue)
        {
            conditions.Add("IsReviewed = @IsReviewed");
        }

        if (approved.HasValue)
        {
            conditions.Add("IsApproved = @IsApproved");
        }

        if (conditions.Any())
        {
            sql += " WHERE " + string.Join(" AND ", conditions);
        }

        sql += " ORDER BY SubmittedAt DESC";

        command.CommandText = sql;

        if (reviewed.HasValue)
        {
            command.Parameters.AddWithValue("@IsReviewed", reviewed.Value);
        }

        if (approved.HasValue)
        {
            command.Parameters.AddWithValue("@IsApproved", approved.Value);
        }

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            registrations.Add(new Registration
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                FormData = reader.GetString(reader.GetOrdinal("FormData")),
                Email = reader.IsDBNull(reader.GetOrdinal("Email")) ? null : reader.GetString(reader.GetOrdinal("Email")),
                Name = reader.IsDBNull(reader.GetOrdinal("Name")) ? null : reader.GetString(reader.GetOrdinal("Name")),
                IpAddress = reader.GetString(reader.GetOrdinal("IpAddress")),
                UserAgent = reader.GetString(reader.GetOrdinal("UserAgent")),
                SubmittedAt = reader.GetDateTime(reader.GetOrdinal("SubmittedAt")),
                IsReviewed = reader.GetBoolean(reader.GetOrdinal("IsReviewed")),
                IsApproved = reader.GetBoolean(reader.GetOrdinal("IsApproved")),
                ReviewNotes = reader.IsDBNull(reader.GetOrdinal("ReviewNotes")) ? null : reader.GetString(reader.GetOrdinal("ReviewNotes")),
                ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
                ReviewedBy = reader.IsDBNull(reader.GetOrdinal("ReviewedBy")) ? null : reader.GetString(reader.GetOrdinal("ReviewedBy"))
            });
        }

        return registrations;
    }

    public async Task<Registration?> GetRegistrationByIdAsync(int id)
    {
        using var connection = GetConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Registrations WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Registration
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                FormData = reader.GetString(reader.GetOrdinal("FormData")),
                Email = reader.IsDBNull(reader.GetOrdinal("Email")) ? null : reader.GetString(reader.GetOrdinal("Email")),
                Name = reader.IsDBNull(reader.GetOrdinal("Name")) ? null : reader.GetString(reader.GetOrdinal("Name")),
                IpAddress = reader.GetString(reader.GetOrdinal("IpAddress")),
                UserAgent = reader.GetString(reader.GetOrdinal("UserAgent")),
                SubmittedAt = reader.GetDateTime(reader.GetOrdinal("SubmittedAt")),
                IsReviewed = reader.GetBoolean(reader.GetOrdinal("IsReviewed")),
                IsApproved = reader.GetBoolean(reader.GetOrdinal("IsApproved")),
                ReviewNotes = reader.IsDBNull(reader.GetOrdinal("ReviewNotes")) ? null : reader.GetString(reader.GetOrdinal("ReviewNotes")),
                ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
                ReviewedBy = reader.IsDBNull(reader.GetOrdinal("ReviewedBy")) ? null : reader.GetString(reader.GetOrdinal("ReviewedBy"))
            };
        }

        return null;
    }

    public async Task<bool> ReviewRegistrationAsync(int id, bool approved, string? notes = null, string? reviewedBy = null)
    {
        using var connection = GetConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Registrations 
            SET IsReviewed = 1, IsApproved = @IsApproved, ReviewNotes = @ReviewNotes, 
                ReviewedAt = @ReviewedAt, ReviewedBy = @ReviewedBy
            WHERE Id = @Id";

        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@IsApproved", approved);
        command.Parameters.AddWithValue("@ReviewNotes", notes ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ReviewedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        command.Parameters.AddWithValue("@ReviewedBy", reviewedBy ?? (object)DBNull.Value);

        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    // UserGroup management methods
    public void UpsertUserGroup(UserGroup userGroup)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO UserGroups (id, name, description, createdAt)
            VALUES (@Id, @Name, @Description, COALESCE(@CreatedAt, CURRENT_TIMESTAMP))
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                description = excluded.description;
        ";

        command.Parameters.AddWithValue("@Id", userGroup.Id);
        command.Parameters.AddWithValue("@Name", userGroup.Name);
        command.Parameters.AddWithValue("@Description", userGroup.Description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@CreatedAt", userGroup.CreatedAt == default ? (object)DBNull.Value : userGroup.CreatedAt);

        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public List<UserGroup> GetAllUserGroups()
    {
        var groups = new List<UserGroup>();
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM UserGroups ORDER BY createdAt DESC";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            groups.Add(new UserGroup
            {
                Id = reader["id"].ToString()!,
                Name = reader["name"].ToString()!,
                Description = reader["description"]?.ToString(),
                CreatedAt = reader["createdAt"] == DBNull.Value ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("createdAt"))
            });
        }

        return groups;
    }

    public UserGroup? GetUserGroupById(string id)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM UserGroups WHERE id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new UserGroup
            {
                Id = reader["id"].ToString()!,
                Name = reader["name"].ToString()!,
                Description = reader["description"]?.ToString(),
                CreatedAt = reader["createdAt"] == DBNull.Value ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("createdAt"))
            };
        }

        return null;
    }

    public List<User> GetUsersByGroupId(string groupId)
    {
        var users = new List<User>();
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Users WHERE groupId = @GroupId ORDER BY createdAt DESC";
        command.Parameters.AddWithValue("@GroupId", groupId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            users.Add(new User
            {
                Id = reader["id"].ToString()!,
                Email = reader["email"].ToString()!,
                GivenName = reader["givenName"].ToString()!,
                Surname = reader["surname"].ToString()!,
                CreatedAt = reader["createdAt"] == DBNull.Value ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("createdAt")),
                Provider = reader["provider"].ToString()!,
                Role = reader["role"].ToString()!,
                IsActive = reader["isActive"] == DBNull.Value ? true : reader.GetBoolean(reader.GetOrdinal("isActive")),
                GroupId = reader["groupId"]?.ToString() ?? "system"
            });
        }

        return users;
    }

    public void DeleteUserGroup(string id)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        // Check if group has users
        var userCountCommand = connection.CreateCommand();
        userCountCommand.CommandText = "SELECT COUNT(*) FROM Users WHERE groupId = @Id";
        userCountCommand.Parameters.AddWithValue("@Id", id);
        var userCount = Convert.ToInt32(userCountCommand.ExecuteScalar());

        if (userCount > 0)
        {
            throw new InvalidOperationException($"Cannot delete group '{id}' because it contains {userCount} user(s). Please move users to another group first.");
        }

        // Prevent deletion of system group
        if (id == "system")
        {
            throw new InvalidOperationException("Cannot delete the system group.");
        }

        // Delete the group
        var deleteCommand = connection.CreateCommand();
        deleteCommand.CommandText = "DELETE FROM UserGroups WHERE id = @Id";
        deleteCommand.Parameters.AddWithValue("@Id", id);
        deleteCommand.ExecuteNonQuery();

        transaction.Commit();
    }

    public int CountUsersInGroup(string groupId)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Users WHERE groupId = @GroupId";
        command.Parameters.AddWithValue("@GroupId", groupId);

        var result = command.ExecuteScalar();
        return Convert.ToInt32(result);
    }

    public void UpdateUserGroup(string userId, string groupId)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = "UPDATE Users SET groupId = @GroupId WHERE id = @UserId";
        command.Parameters.AddWithValue("@GroupId", groupId);
        command.Parameters.AddWithValue("@UserId", userId);

        command.ExecuteNonQuery();
        transaction.Commit();
    }
}
