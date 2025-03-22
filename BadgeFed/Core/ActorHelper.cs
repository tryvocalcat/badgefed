using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ActivityPubDotNet.Core
{
    public class ActorHelper(string privatePem, string keyId, ILogger? logger = null)
    {
        private readonly string _privatePem = privatePem;

        private readonly string _keyId = keyId;

        public ILogger? Logger { get; set; } = logger;

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public async Task<ActivityPubActor> FetchActorInformationAsync(string actorUrl)
        {
            var jsonContent = await SendGetSignedRequest(new Uri(actorUrl));

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            return JsonSerializer.Deserialize<ActivityPubActor>(jsonContent, options)!;
        }

        static string CreateHashSha256(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return Convert.ToBase64String(hashBytes);
            }
        }

        public async Task<string> SendGetSignedRequest(Uri url)
        {
            Console.WriteLine($"Sending GET request to {url}");

            // Get current UTC date in HTTP format
            string date = DateTime.UtcNow.ToString("r");

            // Load RSA private key from file
            using (RSA rsa = RSA.Create())
            {
                rsa.ImportFromPem(this._privatePem);

                // Build the to-be-signed string
                string signedString = $"(request-target): get {url.AbsolutePath}\nhost: {url.Host}\ndate: {date}";

                // Sign the to-be-signed string
                byte[] signatureBytes = rsa.SignData(Encoding.UTF8.GetBytes(signedString), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                // Base64 encode the signature
                string signature = Convert.ToBase64String(signatureBytes);

                // Build the HTTP signature header
                string header = $"keyId=\"{this._keyId}\",headers=\"(request-target) host date\",signature=\"{signature}\",algorithm=\"rsa-sha256\"";

                // Create HTTP client
                using (HttpClient client = new HttpClient())
                {
                    // Set request headers
                    client.DefaultRequestHeaders.Add("Host", url.Host);
                    client.DefaultRequestHeaders.Add("Date", date);
                    client.DefaultRequestHeaders.Add("Signature", header);
                    client.DefaultRequestHeaders.Add("Accept", "application/activity+json");

                    // Make the GET request
                    var response = await client.GetAsync(url);

                    response.EnsureSuccessStatusCode();

                    // Print the response
                    var responseString = await response.Content.ReadAsStringAsync();

                    Logger?.LogInformation($"Response {response.StatusCode} - {responseString}");

                    return responseString;
                }
            }
        }

        public async Task SendPostSignedRequest(string document, Uri url)
        {
            Console.WriteLine($"Sending POST request to {url}");

            // Get current UTC date in HTTP format
            string date = DateTime.UtcNow.ToString("r");

            // Load RSA private key from file
            using (RSA rsa = RSA.Create())
            {

                rsa.ImportFromPem(this._privatePem);

                string digest = $"SHA-256={CreateHashSha256(document)}";

                // Build the to-be-signed string
                // string signedString = $"(request-target): post {url.AbsolutePath}\nhost: {url.Host}\ndate: {date}";
                string signedString = $"(request-target): post {url.AbsolutePath}\nhost: {url.Host}\ndate: {date}\ndigest: {digest}";

                // Sign the to-be-signed string

                byte[] signatureBytes = rsa.SignData(Encoding.UTF8.GetBytes(signedString), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                // Base64 encode the signature
                string signature = Convert.ToBase64String(signatureBytes);

                Logger?.LogInformation($"Using key: {this._keyId}");
                Console.WriteLine($"Using key: {this._keyId}");

                // Build the HTTP signature header
                // string header = $"keyId=\"{privateKeyId}\",headers=\"(request-target) host date digest\",signature=\"{signature}\",algorithm=\"rsa-sha256\"";
                string header = $"keyId=\"{this._keyId}\",headers=\"(request-target) host date digest\",signature=\"{signature}\",algorithm=\"rsa-sha256\"";

                // Create HTTP client
                using (HttpClient client = new HttpClient())
                {
                    // Set request headers
                    client.DefaultRequestHeaders.Add("Host", url.Host);
                    client.DefaultRequestHeaders.Add("Date", date);
                    client.DefaultRequestHeaders.Add("Signature", header);
                    client.DefaultRequestHeaders.Add("Digest", digest);

                    Logger?.LogInformation(document);
                    
                    // Make the POST request
                    var response = await client.PostAsync(url, new StringContent(document, Encoding.UTF8, "application/activity+json"));

                    response.EnsureSuccessStatusCode();

                    // Print the response
                    var responseString = await response.Content.ReadAsStringAsync();

                    Logger?.LogInformation($"Response {response.StatusCode} - {responseString}");
                }
            }
        }
    }
}