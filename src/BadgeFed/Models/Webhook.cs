namespace BadgeFed.Models;

public class WebhookConfig
{
    public string Name { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public string? Secret { get; set; }
    public bool IsActive { get; set; } = true;
    public Dictionary<string, string>? Headers { get; set; }
}

public class WebhooksConfig
{
    public bool Enabled { get; set; } = true;
    public int RetryCount { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 30;
    public List<WebhookConfig> Webhooks { get; set; } = new();
}

public class WebhookDelivery
{
    public long Id { get; set; }
    public string WebhookName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string? Response { get; set; }
    public bool Success { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
}
