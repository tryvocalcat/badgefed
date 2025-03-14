using System.Security.Cryptography;
using System.Text;
using ActivityPubDotNet.Core;
using BadgeFed.Models;

namespace BadgeFed.Services;

public class NotesService
{
    public static string GetLinkUniqueHash(string input)
    {
        using (MD5 md5 = MD5.Create())
        {
            // Convert the input string to a byte array and compute the hash.
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            // Convert the byte array to a hexadecimal string representation.
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("x2")); // "x2" means hexadecimal with two digits.
            }

            return sb.ToString();
        }
    }

    public static ActivityPubNote GetNote(string id, string content, string url, Actor actor)
    {
        var tags = new List<ActivityPubNote.Tag>()
        {
            //new NoteTag() { Type = "Mention", Href = outboxConfig.AuthorUrl, Name = outboxConfig.AuthorUsername }
        };

        var baseTagUrl = $"{actor.Domain}/tags";

        /*var itemTags = item?.Tags as List<string> ?? [];

        if (itemTags?.Count > 0)
        {
            foreach (var tag in item?.Tags ?? Enumerable.Empty<string>())
            {
                tags.Add(new NoteTag()
                {
                    Type = "Hashtag",
                    Href = $"{baseTagUrl}/{tag}",
                    Name = $"#{tag}"
                });
            }
        }*/

        var noteId = $"https://{actor.Domain}/badge/{id}";

        var note = new ActivityPubNote
        {
            Id = noteId,
            Type = "Note",
            Content = content,
            Url = url,
            AttributedTo = actor.Uri?.ToString()!,
            To = new List<string>() { "https://www.w3.org/ns/activitystreams#Public" },
            Cc = new List<string>(),
            Published = DateTime.UtcNow, // Added current date and time
            Tags = tags,
            Replies = new ActivityPubNote.Collection
            {
                Id = $"https://{actor.Domain}/comments/{id}",
                Type = "Collection",
                First = new ActivityPubNote.CollectionPage
                {
                    Type = "CollectionPage",
                    Next = $"https://{actor.Domain}/comments/{id}?page=true",
                    PartOf = $"https://{actor.Domain}/comments/{id}",
                    Items = new List<string>()
                }
            }
        };

        return note;
    }

    public static ActivityPubCreate GetCreateNote(ActivityPubNote note, Actor actor)
    {
        return new ActivityPubCreate()
        {
            Id = $"{note.Id}/create",
            Actor = actor.Uri?.ToString()!,
            Published = note.Published,
            Object = note
        };
    }
}