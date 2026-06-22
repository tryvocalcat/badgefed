using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BadgeFed.Services
{
    /// <summary>
    /// Sends emails using Azure Communication Services REST API with connection string authentication.
    /// </summary>
    public class AzureEmailSender : IEmailSender
    {
        private readonly string _endpoint;
        private readonly string _accessKey;
        private readonly HttpClient _httpClient;

        public AzureEmailSender(string connectionString)
        {
            (_endpoint, _accessKey) = ParseConnectionString(connectionString);
            _httpClient = new HttpClient();
        }

        public async Task SendAsync(string toEmail, string subject, string body, string senderEmail, string senderName, bool isHtml = true)
        {
            var apiVersion = "2023-03-31";
            var url = $"{_endpoint}/emails:send?api-version={apiVersion}";

            var payload = new
            {
                senderAddress = senderEmail,
                content = new
                {
                    subject = subject,
                    html = isHtml ? body : null,
                    plainText = isHtml ? null : body
                },
                recipients = new
                {
                    to = new[]
                    {
                        new { address = toEmail, displayName = "" }
                    }
                },
                replyTo = new[]
                {
                    new { address = senderEmail, displayName = senderName }
                },
                headers = new Dictionary<string, string>
                {
                    ["x-sender-display-name"] = senderName
                },
                userEngagementTrackingDisabled = true
            };

            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            // Sign the request using HMAC-SHA256
            var utcNow = DateTimeOffset.UtcNow.ToString("r");
            request.Headers.Add("x-ms-date", utcNow);
            request.Headers.Add("repeatability-request-id", Guid.NewGuid().ToString());
            request.Headers.Add("repeatability-first-sent", utcNow);

            var contentHash = ComputeContentHash(jsonPayload);
            request.Headers.Add("x-ms-content-sha256", contentHash);

            var uri = new Uri(url);
            var host = uri.Authority;
            var pathAndQuery = uri.PathAndQuery;

            var stringToSign = $"POST\n{pathAndQuery}\n{utcNow};{host};{contentHash}";
            var signature = ComputeHmacSignature(stringToSign, _accessKey);

            request.Headers.Authorization = new AuthenticationHeaderValue(
                "HMAC-SHA256",
                $"SignedHeaders=x-ms-date;host;x-ms-content-sha256&Signature={signature}"
            );

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"Azure Communication Services email send failed ({response.StatusCode}): {errorContent}");
            }
        }

        private static (string endpoint, string accessKey) ParseConnectionString(string connectionString)
        {
            var parts = connectionString.Split(';')
                .Select(p => p.Split('=', 2))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);

            if (!parts.TryGetValue("endpoint", out var endpoint))
                throw new ArgumentException("Connection string must contain 'endpoint'.");
            if (!parts.TryGetValue("accesskey", out var accessKey))
                throw new ArgumentException("Connection string must contain 'accesskey'.");

            return (endpoint.TrimEnd('/'), accessKey);
        }

        private static string ComputeContentHash(string content)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
            return Convert.ToBase64String(hash);
        }

        private static string ComputeHmacSignature(string stringToSign, string accessKey)
        {
            var keyBytes = Convert.FromBase64String(accessKey);
            using var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes);
            var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
            return Convert.ToBase64String(signatureBytes);
        }
    }
}
