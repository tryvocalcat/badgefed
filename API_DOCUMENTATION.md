# BadgeFed Public API Documentation

## Overview

The BadgeFed Public API allows external applications to programmatically grant badges to recipients. All API endpoints are under the `/api/badges` base path.

## Authentication

All protected endpoints require an **API key**. The key can be provided in one of two ways:

| Method | Location | Example |
|--------|----------|---------|
| **Header** (recommended) | `X-ApiKey` request header | `X-ApiKey: your-api-key-here` |
| **Query parameter** | `apiKey` query string | `/api/badges/grant?apiKey=your-api-key-here` |

> **Note:** API keys are tied to active user accounts. There is currently no admin portal UI for managing API keys. You must set them directly in the database.

#### Setting an API Key via SQL

Generate a secure random key and assign it to a user:

```sql
-- Set an API key for a specific user by email
UPDATE Users SET ApiKey = 'your-secure-api-key-here' WHERE Email = 'user@example.com';
```

> **Tip:** Use a cryptographically random string for the key (e.g., a UUID or a 32+ character random hex string). For example on Linux/macOS: `openssl rand -hex 32`

---

## Endpoints

### List All Badges

Retrieve all active badge definitions owned by the authenticated user.

```
GET /api/badges
```

**Authentication:** Required

#### Request Headers

| Header | Required | Description |
|--------|----------|-------------|
| `X-ApiKey` | Yes* | Your API key (*or use `apiKey` query param) |

#### Example Request

```bash
curl https://your-instance.example.com/api/badges \
  -H "X-ApiKey: your-api-key-here"
```

#### Success Response

**Status:** `200 OK`

```json
{
  "success": true,
  "count": 2,
  "badges": [
    {
      "id": 1,
      "title": "Advanced Training",
      "description": "Awarded for completing advanced training",
      "badgeType": "Badge",
      "earningCriteria": "Complete the advanced training course",
      "image": "https://your-instance.example.com/badge-image/1",
      "imageAltText": "Advanced Training Badge",
      "hashtags": "#training #advanced",
      "infoUri": "https://example.com/training",
      "isCertificate": false,
      "issuer": {
        "id": 1,
        "fullName": "My Organization",
        "username": "org",
        "domain": "your-instance.example.com"
      }
    }
  ]
}
```

#### Error Responses

| Status | Condition | Example Body |
|--------|-----------|------|
| `401 Unauthorized` | Missing API key | `{ "error": "API key is required. Provide it via X-ApiKey header or apiKey query parameter." }` |
| `401 Unauthorized` | Invalid API key | `{ "error": "Invalid API key." }` |

---

### Grant a Badge

Award a badge to a recipient by profile URI or email.

```
POST /api/badges/grant
```

**Authentication:** Required

#### Request Headers

| Header | Required | Description |
|--------|----------|-------------|
| `Content-Type` | Yes | Must be `application/json` |
| `X-ApiKey` | Yes* | Your API key (*or use `apiKey` query param) |

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `badgeId` | `integer` | **Yes** | ID of the badge to grant. Must be greater than 0. |
| `profileUri` | `string` | Conditional | ActivityPub profile URI of the recipient. Max 500 characters. **Either `profileUri` or `email` must be provided.** |
| `name` | `string` | No | Display name of the recipient. Max 200 characters. |
| `email` | `string` | Conditional | Email address of the recipient. Max 254 characters. **Either `profileUri` or `email` must be provided.** |
| `evidence` | `string` | No | Evidence or reason for granting the badge. Max 2000 characters. If omitted, the badge definition's default earning criteria is used. |

#### Example Request (curl)

```bash
curl -X POST https://your-instance.example.com/api/badges/grant \
  -H "Content-Type: application/json" \
  -H "X-ApiKey: your-api-key-here" \
  -d '{
    "badgeId": 1,
    "profileUri": "https://mastodon.social/@recipient",
    "name": "Jane Doe",
    "evidence": "Completed the advanced training course"
  }'
```

#### Example Request (with email)

