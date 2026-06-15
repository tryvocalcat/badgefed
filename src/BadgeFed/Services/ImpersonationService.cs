using System.Collections.Concurrent;

namespace BadgeFed.Services;

/// <summary>
/// Tracks admin impersonation sessions. Singleton service that maps
/// an admin's UserId to the target user they are impersonating.
/// Only admins can start impersonation; the real role is always preserved
/// so the admin can stop impersonation at any time.
/// </summary>
public class ImpersonationService
{
    private readonly ConcurrentDictionary<string, ImpersonationSession> _sessions = new();

    public void Start(string adminUserId, string targetGroupId, string targetRole, string targetUserName)
    {
        _sessions[adminUserId] = new ImpersonationSession
        {
            TargetGroupId = targetGroupId,
            TargetRole = targetRole,
            TargetUserName = targetUserName,
            StartedAt = DateTime.UtcNow
        };
    }

    public void Stop(string adminUserId)
    {
        _sessions.TryRemove(adminUserId, out _);
    }

    public ImpersonationSession? GetSession(string adminUserId)
    {
        _sessions.TryGetValue(adminUserId, out var session);
        return session;
    }

    public bool IsImpersonating(string adminUserId)
    {
        return _sessions.ContainsKey(adminUserId);
    }
}

public class ImpersonationSession
{
    public string TargetGroupId { get; set; } = string.Empty;
    public string TargetRole { get; set; } = string.Empty;
    public string TargetUserName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
}
