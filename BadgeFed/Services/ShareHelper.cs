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

        var url = $"https://{record.Actor.Domain}/view/grant/{record.NoteId}";

        text += $". Check it out: {url}";
         
        return $"https://{mastodonDomain}/share?text={Uri.EscapeDataString(text)}&hashtags={Uri.EscapeDataString("badge,badgefed,activitypub,fediverse")}";
    }

    public static string GetLinkedInShareLink(BadgeRecord record)
    {
        if (string.IsNullOrEmpty(record.Actor?.LinkedInOrganizationId))
        {
            return string.Empty;
        }

        var certId = record.NoteId;
        var issueYear = record.IssuedOn.Year.ToString();
        var issueMonth = record.IssuedOn.Month.ToString();

        var url = $"https://{record.Actor.Domain}/view/grant/{record.NoteId}";
       
        var linkedinId = record.Actor.LinkedInOrganizationId ?? string.Empty;

        return GetLinkedInShareLink(linkedinId, record.Title, url, certId, issueYear, issueMonth, null, null);
    }

    public static string GetLinkedInShareLink(string organizationId, string certificationName, string certUrl, string certId, string issueYear, string issueMonth, string? expirationYear = null, string? expirationMonth = null)
    {
        return $"https://www.linkedin.com/profile/add?startTask=CERTIFICATION_NAME&name={certificationName}&organizationId={organizationId}&issueYear={issueYear}&issueMonth={issueMonth}&expirationYear={expirationYear ?? string.Empty}&expirationMonth={expirationMonth ?? string.Empty}&certUrl={certUrl}&certId={certId}";
    }
}