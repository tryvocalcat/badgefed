# Badge Templates – Configurable Badge Sets for BadgeFed

## Overview

Badge Templates are pre-configured collections of badge definitions that can be applied with a single action. They are especially useful for **conferences**, **workshops**, **hackathons**, and **recurring events** where the same types of badges are granted repeatedly.

Badge Templates are stored in the database, making them fully configurable per instance. Instance admins can create, edit, and delete their own templates.

---

## How It Works

### For Admins (Configuring Templates)

1. Go to **Settings → Badge Templates** in the admin panel
2. Click **Create Template**
3. Fill in:
   - **Name**: A descriptive name (e.g. "Conference Badge Set")
   - **Description**: What this template is for
   - **Category**: A grouping label (e.g. "Conference", "Hackathon")
4. Define **Variables** — dynamic values that users fill in when applying the template (e.g. `CONFERENCE_NAME`, `YEAR`)
5. Add **Badge Items** — the individual badges that will be created, using `{VARIABLE_NAME}` placeholders in any text field
6. Save the template

### For Users (Applying Templates)

1. Go to **Badges → From Template** in the admin panel
2. Browse available templates and click **Use this template**
3. Select an **Issuer** (the actor/organization that will issue these badges)
4. Fill in the **variable values** (e.g. enter "FOSDEM 2025" for `CONFERENCE_NAME`)
5. Review the **preview** showing the badges that will be created
6. Click **Create Badges** to generate all badges at once

---

## Variables

Variables allow dynamic text replacement when applying a template. Define them with:

- **Name**: Uppercase identifier with underscores (e.g. `CONFERENCE_NAME`)
- **Description**: Explains what the user should enter
- **Placeholder**: Example value shown in the input field

### Built-in Variables

These are automatically available in every template without needing to define them:

| Variable | Description |
|----------|-------------|
| `{ISSUER_NAME}` | Replaced with the selected issuer's display name |

### Using Variables

Use the format `{VARIABLE_NAME}` in any badge item field (title, description, earning criteria, hashtags, image alt text). When the template is applied, all occurrences are replaced with the user-provided value.

---

## Badge Item Fields

Each badge item in a template supports these fields:

| Field | Description |
|-------|-------------|
| `title` | Badge display name (supports variables) |
| `description` | About the badge (supports variables) |
| `earningCriteria` | How to earn it — **required** (supports variables) |
| `badgeType` | One of: Achievement, Badge, Credential, Recognition, Milestone, Honor, Certification, Distinction |
| `hashtags` | Space-separated hashtags with `#` prefix (supports variables) |
| `isCertificate` | Display as certificate/diploma format |
| `imageAltText` | Accessibility description for the badge image (supports variables) |

> **Note:** Badge images are not included in templates. After creating badges from a template, you can edit each badge individually to upload images.

---

## Example: Conference Badge Set

Here is an example template configuration for a conference with four badge types. You can recreate this in your instance via **Settings → Badge Templates → Create Template**.

### Template Details

- **Name:** Conference Badge Set
- **Description:** Standard badges for conference events — speaker, volunteer, sponsor, and organizer/staff
- **Category:** Conference

### Variables

| Name | Description | Placeholder |
|------|-------------|-------------|
| `CONFERENCE_NAME` | Name of the conference | FOSDEM 2025 |

### Badge Items

#### 1. Speaker Badge

| Field | Value |
|-------|-------|
| Title | `{CONFERENCE_NAME} Speaker` |
| Description | `Awarded to speakers who presented at {CONFERENCE_NAME}. Issued by {ISSUER_NAME}.` |
| Earning Criteria | `Delivered a talk, workshop, or lightning talk at {CONFERENCE_NAME}.` |
| Badge Type | `Recognition` |
| Hashtags | `#speaker #{CONFERENCE_NAME}` |
| Is Certificate | No |
| Image Alt Text | `Speaker badge for {CONFERENCE_NAME}` |

#### 2. Volunteer Badge

| Field | Value |
|-------|-------|
| Title | `{CONFERENCE_NAME} Volunteer` |
| Description | `Awarded to volunteers who helped organize and run {CONFERENCE_NAME}. Issued by {ISSUER_NAME}.` |
| Earning Criteria | `Volunteered time and effort to support {CONFERENCE_NAME} operations.` |
| Badge Type | `Achievement` |
| Hashtags | `#volunteer #{CONFERENCE_NAME}` |
| Is Certificate | No |
| Image Alt Text | `Volunteer badge for {CONFERENCE_NAME}` |

