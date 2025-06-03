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
- **Usage:** Update the SMTP server, port, sender email, and credentials for email functionality. Mail feature

### Docker Configuration

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

For more details, refer to the [Microsoft Configuration Documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration).

---

## Contributing

Contributions are welcome! To add a new server, update `servers.json` and run `gen.sh`. For code contributions, please open an issue or submit a pull request.

---

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
