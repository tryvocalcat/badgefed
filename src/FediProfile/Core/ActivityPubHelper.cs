namespace FediProfile.Core;

public static class ActivityPubHelper
{
    public static bool IsActivityPubRequest(string acceptHeader)
    {
        if (string.IsNullOrWhiteSpace(acceptHeader))
            return false;

        var accept = acceptHeader.ToLowerInvariant();
        return accept.Contains("application/activity+json")
            || accept.Contains("application/ld+json")
            || accept.Contains("application/json");
    }
}
