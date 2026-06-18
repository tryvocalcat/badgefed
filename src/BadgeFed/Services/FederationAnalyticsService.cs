using System.Data.SQLite;
using BadgeFed.Models;

namespace BadgeFed.Services;

public class FederationAnalyticsService
{
    private readonly LocalScopedDb _db;
    private readonly ILogger<FederationAnalyticsService> _logger;

    public FederationAnalyticsService(LocalScopedDb db, ILogger<FederationAnalyticsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the GroupId for an actor by looking up Actor.OwnerId → Users.GroupId
    /// </summary>
    public string? ResolveGroupId(string? actorUri)
    {
        if (string.IsNullOrEmpty(actorUri)) return null;

        try
        {
            // Extract domain and username from URI like https://domain/actors/domain/username
            var uri = new Uri(actorUri);
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            
            // Expected format: actors/{domain}/{username}
            if (segments.Length >= 3 && segments[0] == "actors")
            {
                var domain = segments[1];
                var username = segments[2];
                var actor = _db.GetActorByFilter($"Username = \"{username}\" AND Domain = \"{domain}\"");
                if (actor != null && !string.IsNullOrEmpty(actor.OwnerId))
                {
                    var user = _db.GetUserById(actor.OwnerId);
                    return user?.GroupId;
                }
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Track event with automatic GroupId resolution from actorUri
    /// </summary>
    public void TrackEventAutoGroup(string eventType, string? actorUri = null, string? objectUri = null, string? targetUri = null, string? remoteHost = null, string? requestIp = null, string? userAgent = null)
    {
        var groupId = ResolveGroupId(actorUri);
        TrackEvent(eventType, actorUri, objectUri, targetUri, remoteHost, requestIp, userAgent, groupId);
    }

    public void TrackEvent(string eventType, string? actorUri = null, string? objectUri = null, string? targetUri = null, string? remoteHost = null, string? requestIp = null, string? userAgent = null, string? groupId = null)
    {
        try
        {
            using var connection = _db.GetConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO FederationEvent (EventType, ActorUri, ObjectUri, TargetUri, RemoteHost, RequestIp, UserAgent, GroupId, CreatedAt)
                VALUES (@EventType, @ActorUri, @ObjectUri, @TargetUri, @RemoteHost, @RequestIp, @UserAgent, @GroupId, @CreatedAt)";

            command.Parameters.AddWithValue("@EventType", eventType);
            command.Parameters.AddWithValue("@ActorUri", (object?)actorUri ?? DBNull.Value);
            command.Parameters.AddWithValue("@ObjectUri", (object?)objectUri ?? DBNull.Value);
            command.Parameters.AddWithValue("@TargetUri", (object?)targetUri ?? DBNull.Value);
            command.Parameters.AddWithValue("@RemoteHost", (object?)remoteHost ?? DBNull.Value);
            command.Parameters.AddWithValue("@RequestIp", (object?)requestIp ?? DBNull.Value);
            command.Parameters.AddWithValue("@UserAgent", (object?)userAgent ?? DBNull.Value);
            command.Parameters.AddWithValue("@GroupId", (object?)groupId ?? DBNull.Value);
            command.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow.ToString("o"));

            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track federation event: {EventType}", eventType);
        }
    }

    public List<FederationEvent> GetEvents(int limit = 100, string? eventType = null, string? groupId = null)
    {
        var events = new List<FederationEvent>();

        using var connection = _db.GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();

        var conditions = new List<string>();
        if (!string.IsNullOrEmpty(eventType))
        {
            conditions.Add("EventType = @EventType");
            command.Parameters.AddWithValue("@EventType", eventType);
        }
        if (!string.IsNullOrEmpty(groupId))
        {
            conditions.Add("GroupId = @GroupId");
            command.Parameters.AddWithValue("@GroupId", groupId);
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        command.CommandText = $"SELECT * FROM FederationEvent {whereClause} ORDER BY CreatedAt DESC LIMIT @Limit";
        command.Parameters.AddWithValue("@Limit", limit);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            events.Add(ReadEvent(reader));
        }

        return events;
    }

    public Dictionary<string, long> GetEventCounts(DateTime? since = null, string? groupId = null)
    {
        var counts = new Dictionary<string, long>();

        using var connection = _db.GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();

        var conditions = new List<string>();
        if (since.HasValue)
        {
            conditions.Add("CreatedAt >= @Since");
            command.Parameters.AddWithValue("@Since", since.Value.ToString("o"));
        }
        if (!string.IsNullOrEmpty(groupId))
        {
            conditions.Add("GroupId = @GroupId");
            command.Parameters.AddWithValue("@GroupId", groupId);
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        command.CommandText = $"SELECT EventType, COUNT(*) as Count FROM FederationEvent {whereClause} GROUP BY EventType ORDER BY Count DESC";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            counts[reader.GetString(0)] = reader.GetInt64(1);
        }

        return counts;
    }

    public Dictionary<string, Dictionary<string, long>> GetEventCountsByGroup(DateTime? since = null)
    {
        var result = new Dictionary<string, Dictionary<string, long>>();

        using var connection = _db.GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();

        if (since.HasValue)
        {
            command.CommandText = "SELECT GroupId, EventType, COUNT(*) as Count FROM FederationEvent WHERE CreatedAt >= @Since AND GroupId IS NOT NULL GROUP BY GroupId, EventType ORDER BY GroupId, Count DESC";
            command.Parameters.AddWithValue("@Since", since.Value.ToString("o"));
        }
        else
        {
            command.CommandText = "SELECT GroupId, EventType, COUNT(*) as Count FROM FederationEvent WHERE GroupId IS NOT NULL GROUP BY GroupId, EventType ORDER BY GroupId, Count DESC";
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var group = reader.GetString(0);
            var evtType = reader.GetString(1);
            var count = reader.GetInt64(2);

            if (!result.ContainsKey(group))
                result[group] = new Dictionary<string, long>();

            result[group][evtType] = count;
        }

        return result;
    }

    public long GetTotalEvents()
    {
        using var connection = _db.GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM FederationEvent";
        return (long)command.ExecuteScalar();
    }

    private static FederationEvent ReadEvent(SQLiteDataReader reader)
    {
        return new FederationEvent
        {
            Id = reader.GetInt64(reader.GetOrdinal("Id")),
            EventType = reader.GetString(reader.GetOrdinal("EventType")),
            ActorUri = reader.IsDBNull(reader.GetOrdinal("ActorUri")) ? null : reader.GetString(reader.GetOrdinal("ActorUri")),
            ObjectUri = reader.IsDBNull(reader.GetOrdinal("ObjectUri")) ? null : reader.GetString(reader.GetOrdinal("ObjectUri")),
            TargetUri = reader.IsDBNull(reader.GetOrdinal("TargetUri")) ? null : reader.GetString(reader.GetOrdinal("TargetUri")),
            RemoteHost = reader.IsDBNull(reader.GetOrdinal("RemoteHost")) ? null : reader.GetString(reader.GetOrdinal("RemoteHost")),
            RequestIp = reader.IsDBNull(reader.GetOrdinal("RequestIp")) ? null : reader.GetString(reader.GetOrdinal("RequestIp")),
            UserAgent = reader.IsDBNull(reader.GetOrdinal("UserAgent")) ? null : reader.GetString(reader.GetOrdinal("UserAgent")),
            GroupId = reader.IsDBNull(reader.GetOrdinal("GroupId")) ? null : reader.GetString(reader.GetOrdinal("GroupId")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")))
        };
    }
}
