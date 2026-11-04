# BadgeFed - ActivityPub + OpenBadges

BadgeFed (aka ActivityPub Badges) is a minimalistic, federated badge system inspired by Credly, built with .NET and leveraging the ActivityPub protocol and the OpenBadges spec. It enables issuing, managing, and verifying digital badges across federated servers.

- **Blog:** [badgefed.vocalcat.com](https://badgefed.vocalcat.com/)
- **Matrix:** [@badgefed:matrix.org](https://matrix.to/#/#badgefed:matrix.org)

---

## Features

- Issue and manage digital badges and grants
- ActivityPub protocol support for federation ([see implementation details](./FEDERATION.md))
- Built with .NET 9
- OAuth login (Mastodon, LinkedIn)
- Email notifications
- Easily extensible and self-hostable
- 100% Open Source under the [LGPL license](LICENSE.md).

---

## BadgeFed Servers

A list of public BadgeFed servers is maintained in [`SERVERS.md`](./SERVERS.md), generated from [`servers.json`](./servers.json) using the [`gen.sh`](./gen.sh) script. To contribute a server, create a pull request in this repo by adding it to `servers.json`.

---

## Getting Started

### Optional local development with Aspire

Aspire runs the **existing web project from source** plus a local Mailpit SMTP
capture container. It is not required for self-hosting: the standalone .NET and
Docker workflows below still work without Aspire, a cloud account, or a cloud login.
SQLite, the in-process worker, and filesystem assets are unchanged.

**Prerequisites**

- .NET **10 SDK** for the `BadgeFed.AppHost` orchestrator, and the .NET **9
  ASP.NET Core runtime** for BadgeFed. Installing the .NET 9 SDK supplies that
  runtime too. BadgeFed and ServiceDefaults still target `net9.0`; installing
  only .NET 10 does not supply the .NET 9 runtime.
- [Aspire CLI](https://aspire.dev/get-started/install-cli/), version **13.6.4**.
  The AppHost SDK is pinned to 13.6.4; ServiceDefaults uses stable OpenTelemetry
  1.17.0 packages. No Aspire workload or preview SDK is needed.
- Docker with a running **Linux-container** engine, for the pinned
  `axllent/mailpit:v1.33.0` container. BadgeFed itself runs natively; its SQLite
  and Magick.NET native libraries must support your OS/architecture.
- A trusted development HTTPS certificate for the dashboard:
  `dotnet dev-certs https --trust`.

From the repository root:

```sh
dotnet build BadgeFed.AppHost/BadgeFed.AppHost.csproj
aspire run
```

Open the authenticated dashboard URL printed by Aspire. BadgeFed is at
**http://localhost:5080**, with login at **http://localhost:5080/admin/login**.
Mailpit's web UI has a dynamically allocated localhost port shown in the
dashboard. SMTP host/port are resolved from that container's endpoint for the
project-hosted app, not from its internal Docker hostname. SMTP TLS is disabled
**only in this local AppHost**, and no real mail server or SMTP credentials are
needed. Captured messages are disposable and are lost when the container is
recreated; use synthetic recipient data.

Use **localhost**, not `127.0.0.1`, a machine name, or another alias. AppHost sets
`BadgesDomains:0=localhost:5080`, permits only the localhost HTTP host, and clears
`SQLITE_DB_PATH`. Request-scoped stores use `Request.Host.Host` while the worker
strips the port from `BadgesDomains`; both therefore use the same `localhost`
dataset. Do not add domain aliases in local user secrets. Port 5080 is fixed
(not an Aspire proxy port); free it before starting another instance.

**Authentication and secrets**

Authentication is unchanged, and `OpenRegistration` remains false. Configure a
real administrator and OAuth provider using **the web project's user secrets**,
which are loaded in Development, not by committing credentials to appsettings.
For example, replace the example values with your own:

```sh
dotnet user-secrets set --project src/BadgeFed "AdminAuthentication:AdminUsers:0:Id" "your-user@your-mastodon-server"
dotnet user-secrets set --project src/BadgeFed "AdminAuthentication:AdminUsers:0:Type" "Mastodon"
dotnet user-secrets set --project src/BadgeFed "AdminAuthentication:AdminUsers:1:Id" ""
dotnet user-secrets set --project src/BadgeFed "AdminAuthentication:AdminUsers:1:Type" ""
```

This replaces the checked-in sample administrator entries locally. Dynamic
Mastodon application registration remains available, subject to your provider
accepting the local redirect URI. For a pre-registered Mastodon application,
also set `MastodonConfig:Server`, `MastodonConfig:ClientId`, and
`MastodonConfig:ClientSecret` in those same user secrets, and register
`http://localhost:5080/signin-mastodon-dynamic` with the provider. Alternatively,
configure a supported provider such as LinkedIn using `LinkedInConfig:ClientId`
and `LinkedInConfig:ClientSecret`, an administrator of type `LinkedIn`, and the
`/signin-linkedin` callback. Provider policies may require an externally reachable
HTTPS callback. There is no local login bypass; seeing the login page does not
verify that external OAuth succeeds. User secrets are local development storage,
not an encrypted production secret store.

**Persistent, private local state**

AppHost supplies an absolute `DB_DATA` under **`.aspire/data` at the repository
root**, outside `wwwroot` and ignored by git. It contains `localhost.db`
(including issuer private keys), `localhost-queue.db`, and
`localhost_analytics.db`, plus any SQLite journal files. Stop/start preserves
these files; nothing resets them automatically. Keep this directory private.
Back it up before resetting; to explicitly reset, stop Aspire and remove only
`.aspire/data`. Do not put database files or private keys in static asset folders.
Public uploaded assets still use `src/BadgeFed/wwwroot/pages` (already ignored);
back those up separately. AppHost uses the web project's working directory, so
`init.sql`, `assets/migrations`, templates and image paths resolve as in
`dotnet run`.

**Health and telemetry**

In Development, `/alive` checks process responsiveness and `/health` checks
readiness: initialized relay/schema, pending migrations, all three SQLite files'
integrity/read-write access, and private data-directory writes. A fresh instance
needs no issuer, badge, or recipient before it becomes ready; queue/analytics
schemas are initialized through the existing stores. Failures report unhealthy
rather than a successful placeholder check. Probes send no mail and make no
remote federation calls. Endpoints are not mapped in Production.

The dashboard receives OpenTelemetry request/client traces, ASP.NET Core/HTTP/
runtime metrics, and infrastructure logs after the existing console/debug
logging setup. Export is enabled only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set
(Aspire supplies it). URL paths/queries are removed from exported spans to avoid
exposing acceptance links and OAuth secrets; business logs are excluded from
the OTLP log provider because existing logs may contain recipient data. Existing
console output is still captured by Aspire and can contain application data:
keep the dashboard private, use synthetic data, and do not share raw console logs
or resource environment values. HTTP retries/service discovery are intentionally
not enabled globally: this single-app setup must not retry federation writes.

Localhost cannot receive public ActivityPub federation. For optional external
testing, supply your own reachable HTTPS development deployment, matching
`BadgesDomains`, host/proxy settings and OAuth redirect registration; it will have
its own domain-specific dataset. This local AppHost does not start a tunnel.
See [USAGE.md](./USAGE.md#local-development-checks) for a manual workflow.

### Standalone .NET

With the .NET 9 SDK, configure your domains, administrators, OAuth and SMTP as
described below, then run `dotnet run --project src/BadgeFed`. No AppHost, Docker,
or Aspire CLI is needed. The existing default data location is unchanged; set
`DB_DATA` to a private directory outside `wwwroot` for persistent storage.

### Docker Method

To run BadgeFed in a Docker container, follow these steps:

1. **Build the Docker Image:**
   ```sh
   docker build -t badgefed src/
   ```
   or use the pre-build image: ghcr.io/tryvocalcat/badgefed

2. **Run the Container (with persistence for data storage):**
   ```sh
   docker run -d -p 8080:8080 --name badgefed \
    -v $(pwd)/badgefed/data:/app/data \
    -v $(pwd)/badgefed/config:/app/config \
    -v $(pwd)/badgefed/pages:/app/wwwroot/pages \
    -e DB_DATA="/app/data" \
    badgefed
   ```

A fully docker example would be:

```sh
docker run -d \
  -p 5000:80 \
    --name badgefed \
    -e DB_DATA="/app/data" \
    -e ASPNETCORE_ENVIRONMENT="Production" \
    -e "AdminAuthentication__AdminUsers__0__Id=mapache@hachyderm.io" \
    -e "AdminAuthentication__AdminUsers__0__Type=Mastodon" \
    -v $(pwd)/data:/app/data \
    badgefed
```

For more details, refer to the [Microsoft Configuration Documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration) about how to pass environment variables in a dotnet docker image.

---

## Usage Guide

See the [USAGE.md](./USAGE.md) file for a detailed usage guide, including badge management, grant workflow, recipient profiles, and more.

---

## Configuration

BadgeFed uses a layered configuration system in .NET, allowing settings to be defined in `appsettings.json`, `appsettings.Development.json`, environment variables, and other sources. Below is a detailed guide to the available settings and how to use them.

### Available Settings

#### 1. **Logging**
- **Purpose:** Configures the logging levels for the application.
- **Location:** `appsettings.json` and `appsettings.Development.json`
- **Example:**
  ```json
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
  ```
- **Usage:** Adjust the log levels to control the verbosity of logs. Common levels include `Information`, `Warning`, and `Error`.

#### 2. **Allowed Hosts**
- **Purpose:** Specifies the allowed hosts for the application.
- **Location:** `appsettings.json` and `appsettings.Development.json`
- **Example:**
  ```json
  "AllowedHosts": "*"
  ```
- **Usage:** Use `*` to allow all hosts or specify a list of allowed domains.

#### 3. **Badges Domains**
- **Purpose:** Defines the domains used for badge issuance and verification.
- **Location:** `appsettings.json` and `appsettings.Development.json`
- **Example:**
  ```json
  "BadgesDomains": [
    "badgefed.example.com",
    "localhost:5000"
  ]
  ```
- **Usage:** Update this setting with the domain(s) where your application is hosted.

#### 4. **Admin Authentication**
- **Purpose:** Configures admin users and their authentication types.
- **Location:** `appsettings.json` and `appsettings.Development.json`
- **Example:**
  ```json
  "AdminAuthentication": {
    "AdminUsers": [
      {
        "Id": "admin@example.com",
        "Type": "LinkedIn"
      },
      {
        "Id": "username@mastodoninstance.com",
        "Type": "Mastodon"
      }
    ]
  }
  ```
- **Usage:** Add admin users with their IDs and authentication types (`LinkedIn` or `Mastodon`). LinkedIn uses email as IDs, and Mastodon uses usernames. If only Mastodon users are specified or only LinkedIn users are specified, only the corresponding login button will appear. For example, if no Mastodon users are specified, the Mastodon login button will not appear.

#### 5. **Mastodon Configuration**
- **Purpose:** Mastodon OAuth authentication is supported out-of-the-box.
- **Location:** No configuration required.
- **Example:** N/A
- **Usage:** Mastodon authentication works with any Mastodon server without requiring any configuration. The system automatically registers OAuth applications dynamically when users log in from their Mastodon instances. For more details about Mastodon authentication, visit the [Mastodon Developer Documentation](https://docs.joinmastodon.org/client/token/).

##### Custom Mastodon OAuth Application (Optional)

If you want to use your own pre-registered Mastodon OAuth application instead of the automatic dynamic registration, you can configure it in `appsettings.json`:

```json
"MastodonConfig": {
  "Server": "mastodon.social",
  "ClientId": "your-client-id",
  "ClientSecret": "your-client-secret"
}
```

To register your own OAuth application on your Mastodon server:
1. Go to your Mastodon instance's **Preferences > Development > New Application**
2. Set the required scopes: `read`, `read:accounts`, and `profile`
3. Set the redirect URI to: `https://your-badgefed-domain/signin-mastodon-dynamic`
4. Copy the **Client ID** and **Client Secret** to your configuration

**Note:** When `MastodonConfig` is configured, the specified server will be pre-selected in the login dropdown. If only `AdminAuthentication` with Mastodon users is configured (without `MastodonConfig`), the dropdown will appear but without a pre-selected server, allowing users to choose or enter their server.

#### 6. **LinkedIn Configuration**
- **Purpose:** Configures LinkedIn OAuth for authentication.
- **Location:** `Program.cs` (via `LinkedInConfig` class)
- **Example:**
  ```json
  "LinkedInConfig": {
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret"
  }
  ```
- **Usage:** Add Linked credentials in the configuration file or environment variables. You need to create a LinkedIn app with OpenId auth scope. For more details, visit the [LinkedIn Developer Documentation](https://docs.microsoft.com/en-us/linkedin/shared/authentication/authentication).

#### 7. **Email Settings**
- **Purpose:** Configures SMTP settings for sending emails.
- **Location:** `appsettings.json` and `appsettings.Development.json`
- **Example:**
  ```json
  "EmailSettings": {
    "SmtpServer": "smtp.example.com",
    "Port": 587,
    "SenderEmail": "noreply@example.com",
    "SenderName": "BadgeFed",
    "Username": "smtp-username",
    "Password": "smtp-password"
  }
  ```
- **Usage:** Update the SMTP server, port, sender email, and credentials for email functionality.

### Reverse proxy

If you are using a reverse proxy, you need to make sure that your original host gets forwarded and that your WebSockets upgrades are properly handled. An example configuration for nginx below:

```nginx
    location / {
        proxy_pass https://example.com;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
    }
```
---

## ActivityPub & OpenBadge Implementation

For a technical overview of how BadgeFed implements ActivityPub and OpenBadge 2.0, see [FEDERATION.md](./FEDERATION.md).

---

## Contributing

Contributions are welcome! To add a new server, update `servers.json` and run `gen.sh`. 
For code contributions, please open an issue or submit a pull request.

---

## License

This project is licensed under the LGPL License. See the [LICENSE](LICENSE.md) file for details.
