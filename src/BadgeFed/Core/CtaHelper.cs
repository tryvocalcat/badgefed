using System;
using System.Text;
using System.Text.Json;

namespace BadgeFed.Core;

public static class CtaHelper
{
    /// <summary>
    /// Builds a CTA URL with a ?ref= query parameter containing a base64-encoded JSON
    /// payload with the app name and the return URL.
    /// Example: https://hub.vocalcat.com?ref=eyJuYW1lIjoiQmFkZ2VGZWQiLCJ1cmwiOiJodHRwczovL2JhZGdlZmVkLm9yZy8ifQ==
    /// </summary>
    public static string BuildCtaUrl(string baseCtaUrl, string returnUrl)
    {
        if (string.IsNullOrEmpty(baseCtaUrl))
            return string.Empty;

        var payload = JsonSerializer.Serialize(new
        {
            name = "BadgeFed",
            url = returnUrl ?? ""
        });

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));

        var separator = baseCtaUrl.Contains('?') ? "&" : "?";
        return $"{baseCtaUrl}{separator}ref={base64}";
    }
}