```bash
curl -X POST https://your-instance.example.com/api/badges/grant \
  -H "Content-Type: application/json" \
  -H "X-ApiKey: your-api-key-here" \
  -d '{
    "badgeId": 1,
    "email": "jane@example.com",
    "name": "Jane Doe"
  }'
```

#### Example Request (JavaScript fetch)

```javascript
const response = await fetch('https://your-instance.example.com/api/badges/grant', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'X-ApiKey': 'your-api-key-here',
  },
  body: JSON.stringify({
    badgeId: 1,
    profileUri: 'https://mastodon.social/@recipient',
    name: 'Jane Doe',
    evidence: 'Completed the advanced training course'
  })
});

const result = await response.json();
if (result.success) {
  console.log('Badge granted! Accept URL:', result.acceptUrl);
} else {
  console.error('Error:', result.error);
}
```

#### Success Response

**Status:** `200 OK`

```json
{
  "success": true,
  "message": "Badge granted successfully",
  "acceptUrl": "https://your-instance.example.com/accept/grant/42/abc123",
  "badgeRecord": {
    "id": 42,
    "title": "Advanced Training",
    "issuedBy": "https://your-instance.example.com/actor/admin",
    "issuedOn": "2026-03-12T10:30:00Z",
    "issuedToName": "Jane Doe",
    "issuedToSubjectUri": "https://mastodon.social/@recipient",
    "earningCriteria": "Completed the advanced training course"
  }
}
```

The `acceptUrl` is a link the recipient can visit to accept the badge. Share this URL with them directly or the system will notify them via ActivityPub if applicable.

> **Note:** If the same badge has already been granted to the same recipient and is still pending acceptance, the API will return the existing `acceptUrl` without creating a duplicate grant.

#### Error Responses

| Status | Condition | Example Body |
|--------|-----------|--------------|
| `400 Bad Request` | Missing or invalid fields | `{ "error": "BadgeId is required and must be greater than 0" }` |
| `400 Bad Request` | No recipient identifier | `{ "error": "Either ProfileUri or Email must be provided" }` |
| `400 Bad Request` | Invalid email format | `{ "error": "Invalid email format" }` |
| `400 Bad Request` | Invalid URL format | `{ "error": "Invalid ProfileUri format" }` |
| `401 Unauthorized` | Missing API key | `{ "error": "API key is required. Provide it via X-ApiKey header or apiKey query parameter." }` |
| `401 Unauthorized` | Invalid API key | `{ "error": "Invalid API key." }` |
| `404 Not Found` | Badge ID doesn't exist | `{ "error": "Badge not found" }` |
| `409 Conflict` | Badge already accepted | `{ "error": "This badge has already been granted to this recipient" }` |

---

### Get Grant Status

Retrieve the status of a badge grant by its NoteId. Returns limited information: the recipient's profile URI, badge ID, and current status. Only grants for badges owned by the authenticated user are accessible.

```
GET /api/badges/grant/{noteId}/status
```

**Authentication:** Required (scoped to badges owned by the authenticated user)

#### Request Headers

| Header | Required | Description |
|--------|----------|-------------|
| `X-ApiKey` | Yes* | Your API key (*or use `apiKey` query param) |

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `noteId` | `string` | The NoteId of the badge grant. Can be a full ActivityPub URL or the trailing ID segment. |

#### Example Request

```bash
curl https://your-instance.example.com/api/badges/grant/abc123/status \
  -H "X-ApiKey: your-api-key-here"
```

#### Success Response

**Status:** `200 OK`

```json
{
  "success": true,
  "noteId": "https://your-instance.example.com/actor/org/statuses/abc123",
  "profileUri": "https://mastodon.social/@recipient",
  "badgeId": 1,
  "status": "accepted"
}
```

Possible `status` values:

| Value | Description |
|-------|-------------|
| `pending` | Badge has been granted but not yet accepted by the recipient |
| `accepted` | Badge has been accepted but not yet processed |
| `processed` | Badge has been accepted and fully processed (fingerprint generated) |
| `external` | Badge originates from an external source |

