# Public Badge Grant API

This API allows external systems to grant badges to recipients programmatically.

## Endpoints

### Grant Badge
`POST /api/badges/grant`

Grant a badge to a recipient.

#### Request Body
```json
{
  "badgeId": 123,
  "profileUri": "https://mastodon.social/@username",
  "name": "John Doe",
  "email": "john@example.com",
  "evidence": "Completed advanced training course"
}
```

#### Fields
- `badgeId` (required): The ID of the badge to grant
- `profileUri` (optional): Profile URI of the recipient (either this or email must be provided)
- `name` (optional): Name of the recipient
- `email` (optional): Email of the recipient (either this or profileUri must be provided)
- `evidence` (optional): Evidence or reason for granting the badge

#### Success Response (200 OK)
```json
{
  "success": true,
  "message": "Badge Example Badge granted successfully.",
  "acceptUrl": "https://yourdomain.com/accept/456?key=abc123",
  "badgeRecord": {
    "id": 456,
    "title": "Example Badge",
    "issuedBy": "https://yourdomain.com/actors/yourdomain.com/issuer",
    "issuedOn": "2025-09-05T10:30:00Z",
    "issuedToName": "John Doe",
    "issuedToSubjectUri": "https://mastodon.social/@username",
    "earningCriteria": "Completed advanced training course"
  }
}
```

#### Error Responses

**400 Bad Request** - Invalid request
```json
{
  "error": "Either ProfileUri or Email must be provided"
}
```

**404 Not Found** - Badge not found
```json
{
  "error": "Badge with ID 123 not found."
}
```

**409 Conflict** - Badge already granted
```json
{
  "error": "Badge Example Badge has already been granted to this recipient."
}
```

## Usage Examples

### Using curl
```bash
curl -X POST https://yourdomain.com/api/badges/grant \
  -H "Content-Type: application/json" \
  -d '{
    "badgeId": 123,
    "profileUri": "https://mastodon.social/@username",
    "name": "John Doe",
    "evidence": "Completed advanced training course"
  }'
```

### Using JavaScript fetch
```javascript
const response = await fetch('/api/badges/grant', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
  },
  body: JSON.stringify({
    badgeId: 123,
    profileUri: 'https://mastodon.social/@username',
    name: 'John Doe',
    evidence: 'Completed advanced training course'
  })
});

const result = await response.json();
if (result.success) {
  console.log('Badge granted! Accept URL:', result.acceptUrl);
} else {
  console.error('Error:', result.error);
}
```

## Integration Notes

1. The API returns an `acceptUrl` that the recipient can use to accept the badge
2. You can provide either `profileUri` or `email` (or both) to identify the recipient
3. If `evidence` is not provided, the badge's default earning criteria will be used
4. The API prevents duplicate grants to the same recipient for the same badge
5. Currently, no authentication is required (this may change in future versions)

## Badge Record Flow

1. Call the grant API with recipient information
2. System creates a badge record with an accept key
3. Recipient visits the accept URL to claim the badge
4. Once accepted, the badge becomes part of the recipient's public profile
