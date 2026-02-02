using BadgeFed.Models;
using System.Text.Json;
using ActivityPubDotNet.Core;

namespace BadgeFed.Services
{
    public class ServerDiscoveryService
    {
        private readonly LocalScopedDb _localDbService;
        private readonly HttpClient _httpClient;
        private readonly ILogger<ServerDiscoveryService> _logger;

        private readonly FollowService _followService;
        private const string SERVERS_JSON_URL = "https://raw.githubusercontent.com/tryvocalcat/badgefed/main/servers.json";

        public ServerDiscoveryService(LocalScopedDb localDbService, FollowService followService, HttpClient httpClient, ILogger<ServerDiscoveryService> logger)
        {
            _localDbService = localDbService;
            _followService = followService;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<List<DiscoveredServer>> FetchAndUpdateServersAsync()
        {
            try
            {
                _logger.LogInformation("Fetching servers from GitHub...");
                var response = await _httpClient.GetStringAsync(SERVERS_JSON_URL);
                var serversFromJson = JsonSerializer.Deserialize<List<ServerFromJson>>(response);

                if (serversFromJson == null)
                {
                    _logger.LogWarning("No servers found in JSON response");
                    return new List<DiscoveredServer>();
                }

                var discoveredServers = new List<DiscoveredServer>();

                foreach (var serverJson in serversFromJson)
                {
                    var existingServer = GetServerByUrl(serverJson.url);
                    
                    var discoveredServer = new DiscoveredServer
                    {
                        Id = existingServer?.Id ?? 0,
                        Name = serverJson.name,
                        Url = serverJson.url,
                        Description = serverJson.description,
                        Admin = serverJson.admin,
                        Actor = serverJson.actor,
                        IsFollowed = existingServer?.IsFollowed ?? false,
                        FollowedAt = existingServer?.FollowedAt,
                        CreatedAt = existingServer?.CreatedAt ?? DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    
                    discoveredServer.SetCategories(serverJson.categories);
                    UpsertDiscoveredServer(discoveredServer);
                    discoveredServers.Add(discoveredServer);
                }

                _logger.LogInformation($"Successfully updated {discoveredServers.Count} servers");
                return discoveredServers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching servers from GitHub");
                throw;
            }
        }

        public List<DiscoveredServer> GetAllDiscoveredServers()
        {
            return _localDbService.GetAllDiscoveredServers();
        }

        public List<DiscoveredServer> GetServersByCategory(string category)
        {
            var allServers = GetAllDiscoveredServers();
            return allServers.Where(s => s.GetCategories().Contains(category, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        public List<string> GetAllCategories()
        {
            var allServers = GetAllDiscoveredServers();
            var categories = new HashSet<string>();
            
            foreach (var server in allServers)
            {
                foreach (var category in server.GetCategories())
                {
                    categories.Add(category);
                }
            }
            
            return categories.OrderBy(c => c).ToList();
        }

        public DiscoveredServer? GetServerByUrl(string url)
        {
            return _localDbService.GetDiscoveredServerByUrl(url);
        }

        public void UpsertDiscoveredServer(DiscoveredServer server)
        {
            _localDbService.UpsertDiscoveredServer(server);
        }

        public async Task<bool> FollowServerAsync(int serverId, int actorId)
        {
            try
            {
                var server = _localDbService.GetDiscoveredServerById(serverId);
                var actor = _localDbService.GetActorById(actorId);
                
                if (server == null || actor == null)
                {
                    _logger.LogWarning($"Server {serverId} or Actor {actorId} not found");
                    return false;
                }

                // Use the existing FollowService to follow the server's actor
                var followedActor = await _followService.FollowIssuer(actor, server.Actor);
                
                if (followedActor != null)
                {
                    // Update the server as followed
                    server.IsFollowed = true;
                    server.FollowedAt = DateTime.UtcNow;

                    UpsertDiscoveredServer(server);

                    var issuer = new FollowedIssuer
                    {
                        Name = followedActor.Name,
                        Url = server.Actor,
                        Inbox = followedActor.Inbox,
                        Outbox = followedActor.Outbox,
                        ActorId = actorId,
                        AvatarUri = followedActor.Icon?.Url ?? string.Empty
                    };

                    _localDbService.UpsertFollowedIssuer(issuer);
                    
                    _logger.LogInformation($"Successfully followed server {server.Name}");
                    return true;
                }
                
                _logger.LogWarning($"Failed to follow server {server.Name}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error following server {serverId}");
                return false;
            }
        }

        public async Task<int> FollowAllServersAsync(int actorId, string? categoryFilter = null)
        {
            var servers = GetAllDiscoveredServers()
                .Where(s => !s.IsFollowed)
                .Where(s => categoryFilter == null || s.GetCategories().Contains(categoryFilter, StringComparer.OrdinalIgnoreCase))
                .ToList();

            int successCount = 0;
            
            foreach (var server in servers)
            {
                if (await FollowServerAsync(server.Id, actorId))
                {
                    successCount++;
                }
                
                // Add a small delay to avoid overwhelming the target servers
                await Task.Delay(1000);
            }

            return successCount;
        }
    }
}