> **Note:** Revoked grants are excluded and will return a `404` response.

#### Error Responses

| Status | Condition | Example Body |
|--------|-----------|------|
| `401 Unauthorized` | Missing API key | `{ "error": "API key is required. Provide it via X-ApiKey header or apiKey query parameter." }` |
| `401 Unauthorized` | Invalid API key | `{ "error": "Invalid API key." }` |
| `404 Not Found` | Grant not found, revoked, or not owned by user | `{ "error": "Grant not found." }` |

---

### Get Badge Information

Retrieve detailed information about a specific badge definition. Only returns badges owned by the authenticated user.

```
GET /api/badges/{badgeId}
```

**Authentication:** Required

#### Request Headers

| Header | Required | Description |
|--------|----------|-------------|
| `X-ApiKey` | Yes* | Your API key (*or use `apiKey` query param) |

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `badgeId` | `integer` | The ID of the badge to look up |

#### Example Request

```bash
curl https://your-instance.example.com/api/badges/1 \
  -H "X-ApiKey: your-api-key-here"
```

#### Success Response

**Status:** `200 OK`

```json
{
  "success": true,
  "badge": {
    "id": 1,
    "title": "Advanced Training",
    "description": "Awarded for completing advanced training",
    "badgeType": "Badge",
    "earningCriteria": "Complete the advanced training course",
    "image": "https://your-instance.example.com/badge-image/1",
    "imageAltText": "Advanced Training Badge",
    "hashtags": "#training #advanced",
    "infoUri": "https://example.com/training",
    "isCertificate": false,
    "issuer": {
      "id": 1,
      "fullName": "My Organization",
      "username": "org",
      "domain": "your-instance.example.com"
    }
  }
}
```

#### Error Responses

| Status | Condition | Example Body |
|--------|-----------|------|
| `401 Unauthorized` | Missing API key | `{ "error": "API key is required. Provide it via X-ApiKey header or apiKey query parameter." }` |
| `401 Unauthorized` | Invalid API key | `{ "error": "Invalid API key." }` |
| `404 Not Found` | Badge not found or not owned by user | `{ "error": "Badge not found or not authorized." }` |

---

## Common Patterns

### Granting a Badge via ActivityPub Profile

When the recipient has a Fediverse/ActivityPub profile, use `profileUri`:

```json
{
  "badgeId": 5,
  "profileUri": "https://mastodon.social/@user",
  "name": "User Name",
  "evidence": "Outstanding contribution to the project"
}
```

### Granting a Badge via Email

When the recipient doesn't have a Fediverse profile, use `email`:

```json
{
  "badgeId": 5,
  "email": "user@example.com",
  "name": "User Name"
}
```

### Providing Both Identifiers

You can provide both `profileUri` and `email` if available:

```json
{
  "badgeId": 5,
  "profileUri": "https://mastodon.social/@user",
  "email": "user@example.com",
  "name": "User Name",
  "evidence": "Speaker at the 2026 conference"
}
```

---

## Integration Notes

1. The API returns an `acceptUrl` that the recipient can use to accept the badge
2. You can provide either `profileUri` or `email` (or both) to identify the recipient
3. If `evidence` is not provided, the badge's default earning criteria will be used
4. The API prevents duplicate grants — if a pending grant already exists, the existing `acceptUrl` is returned
5. All protected endpoints require an API key via `X-ApiKey` header or `apiKey` query parameter

## Badge Record Flow

1. Call the grant API with recipient information and your API key
2. System creates a badge record with an accept key
3. Recipient is notified via ActivityPub (if applicable) or you share the `acceptUrl` directly
4. Recipient visits the accept URL to claim the badge
5. Once accepted, the badge becomes part of the recipient's public profile

---

## Field Constraints Summary

| Field | Max Length | Format |
|-------|-----------|--------|
| `profileUri` | 500 | Valid HTTP/HTTPS URL |
| `name` | 200 | Free text |
| `email` | 254 | Valid email address |
| `evidence` | 2000 | Free text (Optional) |
