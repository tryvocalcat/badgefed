using Microsoft.Extensions.Logging;
using System;
using System.Data.SQLite;
using System.Text.Json;
using BadgeFed.Models;

namespace BadgeFed.Services;

public class LocalDbService
{
    private readonly string connectionString;

    private readonly string dbPath;

    public static LocalDbService GetInstance(string dbName)
    {
        string currentDirectory = Directory.GetCurrentDirectory();
        string filePath = Path.Combine(currentDirectory, $"{dbName.ToLowerInvariant()}.db");
        return new LocalDbService(filePath);
    }

    public LocalDbService(string dbPath)
    {
        this.dbPath = dbPath;
        this.connectionString = $"Data Source={dbPath};Version=3;";

        CreateDb();
    }

    public static LocalDbService GetGlobalDatabase()
    {
        return GetInstance("_global.db");
    }

    public SQLiteConnection GetConnection()
    {
        return new SQLiteConnection(connectionString);
    }

    private void CreateDb()
    {
    }

    public void UpsertBadgeComment(string badgeUri, string noteId)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO BadgeComments (BadgeUri, NoteId)
            VALUES (@BadgeUri, @NoteId)
            ON CONFLICT(BadgeUri, NoteId) DO NOTHING;
        ";

        command.Parameters.AddWithValue("@BadgeUri", badgeUri);
        command.Parameters.AddWithValue("@NoteId", noteId);

        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public List<(string BadgeUri, string NoteId)> GetBadgeComments(string badgeUri = null)
    {
        var result = new List<(string BadgeUri, string NoteId)>();
        
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        
        if (string.IsNullOrEmpty(badgeUri))
        {
            command.CommandText = "SELECT BadgeUri, NoteId FROM BadgeComments";
        }
        else
        {
            command.CommandText = "SELECT BadgeUri, NoteId FROM BadgeComments WHERE BadgeUri = @BadgeUri";
            command.Parameters.AddWithValue("@BadgeUri", badgeUri);
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add((
                reader.GetString(0),
                reader.GetString(1)
            ));
        }

        return result;
    }

    public void UpsertActor(Actor actor)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Actor (Name, Summary, AvatarPath, InformationUri, Uri, Domain, PublicKeyPem, PrivateKeyPem, Username)
            VALUES (@Name, @Summary, @AvatarPath, @InformationUri, @Uri, @Domain, @PublicKeyPem, @PrivateKeyPem, @Username);
        ";

