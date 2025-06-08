# BadgeFed - ActivityPub + OpenBadges

BadgeFed (aka ActivityPub Badges) is a minimalistic, federated badge system inspired by Credly, built with .NET and leveraging the ActivityPub protocol and the OpenBadges spec. It enables issuing, managing, and verifying digital badges across federated servers.

- **Blog:** [badgefed.vocalcat.com](https://badgefed.vocalcat.com/)

---

## Features

- Issue and manage digital badges and grants
- ActivityPub protocol support for federation ([see implementation details](./DETAILS.md))
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

### Docker Method

To run BadgeFed in a Docker container, follow these steps:

1. **Build the Docker Image:**
   ```sh
   docker build -t badgefed .
   ```

2. **Run the Container:**
   ```sh
   docker run -d -p 5000:80 --name badgefed -e SQLITE_DB_FILENAME="badgefed.db" badgefed
   ```

3. **Environment Variables:**
   - Pass environment variables using the `-e` flag.
   - Example:
     ```sh
     docker run -d -p 5000:80 --name badgefed \
       -e SQLITE_DB_FILENAME="badgefed.db" \
       -e ASPNETCORE_ENVIRONMENT="Development" \
       badgefed
     ```

4. **Volume Mounts:**
   - Mount volumes for persistent data storage.
   - Example:
     ```sh
     docker run -d -p 5000:80 --name badgefed \
       -v $(pwd)/data:/app/data \
       badgefed
     ```

A fully docker example would be:

```sh
docker run -d \
  -p 5000:80 \
    --name badgefed \
    -e SQLITE_DB_FILENAME="badgefed.db" \
    -e ASPNETCORE_ENVIRONMENT="Production" \
    -e "MastodonConfig__ClientId=your-client-id" \
    -e "MastodonConfig__ClientSecret=your-client-secret" \
    -e "MastodonConfig__Server=hachyderm.io" \
    -e "AdminAuthentication__AdminUsers__0__Id=mymastodonusername" \
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
    "badgefed.example.com"
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
        "Id": "mastodon_user",
        "Type": "Mastodon"
      }
    ]
  }
  ```
- **Usage:** Add admin users with their IDs and authentication types (`LinkedIn` or `Mastodon`). LinkedIn uses email as IDs, and Mastodon uses usernames. If only Mastodon users are specified or only LinkedIn users are specified, only the corresponding login button will appear. For example, if no Mastodon users are specified, the Mastodon login button will not appear.

#### 5. **Mastodon Configuration**
- **Purpose:** Configures Mastodon OAuth for authentication.
- **Location:** `appsettings.json` and `appsettings.Development.json`
- **Example:**
  ```json
  "MastodonConfig": {
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "Server": "hachyderm.io"
  }
  ```
- **Usage:** Replace `ClientId`, `ClientSecret`, and `Server` with your Mastodon app credentials. For more details, visit the [Mastodon Developer Documentation](https://docs.joinmastodon.org/client/token/).

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

---

## ActivityPub & OpenBadge Implementation

For a technical overview of how BadgeFed implements ActivityPub and OpenBadge 2.0, see [DETAILS.md](./DETAILS.md).

---

## Contributing

Contributions are welcome! To add a new server, update `servers.json` and run `gen.sh`. 
For code contributions, please open an issue or submit a pull request.

---

## License

This project is licensed under the LGPL License. See the [LICENSE](LICENSE.md) file for details.
