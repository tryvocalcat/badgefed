# BadgeFed Usage Guide

Welcome to BadgeFed! This guide will help you use BadgeFed effectively and provide detailed information about its features and usage.

## Getting Started

### Quick Start

1. **Login**: Visit the BadgeFed Admin Panel (`/admin`) and log in using your LinkedIn or Mastodon account.
2. **Manage Issuers**: Navigate to the Issuer Management page to create, update, or delete issuers.
3. **Manage Badges**: Go to the Badge Management page to create, update, or delete badges.
4. **Grant a Badge**: Click the "Grant" button on the badge management page to issue a badge. Provide the recipient's identifier URL (e.g., `https://linkedin.com/in/username`, `https://mastodon.social/@badgefed`), evidence (optional), and email (optional).
5. **Share the Grant Link**: Copy the generated grant URL and share it with the recipient for badge acceptance.

### Detailed Workflow

#### Step 1: Create an Issuer
- Navigate to the issuer management page.
- Create an issuer that acts as an ActivityPub actor and OpenBadge endpoint.

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