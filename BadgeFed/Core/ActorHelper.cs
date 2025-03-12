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

        public static async Task<ActivityPubActor> FetchActorInformationAsync(string actorUrl)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Accept", "application/activity+json");

                HttpResponseMessage response = await httpClient.GetAsync(actorUrl);

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                };

                if (response.IsSuccessStatusCode)
                {
                    string jsonContent = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<ActivityPubActor>(jsonContent, options)!;
                }

                throw new Exception($"Failed to fetch information from {actorUrl} - {response.ReasonPhrase} - {response.StatusCode}");
            }
        }

        static string CreateHashSha256(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return Convert.ToBase64String(hashBytes);
            }
        }

        public async Task<string> GetSignedRequest(Uri url)
        {
            // Get current UTC date in HTTP format
            string date = DateTime.UtcNow.ToString("r");

            // Load RSA private key from file
            using (RSA rsa = RSA.Create())
            {

                Console.WriteLine(this._privatePem);
                rsa.ImportFromPem(this._privatePem);

                string digest = $"SHA-256={CreateHashSha256("")}";

                // Build the to-be-signed string
                // string signedString = $"(request-target): post {url.AbsolutePath}\nhost: {url.Host}\ndate: {date}";
                string signedString = $"(request-target): post {url.AbsolutePath}\nhost: {url.Host}\ndate: {date}\ndigest: {digest}";

                // Sign the to-be-signed string

                byte[] signatureBytes = rsa.SignData(Encoding.UTF8.GetBytes(signedString), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                // Base64 encode the signature
                string signature = Convert.ToBase64String(signatureBytes);

                Logger?.LogInformation($"Using key: {this._keyId}");

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
                    client.DefaultRequestHeaders.Add("Accept", "application/activity+json");
                    
                    // Make the POST request
                    var response = await client.GetAsync(url?.ToString());
                    // Print the response
                    return await response.Content.ReadAsStringAsync();
                }
            }
        }

        public async Task SendSignedRequest(string document, Uri url)
        {
            // Get current UTC date in HTTP format
            string date = DateTime.UtcNow.ToString("r");

            // Load RSA private key from file
            using (RSA rsa = RSA.Create())
            {

                Console.WriteLine(this._privatePem);
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

                    // Print the response
                    var responseString = await response.Content.ReadAsStringAsync();

                    Logger?.LogInformation($"Response {response.StatusCode} - {responseString}");
                }
            }
        }
    }
}