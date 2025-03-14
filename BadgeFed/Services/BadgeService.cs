using System.Security.Cryptography;
using System.Text;
using ActivityPubDotNet.Core;
using BadgeFed.Models;

namespace BadgeFed.Services;

public class BadgeService
{
    private readonly LocalDbService DbService;

    public BadgeService(LocalDbService dbService)
    {
        DbService = dbService;
    }

    public static BadgeRecord GetBadge()
    {
        var badge = new BadgeRecord();
        return badge;
    }

    public static ActivityPubNote GetNoteFromBadgeREcord(BadgeRecord badge)
    {
        var note = NotesService.GetNote(
            badge.Id.ToString(),
            badge.Description,
            $"https://{badge.Actor.Domain}/record/{badge.Id}",
            badge.Actor);

        note.BadgeMetadata = badge;

        return note;
    }

    public static BadgeRecord GetBadgeRecordFromNote(ActivityPubNote note)
    {
        var badge = new BadgeRecord
        {
            Id = long.Parse(note.Id.Split('/').Last()),
            Title = note.Content,
            Description = note.Content,
            IssuedBy = note.AttributedTo,
            IssuedOn = note.Published,
            IssuedTo = note.To?.FirstOrDefault() ?? "",
            Image = "test.png",
            FingerPrint = "0001"
        };

        return badge;
    }

    public BadgeRecord GetGrantBadgeRecord(Badge badge, string recipient)
    {
        var acceptKey = Guid.NewGuid().ToString();

        var actor = DbService.GetActorById(badge.IssuedBy);

        var badgeRecord = new BadgeRecord()
        {
            Title = badge.Title,
            Description = badge.Description,
            IssuedBy = actor.Uri!.ToString(),
            IssuedOn = DateTime.UtcNow,
            Image = badge.Image,
            IssuedTo = recipient,
            EarningCriteria = badge.EarningCriteria,
            AcceptedOn = null,
            Badge = badge,
            AcceptKey = acceptKey
        };

        return badgeRecord;
    }
}