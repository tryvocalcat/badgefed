using BadgeFed.Models;

namespace BadgeFed.Services;

public class NotesService
{
    class NoteTag
    {
        public string Type { get; set; }
        public string Href { get; set; }
        public string Name { get; set; }
    }

    public static dynamic GetNote(string content, string url, Actor actor)
    {
        /*var itemHash = RssUtils.GetLinkUniqueHash(item.Link!);

        var tags = new List<NoteTag>()
        {
            //new NoteTag() { Type = "Mention", Href = outboxConfig.AuthorUrl, Name = outboxConfig.AuthorUsername }
        };

        var baseTagUrl = $"{actor.Uri}/tags";*/

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

       /* var noteId = $"{outboxConfig.Domain}/{outboxConfig.NotesPath}/{itemHash}";

        var note = new
        {
            _context = "https://www.w3.org/ns/activitystreams",
            id = noteId,
            type = "Note",
            hash = itemHash,
            content,
            url,
            attributedTo = actor.Uri, // domain/@blog
            to = new List<string>() { "https://www.w3.org/ns/activitystreams#Public" },
            cc = new List<string>(),
            published = DateTime.UtcNow, // Added current date and time
            tag = tags,
            replies = new
            {
                id = $"{outboxConfig.Domain}/{outboxConfig.RepliesPath}/{itemHash}",
                type = "Collection",
                first = new
                {
                    type = "CollectionPage",
                    next = $"{outboxConfig.Domain}/{outboxConfig.RepliesPath}/{itemHash}?page=true",
                    partOf = $"{outboxConfig.Domain}/{outboxConfig.RepliesPath}/{itemHash}",
                    items = new List<string>()
                }
            }
        };
*/
        return null;
        //return note;
    }

    public static dynamic GetCreateNote(dynamic note, Actor actor)
    {
        var createNote = new {
            _context = "https://www.w3.org/ns/activitystreams",
            id = $"{note.id}/create",
            type = "Create",
            actor = actor.Uri,
            to = new List<string>() { "https://www.w3.org/ns/activitystreams#Public" },
            cc = new List<string>(),
            published = note.published,
            @object = note
        };

        return createNote;
    }
}