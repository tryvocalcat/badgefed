OpenBadges 3.0 credential

```json
{
  "@context": [
    "https://www.w3.org/ns/credentials/v2",
    "https://purl.imsglobal.org/spec/ob/v3p0/context-3.0.3.json",
    "https://your_url/your_context"
  ],
  "id": "http://example.com/credentials/3527",
  "type": ["VerifiableCredential", "OpenBadgeCredential"],
  "issuer": {
    "id": "https://example.com/issuers/876543",
    "type": ["Profile"],
    "name": "Example Corp"
  },
  "validFrom": "2010-01-01T00:00:00Z",
  "name": "Teamwork Badge",
  "credentialSubject": {
    "id": "did:example:ebfeb1f712ebc6f1c276e12ec21",
    "type": ["AchievementSubject"],
    "achievement": {
              "id": "https://example.com/achievements/21st-century-skills/teamwork",
              "type": ["Achievement", "MyCustomAchievement"],
              "criteria": {
                  "narrative": "Team members are nominated for this badge by their peers and recognized upon review by Example Corp management."
              },
              "description": "This badge recognizes the development of the capacity to collaborate within a group environment.",
              "name": "Teamwork",
            "myField": "Put your custom value here."
            "anotherField": "2024-07-24T00.00:00Z"
          }
  },
  "credentialSchema": [{
    "id": "https://purl.imsglobal.org/spec/ob/v3p0/schema/json/ob_v3p0_achievementcredential_schema.json",
    "type": "1EdTechJsonSchemaValidator2019"
  }, {
    "id": "https://your_url/your_schema.json",
    "type": "1EdTechJsonSchemaValidator2019"
  }]
}
```

ActivityPub Mastodon Note

```json
{
  "@context": [
    "https://www.w3.org/ns/activitystreams",
    {
      "ostatus": "http://ostatus.org#",
      "atomUri": "ostatus:atomUri",
      "inReplyToAtomUri": "ostatus:inReplyToAtomUri",
      "conversation": "ostatus:conversation",
      "sensitive": "as:sensitive",
      "toot": "http://joinmastodon.org/ns#",
      "votersCount": "toot:votersCount",
      "blurhash": "toot:blurhash",
      "focalPoint": {
        "@container": "@list",
        "@id": "toot:focalPoint"
      },
      "Hashtag": "as:Hashtag"
    }
  ],
  "id": "https://mastodon.social/users/vocalcat/statuses/114163386791391296",
  "type": "Note",
  "summary": null,
  "inReplyTo": null,
  "published": "2025-03-14T23:30:37Z",
  "url": "https://mastodon.social/@vocalcat/114163386791391296",
  "attributedTo": "https://mastodon.social/users/vocalcat",
  "to": [
    "https://mastodon.social/users/vocalcat/followers"
  ],
  "cc": [
    "https://www.w3.org/ns/activitystreams#Public"
  ],
  "sensitive": false,
  "atomUri": "https://mastodon.social/users/vocalcat/statuses/114163386791391296",
  "inReplyToAtomUri": null,
  "conversation": "tag:mastodon.social,2025-03-14:objectId=946081145:objectType=Conversation",
  "content": "<p>What are you doing? Testing a Badge System built on <a href=\"https://mastodon.social/tags/activitypub\" class=\"mention hashtag\" rel=\"tag\">#<span>activitypub</span></a> and the <a href=\"https://mastodon.social/tags/fediverse\" class=\"mention hashtag\" rel=\"tag\">#<span>fediverse</span></a> empowering communities to issue and verify badges in a federated, open way, or what are you doing?</p>",
  "contentMap": {
    "en": "<p>What are you doing? Testing a Badge System built on <a href=\"https://mastodon.social/tags/activitypub\" class=\"mention hashtag\" rel=\"tag\">#<span>activitypub</span></a> and the <a href=\"https://mastodon.social/tags/fediverse\" class=\"mention hashtag\" rel=\"tag\">#<span>fediverse</span></a> empowering communities to issue and verify badges in a federated, open way, or what are you doing?</p>"
  },
  "attachment": [
    {
      "type": "Document",
      "mediaType": "image/png",
      "url": "https://files.mastodon.social/media_attachments/files/114/163/316/878/576/964/original/924b020b3a096315.png",
      "name": null,
      "blurhash": "UBRW3k4m9Fj?t7offkWBkE%N%Mt8ofWBWBae",
      "width": 1009,
      "height": 922
    },
    {
      "type": "Document",
      "mediaType": "image/png",
      "url": "https://files.mastodon.social/media_attachments/files/114/163/382/738/140/201/original/e4e7a49e6ca2a418.png",
      "name": null,
      "blurhash": "UAS6Pm00xu-;auM_a#axxvj^Rjayt6Rjt6t7",
      "width": 593,
      "height": 479
    }
  ],
  "tag": [
    {
      "type": "Hashtag",
      "href": "https://mastodon.social/tags/activitypub",
      "name": "#activitypub"
    },
    {
      "type": "Hashtag",
      "href": "https://mastodon.social/tags/fediverse",
      "name": "#fediverse"
    }
  ],
  "replies": {
    "id": "https://mastodon.social/users/vocalcat/statuses/114163386791391296/replies",
    "type": "Collection",
    "first": {
      "type": "CollectionPage",
      "next": "https://mastodon.social/users/vocalcat/statuses/114163386791391296/replies?only_other_accounts=true&page=true",
      "partOf": "https://mastodon.social/users/vocalcat/statuses/114163386791391296/replies",
      "items": []
    }
  },
  "likes": {
    "id": "https://mastodon.social/users/vocalcat/statuses/114163386791391296/likes",
    "type": "Collection",
    "totalItems": 2
  },
  "shares": {
    "id": "https://mastodon.social/users/vocalcat/statuses/114163386791391296/shares",
    "type": "Collection",
    "totalItems": 3
  }
}
```

