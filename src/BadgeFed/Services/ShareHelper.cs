using BadgeFed.Models;

namespace BadgeFed.Services;

public class ShareHelper
{
    public static string GetMastodonShareLink(BadgeRecord record)
    {
        var issueYear = record.IssuedOn.Year.ToString();
        var issueMonth = record.IssuedOn.Month.ToString();
        
        var uri = new Uri(record.IssuedToSubjectUri);
        var mastodonDomain = uri.Host;

        var text = $"I earned a badge! {record.Title} issued by {record.Actor.FullName} on {issueMonth}/{issueYear}";

        var url = $"{record.NoteId}";

        text += $". Check it out: {url}";
         
        return $"https://{mastodonDomain}/share?text={Uri.EscapeDataString(text)}&hashtags={Uri.EscapeDataString("badge,badgefed,activitypub,fediverse")}";
    }

    public static string GetLinkedInProfileLink(BadgeRecord record)
    {
        if (string.IsNullOrEmpty(record.Actor?.LinkedInOrganizationId))
        {
            return string.Empty;
        }

        var certId = record.NoteId.Split('/').LastOrDefault() ?? string.Empty;
        var issueYear = record.IssuedOn.Year.ToString();
        var issueMonth = record.IssuedOn.Month.ToString();

        var url = $"{record.NoteId}";
       
        var linkedinId = record.Actor.LinkedInOrganizationId ?? string.Empty;

        return GetLinkedInCertificationLink(linkedinId, record.Title, url, certId, issueYear, issueMonth, null, null);
    }

    public static string GetLinkedInPostShareLink(BadgeRecord record)
    {
        var issueYear = record.IssuedOn.Year.ToString();
        var issueMonth = record.IssuedOn.Month.ToString();
        
        var text = $"I'm very excited to share this badge! {record.Title} issued by {record.Actor.FullName} on {issueMonth}/{issueYear}";
        var url = $"{record.NoteId}";

        return $"http://www.linkedin.com/shareArticle?url={Uri.EscapeDataString(url)}&text={Uri.EscapeDataString(text)}";
    }

    // Keep the old method name for backward compatibility but rename the implementation
    public static string GetLinkedInShareLink(BadgeRecord record)
    {
        return GetLinkedInProfileLink(record);
    }

    public static string GetLinkedInCertificationLink(string organizationId, string certificationName, string certUrl, string certId, string issueYear, string issueMonth, string? expirationYear = null, string? expirationMonth = null)
    {
        return $"https://www.linkedin.com/profile/add?startTask=CERTIFICATION_NAME&name={certificationName}&organizationId={organizationId}&issueYear={issueYear}&issueMonth={issueMonth}&expirationYear={expirationYear ?? string.Empty}&expirationMonth={expirationMonth ?? string.Empty}&certUrl={certUrl}&certId={certId}";
    }
}