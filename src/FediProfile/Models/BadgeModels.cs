namespace FediProfile.Models;

public record BadgeIssuerResponse(
    int Id,
    string Name,
    string ActorUrl,
    string? Avatar,
    string? Bio,
    bool Following,
    DateTime CreatedUtc
);

public record ReceivedBadgeResponse(
    int Id,
    string NoteId,
    int IssuerId,
    string Title,
    string? Image,
    string? Description,
    string? IssuedOn,
    string? AcceptedOn,
    DateTime ReceivedUtc
);

public record FollowerResponse(
    string FollowerUri,
    string Domain,
    string? AvatarUri,
    string? DisplayName,
    DateTime CreatedUtc
);