        command.Parameters.AddWithValue("@Id", actor.Id);
        command.Parameters.AddWithValue("@Name", actor.FullName);
        command.Parameters.AddWithValue("@Summary", actor.Summary);
        command.Parameters.AddWithValue("@AvatarPath", actor.AvatarPath);
        command.Parameters.AddWithValue("@InformationUri", actor.InformationUri);
        command.Parameters.AddWithValue("@Domain", actor.Domain);
        command.Parameters.AddWithValue("@PublicKeyPem", actor.PublicKeyPem);
        command.Parameters.AddWithValue("@PrivateKeyPem", actor.PrivateKeyPem);
        command.Parameters.AddWithValue("@Uri", actor.Uri);
        command.Parameters.AddWithValue("@Username", actor.Username);

        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public void DeleteActor(long id)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Actor WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        command.ExecuteNonQuery();
    }

    public List<Actor> GetActors()
    {
        var actors = new List<Actor>();

        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Actor";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            actors.Add(new Actor
            {
                Id = int.Parse(reader["Id"].ToString()!),
                FullName = reader["Name"].ToString()!,
                Summary = reader["Summary"] == DBNull.Value ? string.Empty : reader["Summary"].ToString()!,
                AvatarPath = reader["AvatarPath"] == DBNull.Value ? null : reader["AvatarPath"].ToString(),
                InformationUri = reader["InformationUri"] == DBNull.Value ? null : reader["InformationUri"].ToString()!,
                Domain = reader["Domain"] == DBNull.Value ? null : reader["Domain"].ToString(),
                PublicKeyPem = reader["PublicKeyPem"] == DBNull.Value ? null : reader["PublicKeyPem"].ToString(),
                PrivateKeyPem = reader["PrivateKeyPem"] == DBNull.Value ? null : reader["PrivateKeyPem"].ToString(),
                Username = reader["Username"] == DBNull.Value ? null : reader["Username"].ToString()
            });
        }

        return actors;
    }

    public Actor GetActorById(long id)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Actor WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new Actor
            {
                Id = int.Parse(reader["Id"].ToString()!),
                FullName = reader["Name"].ToString()!,
                Summary = reader["Summary"] == DBNull.Value ? string.Empty : reader["Summary"].ToString()!,
                AvatarPath = reader["AvatarPath"] == DBNull.Value ? null : reader["AvatarPath"].ToString(),
                InformationUri = reader["InformationUri"] == DBNull.Value ? null : reader["InformationUri"].ToString()!,
                Domain = reader["Domain"] == DBNull.Value ? null : reader["Domain"].ToString(),
                PublicKeyPem = reader["PublicKeyPem"] == DBNull.Value ? null : reader["PublicKeyPem"].ToString(),
                PrivateKeyPem = reader["PrivateKeyPem"] == DBNull.Value ? null : reader["PrivateKeyPem"].ToString(),
                Username = reader["Username"] == DBNull.Value ? null : reader["Username"].ToString()
            };
        }

        return null;
    }

    public Actor GetActorByFilter(string filter)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = $"SELECT Id, Name, Summary, AvatarPath, InformationUri, Domain, PublicKeyPem, Username, PrivateKeyPem FROM Actor WHERE {filter}";
        
        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new Actor
            {
                Id = int.Parse(reader["Id"].ToString()!),
                FullName = reader["Name"].ToString()!,
                Summary = reader["Summary"] == DBNull.Value ? string.Empty : reader["Summary"].ToString()!,
                AvatarPath = reader["AvatarPath"] == DBNull.Value ? null : reader["AvatarPath"].ToString(),
                InformationUri = reader["InformationUri"] == DBNull.Value ? null : reader["InformationUri"].ToString()!,
                Domain = reader["Domain"] == DBNull.Value ? null : reader["Domain"].ToString(),
                PublicKeyPem = reader["PublicKeyPem"] == DBNull.Value ? null : reader["PublicKeyPem"].ToString(),
                PrivateKeyPem = reader["PrivateKeyPem"] == DBNull.Value ? null : reader["PrivateKeyPem"].ToString(),
                Username = reader["Username"] == DBNull.Value ? null : reader["Username"].ToString()
            };
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
            INSERT INTO Follower (FollowerUri, Domain, ActorId)
            VALUES (@FollowerUri, @Domain, @ActorId)
            ON CONFLICT(FollowerUri) DO UPDATE SET
                Domain = excluded.Domain,
                ActorId = excluded.ActorId,
                CreatedAt = excluded.CreatedAt;
        ";

        command.Parameters.AddWithValue("@FollowerUri", follower.FollowerUri);
        command.Parameters.AddWithValue("@Domain", follower.Domain);
        command.Parameters.AddWithValue("@ActorId", follower.Parent.Id);

        command.ExecuteNonQuery();
        transaction.Commit();
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
                Parent = new Actor() { Id = reader.GetInt64(3) }
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
                INSERT INTO Badge (Title, Description, IssuedBy, Image, ImageAltText, EarningCriteria, CreatedAt, UpdatedAt, BadgeType)
                VALUES (@Title, @Description, @IssuedBy, @Image, @ImageAltText, @EarningCriteria, datetime('now'), datetime('now'), @BadgeType);
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
                    BadgeType = @BadgeType
                WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", badge.Id);
        }

         Console.WriteLine($"Saving badge: {badge.Image}");

        command.Parameters.AddWithValue("@Title", badge.Title);
        command.Parameters.AddWithValue("@Description", badge.Description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IssuedBy", badge.IssuedBy);
        command.Parameters.AddWithValue("@Image", badge.Image ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ImageAltText", badge.ImageAltText ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@EarningCriteria", badge.EarningCriteria ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@BadgeType", badge.BadgeType);

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

    public List<Badge> GetAllBadgeDefinitions()
    {
        var badges = new List<Badge>();

        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Badge";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            badges.Add(new Badge
            {
                Id = reader.GetInt64(0),
                Title = reader.GetString(1),
                Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString()!,
                IssuedBy = reader.GetInt32(3),
                Image = reader["Image"] == DBNull.Value ? null : reader["Image"].ToString()!,
                ImageAltText = reader["ImageAltText"] == DBNull.Value ? null : reader["ImageAltText"].ToString()!,
                EarningCriteria = reader["EarningCriteria"] == DBNull.Value ? null : reader["EarningCriteria"].ToString()!,
                BadgeType = reader["BadgeType"].ToString()
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

    public Badge GetBadgeDefinitionById(long id)
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
                BadgeType = reader["BadgeType"].ToString()
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
            AND AcceptedOn IS NOT NULL 
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
    }

    public void UpdateBadgeSignature(BadgeRecord badgeRecord)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE BadgeRecord SET 
                FingerPrint = @FingerPrint
            WHERE Id = @Id;
        ";

        command.Parameters.AddWithValue("@Id", badgeRecord.Id);
        command.Parameters.AddWithValue("@FingerPrint", badgeRecord.FingerPrint);

        command.ExecuteNonQuery();
        transaction.Commit();
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
                AcceptKey, BadgeId
            )
            VALUES (
                @Title, @IssuedBy, @Description, @Image, @ImageAltText, @EarningCriteria,
                 @IssuedOn, @IssuedToSubjectUri, @IssuedToName, @IssuedToEmail,
                @AcceptKey, @BadgeId
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

        record.Id = Convert.ToInt64(command.ExecuteScalar());

        transaction.Commit();
    }

    public BadgeRecord? GetBadgeToAccept(long id, string key)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM BadgeRecord WHERE Id = @Id AND AcceptKey = @AcceptKey";
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@AcceptKey", key);

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
                FingerPrint = reader["FingerPrint"] == DBNull.Value ? null : reader["FingerPrint"].ToString(),
                AcceptKey = reader["AcceptKey"] == DBNull.Value ? null : reader["AcceptKey"].ToString(),
                Badge = new Badge { Id = reader.GetInt64(reader.GetOrdinal("BadgeId")) }
            };
        }

        return null;
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
                 AND AcceptedOn IS NOT NULL " +
                 (whereClause.Count > 0 ? " AND " + string.Join(" AND ", whereClause) : "");

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            records.Add(new BadgeRecord
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
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
                FingerPrint = reader["FingerPrint"] == DBNull.Value ? null : reader["FingerPrint"].ToString(),
                AcceptKey = reader["AcceptKey"] == DBNull.Value ? null : reader["AcceptKey"].ToString(),
                Badge = new Badge { Id = reader.GetInt64(reader.GetOrdinal("BadgeId")) }
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

        Console.WriteLine($"{command.CommandText} - Id = {id}");

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            records.Add(new BadgeRecord
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
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
                FingerPrint = reader["FingerPrint"] == DBNull.Value ? null : reader["FingerPrint"].ToString(),
                AcceptKey = reader["AcceptKey"] == DBNull.Value ? null : reader["AcceptKey"].ToString(),
                Badge = new Badge { Id = reader.GetInt64(reader.GetOrdinal("BadgeId")) }
            });
        }

        return records;
    }
}