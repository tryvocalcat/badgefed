var builder = DistributedApplication.CreateBuilder(args);

var dataDirectory = Path.GetFullPath("../.aspire/data", builder.AppHostDirectory);
Directory.CreateDirectory(dataDirectory);

var mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "v1.33.0")
    .WithHttpEndpoint(targetPort: 8025, name: "http")
    .WithEndpoint(targetPort: 1025, name: "smtp")
    .WithHttpHealthCheck("/readyz");

var smtp = mailpit.GetEndpoint("smtp");

builder.AddProject<Projects.BadgeFed>("badgefed", launchProfileName: null)
    .WithHttpEndpoint(port: 5080, name: "http", isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("AllowedHosts", "localhost")
    .WithEnvironment("BadgesDomains__0", "localhost:5080")
    .WithEnvironment("DB_DATA", dataDirectory)
    // The worker derives its filenames from BadgesDomains, not SQLITE_DB_PATH.
    .WithEnvironment("SQLITE_DB_PATH", "")
    .WithEnvironment("EmailSettings__Provider", "Smtp")
    .WithEnvironment("EmailSettings__SmtpServer", smtp.Property(EndpointProperty.Host))
    .WithEnvironment("EmailSettings__Port", smtp.Property(EndpointProperty.Port))
    .WithEnvironment("EmailSettings__EnableSsl", "false")
    .WithEnvironment("EmailSettings__Username", "")
    .WithEnvironment("EmailSettings__Password", "")
    .WithEnvironment("EmailSettings__SenderEmail", "noreply@badgefed.localhost")
    .WithEnvironment("EmailSettings__SenderName", "BadgeFed local")
    .WithHttpHealthCheck("/health")
    .WaitFor(mailpit);

builder.Build().Run();