ActivityPub Private Note

```json
{
    "@context": [
        "https://www.w3.org/ns/activitystreams",
        {
            "ostatus": "http://ostatus.org#",
            "atomUri": "ostatus:atomUri",
            "inReplyToAtomUri": "ostatus:inReplyToAtomUri",
            "conversation": "ostatus:conversation",
            "sensitive": "as:sensitive",
            "toot": "http://joinmastodon.org/ns#",
            "votersCount": "toot:votersCount"
        }
    ],
    ...
    "to": [
        <userid>
    ],
    "cc": [], <-- blank
    "sensitive": false,
    "conversation": "tag:mastodon.social,2025-03-20:objectId=951173231:objectType=Conversation", 
    ...
    "tag": [
        {
            "type": "Mention",
            "href": "https://mastodon.social/users/sweedarbk",
            "name": "@sweedarbk"
        }
    ],
    ...
}
```

BadgeFed badge

```json
{
  "@context": "https://www.w3.org/ns/activitystreams",
  "id": "https://badges.vocalcat.com/badge/10",
  "type": "Note or Document?",
  "content": "<human readable representation of the badge>", <--- Title + Description + Earning Criteria
  "url": "https://badges.vocalcat.com/record/10",
  "attributedTo": "https://badges.vocalcat.com/actors/badges.vocalcat.com/fediverse", <--- IssuedBy
  "vocalcat:badges": {
    "Id": 10, <- omit this
    "Title": "Fediverse Early Adopter of a Decentralized Badge System",
    "IssuedBy": "https://badges.vocalcat.com/actors/badges.vocalcat.com/fediverse",
    "Description": "This badge recognizes the recipient as an early adopter of decentralized badge systems within the Fediverse. By embracing open, federated technologies, they have contributed to the growth of verifiable, community-driven recognition across decentralized platforms.",
    "Image": "/uploads/badges/5db7c054-8f5b-43d1-8443-a17b9c322b16.png",
    "EarningCriteria": "The recipient of this badge was an early adopter of a decentralized badge system within the Fediverse, actively engaging in its development, adoption, or advocacy. Their contributions helped advance federated, verifiable recognition across decentralized platforms, shaping the future of digital credentials.",
    "IssuedUsing": "", <- delete this
    "IssuedOn": "2025-03-16T00:50:41.1217287",
    "IssuedTo": "mictlan@gmail.com",
    "AcceptedOn": "2025-03-16T00:50:59.7959342",
    "LastUpdated": null
  },
  "to": [
    "https://www.w3.org/ns/activitystreams#Public" <-- default is public
    <other> <-- others are the actor receiving the grant
  ],
  "cc": [], <-- followers or other instances
  "published": "2025-03-16T00:51:43.2827218Z", <-- AcceptedOn
  "tags": [],
  "replies": {
    "id": "https://badges.vocalcat.com/comments/10",
    "type": "Collection",
    "first": {
      "type": "CollectionPage",
      "next": "https://badges.vocalcat.com/comments/10?page=true",
      "partOf": "https://badges.vocalcat.com/comments/10",
      "items": []
    }
  }
}

CREATE TABLE Badge (
    Id INTEGER PRIMARY KEY,
    Title TEXT NOT NULL CHECK(length(Title) >= 2 AND length(Title) <= 100),
    IssuedBy TEXT NOT NULL,
    Description TEXT CHECK(length(Description) <= 500),
    Image TEXT,
    EarningCriteria TEXT CHECK(length(EarningCriteria) <= 500),
    IssuedUsing TEXT,
    IssuedOn DATETIME NOT NULL,
    IssuedTo INTEGER NOT NULL,
    AcceptedOn DATETIME,
    LastUpdated DATETIME DEFAULT CURRENT_TIMESTAMP,
    FingerPrint TEXT NOT NULL,
    BadgeDefinitionId INTEGER NOT NULL,
    FOREIGN KEY (BadgeDefinitionId) REFERENCES BadgeDefinition(Id),
    FOREIGN KEY (IssuedTo) REFERENCES Recipient(Id)
);
```

# Supported activities for statuses

Created

BadgeFed tries to detect if the status is a badge, in that case store the badge into database. If the status is a reply to a badge, it is stored as as a celebration comment.

Delete

BadgeFed tries to detect if the status is a badge, in that case deletes the badge into database. If the status is a reply to a badge, it is deleted as a celebration comment.

Like

BadgeFed likes the badge.

Undo

Undo a previous like.

Announce

Used in other platforms to boost in their social graphs is ignored by BadgeFed, and we don't keep track of boosts in the platform.

The first-class Object type supported by BadgeFed is Note and Document.

