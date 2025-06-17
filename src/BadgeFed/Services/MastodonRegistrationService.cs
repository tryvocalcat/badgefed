using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using System.Security.Cryptography;

namespace BadgeFed.Services
{
    public class MastodonRegistrationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _clientName;
        private readonly string _website;
        private readonly string _scopes = "read";
        private readonly string _redirectUri;

        public MastodonRegistrationService(HttpClient httpClient, string clientName, string website, string redirectUri)
        {
            _httpClient = httpClient;
            _clientName = clientName;
            _website = website;
            _redirectUri = redirectUri;
        }

        public async Task<JsonDocument> RegisterApplicationAsync(string instanceUrl)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_name", _clientName),
                new KeyValuePair<string, string>("redirect_uris", _redirectUri),
                new KeyValuePair<string, string>("scopes", _scopes),
                new KeyValuePair<string, string>("website", _website)
            });

            var response = await _httpClient.PostAsync($"https://{instanceUrl}/api/v1/apps", content);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(json);
        }

        public (string codeVerifier, string codeChallenge) GeneratePkceCodes()
        {
            var codeVerifier = GenerateCodeVerifier();
            var codeChallenge = GenerateCodeChallenge(codeVerifier);
            return (codeVerifier, codeChallenge);
        }

        private string GenerateCodeVerifier()
        {
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Base64UrlEncode(bytes);
        }

        private string GenerateCodeChallenge(string codeVerifier)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.ASCII.GetBytes(codeVerifier);
            var hash = sha256.ComputeHash(bytes);
            return Base64UrlEncode(hash);
        }

        private string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }

        public string GetPkceAuthorizationUrl(string instanceUrl, string clientId, string codeChallenge, bool forceLogin = false)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["client_id"] = clientId;
            query["code_challenge_method"] = "S256";
            query["code_challenge"] = codeChallenge;
            query["redirect_uri"] = _redirectUri;
            query["response_type"] = "code";
            query["scope"] = _scopes;
            if (forceLogin) query["force_login"] = "true";
            return $"https://{instanceUrl}/oauth/authorize?{query}";
        }

        public string GetAuthorizationUrl(string instanceUrl, string clientId, bool forceLogin = false)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["client_id"] = clientId;
            query["scope"] = _scopes;
            query["redirect_uri"] = _redirectUri;
            query["response_type"] = "code";
            if (forceLogin) query["force_login"] = "true";
            return $"https://{instanceUrl}/oauth/authorize?{query}";
        }

        public async Task<JsonDocument> GetAccessTokenAsync(string instanceUrl, string clientId, string code, string clientSecret = null, string codeVerifier = null)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("redirect_uri", _redirectUri),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", code)
            });

        /*FIXME:    if (!string.IsNullOrEmpty(clientSecret))
                content.Add(new KeyValuePair<string, string>("client_secret", clientSecret));
            if (!string.IsNullOrEmpty(codeVerifier))
                content.Add(new KeyValuePair<string, string>("code_verifier", codeVerifier));*/

            var response = await _httpClient.PostAsync($"https://{instanceUrl}/oauth/token", content);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(json);
        }

        public async Task<bool> RevokeAccessTokenAsync(string instanceUrl, string clientId, string clientSecret, string token)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("token", token)
            });
            try
            {
                var response = await _httpClient.PostAsync($"https://{instanceUrl}/oauth/revoke", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error revoking token: {ex.Message}");
                return false;
            }
        }
    }
}
