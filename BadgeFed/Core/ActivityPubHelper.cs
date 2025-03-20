namespace BadgeFed.Core
{
    public class ActivityPubHelper
    {
        public static bool IsActivityPubRequest(string acceptHeader)
        {
            var accept = acceptHeader.ToLower();
            return accept.Contains("application/json") || accept.Contains("application/activity") || accept.Contains("application/ld+json");
        }
    }
}