#### 3. Sponsor Badge

| Field | Value |
|-------|-------|
| Title | `{CONFERENCE_NAME} Sponsor` |
| Description | `Awarded to sponsors who supported {CONFERENCE_NAME}. Issued by {ISSUER_NAME}.` |
| Earning Criteria | `Provided sponsorship or financial support for {CONFERENCE_NAME}.` |
| Badge Type | `Honor` |
| Hashtags | `#sponsor #{CONFERENCE_NAME}` |
| Is Certificate | No |
| Image Alt Text | `Sponsor badge for {CONFERENCE_NAME}` |

#### 4. Organizer / Staff Badge

| Field | Value |
|-------|-------|
| Title | `{CONFERENCE_NAME} Organizer` |
| Description | `Awarded to organizers and staff members of {CONFERENCE_NAME}. Issued by {ISSUER_NAME}.` |
| Earning Criteria | `Served as an organizer or staff member for {CONFERENCE_NAME}.` |
| Badge Type | `Distinction` |
| Hashtags | `#organizer #staff #{CONFERENCE_NAME}` |
| Is Certificate | No |
| Image Alt Text | `Organizer badge for {CONFERENCE_NAME}` |

### JSON Representation

For reference, here is the raw JSON stored in the `ConfigurationJson` column for this template:

```json
{
  "badgeItems": [
    {
      "title": "{CONFERENCE_NAME} Speaker",
      "description": "Awarded to speakers who presented at {CONFERENCE_NAME}. Issued by {ISSUER_NAME}.",
      "earningCriteria": "Delivered a talk, workshop, or lightning talk at {CONFERENCE_NAME}.",
      "badgeType": "Recognition",
      "hashtags": "#speaker #{CONFERENCE_NAME}",
      "isCertificate": false,
      "imageAltText": "Speaker badge for {CONFERENCE_NAME}"
    },
    {
      "title": "{CONFERENCE_NAME} Volunteer",
      "description": "Awarded to volunteers who helped organize and run {CONFERENCE_NAME}. Issued by {ISSUER_NAME}.",
      "earningCriteria": "Volunteered time and effort to support {CONFERENCE_NAME} operations.",
      "badgeType": "Achievement",
      "hashtags": "#volunteer #{CONFERENCE_NAME}",
      "isCertificate": false,
      "imageAltText": "Volunteer badge for {CONFERENCE_NAME}"
    },
    {
      "title": "{CONFERENCE_NAME} Sponsor",
      "description": "Awarded to sponsors who supported {CONFERENCE_NAME}. Issued by {ISSUER_NAME}.",
      "earningCriteria": "Provided sponsorship or financial support for {CONFERENCE_NAME}.",
      "badgeType": "Honor",
      "hashtags": "#sponsor #{CONFERENCE_NAME}",
      "isCertificate": false,
      "imageAltText": "Sponsor badge for {CONFERENCE_NAME}"
    },
    {
      "title": "{CONFERENCE_NAME} Organizer",
      "description": "Awarded to organizers and staff members of {CONFERENCE_NAME}. Issued by {ISSUER_NAME}.",
      "earningCriteria": "Served as an organizer or staff member for {CONFERENCE_NAME}.",
      "badgeType": "Distinction",
      "hashtags": "#organizer #staff #{CONFERENCE_NAME}",
      "isCertificate": false,
      "imageAltText": "Organizer badge for {CONFERENCE_NAME}"
    }
  ],
  "variables": [
    {
      "name": "CONFERENCE_NAME",
      "description": "Name of the conference",
      "placeholder": "FOSDEM 2025"
    }
  ]
}
```

---

## Database Schema

Badge Templates use a single table:

```sql
CREATE TABLE IF NOT EXISTS BadgeTemplate (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Description TEXT,
    Category TEXT,
    ConfigurationJson TEXT NOT NULL DEFAULT '{}',
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

- **ConfigurationJson**: A single JSON object containing `badgeItems` (array of badge item objects) and `variables` (array of variable definitions). This design keeps the schema simple and extensible — future configuration fields can be added to the JSON without requiring schema migrations.

Metadata columns (Name, Description, Category) remain in their own columns for easy querying and listing.
