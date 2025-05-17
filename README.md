# BadgeFed - ActivityPub Badges

BadgeFed (aka ActivityPub Badges) is a minimalistic, federated badge system inspired by Credly, built with .NET and leveraging the ActivityPub protocol. It enables issuing, managing, and verifying digital badges across federated servers.

- **Primary Instance:** [badges.vocalcat.com](https://badges.vocalcat.com)
- **Blog:** [badgefed.vocalcat.com](https://badgefed.vocalcat.com/)

---

## Features

- Issue and manage digital badges
- ActivityPub protocol support for federation
- Minimalistic, modern design
- Built with .NET 9 for robust performance
- OAuth login (Mastodon, LinkedIn)
- Email notifications
- Easily extensible and self-hostable

---

## BadgeFed Servers

A list of public BadgeFed servers is maintained in [`SERVERS.md`](./SERVERS.md), generated from [`servers.json`](./servers.json) using the [`gen.sh`](./gen.sh) script. To contribute a server, create a pull request in this repo by adding it to `servers.json`.

---

## Getting Started

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

### Installation

1. Clone the repository:
    ```sh
    git clone https://github.com/tryvocalcat/activitypub-badges.git
    cd activitypub-badges
    ```
2. Restore dependencies:
    ```sh
    dotnet restore
    ```
3. Build the project:
    ```sh
    dotnet build
    ```

### Running the Application

- **Development:**
    ```sh
    dotnet watch --project src/BadgeFed/BadgeFed.csproj
    ```
- **Production:**
    ```sh
    dotnet run --project src/BadgeFed/BadgeFed.csproj
    ```

---

## Configuration
Application settings are managed using a layered configuration approach in .NET, which supports multiple sources such as environment variables, `appsettings.json`, and platform-specific configurations (e.g., Azure App Configuration). Key configuration options include:

The configuration system automatically merges these layers, with environment variables taking precedence over `appsettings.json`. This ensures flexibility for local development, containerized deployments, and cloud-hosted environments.

- **Database:**
    - SQLite file path can be set via the `SQLITE_DB_FILENAME` environment variable (default: `badgefed.db`).
- **OAuth Providers:**
    - Mastodon and LinkedIn credentials are configured in `appsettings.json` under `MastodonConfig` and `LinkedInConfig`.
- **Admin Users:**
    - Set in `AdminAuthentication` section.
- **Email Settings (Optional):**
    - Configure SMTP and sender details in `EmailSettings`.

---

## Docker

A multi-stage Dockerfile is provided in [`src/Dockerfile`](src/Dockerfile):

```sh
docker build -t badgefed .
docker run -v `pwd`/data:/app/data \
    -p 8080:8080 \
    -e SQLITE_DB_FILENAME=/app/data/badges.db \
    -e AdminAuthentication__AdminUsers__0__Id=your-mastodon-username \
    -e AdminAuthentication__AdminUsers__0__Type=Mastodon \
    -e MastodonConfig__ClientId=your-mastodon-client-id \
    -e MastodonConfig__ClientSecret=your-mastodon-client-secret \
    -e MastodonConfig__Server=your-mastodon-server \
    badgefed
```

Example:

```
docker run -v `pwd`/data:/app/data \
    -p 8080:8080 \
    -e SQLITE_DB_FILENAME=/app/data/badges.db \
    -e AdminAuthentication__AdminUsers__0__Id=mapache \
    -e AdminAuthentication__AdminUsers__0__Type=Mastodon \
    -e MastodonConfig__ClientId=yourclientid \
    -e MastodonConfig__ClientSecret=yourclientsecret \
    -e MastodonConfig__Server=hachyderm.io \
    badgefed
```

- The container exposes port 8080.
- Mount a volume or set environment variables as needed for persistent storage and configuration.

---

## Contributing

Contributions are welcome! To add a new server, update `servers.json` and run `gen.sh`. For code contributions, please open an issue or submit a pull request.

---

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
