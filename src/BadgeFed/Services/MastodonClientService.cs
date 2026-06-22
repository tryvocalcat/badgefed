using System.Net.Http.Headers;
using System.Text.Json;

namespace BadgeFed.Services
{
    public class MastodonClientService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public MastodonClientService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Resolves a remote status URL on the user's instance, returning the local status ID.
        /// </summary>
        public async Task<JsonElement?> ResolveStatusAsync(string instanceHost, string accessToken, string statusUrl)
        {
            var client = CreateClient(instanceHost, accessToken);
            var encodedUrl = Uri.EscapeDataString(statusUrl);
            var response = await client.GetAsync($"https://{instanceHost}/api/v2/search?q={encodedUrl}&type=statuses&resolve=true&limit=1");

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var statuses = doc.RootElement.GetProperty("statuses");

            if (statuses.GetArrayLength() == 0) return null;

            return statuses[0];
        }

        /// <summary>
        /// Favourite (like) a status on the user's instance.
        /// </summary>
        public async Task<JsonElement?> FavouriteAsync(string instanceHost, string accessToken, string statusId)
        {
            var client = CreateClient(instanceHost, accessToken);
            var response = await client.PostAsync($"https://{instanceHost}/api/v1/statuses/{statusId}/favourite", null);

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(json).RootElement;
        }

        /// <summary>
        /// Unfavourite (unlike) a status on the user's instance.
        /// </summary>
        public async Task<JsonElement?> UnfavouriteAsync(string instanceHost, string accessToken, string statusId)
        {
            var client = CreateClient(instanceHost, accessToken);
            var response = await client.PostAsync($"https://{instanceHost}/api/v1/statuses/{statusId}/unfavourite", null);

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(json).RootElement;
        }

        /// <summary>
        /// Boost (reblog) a status on the user's instance.
        /// </summary>
        public async Task<JsonElement?> BoostAsync(string instanceHost, string accessToken, string statusId)
        {
            var client = CreateClient(instanceHost, accessToken);
            var response = await client.PostAsync($"https://{instanceHost}/api/v1/statuses/{statusId}/reblog", null);

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(json).RootElement;
        }

        /// <summary>
        /// Unboost (unreblog) a status on the user's instance.
        /// </summary>
        public async Task<JsonElement?> UnboostAsync(string instanceHost, string accessToken, string statusId)
        {
            var client = CreateClient(instanceHost, accessToken);
            var response = await client.PostAsync($"https://{instanceHost}/api/v1/statuses/{statusId}/unreblog", null);

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(json).RootElement;
        }

        /// <summary>
        /// Post a reply (comment) to a status. Optionally attach media IDs.
        /// </summary>
        public async Task<JsonElement?> ReplyAsync(string instanceHost, string accessToken, string inReplyToId, string content, List<string>? mediaIds = null)
        {
            var client = CreateClient(instanceHost, accessToken);

            var formData = new Dictionary<string, string>
            {
                ["status"] = content,
                ["in_reply_to_id"] = inReplyToId,
                ["visibility"] = "public"
            };

            var formContent = new FormUrlEncodedContent(formData);

            // If media IDs are present, we need multipart or repeated params
            if (mediaIds != null && mediaIds.Count > 0)
            {
                var multipart = new MultipartFormDataContent();
                multipart.Add(new StringContent(content), "status");
                multipart.Add(new StringContent(inReplyToId), "in_reply_to_id");
                multipart.Add(new StringContent("public"), "visibility");
                foreach (var mediaId in mediaIds)
                {
                    multipart.Add(new StringContent(mediaId), "media_ids[]");
                }
                var response = await client.PostAsync($"https://{instanceHost}/api/v1/statuses", multipart);
                if (!response.IsSuccessStatusCode) return null;
                var json = await response.Content.ReadAsStringAsync();
                return JsonDocument.Parse(json).RootElement;
            }
            else
            {
                var response = await client.PostAsync($"https://{instanceHost}/api/v1/statuses", formContent);
                if (!response.IsSuccessStatusCode) return null;
                var json = await response.Content.ReadAsStringAsync();
                return JsonDocument.Parse(json).RootElement;
            }
        }

        /// <summary>
        /// Upload a media attachment. Returns the media object with its ID.
        /// </summary>
        public async Task<JsonElement?> UploadMediaAsync(string instanceHost, string accessToken, Stream fileStream, string fileName, string contentType, string? description = null)
        {
            var client = CreateClient(instanceHost, accessToken);

            var multipart = new MultipartFormDataContent();
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            multipart.Add(streamContent, "file", fileName);

            if (!string.IsNullOrEmpty(description))
            {
                multipart.Add(new StringContent(description), "description");
            }

            var response = await client.PostAsync($"https://{instanceHost}/api/v2/media", multipart);

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(json).RootElement;
        }

        /// <summary>
        /// Follow an account by searching for it first, then following.
        /// </summary>
        public async Task<JsonElement?> FollowAccountAsync(string instanceHost, string accessToken, string accountUri)
        {
            var client = CreateClient(instanceHost, accessToken);

            // First resolve the account on the user's instance
            var encodedUri = Uri.EscapeDataString(accountUri);
            var searchResponse = await client.GetAsync($"https://{instanceHost}/api/v2/search?q={encodedUri}&type=accounts&resolve=true&limit=1");

            if (!searchResponse.IsSuccessStatusCode) return null;

            var searchJson = await searchResponse.Content.ReadAsStringAsync();
            var searchDoc = JsonDocument.Parse(searchJson);
            var accounts = searchDoc.RootElement.GetProperty("accounts");

            if (accounts.GetArrayLength() == 0) return null;

            var accountId = accounts[0].GetProperty("id").GetString();

            // Now follow
            var response = await client.PostAsync($"https://{instanceHost}/api/v1/accounts/{accountId}/follow", null);

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(json).RootElement;
        }

        /// <summary>
        /// Unfollow an account.
        /// </summary>
        public async Task<JsonElement?> UnfollowAccountAsync(string instanceHost, string accessToken, string accountUri)
        {
            var client = CreateClient(instanceHost, accessToken);

            var encodedUri = Uri.EscapeDataString(accountUri);
            var searchResponse = await client.GetAsync($"https://{instanceHost}/api/v2/search?q={encodedUri}&type=accounts&resolve=true&limit=1");

            if (!searchResponse.IsSuccessStatusCode) return null;

            var searchJson = await searchResponse.Content.ReadAsStringAsync();
            var searchDoc = JsonDocument.Parse(searchJson);
            var accounts = searchDoc.RootElement.GetProperty("accounts");

            if (accounts.GetArrayLength() == 0) return null;

            var accountId = accounts[0].GetProperty("id").GetString();

            var response = await client.PostAsync($"https://{instanceHost}/api/v1/accounts/{accountId}/unfollow", null);

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(json).RootElement;
        }

        /// <summary>
        /// Get the status of a resolved post (favourited, reblogged, etc.)
        /// </summary>
        public async Task<JsonElement?> GetStatusAsync(string instanceHost, string accessToken, string statusId)
        {
            var client = CreateClient(instanceHost, accessToken);
            var response = await client.GetAsync($"https://{instanceHost}/api/v1/statuses/{statusId}");

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(json).RootElement;
        }

        private HttpClient CreateClient(string instanceHost, string accessToken)
        {
            var client = _httpClientFactory.CreateClient("MastodonApi");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }
    }
}
