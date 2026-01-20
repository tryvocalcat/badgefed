# Federation

## Supported federation protocols and standards

- [ActivityPub](https://www.w3.org/TR/activitypub/) (Server-to-Server)
- [WebFinger](https://webfinger.net/)
- [HTTP Signatures](https://datatracker.ietf.org/doc/html/draft-cavage-http-signatures)
- [OpenBadges 2.0](https://www.imsglobal.org/sites/default/files/Badges/OBv2p0Final/index.html)

## Supported FEPs

- [FEP-67ff: FEDERATION.md](https://codeberg.org/fediverse/fep/src/branch/main/fep/67ff/fep-67ff.md)
- [FEP-044f: Consent-respecting quote posts](https://codeberg.org/fediverse/fep/src/branch/main/fep/044f/fep-044f.md)

## Overview

BadgeFed is a federated digital credentials platform that leverages ActivityPub to enable decentralized badge issuance, sharing, and verification across the fediverse. BadgeFed combines ActivityPub federation with OpenBadges 2.0 standards to create a trusted, interoperable credential ecosystem.

## Actor Types

BadgeFed supports the following ActivityPub actor types:

- **Person**: Badge recipients and individual user accounts. BadgeFed do not provide new recipients.
- **Organization**: Badge issuers and institutional accounts. This is the issuer actor.
- **Application**: Instance announcer (relay bot) and system actors.

## ActivityPub Implementation

### Badge Distribution via Notes

- **Badges are distributed as ActivityPub Notes** for maximum fediverse compatibility
- Each Note contains an **OpenBadge 2.0 Assertion** as an attachment
- Notes include human-readable badge information in the `content` field
- Recipients are mentioned using ActivityPub `Mention` objects in the `tag` array

### Issuer-Actor Authentication

BadgeFed enforces strict authentication between OpenBadge issuers and ActivityPub actors:

- The OpenBadge `issuer.url` **MUST exactly match** the ActivityPub Actor URL
- Only authenticated issuers can distribute badges under their identity
- Badges from unverified sources are rejected to prevent spoofing

### Recipient Verification

- OpenBadge recipients **MUST use the `url` type** pointing to their ActivityPub actor and be non-hashed
- The recipient URL **SHOULD appear as a Mention** in the ActivityPub Note
- This dual-verification prevents credential misattribution

### Instance Announcer Actor

Each BadgeFed instance includes a special announcer actor that:

- **Type**: `Application`
- **Purpose**: Announces all badges issued on the instance using `Announce` activities
- **Discovery**: Enables followers to receive all instance badge activity (aka relay)

## Supported Activities

### Outbound Activities

Activities that BadgeFed sends to other federated instances:

#### Create
Sent when a badge is issued and shared.

```json
{
  "@context": "https://www.w3.org/ns/activitystreams",
  "type": "Create",
  "actor": "https://badges.example.com/actors/issuer1",
  "object": {
    "type": "Note",
    "id": "https://badges.example.com/badge/123",
    "attributedTo": "https://badges.example.com/actors/issuer1",
    "content": "<h1>Achievement Badge</h1><p>Awarded to @recipient for outstanding work.</p>",
    "attachment": [
      {
        "@context": "https://w3id.org/openbadges/v2",
        "type": "Assertion",
        "id": "https://badges.example.com/badge/123/assertion",
        "recipient": {
          "type": "url",
          "identity": "https://social.example.com/users/recipient"
        },
        "badge": "https://badges.example.com/badge/123/badgeclass",
        "verification": {
          "type": "HostedBadge"
        },
        "issuedOn": "2024-01-15T12:00:00Z"
      }
    ],
    "tag": [
      {
        "type": "Mention",
        "href": "https://social.example.com/users/recipient",
        "name": "@recipient@social.example.com"
      }
    ],
    "to": ["https://www.w3.org/ns/activitystreams#Public"],
    "cc": ["https://social.example.com/users/recipient"]
  }
}
```

#### Announce
Sent by the instance announcer to boost all badges issued on the instance.

```json
{
  "@context": "https://www.w3.org/ns/activitystreams",
  "type": "Announce",
  "actor": "https://badges.example.com/actors/announcer",
  "object": "https://badges.example.com/badge/123",
  "published": "2024-01-15T12:01:00Z"
}
```

#### Follow
Sent when issuers follow other issuers or when users follow the announcer.

#### Accept/Reject
Sent in response to Follow activities based on account settings.

#### Update
Sent when badge or actor information is modified.

#### Delete
Sent when badges are revoked or actors are deleted.

### Inbound Activities

Activities that BadgeFed processes from other federated instances:

#### Create
- Processes incoming Notes with OpenBadge attachments
- Validates issuer-actor authentication
- Imports valid badges into the local database
- Processes comments/replies to existing badges

#### Follow
- Accepts follows from other ActivityPub actors
- Enables badge distribution to followers

#### Accept/Reject
- Processes responses to outbound follow requests

#### Update/Delete
- Processes updates and deletions from federated actors
- Maintains data consistency across instances

## Security

### HTTP Signatures
- All federated requests use HTTP Signatures for authentication
- Algorithm: `rsa-sha256`
- Headers: `(request-target) host date`
- Minimum key size: 2048 bits RSA

### Dual Signature Validation
BadgeFed validates both:
- **ActivityPub signatures** for message authenticity
- **OpenBadge signatures** (when present) for credential integrity

### Domain Management
- Administrators can block domains to prevent federation
- Blocked domains cannot deliver activities or access content
- Instance-level moderation controls available

## WebFinger

BadgeFed implements WebFinger for actor discovery:

### User Discovery
```
GET /.well-known/webfinger?resource=acct:issuer@badges.example.com
```

### Response Example
```json
{
  "subject": "acct:issuer@badges.example.com",
  "aliases": [
    "https://badges.example.com/actors/issuer",
    "https://badges.example.com/@issuer"
  ],
  "links": [
    {
      "rel": "self",
      "type": "application/activity+json",
      "href": "https://badges.example.com/actors/issuer"
    }
  ]
}
```

## Collections

### Followers/Following
Standard ActivityPub collections for social graph management.

### Outbox
Contains public activities including badge issuance and announcements.

### Badge Collections
Custom collections for organizing badges by category, issuer, or recipient.

## OpenBadges Integration

BadgeFed seamlessly integrates OpenBadges 2.0 with ActivityPub:

- **Badge Classes** define the criteria and metadata for badges
- **Assertions** represent individual badge awards
- **Verification** ensures badge authenticity and prevents tampering
- **Evidence** links provide proof of achievement
- **Endorsements** enable third-party validation

## Example Badge Federation

The following example demonstrates a complete badge issuance flow:

### Badge Note Example
```json
{
  "@context": "https://www.w3.org/ns/activitystreams",
  "id": "https://badges.vocalcat.com/grant/badgesvocalcatcom_72_11_89fb419013f52130e83e223663906b6b",
  "type": "Note",
  "attributedTo": "https://badges.vocalcat.com/actors/badges.vocalcat.com/badgefed",
  "content": "<h1>BadgeFed Unconference Participant – FediForum June 2025</h1><p>The verified Badge was issued to <a href=\"https://toot.lqdev.tech/@lqdev\" class=\"u-url mention\">@lqdev</a></p>",
  "attachment": [
    {
      "@context": "https://w3id.org/openbadges/v2",
      "type": "Assertion",
      "id": "https://badges.vocalcat.com/grant/badgesvocalcatcom_72_11_89fb419013f52130e83e223663906b6b/assertion",
      "recipient": {
        "type": "url",
        "identity": "https://toot.lqdev.tech/@lqdev"
      },
      "badge": "https://badges.vocalcat.com/grant/badgesvocalcatcom_72_11_89fb419013f52130e83e223663906b6b/badgeclass",
      "verification": {
        "type": "HostedBadge"
      },
      "issuedOn": "2025-06-06T16:38:42.5864904Z",
      "image": "https://badges.vocalcat.com/uploads/badges/654b53e0-6162-4b66-949d-17d3ddf2af77.png"
    }
  ],
  "to": [
    "https://www.w3.org/ns/activitystreams#Public",
    "https://toot.lqdev.tech/@lqdev"
  ],
  "cc": ["https://toot.lqdev.tech/@lqdev"],
  "published": "2025-06-07T03:03:49.4957233Z",
  "tag": [
    {
      "type": "Hashtag",
      "href": "https://badges.vocalcat.com/tags/badgefedopenbadgesbadefedfediforumfediverseactivitypub",
      "name": "#badgefed#openbadges#badefed#fediforum#fediverse#activitypub"
    },
    {
      "type": "Mention",
      "href": "https://toot.lqdev.tech/@lqdev",
      "name": "@lqdev"
    }
  ],
  "replies": {
    "id": "https://badges.vocalcat.com/grant/badgesvocalcatcom_72_11_89fb419013f52130e83e223663906b6b/comments/",
    "type": "Collection",
    "first": {
      "type": "CollectionPage",
      "partOf": "https://badges.vocalcat.com/grant/badgesvocalcatcom_72_11_89fb419013f52130e83e223663906b6b/comments/",
      "items": []
    }
  }
}
```

This example shows how BadgeFed:
1. Embeds OpenBadge assertions as attachments in ActivityPub Notes
2. Uses mentions to link recipients to their ActivityPub actors
3. Maintains compatibility with standard fediverse platforms
4. Provides human-readable badge information in the content field

## Delivery and Processing

### Outbound Delivery
- Activities are queued for asynchronous delivery
- HTTP POST requests to recipient inboxes
- Retry with exponential backoff for failed deliveries
- Permanent failure logging after maximum retries

### Inbound Processing
- HTTP signature verification
- Badge validation against OpenBadge standards
- Issuer-actor authentication checks
- Import valid badges to local database
- Federated comment processing

## Testing Federation

### Manual Testing
Test BadgeFed federation with curl:

```bash
# Fetch issuer actor
curl -H "Accept: application/activity+json" \
     https://badges.example.com/actors/issuer

# WebFinger lookup
curl "https://badges.example.com/.well-known/webfinger?resource=acct:issuer@badges.example.com"

# Fetch badge Note
curl -H "Accept: application/activity+json" \
     https://badges.example.com/badge/123
```

## Future Considerations

Planned federation features:
- **Report/Flag**: Cross-instance moderation reports
- **Move**: Account migration support  

## References

- [ActivityPub Specification](https://www.w3.org/TR/activitypub/)
- [ActivityStreams 2.0](https://www.w3.org/TR/activitystreams-core/)
- [OpenBadges 2.0 Specification](https://www.imsglobal.org/sites/default/files/Badges/OBv2p0Final/index.html)
- [WebFinger RFC 7033](https://tools.ietf.org/html/rfc7033)
- [HTTP Signatures](https://tools.ietf.org/html/draft-cavage-http-signatures)