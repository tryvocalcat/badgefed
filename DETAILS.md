# ActivityPub Implementation in BadgeFed

BadgeFed leverages the ActivityPub protocol to enable decentralized badge issuance, sharing, and interaction across federated servers. Below is an overview of how ActivityPub is implemented in BadgeFed:

## Issuers as Actors

- **Issuers** in BadgeFed are represented as **ActivityPub Actors**.

- Each issuer acts as an ActivityPub actor and can interact with other actors in the network.

## Decentralized Badges via ActivityPub Notes

- **Badges** are decentralized using **ActivityPub Notes**.

- Initially, ActivityPub Documents were considered for badge distribution, but Mastodon and other major Fediverse platforms do not render Documents. Therefore, Notes are used for compatibility and visibility.

- Each Note can have **attachments**; one of these attachments is the badge itself (see [ActivityStreams attachment](https://www.w3.org/TR/activitystreams-vocabulary/#dfn-attachment)).

## OpenBadge 2.0 as Attachments

- The badge attachment within the Note follows the **OpenBadge 2.0 specification**.
- This ensures interoperability and standardization for badge data.
- When an issuer receives a Note in their inbox with an OpenBadge 2.0 attachment, the badge is automatically imported into the system. Other incoming Notes are ignored unless they are comments (replies) to a badge Note, in which case they are processed as federated comments.

## Verification and Issuer Linking

To ensure the authenticity of badges and prevent spoofing, BadgeFed enforces a strict link between the OpenBadge Issuer profile and the corresponding ActivityPub Actor:

- The `url` field in the OpenBadge Issuer profile **must exactly match** the ActivityPub Actor's URL for the issuer.
- When a badge is received, BadgeFed validates that the OpenBadge's `issuer.url` is identical to the ActivityPub Actor that sent the Note.
- Only badges where this relationship is confirmed are accepted and imported. Any OpenBadge assertions found in Notes where the issuer's URL does not match the sending ActivityPub Actor are ignored.

This mechanism ensures that only legitimate issuers can issue badges under their identity, and prevents badges from being circulated in the Fediverse by unauthorized actors.

## Recipient URL Linking

To further strengthen badge authenticity and traceability, BadgeFed enforces strict recipient URL linking:

- The **recipient** in the OpenBadge Assertion must use the `url` type, specifying the recipient's ActivityPub actor URL.
- This recipient URL must also appear as a **Mention** in the ActivityPub Note's `tags` array, referencing the same actor.
- BadgeFed validates that the recipient's URL in the OpenBadge matches the Mention in the ActivityPub object. Only badges where this relationship is confirmed are accepted and imported.

This ensures that badges are issued to verifiable, federated identities and prevents misattribution or spoofing of badge recipients.

## Signature Validation

BadgeFed validates both **OpenBadge** and **ActivityPub** signatures to ensure the authenticity and integrity of badge data:

- **ActivityPub signatures** are checked using HTTP Signatures to verify that incoming Notes and activities are genuinely sent by the claimed actor.
- **OpenBadge signatures** (if present) are validated according to the OpenBadge specification, ensuring that badge assertions have not been tampered with.

This dual-signature validation provides strong guarantees that badges and related activities originate from trusted sources and have not been altered in transit.

## Federation and Decentralization

- **Issuers can follow other issuers** using ActivityPub's follow mechanism.
- When a badge is issued, it is shared as a Note to the followers of the issuer, enabling decentralized propagation of badges across servers.

### Main Announcer Actor

- Each BadgeFed instance includes a **special announcer actor** responsible for boosting (using the ActivityPub `Announce` activity) every badge created within that instance.
- By following this main/default announcer, users can receive notifications for all badges issued on the instance, regardless of which issuer created them.
- This design improves badge discoverability and simplifies maintenance, as new badges are automatically shared by the announcer actor, making it easier for followers to stay updated with all badge activity.

## Comments and Interactions

- Users can **comment on badges**.
- Comments are also ActivityPub Notes and are federated, allowing them to appear across different BadgeFed instances and compatible Fediverse platforms.

## Example

The following example demonstrates how a badge is represented as an ActivityPub Note, with the badge data included as an OpenBadge 2.0 Assertion in the `attachment` field:

```json
{
  "@context": "https://www.w3.org/ns/activitystreams",
  "id": "https://badges.vocalcat.com/grant/badgesvocalcatcom_72_11_89fb419013f52130e83e223663906b6b",
  "type": "Note",
  "content": "<h1>BadgeFed Unconference Participant – FediForum June 2025</h1>\r\n        \r\n        <p>The verified Badge was issued to <a href=\"https://toot.lqdev.tech/@lqdev\" class=\"u-url mention\">@<span>lqdev</span></a></p>\r\n\r\n        <p><strong>This badge recognizes active participation in the BadgeFed Unconference session held during FediForum's June 2025 event. \n\nBadgeFed is an initiative dedicated to exploring and advancing decentralized digital credentials within the Fediverse. \n\nThis session brought together innovators, educators, enthusiasts, curious raccoons, and technologists to collaboratively shape the future of open badges and verifiable credentials in decentralized networks.</strong></p>\r\n\r\n        <p>Earning Criteria: To earn this badge, participants must have:\n\n* Attended the BadgeFed Unconference session at FediForum June 2025.\n* Actively engaging by asking questions, commenting, leading discussions, or engaging meaningfully in collaborative activities.\n* Demonstrated a commitment to advancing decentralized credentialing systems within the open social web.. <br />\r\n        \r\n        <i>Issued on: 06/06/2025 16:38:42</i><br />\r\n        <i>Accepted On: 06/06/2025 16:39:10</i>\r\n        </p>\r\n        \r\n        <p>Verify the Badge <a href='https://badges.vocalcat.com/grant/badgesvocalcatcom_72_11_89fb419013f52130e83e223663906b6b'>here</a>.</p>\r\n\r\n        <p> <a href =\"https://badges.vocalcat.com/tags/badgefedopenbadgesbadefedfediforumfediverseactivitypub\" class=\"mention hashtag\" rel=\"tag\">#<span>badgefed#openbadges#badefed#fediforum#fediverse#activitypub</span></a></p>\r\n        ",
  "url": "https://badges.vocalcat.com/grant/badgesvocalcatcom_72_11_89fb419013f52130e83e223663906b6b",
  "attributedTo": "https://badges.vocalcat.com/actors/badges.vocalcat.com/badgefed",
  "attachment": [
    {
        "@context": "https://w3id.org/openbadges/v2",
        "type": "Assertion",
        "id": "https://badges.vocalcat.com/grant/badgesvocalcatcom_72_11_89fb419013f52130e83e223663906b6b/assertion",
        "recipient": {
            "type": "email",
            "identity": "sha256$REDACTED",
            "hashed": true
        },
        "badge": "https://badges.vocalcat.com/grant/badgesvocalcatcom_72_11_89fb419013f52130e83e223663906b6b/badgeclass",
        "verification": {
            "type": "HostedBadge"
        },
        "issuedOn": "2025-06-06T16:38:42.5864904Z",
        "image": "https://badges.vocalcat.com/uploads/badges/654b53e0-6162-4b66-949d-17d3ddf2af77.png",
        "evidence": [
            {
                "id": "https://badges.vocalcat.com/grant/badgesvocalcatcom_72_11_89fb419013f52130e83e223663906b6b/evidence",
                "narrative": "Active participation in the BadgeFed Unconference session at FediForum June 2025, including asking questions, commenting, leading discussions, or engaging meaningfully in collaborative activities."
            }
        ]
    }
  ],
  "to": [
    "https://www.w3.org/ns/activitystreams#Public",
    "https://toot.lqdev.tech/@lqdev"
  ],
  "cc": [
    "https://toot.lqdev.tech/@lqdev"
  ],
  "published": "2025-06-07T03:03:49.4957233Z",
  "tag": [
    {
      "type": "Hashtag",
      "href": "https://badges.vocalcat.com/tags/badgefedopenbadgesbadefedfediforumfediverseactivitypub",
      "name": "#badgefed#openbadges#badefed#fediforum#fediverse#activitypub"
    },
    {
      "type": "Mention",
      "href": "https://badges.vocalcat.com/actors/badges.vocalcat.com/badgefed",
      "name": "@badgefed@badges.vocalcat.com"
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
      "next": "https://badges.vocalcat.com/grant/badgesvocalcatcom_72_11_89fb419013f52130e83e223663906b6b/comments/?page=true",
      "partOf": "https://badges.vocalcat.com/grant/badgesvocalcatcom_72_11_89fb419013f52130e83e223663906b6b/comments/",
      "items": []
    }
  },
  "inReplyTo": null
}
```

In this example, the `attachment` field contains an OpenBadge 2.0 Assertion, which holds the badge metadata, recipient, evidence, and verification information. This approach enables badges to be shared and verified across federated platforms while maintaining compatibility with both ActivityPub and OpenBadge standards.