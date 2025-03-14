using System.Security.Cryptography;
using System.Text;
using ActivityPubDotNet.Core;
using BadgeFed.Models;

namespace BadgeFed.Services;

public class BadgeService
{
    public static Badge GetBadge()
    {
        var badge = new Badge();
        return badge;
    }

    public static ActivityPubNote GetNoteFromBadge(Badge badge)
    {
        var note = NotesService.GetNote(
            badge.Id.ToString(),
            badge.Description,
            $"https://{badge.Actor.Domain}/badge/{badge.Id}",
            badge.Actor);

        note.BadgeMetadata = badge;

        return note;
    }

    public static Badge GetBadgeFromNote(ActivityPubNote note)
    {
        var badge = new Badge
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
}