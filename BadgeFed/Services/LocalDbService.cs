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

    public static LocalDbService GetInstance(string username)
    {
        string currentDirectory = Directory.GetCurrentDirectory();
        string filePath = Path.Combine(currentDirectory, $"{username.ToLowerInvariant()}.db");
        return new LocalDbService(filePath);
    }

    public LocalDbService(string dbPath)
    {
        this.dbPath = dbPath;
        this.connectionString = $"Data Source={dbPath};Version=3;";

        CreateDb();
    }

    public SQLiteConnection GetConnection()
    {
        return new SQLiteConnection(connectionString);
    }

    private void CreateDb()
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Actor (
                Id INTEGER PRIMARY KEY,
                Name TEXT NOT NULL,
                Summary TEXT,
                AvatarPath TEXT,
                InformationUri TEXT,
                Domain TEXT,
                CreatedAt DATETIME NOT NULL,
                UpdatedAt DATETIME NOT NULL,
                PublicKeyPem TEXT
            );
        ";
        command.ExecuteNonQuery();
    }

    public void UpsertActor(Actor actor)
    {
        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Actor (Id, Name, Summary, AvatarPath, InformationUri, Uri, Domain, PublicKeyPem, PrivateKeyPem)
            VALUES (@Id, @Name, @Summary, @AvatarPath, @InformationUri, @Uri, @Domain, @PublicKeyPem, @PrivateKeyPem)
            ON CONFLICT(Id) DO UPDATE SET
                Name = excluded.Name,
                Summary = excluded.Summary,
                AvatarPath = excluded.AvatarPath,
                InformationUri = excluded.InformationUri,
                Domain = excluded.Domain,
                PublicKeyPem = excluded.PublicKeyPem,
                PrivateKeyPem = excluded.PrivateKeyPem,
                Uri = excluded.Uri;
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

        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public void DeleteActor(int id)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Actor WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        command.ExecuteNonQuery();
    }

    public Actor GetActorById(int id)
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
                FullName = reader["FullName"].ToString()!,
                Summary = reader["Summary"] == DBNull.Value ? string.Empty : reader["Summary"].ToString()!,
                AvatarPath = reader["AvatarPath"] == DBNull.Value ? null : reader["AvatarPath"].ToString(),
                InformationUri = reader["InformationUri"] == DBNull.Value ? null : new Uri(reader["InformationUri"].ToString()!),
                Domain = reader["Domain"] == DBNull.Value ? null : reader["Domain"].ToString(),
                PublicKeyPem = reader["PublicKeyPem"] == DBNull.Value ? null : reader["PublicKeyPem"].ToString(),
                Uri = reader["Uri"] == DBNull.Value ? null : new Uri(reader["Uri"].ToString()!),
                PrivateKeyPem = reader["PrivateKeyPem"] == DBNull.Value ? null : reader["PrivateKeyPem"].ToString()
            };
        }

        return null;
    }

    public Actor GetActorByFilter(string filter)
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = $"SELECT Id, Name, Summary, AvatarPath, InformationUri, Domain, PublicKeyPem, Uri, PrivateKeyPem FROM Actor WHERE {filter}";
        
        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new Actor
            {
                Id = int.Parse(reader["Id"].ToString()!),
                FullName = reader["Name"].ToString()!,
                Summary = reader["Summary"] == DBNull.Value ? string.Empty : reader["Summary"].ToString()!,
                AvatarPath = reader["AvatarPath"] == DBNull.Value ? null : reader["AvatarPath"].ToString(),
                InformationUri = reader["InformationUri"] == DBNull.Value ? null : new Uri(reader["InformationUri"].ToString()!),
                Domain = reader["Domain"] == DBNull.Value ? null : reader["Domain"].ToString(),
                PublicKeyPem = reader["PublicKeyPem"] == DBNull.Value ? null : reader["PublicKeyPem"].ToString(),
                Uri = reader["Uri"] == DBNull.Value ? null : new Uri(reader["Uri"].ToString()!),
                PrivateKeyPem = reader["PrivateKeyPem"] == DBNull.Value ? null : reader["PrivateKeyPem"].ToString()
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
}