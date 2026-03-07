using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BadgeFed.Models;
using Microsoft.Extensions.Options;

namespace BadgeFed.Services;

public class WebhookService
{
    private readonly WebhooksConfig _config;
    private readonly JobQueueService _jobQueue;
    private readonly ILogger<WebhookService> _logger;

    public WebhookService(IOptions<WebhooksConfig> config, JobQueueService jobQueue, ILogger<WebhookService> logger)
    {
        _config = config.Value;
        _jobQueue = jobQueue;
        _logger = logger;
    }

    /// <summary>
    /// Trigger webhooks for a specific event by enqueueing delivery jobs
    /// </summary>
    public async Task TriggerEventAsync(string eventType, object eventData)
    {
        if (!_config.Enabled)
        {
            _logger.LogDebug("Webhooks are disabled in configuration");
            return;
        }

        try
        {
            // Get all active webhooks for this event type from configuration
            var webhooks = _config.Webhooks
                .Where(w => w.IsActive && w.EventType == eventType)
                .ToList();
            
            if (!webhooks.Any())
            {
                _logger.LogDebug("No active webhooks found for event: {EventType}", eventType);
                return;
            }

            _logger.LogInformation("Triggering {Count} webhooks for event: {EventType}", webhooks.Count, eventType);

            // Enqueue a delivery job for each webhook
            foreach (var webhook in webhooks)
            {
                var payload = new
                {
                    eventType = eventType,
                    timestamp = DateTime.UtcNow,
                    data = eventData,
                    webhookName = webhook.Name
                };

                await _jobQueue.AddJobAsync(
                    jobType: "webhook.delivery",
                    payload: payload,
                    notes: $"Webhook delivery for {eventType} to {webhook.Name}",
                    entityType: "Webhook",
                    entityId: webhook.Name
                );

                _logger.LogDebug("Enqueued webhook delivery job for webhook: {WebhookName}", webhook.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering webhooks for event: {EventType}", eventType);
        }
    }

    /// <summary>
    /// Deliver a webhook (called by JobProcessor)
    /// </summary>
    public async Task<bool> DeliverWebhookAsync(string webhookName, string eventType, object eventData)
    {
        var webhook = _config.Webhooks.FirstOrDefault(w => w.Name == webhookName);
        if (webhook == null)
        {
            _logger.LogWarning("Webhook {WebhookName} not found in configuration", webhookName);
            return false;
        }

        if (!webhook.IsActive)
        {
            _logger.LogInformation("Webhook {WebhookName} is not active, skipping delivery", webhookName);
            return false;
        }

        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);

            var payload = new
            {
                eventType = eventType,
                timestamp = DateTime.UtcNow,
                data = eventData
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            // Add custom headers if configured
            if (webhook.Headers != null)
            {
                foreach (var header in webhook.Headers)
                {
                    httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }

            // Add signature header if secret is configured
            if (!string.IsNullOrEmpty(webhook.Secret))
            {
                var signature = GenerateSignature(jsonPayload, webhook.Secret);
                httpClient.DefaultRequestHeaders.Add("X-BadgeFed-Signature", signature);
            }

            // Send the webhook
            var response = await httpClient.PostAsync(webhook.TargetUrl, content);
            
            var statusCode = (int)response.StatusCode;
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Webhook {WebhookName} delivery failed with status {StatusCode}", 
                    webhookName, statusCode);
                return false;
            }

            _logger.LogInformation("Webhook {WebhookName} delivered successfully to {TargetUrl}", 
                webhookName, webhook.TargetUrl);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error delivering webhook {WebhookName} to {TargetUrl}", 
                webhookName, webhook.TargetUrl);
            return false;
        }
    }

    /// <summary>
    /// Generate HMAC-SHA256 signature for webhook payload
    /// </summary>
    private string GenerateSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return "sha256=" + BitConverter.ToString(hash).Replace("-", "").ToLower();
    }

    public List<WebhookConfig> GetConfiguredWebhooks()
    {
        return _config.Webhooks;
    }
}
