# BadgeFed Usage Guide

Welcome to BadgeFed! This guide will help you use BadgeFed effectively and provide detailed information about its features and usage.

## Getting Started

### Quick Start

1. **Login**: Visit the BadgeFed Admin Panel (`/admin`) and log in using your LinkedIn or Mastodon account.
2. **Manage Issuers**: Navigate to the Issuer Management page to create, update, or delete issuers.
3. **Manage Badges**: Go to the Badge Management page to create, update, or delete badges.
4. **Grant a Badge**: Click the "Grant" button on the badge management page to issue a badge. Provide the recipient's identifier URL (e.g., `https://linkedin.com/in/username`, `https://mastodon.social/@badgefed`), evidence (optional), and email (optional).
5. **Share the Grant Link**: Copy the generated grant URL and share it with the recipient for badge acceptance.

### Local development checks

For the optional Aspire setup, follow the prerequisites and secret configuration
in [README.md](./README.md#optional-local-development-with-aspire), then run
`aspire run` from the repository root.

1. Open `http://localhost:5080/health` and `/alive`; both should say `Healthy`.
   The dashboard should show BadgeFed and Mailpit healthy. Open the homepage and
   `/admin/login`; log in with your configured real provider and admin identity.
2. Create an issuer and badge through the existing admin UI. Upload a test image,
   grant the badge to a synthetic email recipient, and open Mailpit from the
   dashboard to inspect captured mail. Mailpit never forwards mail externally.
3. Use the grant's acceptance link, confirm the recipient information, and accept
   the badge. Check the existing job-queue admin page, processed grant and
   downloadable baked PNG. A homepage response alone does not exercise SQLite
   queue processing or Magick.NET image generation. Public federation delivery
   cannot work against localhost.
4. Browse a few pages and inspect dashboard traces, metrics and structured logs.
   Health probes are excluded from traces. Console logs retain the application's
   existing behavior; do not use real recipient data or publish raw diagnostics.
5. Stop Aspire with Ctrl+C, restart with `aspire run`, and confirm the same issuer,
   badge and grant remain. Mailpit capture is disposable, but the three SQLite
   databases in `.aspire/data` and public uploads in `src/BadgeFed/wwwroot/pages`
   survive. Reset only by explicitly removing the relevant local state while
   Aspire is stopped, after making any required backup.

If readiness fails, inspect the health-check log in the dashboard for storage,
schema or migration errors. Verify the .NET 9 runtime, native SQLite libraries,
web-project working directory and writable private `DB_DATA`. For a Mailpit
startup failure, check the Linux Docker engine and container logs. Do not resolve
errors by moving databases into `wwwroot`, changing to a second host alias, or
setting a universal `SQLITE_DB_PATH` override.

### Detailed workflow steps

#### Step 1: Create an Issuer
- Navigate to the issuer management page.
- Create an issuer that acts as an ActivityPub actor and OpenBadge endpoint.

#### Admin-Only: Advanced Issuer Settings
Administrators (not managers) have access to an **Advanced (Admin Only)** section when creating or editing an issuer:

- **LinkedIn Organization ID**: Enter your numeric LinkedIn Organization ID to enable LinkedIn certification integration. You can find this ID in your LinkedIn Company Page URL (e.g., `https://www.linkedin.com/company/12345678` → the ID is `12345678`).
- **Owner (Group)**: Assign or reassign the group that owns the issuer. The dropdown lists all available groups and pre-selects the currently assigned group when editing an existing issuer. This controls which group's managers and collaborators can manage the issuer's badges.

#### Step 2: Create a Badge
- Associate the badge with an issuer.

#### Step 3: Grant a Badge
- Use a recipient's URL, phone number, or email address to issue the badge.
- If the recipient's URL is a Fediverse actor, they receive a private ActivityPub notification.

#### Step 4: Accept the Badge
- Recipients can choose to make the badge public or private.

#### Step 5: Decentralized Sharing
- BadgeFed creates a note associated with the OpenBadge.
- The note is decentralized by sending it to the issuer's followers using ActivityPub.

#### Step 6: Search for Badges
- Recipients can search for their badges across BadgeFed instances.

## Features

### Badge Management
- Create, edit, and delete badges.
- Explore badges issued by various actors.

### Grant Management
- Issue badges to recipients.
- Manage pending grants and track acceptance.

### Recipient Profiles
- View recipient profiles and their badge collections.
- Search for badges by recipient name, profile URL, or fediverse handle.

### OpenBadge Integration
- Import OpenBadge JSON data.
- Export badges in OpenBadge format.

### Decentralized Network
- Built on ActivityPub for decentralized badge management.
- Federated actors and recipients.

### Support
- For issues, visit the [GitHub repository](https://github.com/tryvocalcat/activitypub-badges) and open an issue.