namespace FediProfile.Models;

public record LinkResponse(
    int Id,
    string Name,
    string? Icon,
    string Url,
    string? Description,
    bool AutoBoost,
    string? Category,
    string? Type,
    bool Hidden,
    DateTime CreatedUtc
);
