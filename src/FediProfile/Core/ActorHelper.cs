using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FediProfile.Models;

namespace FediProfile.Core;

public class ActorHelper
{
    private readonly string _privatePem;
    private readonly string _keyId;
    private readonly ILogger? _logger;

    public ActorHelper(string privatePem, string keyId, ILogger? logger = null)
    {
        _privatePem = privatePem;
        _keyId = keyId;
        _logger = logger;
    }

    public async Task<ActivityPubActor?> FetchActorInformationAsync(string actorUrl)
    {
        _logger?.LogInformation("Fetching actor information from {ActorUrl}", actorUrl);

        try
        {
            var jsonContent = await SendGetSignedRequest(new Uri(actorUrl));

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            var actor = JsonSerializer.Deserialize<ActivityPubActor>(jsonContent, options);
            _logger?.LogInformation("Successfully fetched actor: {ActorId}", actor?.Id);
            return actor;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error fetching actor information from {ActorUrl}", actorUrl);
            return null;
        }
    }

    public async Task<string> SendGetSignedRequest(Uri url)
    {
        _logger?.LogInformation("Sending signed GET request to {Url}", url);

        string date = DateTime.UtcNow.ToString("r");

        using (RSA rsa = RSA.Create())
        {
            rsa.ImportFromPem(_privatePem);

            string signedString = $"(request-target): get {url.AbsolutePath}\nhost: {url.Host}\ndate: {date}";

            byte[] signatureBytes = rsa.SignData(Encoding.UTF8.GetBytes(signedString), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            string signature = Convert.ToBase64String(signatureBytes);

            Console.WriteLine(_keyId);
            Console.WriteLine(signature);

            string header = $"keyId=\"{_keyId}\",headers=\"(request-target) host date\",signature=\"{signature}\",algorithm=\"rsa-sha256\"";

            using (HttpClient client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true }))
            {
                client.DefaultRequestHeaders.Add("Host", url.Host);
                client.DefaultRequestHeaders.Add("Date", date);
                client.DefaultRequestHeaders.Add("Signature", header);
                client.DefaultRequestHeaders.Add("Accept", "application/activity+json, application/ld+json, application/json");

                var response = await client.GetAsync(url);

                _logger?.LogInformation("GET {Url} - {StatusCode}", url, response.StatusCode);

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
        }
    }

    private static string CreateHashSha256(string input)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(hashBytes);
        }
    }

    public async Task<string> SendPostSignedRequest(string document, Uri url)
    {
        _logger?.LogInformation($"Sending POST request to {url}");

        string date = DateTime.UtcNow.ToString("r");

        using (RSA rsa = RSA.Create())
        {
            rsa.ImportFromPem(_privatePem);

            string digest = $"SHA-256={CreateHashSha256(document)}";
            string signedString = $"(request-target): post {url.AbsolutePath}\nhost: {url.Host}\ndate: {date}\ndigest: {digest}";

            byte[] signatureBytes = rsa.SignData(Encoding.UTF8.GetBytes(signedString), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            string signature = Convert.ToBase64String(signatureBytes);

            string header = $"keyId=\"{_keyId}\",headers=\"(request-target) host date digest\",signature=\"{signature}\",algorithm=\"rsa-sha256\"";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Host", url.Host);
                client.DefaultRequestHeaders.Add("Date", date);
                client.DefaultRequestHeaders.Add("Signature", header);
                client.DefaultRequestHeaders.Add("Digest", digest);

                _logger?.LogInformation($"Document: {document}");

                var response = await client.PostAsync(url, new StringContent(document, Encoding.UTF8, "application/activity+json"));
                var responseString = await response.Content.ReadAsStringAsync();

                _logger?.LogInformation($"POST {url} - {response.StatusCode}");

                return responseString;
            }
        }
    }
}
